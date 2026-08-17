using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace DynamicBird.UI.Localization
{
    /// <summary>
    /// 本地化管理器（运行时切换语言）：
    ///  - 从嵌入式 resx 读取字符串（中性资源=中文，en-US 卫星资源=英文）
    ///  - 实现 INotifyPropertyChanged，切换语言后所有 Loc 绑定自动刷新
    /// 使用：
    ///   启动时  LocalizationManager.Instance.SetCulture("zh-CN" | "en-US" | ...)
    ///   XAML    Text="{loc:Loc Key=WidgetTabs_Timer}" 或 "{loc:Loc WidgetTabs_Timer}"
    ///   代码    LocalizationManager.Instance["WidgetTabs_Timer"]
    /// </summary>
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        public static LocalizationManager Instance { get; } = new();

        private readonly ResourceManager _resources;

        private LocalizationManager()
        {
            _resources = new ResourceManager(
                "DynamicBird.UI.Localization.Strings", typeof(LocalizationManager).Assembly);
        }

        /// <summary>按 key 取当前语言的字符串；缺失时返回 key 本身（便于发现漏译）。</summary>
        public string this[string key]
        {
            get
            {
                try
                {
                    string? s = _resources.GetString(key, CultureInfo.CurrentUICulture);
                    return string.IsNullOrEmpty(s) ? key : s;
                }
                catch
                {
                    return key;
                }
            }
        }

        /// <summary>当前语言名称（如 zh-CN / en-US）。</summary>
        public string CurrentCultureName => CultureInfo.CurrentUICulture.Name;

        /// <summary>切换语言并通知所有绑定刷新。参数为空时回退系统语言。</summary>
        public void SetCulture(string? cultureName)
        {
            try
            {
                var culture = string.IsNullOrWhiteSpace(cultureName)
                    ? CultureInfo.InstalledUICulture
                    : new CultureInfo(cultureName);
                CultureInfo.CurrentUICulture = culture;
            }
            catch
            {
                CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
