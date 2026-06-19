using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using LingDongBird.Core;

namespace LingDongBird.ViewModels
{
    public class WindowSwitcherViewModel
    {
        private readonly DispatcherTimer _refreshTimer;
        private readonly ObservableCollection<WindowListProvider.WindowItem> _windows = new();

        public ObservableCollection<WindowListProvider.WindowItem> Windows => _windows;

        public WindowSwitcherViewModel()
        {
            Refresh();
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(500);
            _refreshTimer.Tick += (s, e) => Refresh();
            _refreshTimer.Start();
        }

        public void Refresh()
        {
            try
            {
                var newList = WindowListProvider.GetOpenWindows();
                var existingHandles = _windows.Select(x => x.Handle).ToHashSet();

                foreach (var item in newList)
                {
                    if (!existingHandles.Contains(item.Handle))
                        _windows.Add(item);
                }

                var newHandles = newList.Select(x => x.Handle).ToHashSet();
                for (int i = _windows.Count - 1; i >= 0; i--)
                {
                    if (!newHandles.Contains(_windows[i].Handle))
                        _windows.RemoveAt(i);
                }

                var newDict = newList.ToDictionary(x => x.Handle);
                foreach (var item in _windows)
                {
                    if (newDict.TryGetValue(item.Handle, out var updated))
                        item.Title = updated.Title;
                }
            }
            catch { }
        }

        public WindowListProvider.WindowItem? FindItemByHandle(IntPtr handle)
        {
            return _windows.FirstOrDefault(x => x.Handle == handle);
        }

        public void Close(IntPtr hwnd) => WindowAction.Close(hwnd);

        public void Stop() => _refreshTimer?.Stop();
    }
}