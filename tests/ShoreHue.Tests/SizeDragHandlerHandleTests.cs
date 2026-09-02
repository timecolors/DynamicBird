using System.Windows;
using ShoreHue.Core.Controllers;
using Xunit;

namespace ShoreHue.Tests
{
    /// <summary>
    /// SizeDragHandler.HitTest 手柄区域回归：任务栏矮条面板/左右边缘窄条面板上，
    /// 手柄区不得覆盖图标点击区（原 corner=42px 在 86px 高任务栏上占 49% 高度，盖住右侧图标）。
    /// </summary>
    public class SizeDragHandlerHandleTests
    {
        // 任务栏面板（2/3 屏宽 × 最小高 86）——用户反馈场景
        private const double TaskbarW = 1138;
        private const double TaskbarH = 86;

        // 左右边缘小组件窄条（宽 ~86 × 高 2/3 屏）
        private const double StripW = 86;
        private const double StripH = 1138;

        private static Point P(double x, double y) => new Point(x, y);

        [Fact]
        public void 任务栏面板中间图标区无手柄()
        {
            // 图标行位于面板上部/中部（Y 46-74），中间 X 处点击不应触发调整大小
            Assert.Null(SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(200, 50)));
            Assert.Null(SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(500, 60)));
            Assert.Null(SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(900, 70)));
        }

        [Fact]
        public void 任务栏面板右侧图标区不再被角手柄覆盖()
        {
            // 原逻辑 corner=42：X>W-42 且 Y>H-42 → BottomRight，右侧最后图标点不动。
            // 新逻辑 corner≈18.9：X=W-30 处 Y 60 无手柄，可正常点击
            Assert.Null(SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(TaskbarW - 30, 60)));
            Assert.Null(SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(TaskbarW - 30, 75)));
        }

        [Fact]
        public void 任务栏面板边缘仍可调整大小()
        {
            // 顶部/底部/左右边缘 6px 内保留手柄
            Assert.Equal(SizeDragHandler.ResizeHandle.Top, SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(500, 2)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Bottom, SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(500, TaskbarH - 2)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Left, SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(2, 50)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Right, SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(TaskbarW - 2, 50)));
            // 四角仍可抓
            Assert.Equal(SizeDragHandler.ResizeHandle.TopLeft, SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(3, 3)));
            Assert.Equal(SizeDragHandler.ResizeHandle.BottomRight, SizeDragHandler.HitTest(TaskbarW, TaskbarH, P(TaskbarW - 3, TaskbarH - 3)));
        }

        [Fact]
        public void 竖条面板中部无手柄_边缘可调()
        {
            // 原逻辑 corner=42 在 86 宽竖条上几乎全面板覆盖；新逻辑只留边缘
            Assert.Null(SizeDragHandler.HitTest(StripW, StripH, P(43, 500)));
            Assert.Null(SizeDragHandler.HitTest(StripW, StripH, P(43, 1000)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Left, SizeDragHandler.HitTest(StripW, StripH, P(2, 500)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Right, SizeDragHandler.HitTest(StripW, StripH, P(StripW - 2, 500)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Top, SizeDragHandler.HitTest(StripW, StripH, P(43, 2)));
            Assert.Equal(SizeDragHandler.ResizeHandle.Bottom, SizeDragHandler.HitTest(StripW, StripH, P(43, StripH - 2)));
        }

        [Fact]
        public void 常规面板保持可用角区()
        {
            // 340×260 常规面板：角区 24px（缩小但可抓），边带 6px
            Assert.Equal(SizeDragHandler.ResizeHandle.BottomRight, SizeDragHandler.HitTest(340, 260, P(335, 255)));
            Assert.Equal(SizeDragHandler.ResizeHandle.TopLeft, SizeDragHandler.HitTest(340, 260, P(5, 5)));
            Assert.Null(SizeDragHandler.HitTest(340, 260, P(170, 130)));
        }
    }
}