using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Core.Infrastructure.Service;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Clipboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace DynamicBird.Core.Services
{
    /// <summary>
    /// 剪贴板管理器（实例类，实现 IClipboardService + IService）
    /// </summary>
    public class ClipboardManager : IClipboardService, IService, IDisposable
    {
        private readonly DispatcherTimer _pollTimer;
        private readonly ISettingsService _settings;
        private string? _lastContentHash;
        private bool _isListening = false;
        private bool _isRestoring = false;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public ObservableCollection<ClipboardItem> History { get; } = new ObservableCollection<ClipboardItem>();

        public event EventHandler? HistoryChanged;

        // ========== IService 实现 ==========
        public string Name => "ClipboardManager";
        public bool IsInitialized { get; private set; } = false;

        public ClipboardManager(ISettingsService settings)
        {
            _settings = settings;
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(500);
            _pollTimer.Tick += PollClipboard;
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
            _pollTimer.Start();
            LogManager.Debug("剪贴板监听已启动");
        }

        public void StopListening()
        {
            if (!_isListening) return;
            _isListening = false;
            _pollTimer.Stop();
            LogManager.Debug("剪贴板监听已停止");
        }

        private void PollClipboard(object? sender, EventArgs e)
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
                    while (History.Count > maxCount)
                    {
                        var removed = History[History.Count - 1];
                        History.RemoveAt(History.Count - 1);
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
                            return ClipboardItem.FromImage(img);
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
        }

        private string GetHistoryFilePath()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "clipboard_history.json");
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
            _pollTimer.Stop();
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

            public static ClipboardItem FromImage(System.Windows.Media.Imaging.BitmapSource image)
            {
                try
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "clipboard_cache");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string fileName = $"img_{Guid.NewGuid():N}.png";
                    string filePath = Path.Combine(dir, fileName);

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                    using var stream = File.OpenWrite(filePath);
                    encoder.Save(stream);

                    return new ClipboardItem
                    {
                        Type = "Image",
                        CachePath = filePath,
                        DisplayText = $"🖼️ 图片 ({image.PixelWidth}×{image.PixelHeight})"
                    };
                }
                catch
                {
                    return new ClipboardItem
                    {
                        Type = "Image",
                        DisplayText = "🖼️ 图片 (保存失败)"
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
                    DisplayText = $"📁 {string.Join(", ", names.Take(3))}" + (names.Count > 3 ? $" (+{names.Count - 3})" : "")
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
                    DisplayText = $"📄 HTML: {(plainText.Length > 200 ? plainText.Substring(0, 200) + "..." : plainText)}"
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
                        HtmlContent = data.HtmlContent
                    };

                    if (item.Type == "Image" && !string.IsNullOrEmpty(item.CachePath) && !File.Exists(item.CachePath))
                    {
                        item.DisplayText = "🖼️ 图片 (文件丢失)";
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
                    HtmlContent = HtmlContent
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
        }
    }
}