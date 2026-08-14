using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace DynamicBird.UI.AppHelper
{
    /// <summary>
    /// 单个正在播放的媒体会话（来自 QQ 音乐、浏览器等注册了系统媒体会话的应用）。
    /// </summary>
    public class MediaSessionInfo
    {
        public GlobalSystemMediaTransportControlsSession Session { get; }
        public string AppName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public bool IsPlaying { get; set; }
        public ImageSource? Thumbnail { get; set; }

        public MediaSessionInfo(GlobalSystemMediaTransportControlsSession session)
        {
            Session = session;
        }

        public string DisplayText =>
            string.IsNullOrEmpty(Title)
                ? AppName
                : (string.IsNullOrEmpty(Artist) ? Title : $"{Title} · {Artist}");
    }

    /// <summary>
    /// 系统媒体会话控制：枚举并控制其他应用（如 QQ 音乐）的播放。
    /// 基于 GlobalSystemMediaTransportControlsSession（Windows 10 1809+）。
    /// </summary>
    public static class MediaSessionController
    {
        private static GlobalSystemMediaTransportControlsSessionManager? _manager;

        public static async Task<List<MediaSessionInfo>> GetSessionsAsync()
        {
            var result = new List<MediaSessionInfo>();
            try
            {
                if (_manager == null)
                {
                    _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
                }
                if (_manager == null) return result;

                foreach (var session in _manager.GetSessions())
                {
                    var info = new MediaSessionInfo(session)
                    {
                        AppName = FriendlyAppName(session.SourceAppUserModelId)
                    };

                    try
                    {
                        var props = await session.TryGetMediaPropertiesAsync().AsTask();
                        info.Title = props.Title ?? "";
                        info.Artist = props.Artist ?? "";

                        // ★ 专辑封面：从媒体属性读取缩略图流
                        if (props.Thumbnail != null)
                        {
                            try
                            {
                                using var thumbStream = await props.Thumbnail.OpenReadAsync().AsTask();
                                // ★ WinRT 流可能不支持随机访问，先复制到 MemoryStream 再解码
                                using var ms = new MemoryStream();
                                await thumbStream.AsStreamForRead().CopyToAsync(ms);
                                if (ms.Length > 0)
                                {
                                    ms.Position = 0;
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.StreamSource = ms;
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.DecodePixelWidth = 96;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                    info.Thumbnail = bitmap;
                                }
                            }
                            catch (Exception ex)
                            {
                                DynamicBird.Core.Infrastructure.Logging.LogManager.Debug(
                                    $"[MediaCover] 读取封面失败: {ex.Message}");
                            }
                        }
                        else
                        {
                            DynamicBird.Core.Infrastructure.Logging.LogManager.Debug(
                                $"[MediaCover] {info.AppName} 媒体属性无缩略图");
                        }
                    }
                    catch { }

                    try
                    {
                        var playback = session.GetPlaybackInfo();
                        info.IsPlaying =
                            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    }
                    catch { }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取媒体会话失败: {ex.Message}");
            }
            return result;
        }

        public static async Task<bool> TogglePlayPauseAsync(MediaSessionInfo info)
        {
            try { return await info.Session.TryTogglePlayPauseAsync().AsTask(); }
            catch { return false; }
        }

        public static async Task<bool> NextAsync(MediaSessionInfo info)
        {
            try { return await info.Session.TrySkipNextAsync().AsTask(); }
            catch { return false; }
        }

        public static async Task<bool> PrevAsync(MediaSessionInfo info)
        {
            try { return await info.Session.TrySkipPreviousAsync().AsTask(); }
            catch { return false; }
        }

        public static async Task<bool> PlayAsync(MediaSessionInfo info)
        {
            try { return await info.Session.TryPlayAsync().AsTask(); }
            catch { return false; }
        }

        public static async Task<bool> PauseAsync(MediaSessionInfo info)
        {
            try { return await info.Session.TryPauseAsync().AsTask(); }
            catch { return false; }
        }

        private static string FriendlyAppName(string sourceAppUserModelId)
        {
            if (string.IsNullOrEmpty(sourceAppUserModelId)) return "未知应用";

            // 常见 AUMID 形如 "Tencent.QQMusic..."/"Spotify..."/"Chrome..."，
            // 取第一段作为友好名称
            int dot = sourceAppUserModelId.IndexOf('.');
            if (dot > 0 && dot < sourceAppUserModelId.Length - 1)
            {
                string head = sourceAppUserModelId[..dot];
                if (!head.Contains('!') && !head.Contains('{'))
                    return head;
            }
            return sourceAppUserModelId;
        }
    }
}
