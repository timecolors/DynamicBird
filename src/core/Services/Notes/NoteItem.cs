using System;

namespace ShoreHue.Core.Services
{
    /// <summary>
    /// 便签数据模型
    /// </summary>
    public class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Color { get; set; } = "#FFFF99";
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public DateTime UpdateTime { get; set; } = DateTime.Now;
        public bool ShowTitle { get; set; } = true;
        public bool IsCurrent { get; set; } = false;
    }
}