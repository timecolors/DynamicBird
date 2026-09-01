namespace ShoreHue.UI.Panels
{
    public partial class TaskbarView
    {
        private const double MIN_ITEM_WIDTH = 36;
        private const double ITEM_PADDING = 4;
        private const double DIVIDER_THICKNESS = 3;
        private const double DIVIDER_MARGIN = 2;
        private const double DIVIDER_MIN_WIDTH = 120;

#pragma warning disable CS0108
        private bool _isLayoutUpdating = false;
#pragma warning restore CS0108

        private enum LayoutMode
        {
            Horizontal,
            Vertical
        }

        private LayoutMode _currentLayoutMode = LayoutMode.Horizontal;
        private double _cachedIconSize = -1;
        private int _cachedShortcutCount = -1;
        private int _cachedWindowCount = -1;
        private bool _cachedHasDivider = false;
    }
}