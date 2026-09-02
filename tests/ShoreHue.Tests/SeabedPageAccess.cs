namespace ShoreHue.Tests
{
    /// <summary>测试访问器：暴露 SeabedPage 的内部静态方法。</summary>
    public static class SeabedPageAccess
    {
        public static (string Source, string Xaml, string XamlCs, string ConfigJson) LoadNodeFromFolder(
            ShoreHue.Core.Models.ConfigNode node, ShoreHue.Core.Models.CustomPanelDefinition? cp)
            => ShoreHue.UI.Settings.Pages.SeabedPage.LoadNodeFromFolderPublic(node, cp);
    }
}
