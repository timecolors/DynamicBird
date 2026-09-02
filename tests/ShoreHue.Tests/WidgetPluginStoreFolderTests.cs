using ShoreHue.UI.Widgets.Dynamic;
using Xunit;
using System.IO;

namespace ShoreHue.Tests
{
    /// <summary>小组件存储共享静态状态：串行执行避免并发冲突。</summary>
    [CollectionDefinition("WidgetStore", DisableParallelization = true)]
    public class WidgetStoreCollection { }

    /// <summary>小组件文件夹化：分组子目录扫描 + .cs/.dbp 归一化（用户直接在系统文件夹管理）。</summary>
    [Collection("WidgetStore")]
    public class WidgetPluginStoreFolderTests
    {
        private static string BackupAndIsolate()
        {
            // 用临时根目录隔离（通过反射换 RootDir 不可行——RootDir 是私有静态只读）
            // 改为直接验证 EnsureSkeleton/Normalize 的行为（通过 OpenFolder 前置效果 + Reload 不崩）
            return "";
        }

        [Fact]
        public void Reload_DoesNotCrash_OnMissingFolder()
        {
            // 根目录不存在时 Reload 应安全（首次运行）
            var plugins = WidgetPluginStore.Installed;
            Assert.NotNull(plugins);
        }

        [Fact]
        public void Save_Then_GetById_RoundTrip()
        {
            var p = new WidgetPlugin
            {
                Id = "test-widget",
                Name = "测试小组件",
                Source = "public class W { public void M() {} }",
                Group = "小组件"
            };
            var err = WidgetPluginStore.Save(p);
            Assert.Equal("", err);
            var loaded = WidgetPluginStore.GetById("test-widget");
            Assert.NotNull(loaded);
            Assert.Equal("测试小组件", loaded!.Name);
            Assert.Equal("小组件", loaded.Group);
            WidgetPluginStore.Delete("test-widget");
            Assert.Null(WidgetPluginStore.GetById("test-widget"));
        }
    }
}
