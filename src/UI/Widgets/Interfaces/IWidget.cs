using System.Windows.Controls;

namespace ShoreHue.UI.Widgets
{
    /// <summary>
    /// 小组件接口，所有组件必须实现
    /// </summary>
    public interface IWidget
    {
        /// <summary>
        /// 组件名称（用于设置界面显示）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 创建组件视图
        /// </summary>
        UserControl CreateView();

        /// <summary>
        /// 组件被激活时调用（可选初始化）
        /// </summary>
        void OnActivated();

        /// <summary>
        /// 组件被停用时调用（可选清理）
        /// </summary>
        void OnDeactivated();
    }
}