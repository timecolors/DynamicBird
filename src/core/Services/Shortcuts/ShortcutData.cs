using System;

namespace ShoreHue.Core.Services
{
    /// <summary>
    /// 快捷方式数据模型（用于序列化/反序列化）
    /// </summary>
    public class ShortcutData
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 应用路径（.exe 完整路径）
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// 显示名称（自动从文件获取，也可用户自定义）
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 排序顺序（数字越小越靠前）
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 是否在面板中显示
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 参数（启动参数）
        /// </summary>
        public string Arguments { get; set; } = "";

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; } = "";
    }
}