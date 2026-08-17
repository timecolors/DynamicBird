using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 天气服务（Open-Meteo，完全免费、无需 API Key）：
    ///  - 按城市名（geocoding）或 IP 自动定位
    ///  - 当前温度 + 天气码 → 中文描述与图标
    ///  - 15 分钟缓存，失败静默返回 null
    /// 隐私：查询会向 open-meteo.com / ipapi.co 发起请求（仅当用户启用天气时）。
    /// </summary>
    public static class WeatherService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
        private static (double Lat, double Lon)? _location;
        private static DateTime _locationTime = DateTime.MinValue;
        private static (string Text, string Emoji)? _cached;
        private static DateTime _cacheTime = DateTime.MinValue;

        /// <summary>获取天气文本（如 “☀️ 25° 晴”）；失败返回 null。</summary>
        public static async Task<(string Text, string Emoji)?> GetWeatherAsync(string city)
        {
            if (_cached != null && (DateTime.Now - _cacheTime).TotalMinutes < 15)
                return _cached;

            try
            {
                var (lat, lon) = await GetLocationAsync(city);
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,weather_code&timezone=auto";
                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var cur = doc.RootElement.GetProperty("current");
                double temp = cur.GetProperty("temperature_2m").GetDouble();
                int code = cur.GetProperty("weather_code").GetInt32();
                var (emoji, desc) = WeatherCode(code);
                _cached = ($"{emoji} {temp:F0}° {desc}", emoji);
                _cacheTime = DateTime.Now;
                return _cached;
            }
            catch
            {
                return null;
            }
        }

        public sealed class CitySuggestion
        {
            public string Name { get; set; } = "";
            public string Label { get; set; } = "";
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        /// <summary>城市联想搜索（Open-Meteo geocoding），供设置页下拉候选。</summary>
        public static async Task<List<CitySuggestion>> SearchCitiesAsync(string query)
        {
            var empty = new List<CitySuggestion>();
            if (string.IsNullOrWhiteSpace(query)) return empty;
            try
            {
                string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=8&language=zh";
                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var results)) return empty;
                var list = new List<CitySuggestion>();
                foreach (var r in results.EnumerateArray())
                {
                    string name = r.GetProperty("name").GetString() ?? "";
                    string? admin = r.TryGetProperty("admin1", out var a) ? a.GetString() : null;
                    string? country = r.TryGetProperty("country", out var c) ? c.GetString() : null;
                    list.Add(new CitySuggestion
                    {
                        Name = name,
                        Label = string.Join(" · ", new[] { name, admin, country }.Where(x => !string.IsNullOrWhiteSpace(x))),
                        Latitude = r.GetProperty("latitude").GetDouble(),
                        Longitude = r.GetProperty("longitude").GetDouble()
                    });
                }
                return list;
            }
            catch
            {
                return empty;
            }
        }

        public static void ClearCache()
        {
            _cached = null;
            _location = null;
        }

        private static async Task<(double Lat, double Lon)> GetLocationAsync(string city)
        {
            if (!string.IsNullOrWhiteSpace(city))
            {
                try
                {
                    string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=zh";
                    string json = await _http.GetStringAsync(url);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var first = results[0];
                        return (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
                    }
                }
                catch { }
            }

            // IP 定位兜底（缓存坐标 1 小时）；ipapi.co 经常 403，改用 ipwho.is
            if (_location != null && (DateTime.Now - _locationTime).TotalHours < 1)
                return _location.Value;
            string ipJson = await _http.GetStringAsync("https://ipwho.is/");
            using var ipDoc = JsonDocument.Parse(ipJson);
            if (!ipDoc.RootElement.TryGetProperty("success", out var ok) || ok.GetBoolean() != true ||
                !ipDoc.RootElement.TryGetProperty("latitude", out var latEl) ||
                !ipDoc.RootElement.TryGetProperty("longitude", out var lonEl))
            {
                throw new InvalidOperationException("IP 定位失败");
            }
            var loc = (latEl.GetDouble(), lonEl.GetDouble());
            _location = loc;
            _locationTime = DateTime.Now;
            return loc;
        }

        private static (string Emoji, string Desc) WeatherCode(int code) => code switch
        {
            0 => ("☀️", "晴"),
            1 => ("🌤", "基本晴朗"),
            2 => ("⛅", "多云"),
            3 => ("☁️", "阴"),
            45 or 48 => ("🌫", "雾"),
            51 or 53 or 55 or 56 or 57 => ("🌦", "毛毛雨"),
            61 or 63 or 65 or 66 or 67 => ("🌧", "雨"),
            71 or 73 or 75 or 77 => ("🌨", "雪"),
            80 or 81 or 82 => ("🌦", "阵雨"),
            85 or 86 => ("🌨", "阵雪"),
            95 => ("⛈", "雷暴"),
            96 or 99 => ("⛈", "雷暴伴冰雹"),
            _ => ("🌡", "未知")
        };
    }
}