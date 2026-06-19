using System.Windows;
using System.Windows.Media;

namespace LingDongBird.Core
{
    public static class DpiHelper
    {
        public static double GetDpiScale(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformToDevice.M11;
            return 1.0;
        }
    }
}