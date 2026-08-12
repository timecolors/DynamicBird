using System;
using System.Windows;
using System.Windows.Input;
using DynamicBird.Core.Services.Configuration;

namespace DynamicBird.Core.Controllers
{
    public class DragController
    {
        private readonly Window _window;
        private readonly FrameworkElement _dragTarget;
        private readonly EdgeTriggerController _edgeController;
        private readonly PanelVisibilityController _visibilityController;
        private readonly ISettingsService _settings;

        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _dragStartLeft;
        private double _dragStartTop;

        private DateTime _lastDragEndTime = DateTime.MinValue;

        public bool IsDragging => _isDragging;
        public bool IsRecentlyDragged => (DateTime.Now - _lastDragEndTime).TotalMilliseconds < 500;

        public DragController(
            Window window,
            FrameworkElement dragTarget,
            EdgeTriggerController edgeController,
            PanelVisibilityController visibilityController,
            ISettingsService settings)
        {
            _window = window;
            _dragTarget = dragTarget;
            _edgeController = edgeController;
            _visibilityController = visibilityController;
            _settings = settings;

            _dragTarget.MouseDown += OnMouseDown;
            _dragTarget.MouseMove += OnMouseMove;
            _dragTarget.MouseUp += OnMouseUp;
            _dragTarget.MouseLeave += OnMouseLeave;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string currentEdge = _edgeController.CurrentEdge;
                if (string.IsNullOrEmpty(currentEdge) || _settings.GetEdgeMode(currentEdge) != "Fixed")
                    return;

                _visibilityController.SetPanelLock(true);
                _visibilityController.CancelHide();

                _isDragging = true;
                _edgeController.IsDragging = true;  // ★★★ 同步到 EdgeController ★★★
                _dragStartPoint = e.GetPosition(_window);
                _dragStartLeft = _window.Left;
                _dragStartTop = _window.Top;
                _dragTarget.CaptureMouse();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragController.OnMouseDown error: {ex.Message}");
                ForceRelease();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            try
            {
                var currentPos = e.GetPosition(_window);
                double deltaX = currentPos.X - _dragStartPoint.X;
                double deltaY = currentPos.Y - _dragStartPoint.Y;

                double newLeft = _dragStartLeft + deltaX;
                double newTop = _dragStartTop + deltaY;

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                newLeft = Math.Max(0, Math.Min(newLeft, screenWidth - _window.Width));
                newTop = Math.Max(0, Math.Min(newTop, screenHeight - _window.Height));

                _window.Left = newLeft;
                _window.Top = newTop;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragController.OnMouseMove error: {ex.Message}");
                ForceRelease();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            try
            {
                _isDragging = false;
                _edgeController.IsDragging = false;  // ★★★ 同步到 EdgeController ★★★
                _dragTarget.ReleaseMouseCapture();

                _lastDragEndTime = DateTime.Now;

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double offset = 0;
                string edge = _edgeController.CurrentEdge;

                switch (edge)
                {
                    case "Top":
                    case "Bottom":
                        offset = _window.Left - (screenWidth / 2 - _window.Width / 2);
                        break;
                    case "Left":
                    case "Right":
                        offset = _window.Top - (screenHeight / 2 - _window.Height / 2);
                        break;
                }
                _settings.SetFixedOffset(edge, offset);

                _window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _visibilityController.SetPanelLock(false);
                        _visibilityController.ForceHide();
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);

                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragController.OnMouseUp error: {ex.Message}");
                ForceRelease();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                System.Diagnostics.Debug.WriteLine("DragController: MouseLeave triggered, forcing release");
                ForceRelease();
                e.Handled = true;
            }
        }

        private void ForceRelease()
        {
            _isDragging = false;
            _edgeController.IsDragging = false;  // ★★★ 同步到 EdgeController ★★★
            try
            {
                if (_dragTarget.IsMouseCaptured)
                    _dragTarget.ReleaseMouseCapture();
            }
            catch { }
            try
            {
                _visibilityController.SetPanelLock(false);
            }
            catch { }
            Mouse.OverrideCursor = null;
        }

        public void Detach()
        {
            ForceRelease();
            _dragTarget.MouseDown -= OnMouseDown;
            _dragTarget.MouseMove -= OnMouseMove;
            _dragTarget.MouseUp -= OnMouseUp;
            _dragTarget.MouseLeave -= OnMouseLeave;
        }
    }
}