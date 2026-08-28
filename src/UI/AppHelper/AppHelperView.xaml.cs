using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DynamicBird.UI.Media;

namespace DynamicBird.UI.AppHelper
{
    /// <summary>
    /// 应用辅助模式主页：画中画（始终显示）+ 媒体控制（有媒体会话时显示）。
    /// 点击面板中的灵动鸟图标循环切换页面。
    /// </summary>
    public partial class AppHelperView : UserControl
    {
        private sealed class PageInfo
        {
            public string Title { get; }
            public string Hint { get; }
            public FrameworkElement Content { get; }

            public PageInfo(string title, string hint, FrameworkElement content)
            {
                Title = title;
                Hint = hint;
                Content = content;
            }
        }

        private readonly List<PageInfo> _pages = new();
        private readonly PanelMediaPlayer _player = new();
        private readonly MediaControlView _mediaControl = new();
        private readonly DispatcherTimer _sessionCheckTimer;
        private bool _hasMediaSessions;
        private int _pageIndex;

        public AppHelperView()
        {
            InitializeComponent();

            _sessionCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _sessionCheckTimer.Tick += async (_, _) => await CheckSessionsAsync();

            Loaded += async (_, _) =>
            {
                await CheckSessionsAsync();
                _sessionCheckTimer.Start();
            };
            Unloaded += (_, _) => _sessionCheckTimer.Stop();

            RebuildPages();
        }

        public void CyclePage()
        {
            if (_pages.Count > 1) ShowPage((_pageIndex + 1) % _pages.Count);
        }

        private async Task CheckSessionsAsync()
        {
            bool any = (await MediaSessionController.GetSessionsAsync()).Count > 0;
            if (any != _hasMediaSessions)
            {
                _hasMediaSessions = any;
                RebuildPages();
            }
        }

        private void RebuildPages()
        {
            _pages.Clear();
            _pages.Add(new PageInfo(DynamicBird.UI.Localization.LocalizationManager.Instance["AppHelper_Pip"], DynamicBird.UI.Localization.LocalizationManager.Instance["AppHelper_PipDesc"], _player));
            if (_hasMediaSessions)
            {
                _pages.Add(new PageInfo(DynamicBird.UI.Localization.LocalizationManager.Instance["AppHelper_Media"], DynamicBird.UI.Localization.LocalizationManager.Instance["AppHelper_MediaDesc"], _mediaControl));
            }

            if (_pageIndex >= _pages.Count) _pageIndex = 0;
            ShowPage(_pageIndex);
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pages.Count > 1) ShowPage((_pageIndex + _pages.Count - 1) % _pages.Count);
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pages.Count > 1) ShowPage((_pageIndex + 1) % _pages.Count);
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= _pages.Count) return;
            _pageIndex = index;
            var page = _pages[index];
            PageContent.Content = page.Content;
            PageTitle.Text = page.Title;
            PageHint.Text = page.Hint;

            bool multi = _pages.Count > 1;
            BtnPrevPage.Visibility = multi ? Visibility.Visible : Visibility.Collapsed;
            BtnNextPage.Visibility = multi ? Visibility.Visible : Visibility.Collapsed;
            PageIndicator.Visibility = multi ? Visibility.Visible : Visibility.Collapsed;
            PageIndicator.Text = $"{index + 1}/{_pages.Count}";
        }
    }
}
