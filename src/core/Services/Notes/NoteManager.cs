using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Core.Infrastructure.Service;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Notes;
using DynamicBird.Infrastructure.Utils;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DynamicBird.Core.Services
{
    /// <summary>
    /// 便签管理器（实例类，实现 INoteService + IService）
    /// </summary>
    public class NoteManager : INoteService, IService
    {
        private readonly ObservableCollection<NoteItem> _notes = new();
        private readonly string _dataFilePath;
        private readonly ISettingsService _settings;

        public event EventHandler? NotesChanged;

        // ========== IService 实现 ==========
        public string Name => "NoteManager";
        public bool IsInitialized { get; private set; } = false;

        public ObservableCollection<NoteItem> Notes => _notes;
        public NoteItem? CurrentNote { get; private set; }

        public NoteManager(ISettingsService settings)
        {
            _settings = settings;
            if (!Directory.Exists(AppPaths.DataRoot)) Directory.CreateDirectory(AppPaths.DataRoot);
            _dataFilePath = AppPaths.NotesPath;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            Load();
            IsInitialized = true;
            LogManager.Debug($"NoteManager 初始化完成，已加载 {_notes.Count} 个便签");
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            Save();
            IsInitialized = false;
            LogManager.Debug("NoteManager 已关闭");
        }

        // ============ 公开方法 ============

        public void SetCurrentNote(NoteItem? note)
        {
            CurrentNote = note;
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public NoteItem CreateNote(string? title = null, string? color = null)
        {
            var note = new NoteItem
            {
                Title = title ?? "",
                Color = color ?? _settings.DefaultNoteColor,
                ShowTitle = _settings.NoteShowTitleByDefault,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
            _notes.Insert(0, note);
            CurrentNote = note;
            Save();
            NotesChanged?.Invoke(this, EventArgs.Empty);
            return note;
        }

        public void DeleteNote(NoteItem note)
        {
            if (_notes.Remove(note))
            {
                if (CurrentNote == note)
                {
                    CurrentNote = _notes.FirstOrDefault();
                }
                Save();
                NotesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UpdateNoteContent(NoteItem note, string content)
        {
            note.Content = content;
            note.UpdateTime = DateTime.Now;
            Save();
        }

        public void UpdateNoteTitle(NoteItem note, string title)
        {
            note.Title = title;
            note.UpdateTime = DateTime.Now;
            Save();
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateNoteColor(NoteItem note, string color)
        {
            note.Color = color;
            note.UpdateTime = DateTime.Now;
            Save();
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateNoteShowTitle(NoteItem note, bool showTitle)
        {
            note.ShowTitle = showTitle;
            note.UpdateTime = DateTime.Now;
            Save();
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                LogManager.Error($"保存便签失败", ex);
            }
        }

        private void Load()
        {
            if (!File.Exists(_dataFilePath)) return;
            try
            {
                string json = File.ReadAllText(_dataFilePath);
                var list = JsonSerializer.Deserialize<ObservableCollection<NoteItem>>(json);
                if (list != null)
                {
                    foreach (var item in list) _notes.Add(item);
                    CurrentNote = _notes.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error($"加载便签失败", ex);
            }
        }
    }
}
