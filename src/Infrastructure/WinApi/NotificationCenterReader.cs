using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DynamicBird.Core.Infrastructure.Logging;
using Microsoft.Data.Sqlite;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 直接读取 Windows 通知中心数据库（wpndatabase.db），获取各应用发到通知中心的 Toast。
    /// 相比屏幕弹窗嗅探，能稳定拿到 QQ 等应用的系统通知（含消息正文）。
    /// </summary>
    public static class NotificationCenterReader
    {
        private const int MaxHistory = 10;   // 首次启动时展示的最近通知条数
        private const int MaxBatch = 50;     // 单轮最多处理的新通知

        private static long _maxSeenId = -1;
        private static bool _initialized;
        private static int _failCount;

        private static string DatabasePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Notifications", "wpndatabase.db");

        /// <summary>
        /// 扫描通知中心，返回尚未展示过的通知（按时间从旧到新）。
        /// 首次调用返回最近的历史通知，之后只返回新增。
        /// </summary>
        public static List<ToastNotificationItem> Scan()
        {
            var result = new List<ToastNotificationItem>();
            try
            {
                if (!File.Exists(DatabasePath)) return result;

                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = DatabasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                    DefaultTimeout = 5
                }.ToString();

                using var conn = new SqliteConnection(cs);
                conn.Open();
                _failCount = 0;

                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT n.Id, n.ArrivalTime, n.Payload, h.PrimaryId " +
                    "FROM Notification n " +
                    "LEFT JOIN NotificationHandler h ON n.HandlerId = h.RecordId " +
                    "WHERE n.Type = 'toast' AND n.PayloadType = 'Xml' " +
                    "ORDER BY n.ArrivalTime DESC LIMIT 200";

                var rows = new List<(long Id, long Arrival, byte[] Payload, string? AppId)>();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        rows.Add((
                            r.GetInt64(0),
                            r.GetInt64(1),
                            r.IsDBNull(2) ? Array.Empty<byte>() : (byte[])r.GetValue(2),
                            r.IsDBNull(3) ? null : r.GetString(3)));
                    }
                }

                if (rows.Count == 0) return result;

                // 首次：取最近 N 条历史；之后：只取比已见最大 Id 更新的
                long maxId = rows.Max(x => x.Id);
                IEnumerable<(long Id, long Arrival, byte[] Payload, string? AppId)> pending;
                if (!_initialized)
                {
                    pending = rows.Take(MaxHistory).Reverse();
                    _initialized = true;
                }
                else
                {
                    pending = rows.Where(x => x.Id > _maxSeenId).Take(MaxBatch).Reverse();
                }

                foreach (var row in pending)
                {
                    var item = BuildItem(row.Arrival, row.Payload, row.AppId);
                    if (item != null) result.Add(item);
                }

                if (maxId > _maxSeenId) _maxSeenId = maxId;
            }
            catch (Exception ex)
            {
                // 通知服务可能短暂占用数据库；失败静默，避免打扰用户
                _failCount++;
                if (_failCount <= 3 || _failCount % 30 == 0)
                {
                    LogManager.Debug($"[NotificationCenter] 读取失败（第 {_failCount} 次）: {ex.Message}");
                }
            }
            return result;
        }

        private static ToastNotificationItem? BuildItem(long arrivalFileTime, byte[] payload, string? appId)
        {
            try
            {
                if (string.Equals(appId, SystemToast.Aumid, StringComparison.OrdinalIgnoreCase))
                    return null; // 不展示灵动鸟自己发出的通知

                string xml = System.Text.Encoding.UTF8.GetString(payload);
                var doc = XDocument.Parse(xml);
                var texts = doc.Descendants("text")
                    .Select(t => t.Value.Trim())
                    .Where(t => t.Length > 0)
                    .ToList();

                string message;
                if (texts.Count == 0)
                {
                    bool hasImage = doc.Descendants("image").Any();
                    message = hasImage ? "[图片]" : "[无内容]";
                }
                else if (texts.Count == 1)
                {
                    message = texts[0];
                }
                else
                {
                    // 第一个 text 通常是标题（发送者/来源），其余为正文
                    string title = texts[0];
                    string body = string.Join(" ", texts.Skip(1));
                    message = string.IsNullOrEmpty(body) ? title : $"{title}：{body}";
                }

                if (message.Length > 240) message = message[..240] + "…";

                return new ToastNotificationItem
                {
                    AppName = FriendlyAppName(appId),
                    Message = message,
                    Time = DateTime.FromFileTimeUtc(arrivalFileTime).ToLocalTime(),
                    AppId = appId
                };
            }
            catch
            {
                return null; // 个别坏 payload 跳过
            }
        }

        /// <summary>把 AUMID / 包名转成可读短名（取最后一段）。</summary>
        public static string FriendlyAppName(string? appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return "系统通知";
            var parts = appId.Split(new[] { '.', '!', '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            // 去掉常见系统前缀，避免显示成 "Microsoft.Windows.xxx"
            while (parts.Count > 1 && (parts[0] == "Microsoft" || parts[0] == "Windows"))
                parts.RemoveAt(0);

            if (parts.Count == 0) return "系统通知";

            string name = parts[^1];
            // 末尾是 "App" 之类无意义后缀时，用前一段（如 "xxx!App" -> "xxx"）
            if ((name.Length <= 3 || name == "App") && parts.Count > 1)
                name = parts[^2];

            return name.Length > 24 ? name[..24] : name;
        }
    }
}
