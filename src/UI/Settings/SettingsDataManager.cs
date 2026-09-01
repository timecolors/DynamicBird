using ShoreHue.Core.Services;
using ShoreHue.Core.Services.Configuration;

namespace ShoreHue.UI.Settings
{
    public static class SettingsDataManager
    {
        public static SettingsData Load()
        {
            return SettingsFileManager.Load();
        }

        public static void Save(SettingsData data)
        {
            SettingsFileManager.Save(data);
            // 移除静态 Reload 调用，由调用方负责重新加载
        }
    }
}