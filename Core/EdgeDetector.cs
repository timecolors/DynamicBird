using System.Runtime.InteropServices;

namespace LingDongBird.Core
{
    public static class EdgeDetector
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public class EdgeResult
        {
            public bool IsTriggered { get; set; }
            public string Position { get; set; } = "";
            public double MouseX { get; set; }
            public double MouseY { get; set; }
        }

        public static EdgeResult Detect(double screenWidth, double screenHeight, double dpiScale, int threshold = 2)
        {
            var point = new POINT();
            GetCursorPos(ref point);

            double mouseX = point.X / dpiScale;
            double mouseY = point.Y / dpiScale;

            bool isTop = mouseY <= threshold;
            bool isBottom = mouseY >= screenHeight - threshold;
            bool isLeft = mouseX <= threshold;
            bool isRight = mouseX >= screenWidth - threshold;

            var result = new EdgeResult { MouseX = mouseX, MouseY = mouseY };

            if (isTop && isLeft)
            {
                result.IsTriggered = true;
                result.Position = "TopLeft";
            }
            else if (isTop && isRight)
            {
                result.IsTriggered = true;
                result.Position = "TopRight";
            }
            else if (isBottom && isLeft)
            {
                result.IsTriggered = true;
                result.Position = "BottomLeft";
            }
            else if (isBottom && isRight)
            {
                result.IsTriggered = true;
                result.Position = "BottomRight";
            }
            else if (isTop)
            {
                result.IsTriggered = true;
                result.Position = "Top";
            }
            else if (isBottom)
            {
                result.IsTriggered = true;
                result.Position = "Bottom";
            }
            else if (isLeft)
            {
                result.IsTriggered = true;
                result.Position = "Left";
            }
            else if (isRight)
            {
                result.IsTriggered = true;
                result.Position = "Right";
            }
            else
            {
                result.IsTriggered = false;
            }

            return result;
        }
    }
}