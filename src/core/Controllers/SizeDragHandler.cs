using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DynamicBird.Core.Controllers
{
    public class SizeDragHandler
    {
        private readonly Window _window;
        private readonly FrameworkElement _mainPanel;
        private readonly WindowSizeController _controller;
        private readonly EdgeTriggerController _edgeController;  // ★★★ 新增 ★★★

        private bool _isResizing = false;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _resizeStartLeft;
        private double _resizeStartTop;

        private enum HandlePosition { TopLeft, TopRight, BottomLeft, BottomRight }
        private HandlePosition _handlePosition = HandlePosition.BottomRight;

        public event Action<bool>? UserResizeStarted;
        public event Action? ResizeEnded;
        public event Action<bool>? LockRequest;

        // ★★★ 构造函数增加 EdgeTriggerController 参数 ★★★
        public SizeDragHandler(Window window, FrameworkElement mainPanel, WindowSizeController controller, EdgeTriggerController edgeController)
        {
            _window = window;
            _mainPanel = mainPanel;
            _controller = controller;
            _edgeController = edgeController;

            _mainPanel.MouseLeave += OnMainPanelMouseLeave;
        }

        public void Reset()
        {
            _isResizing = false;
            if (_edgeController != null) _edgeController.IsDragging = false;
            try
            {
                if (_mainPanel.IsMouseCaptured)
                    _mainPanel.ReleaseMouseCapture();
            }
            catch { }
            Mouse.OverrideCursor = null;
        }

        public void UpdateHandlePosition(string edge)
        {
            if (string.IsNullOrEmpty(edge))
            {
                _handlePosition = HandlePosition.BottomRight;
                return;
            }

            _handlePosition = edge switch
            {
                "TopLeft" => HandlePosition.BottomRight,
                "TopRight" => HandlePosition.BottomLeft,
                "BottomLeft" => HandlePosition.TopRight,
                "BottomRight" => HandlePosition.TopLeft,
                "Top" => HandlePosition.BottomRight,
                "Bottom" => HandlePosition.TopRight,
                "Left" => HandlePosition.BottomRight,
                "Right" => HandlePosition.BottomLeft,
                _ => HandlePosition.BottomRight
            };
        }

        public bool HandleMouseDown(object sender, MouseButtonEventArgs e, string mode)
        {
            try
            {
                var pos = e.GetPosition(_mainPanel);
                bool inHandle = IsInHandleArea(pos, mode);

                if (e.ClickCount == 2 && inHandle)
                {
                    _controller.RestoreAutoSize();
                    e.Handled = true;
                    return true;
                }

                if (inHandle)
                {
                    UserResizeStarted?.Invoke(true);
                    LockRequest?.Invoke(true);

                    _isResizing = true;
                    _edgeController.IsDragging = true;  // ★★★ 同步到 EdgeController ★★★
                    _resizeStartPoint = e.GetPosition(_window);
                    _resizeStartWidth = _window.Width;
                    _resizeStartHeight = _window.Height;
                    _resizeStartLeft = _window.Left;
                    _resizeStartTop = _window.Top;
                    _mainPanel.CaptureMouse();

                    e.Handled = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SizeDragHandler.HandleMouseDown error: {ex.Message}");
                ForceRelease();
            }

            return false;
        }

        public void HandleMouseMove(object sender, MouseEventArgs e, string mode)
        {
            try
            {
                var pos = e.GetPosition(_mainPanel);
                bool inHandle = IsInHandleArea(pos, mode);
                _mainPanel.Cursor = inHandle ? GetHandleCursor() : Cursors.Arrow;

                if (_isResizing)
                {
                    var currentPos = e.GetPosition(_window);
                    double deltaX = currentPos.X - _resizeStartPoint.X;
                    double deltaY = currentPos.Y - _resizeStartPoint.Y;

                    double screenW = SystemParameters.PrimaryScreenWidth;
                    double screenH = SystemParameters.PrimaryScreenHeight;

                    double maxW, maxH;
                    if (mode == "Widget")
                    {
                        maxW = screenW * 2.0 / 5.0;
                        maxH = screenH * 2.0 / 3.0;
                    }
                    else
                    {
                        maxW = screenW * 0.8;
                        maxH = screenH * 0.8;
                    }

                    double minW = mode == "Widget" ? 340 : 120;
                    double minH = mode == "Widget" ? 220 : 80;

                    double newWidth = Math.Max(minW, _resizeStartWidth + deltaX);
                    double newHeight = Math.Max(minH, _resizeStartHeight + deltaY);
                    newWidth = Math.Min(newWidth, maxW);
                    newHeight = Math.Min(newHeight, maxH);

                    double newLeft = _resizeStartLeft;
                    double newTop = _resizeStartTop;

                    switch (_handlePosition)
                    {
                        case HandlePosition.TopLeft:
                            newLeft = _resizeStartLeft - (newWidth - _resizeStartWidth);
                            newTop = _resizeStartTop - (newHeight - _resizeStartHeight);
                            break;
                        case HandlePosition.TopRight:
                            newTop = _resizeStartTop - (newHeight - _resizeStartHeight);
                            break;
                        case HandlePosition.BottomLeft:
                            newLeft = _resizeStartLeft - (newWidth - _resizeStartWidth);
                            break;
                        case HandlePosition.BottomRight:
                            break;
                    }

                    newLeft = Math.Max(0, Math.Min(newLeft, screenW - newWidth));
                    newTop = Math.Max(0, Math.Min(newTop, screenH - newHeight));

                    _window.Width = newWidth;
                    _window.Height = newHeight;
                    _window.Left = newLeft;
                    _window.Top = newTop;

                    _window.UpdateLayout();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SizeDragHandler.HandleMouseMove error: {ex.Message}");
                ForceRelease();
            }
        }

        public void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                try
                {
                    _isResizing = false;
                    _edgeController.IsDragging = false;  // ★★★ 同步到 EdgeController ★★★
                    _mainPanel.ReleaseMouseCapture();

                    e.Handled = true;
                    ResizeEnded?.Invoke();
                    LockRequest?.Invoke(false);
                    UserResizeStarted?.Invoke(false);

                    // ★ 保存尺寸
                    _controller.SaveCurrentSizeWithDelay();

                    _window.UpdateLayout();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SizeDragHandler.HandleMouseUp error: {ex.Message}");
                    ForceRelease();
                }
            }
        }

        private void OnMainPanelMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isResizing)
            {
                System.Diagnostics.Debug.WriteLine("SizeDragHandler: MouseLeave triggered, forcing release");
                ForceRelease();
                e.Handled = true;
            }
        }

        private void ForceRelease()
        {
            _isResizing = false;
            if (_edgeController != null) _edgeController.IsDragging = false;
            try
            {
                if (_mainPanel.IsMouseCaptured)
                    _mainPanel.ReleaseMouseCapture();
            }
            catch { }
            try
            {
                LockRequest?.Invoke(false);
                UserResizeStarted?.Invoke(false);
                ResizeEnded?.Invoke();
            }
            catch { }
            Mouse.OverrideCursor = null;
        }

        private bool IsInHandleArea(Point pos, string mode)
        {
            double handleSize = 40;
            double width = _mainPanel.ActualWidth;
            double height = _mainPanel.ActualHeight;
            if (width < 10 || height < 10) return false;

            return _handlePosition switch
            {
                HandlePosition.TopLeft => pos.X < handleSize && pos.Y < handleSize,
                HandlePosition.TopRight => pos.X > width - handleSize && pos.Y < handleSize,
                HandlePosition.BottomLeft => pos.X < handleSize && pos.Y > height - handleSize,
                HandlePosition.BottomRight => pos.X > width - handleSize && pos.Y > height - handleSize,
                _ => false
            };
        }

        private Cursor GetHandleCursor()
        {
            return _handlePosition switch
            {
                HandlePosition.TopLeft => Cursors.SizeNWSE,
                HandlePosition.TopRight => Cursors.SizeNESW,
                HandlePosition.BottomLeft => Cursors.SizeNESW,
                HandlePosition.BottomRight => Cursors.SizeNWSE,
                _ => Cursors.Arrow
            };
        }
    }
}