using ShoreHue.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ShoreHue.src.core.Services.Shortcuts
{
    /// <summary>
    /// 快捷方式服务接口
    /// </summary>
    public interface IShortcutService
    {
        /// <summary>
        /// 快捷方式列表
        /// </summary>
        ObservableCollection<ShortcutData> Shortcuts { get; }

        /// <summary>
        /// 快捷方式变化事件
        /// </summary>
        event EventHandler? ShortcutsChanged;

        /// <summary>
        /// 添加快捷方式
        /// </summary>
        bool AddShortcut(string path, string? name = null, string? arguments = null);

        /// <summary>
        /// 删除快捷方式（按ID）
        /// </summary>
        bool RemoveShortcut(string id);

        /// <summary>
        /// 删除快捷方式（按路径）
        /// </summary>
        bool RemoveShortcutByPath(string path);

        /// <summary>
        /// 移动排序
        /// </summary>
        void MoveShortcut(int fromIndex, int toIndex);

        /// <summary>
        /// 更新快捷方式名称
        /// </summary>
        void UpdateShortcutName(string id, string newName);

        /// <summary>
        /// 保存快捷方式排序
        /// </summary>
        void SaveShortcutsOrder();

        /// <summary>
        /// 重新加载
        /// </summary>
        void Reload();

        /// <summary>
        /// 获取快捷方式图标
        /// </summary>
        ImageSource? GetIcon(string path);
    }
}