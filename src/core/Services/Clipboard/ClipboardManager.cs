using ShoreHue.Core.Infrastructure.Logging;
using ShoreHue.Core.Infrastructure.Service;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.src.core.Services.Clipboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ShoreHue.Infrastructure.Utils;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ShoreHue.Core.Services
{
    /// <summary>
    /// 剪贴板管理器（实例类，实现 IClipboardService + IService）
    /// </summary>
    public class ClipboardManager : IClipboardService, IService, IDisposable
    {
        private readonly ISettingsService _settings;
        private string? _lastContentHash;
        private bool _isListening = false;
        private bool _isRestoring = false;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private HwndSource? _messageWindow;

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public ObservableCollection<ClipboardItem> History { get; } = new ObservableCollection<ClipboardItem>();

        public event EventHandler? HistoryChanged;

        // ========== IService 实现 ==========
        public string Name => "ClipboardManager";
        public bool IsInitialized { get; private set; } = false;

        public ClipboardManager(ISettingsService settings)
        {
            _settings = settings;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            LoadHistory();
            IsInitialized = true;
            LogManager.Debug($"ClipboardManager 初始化完成，已加载 {History.Count} 条记录");
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            StopListening();
            SaveHistory();
            IsInitialized = false;
            LogManager.Debug("ClipboardManager 已关闭");
        }

        // ============ 公开方法 ============

        public void StartListening()
        {
            if (_isListening) return;
            _isListening = true;
            CreateClipboardListenerWindow();
            LogManager.Debug("剪贴板监听已启动（事件驱动）");
        }

        public void StopListening()
        {
            if (!_isListening) return;
            _isListening = false;
            DestroyClipboardListenerWindow();
            LogManager.Debug("剪贴板监听已停止");
        }

        /// <summary>创建隐藏消息窗口并注册 WM_CLIPBOARDUPDATE 监听（替代轮询）。</summary>
        private void CreateClipboardListenerWindow()
        {
            try
            {
                var p = new HwndSourceParameters("ShoreHueClipboardListener")
                {
                    Width = 0,
                    Height = 0,
                    WindowStyle = unchecked((int)0x80000000), // WS_POPUP
                    ExtendedWindowStyle = 0x80 // WS_EX_TOOLWINDOW
                };
                _messageWindow = new HwndSource(p);
                _messageWindow.AddHook(WndProc);
                if (!AddClipboardFormatListener(_messageWindow.Handle))
                {
                    LogManager.Warning("AddClipboardFormatListener 失败");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("创建剪贴板监听窗口失败", ex);
                _messageWindow = null;
            }
        }

        private void DestroyClipboardListenerWindow()
        {
            try
            {
                if (_messageWindow != null)
                {
                    if (_messageWindow.Handle != IntPtr.Zero)
                        RemoveClipboardFormatListener(_messageWindow.Handle);
                    _messageWindow.Dispose();
                }
            }
            catch { }
            _messageWindow = null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                // 剪贴板变化事件：延迟一拍再读，避免与写入方抢占剪贴板
                Application.Current?.Dispatcher.BeginInvoke(new Action(CaptureClipboardNow),
                    System.Windows.Threading.DispatcherPriority.Background);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void CaptureClipboardNow()
        {
            if (!_isListening || _isRestoring) return;

            try
            {
                if (!System.Windows.Clipboard.ContainsText() &&
                    !System.Windows.Clipboard.ContainsImage() &&
                    !System.Windows.Clipboard.ContainsFileDropList())
                    return;

                var item = CaptureClipboard();
                if (item == null) return;

                lock (_lock)
                {
                    string hash = item.GetHashString();
                    if (_lastContentHash == hash) return;
                    _lastContentHash = hash;

                    if (History.Count > 0 && History[0].GetHashString() == hash)
                        return;

                    History.Insert(0, item);

                    int maxCount = _settings.ClipboardMaxCount;
                    // ★ 记忆库：收藏（IsPinned）的条目不被自动清理淘汰
                    while (History.Count > maxCount && History.Any(i => !i.IsPinned))
                    {
                        int last = History.Count - 1;
                        while (last >= 0 && History[last].IsPinned) last--;
                        if (last < 0) break;
                        var removed = History[last];
                        History.RemoveAt(last);
                        removed.CleanupCache();
                    }

                    SaveHistory();
                    HistoryChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("剪贴板轮询异常", ex);
            }
        }

        private ClipboardItem? CaptureClipboard()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                        return ClipboardItem.FromText(text);
                }

                if (System.Windows.Clipboard.ContainsImage())
                {
                    try
                    {
                        var img = System.Windows.Clipboard.GetImage();
                        if (img != null)
                            return ClipboardItem.FromImage(img, _settings.ClipboardImageMaxWidth);
                    }
                    catch { }
                }

                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var files = System.Windows.Clipboard.GetFileDropList();
                    if (files.Count > 0)
                        return ClipboardItem.FromFiles(files.Cast<string>().ToList());
                }

                if (System.Windows.Clipboard.ContainsData(DataFormats.Html))
                {
                    try
                    {
                        var html = System.Windows.Clipboard.GetData(DataFormats.Html) as string;
                        if (!string.IsNullOrWhiteSpace(html))
                            return ClipboardItem.FromHtml(html);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("捕获剪贴板内容失败", ex);
            }
            return null;
        }

        public void RemoveItem(ClipboardItem item)
        {
            lock (_lock)
            {
                if (History.Remove(item))
                {
                    item.CleanupCache();
                    SaveHistory();
                    HistoryChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void RemoveItems(IEnumerable<ClipboardItem> items)
        {
            lock (_lock)
            {
                foreach (var item in items.ToList())
                {
                    if (History.Remove(item))
                        item.CleanupCache();
                }
                SaveHistory();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                bool wasListening = _isListening;
                if (wasListening) StopListening();

                foreach (var item in History)
                    item.CleanupCache();
                History.Clear();
                _lastContentHash = GetCurrentClipboardHash();

                SaveHistory();
                HistoryChanged?.Invoke(this, EventArgs.Empty);

                if (wasListening) StartListening();
            }
        }

        public void CopyToClipboard(ClipboardItem item)
        {
            try
            {
                _isRestoring = true;
                item.RestoreToClipboard();
                _lastContentHash = item.GetHashString();
            }
            catch (Exception ex)
            {
                LogManager.Error("复制到剪贴板失败", ex);
            }
            finally
            {
                _isRestoring = false;
            }
        }

        /// <summary>收藏/取消收藏（收藏条目不被自动清理，记忆库核心）。</summary>
        public void SetPinned(ClipboardItem item, bool pinned)
        {
            lock (_lock)
            {
                if (item.IsPinned == pinned) return;
                item.IsPinned = pinned;
                SaveHistory();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string? GetCurrentClipboardHash()
        {
            try
            {
                var item = CaptureClipboard();
                return item?.GetHashString();
            }
            catch { return null; }
        }

        private void LoadHistory()
        {
            try
            {
                string filePath = GetHistoryFilePath();
                if (!File.Exists(filePath)) return;

                string json = File.ReadAllText(filePath);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<ClipboardItemData>>(json);
                if (list == null) return;

                foreach (var data in list)
                {
                    var item = ClipboardItem.FromData(data);
                    if (item != null)
                        History.Add(item);
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("加载剪贴板历史失败", ex);
            }
        }

        private void SaveHistory()
        {
            try
            {
                var list = History.Select(item => item.ToData()).ToList();
                string json = System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetHistoryFilePath(), json);
            }
            catch (Exception ex)
            {
                LogManager.Error("保存剪贴板历史失败", ex);
            }

            // ★ 图片缓存总量上限：超限时清理"未被收藏引用且最旧"的缓存文件
            try { EnforceImageCacheLimit(); } catch { }
        }

        /// <summary>
        /// 图片缓存总量控制：缓存目录总大小超过 ClipboardImageCacheLimitMB 时，
        /// 按文件修改时间从旧到新删除"不在收藏条目中"的图片缓存，直到低于上限。
        /// 收藏（IsPinned）条目的图片永不自动删除。
        /// </summary>
        private void EnforceImageCacheLimit()
        {
            int limitMB = _settings.ClipboardImageCacheLimitMB;
            if (limitMB <= 0) return;

            string dir = AppPaths.ClipboardCacheDir;
            if (!Directory.Exists(dir)) return;

            // 收藏条目引用的缓存文件（保护集）
            var pinnedCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in History)
            {
                if (item.IsPinned && !string.IsNullOrEmpty(item.CachePath))
                    pinnedCache.Add(item.CachePath);
            }

            var files = Directory.GetFiles(dir, "*.png")
                .Select(f => new FileInfo(f))
                .Where(fi => !pinnedCache.Contains(fi.FullName))
                .OrderBy(fi => fi.LastWriteTime)
                .ToList();

            long limitBytes = (long)limitMB * 1024 * 1024;
            long total = files.Sum(fi => fi.Length);
            if (total <= limitBytes) return;

            foreach (var fi in files)
            {
                if (total <= limitBytes) break;
                try
                {
                    long len = fi.Length;
                    fi.Delete();
                    total -= len;
                    LogManager.Debug($"剪贴板图片缓存清理: {fi.Name} (-{len / 1024}KB)");
                }
                catch { }
            }
        }

        private string GetHistoryFilePath()
        {
            if (!Directory.Exists(AppPaths.DataRoot)) Directory.CreateDirectory(AppPaths.DataRoot);
            return AppPaths.ClipboardHistoryPath;
        }

        public bool SaveDroppedFile(string sourcePath, string targetFolder)
        {
            try
            {
                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(targetFolder, fileName);
                int counter = 1;
                while (File.Exists(destPath))
                {
                    string name = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    destPath = Path.Combine(targetFolder, $"{name}_{counter}{ext}");
                    counter++;
                }

                File.Copy(sourcePath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存拖放文件失败", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Shutdown();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        // ----- 内部类：ClipboardItem 和 ClipboardItemData（保持不变） -----

        public class ClipboardItem
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Type { get; set; } = "Text";
            public string DisplayText { get; set; } = "";
            public string? FullText { get; set; }
            public string? CachePath { get; set; }
            public List<string>? FilePaths { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public string? HtmlContent { get; set; }

            /// <summary>收藏到常用（不被自动清理上限淘汰）。</summary>
            public bool IsPinned { get; set; }

            public string GetHashString()
            {
                if (Type == "Text") return HashText(FullText ?? DisplayText);

                if (Type == "Image")
                {
                    if (!string.IsNullOrEmpty(CachePath) && File.Exists(CachePath))
                    {
                        try
                        {
                            using var fs = File.OpenRead(CachePath);
                            using var sha = SHA256.Create();
                            var hash = sha.ComputeHash(fs);
                            return Convert.ToBase64String(hash);
                        }
                        catch { }
                    }
                    return HashText(DisplayText);
                }

                if (Type == "File") return HashText(string.Join("|", FilePaths ?? new List<string>()));
                if (Type == "Html") return HashText(HtmlContent ?? FullText ?? DisplayText);

                return HashText(DisplayText);
            }

            private static string HashText(string text)
            {
                using var sha = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(text ?? "");
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }

            public static ClipboardItem FromText(string text)
            {
                return new ClipboardItem
                {
                    Type = "Text",
                    FullText = text,
                    DisplayText = text.Length > 500 ? text.Substring(0, 500) + "..." : text
                };
            }

            public static ClipboardItem FromImage(System.Windows.Media.Imaging.BitmapSource image, int maxWidth = 0)
            {
                try
                {
                    if (!Directory.Exists(AppPaths.ClipboardCacheDir)) Directory.CreateDirectory(AppPaths.ClipboardCacheDir);
                    string fileName = $"img_{Guid.NewGuid():N}.png";
                    string filePath = Path.Combine(AppPaths.ClipboardCacheDir, fileName);

                    // ★ 缩略化：最长边超过 maxWidth 时等比缩放后再保存（默认 1280px），
                    //   大幅降低磁盘占用与历史加载开销；恢复时粘贴的也是缩略图（清晰度足够）。
                    var toSave = image;
                    int saveWidth = image.PixelWidth;
                    int saveHeight = image.PixelHeight;
                    if (maxWidth > 0)
                    {
                        int longSide = Math.Max(image.PixelWidth, image.PixelHeight);
                        if (longSide > maxWidth)
                        {
                            double scale = (double)maxWidth / longSide;
                            saveWidth = Math.Max(1, (int)Math.Round(image.PixelWidth * scale));
                            saveHeight = Math.Max(1, (int)Math.Round(image.PixelHeight * scale));
                            try
                            {
                                var scaled = new System.Windows.Media.Imaging.TransformedBitmap(
                                    image, new System.Windows.Media.ScaleTransform(scale, scale));
                                toSave = System.Windows.Media.Imaging.BitmapFrame.Create(scaled);
                            }
                            catch { /* 缩放失败则保存原图 */ }
                        }
                    }

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(toSave));
                    using (var stream = File.OpenWrite(filePath))
                    {
                        encoder.Save(stream);
                    }

                    return new ClipboardItem
                    {
                        Type = "Image",
                        CachePath = filePath,
                        DisplayText = string.Format(ShoreHue.UI.Localization.LocalizationManager.Instance["Clip_ImageSize"], saveWidth, saveHeight)
                    };
                }
                catch
                {
                    return new ClipboardItem
                    {
                        Type = "Image",
                        DisplayText = ShoreHue.UI.Localization.LocalizationManager.Instance["Clip_ImageSaveFailed"]
                    };
                }
            }

            public static ClipboardItem FromFiles(List<string> files)
            {
                var names = files.Select(f => Path.GetFileName(f)).ToList();
                return new ClipboardItem
                {
                    Type = "File",
                    FilePaths = files,
                    DisplayText = $"{string.Join(", ", names.Take(3))}" + (names.Count > 3 ? $" (+{names.Count - 3})" : "")
                };
            }

            public static ClipboardItem FromHtml(string html)
            {
                var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
                plainText = System.Text.RegularExpressions.Regex.Replace(plainText, "\\s+", " ").Trim();
                return new ClipboardItem
                {
                    Type = "Html",
                    HtmlContent = html,
                    FullText = plainText,
                    DisplayText = $"HTML: {(plainText.Length > 200 ? plainText.Substring(0, 200) + "..." : plainText)}"
                };
            }

            public static ClipboardItem? FromData(ClipboardItemData data)
            {
                try
                {
                    var item = new ClipboardItem
                    {
                        Id = data.Id,
                        Type = data.Type,
                        DisplayText = data.DisplayText,
                        FullText = data.FullText,
                        CachePath = data.CachePath,
                        FilePaths = data.FilePaths,
                        Timestamp = data.Timestamp,
                        HtmlContent = data.HtmlContent,
                        IsPinned = data.IsPinned
                    };

                    if (item.Type == "Image" && !string.IsNullOrEmpty(item.CachePath) && !File.Exists(item.CachePath))
                    {
                        item.DisplayText = ShoreHue.UI.Localization.LocalizationManager.Instance["Clip_ImageMissing"];
                    }
                    return item;
                }
                catch { return null; }
            }

            public ClipboardItemData ToData()
            {
                return new ClipboardItemData
                {
                    Id = Id,
                    Type = Type,
                    DisplayText = DisplayText,
                    FullText = FullText,
                    CachePath = CachePath,
                    FilePaths = FilePaths,
                    Timestamp = Timestamp,
                    HtmlContent = HtmlContent,
                    IsPinned = IsPinned
                };
            }

            public void RestoreToClipboard()
            {
                switch (Type)
                {
                    case "Text":
                        System.Windows.Clipboard.SetText(FullText ?? DisplayText);
                        break;
                    case "Image":
                        if (!string.IsNullOrEmpty(CachePath) && File.Exists(CachePath))
                        {
                            var image = new System.Windows.Media.Imaging.BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(CachePath, UriKind.Absolute);
                            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            image.EndInit();
                            System.Windows.Clipboard.SetImage(image);
                        }
                        break;
                    case "File":
                        if (FilePaths != null && FilePaths.Count > 0)
                        {
                            var collection = new System.Collections.Specialized.StringCollection();
                            collection.AddRange(FilePaths.ToArray());
                            System.Windows.Clipboard.SetFileDropList(collection);
                        }
                        break;
                    case "Html":
                        if (!string.IsNullOrEmpty(HtmlContent))
                        {
                            System.Windows.Clipboard.SetData(DataFormats.Html, HtmlContent);
                        }
                        break;
                }
            }

            public void CleanupCache()
            {
                if (!string.IsNullOrEmpty(CachePath) && File.Exists(CachePath))
                {
                    try { File.Delete(CachePath); } catch { }
                }
            }
        }

        public class ClipboardItemData
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "Text";
            public string DisplayText { get; set; } = "";
            public string? FullText { get; set; }
            public string? CachePath { get; set; }
            public List<string>? FilePaths { get; set; }
            public DateTime Timestamp { get; set; }
            public string? HtmlContent { get; set; }

            /// <summary>收藏（不被自动清理）。</summary>
            public bool IsPinned { get; set; }
        }
    }
}
