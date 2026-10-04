using DevWinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;
using WinUIMusicPlayer.DesktopLyrics;
using WinUIMusicPlayer.Helper;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.Services;
using WinUIMusicPlayer.Services.NavigationService;
using WinUIMusicPlayer.Utils;
using WinUIMusicPlayer.View.SubView;
using WinUIMusicPlayer.ViewModel;
using WinUIMusicPlayer.ViewModel.Pages;
using ZLinq;
using static WinUIMusicPlayer.Utils.ToolUtils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUIMusicPlayer.View
{
    public enum TitleBarArea { None, Top }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }
        public EqualizerDialog EqualizerDialog { get; set; }
        public SettingsDialog SettingsDialog { get; set; }
        public AddPlayListDialog AddPlayListDialog { get; set; }
        /// <summary>系统文本缩放绑定根：ActualHeight 无变更通知，经此 INPC 服务随系统设置联动。</summary>
        public TextScaleService TextScale => TextScaleService.Instance;
        private readonly INavigationService _playingNavigation;
        private bool _isPageTransitioning = false;
        /// <summary>NavigationView.IsPaneOpen 属性变化回调令牌（卸载时注销）。</summary>
        private long _isPaneOpenCallbackToken;
        /// <summary>当前 MusicBrowsePage 是否由音乐库页打开：决定子页返回键能否回到音乐库页。</summary>
        private bool _browsePageReturnToLibrary;

        /// <summary>侧边栏导航项 tag → (未选中图标, 选中图标) 配对，沿用参考项目 MusicPlayer 的字形方向（选中=空心）。</summary>
        private static readonly Dictionary<string, (IconKind Unselected, IconKind Selected)> NavIconMap = new()
        {
            ["AllSongs"] = (IconKind.AllSongsOutline, IconKind.AllSongsFilled),
            ["MusicLibrary"] = (IconKind.LibraryOutline, IconKind.LibraryFilled),
            ["ArtistList"] = (IconKind.ArtistOutline, IconKind.ArtistFilled),
            ["AlbumList"] = (IconKind.AlbumOutline, IconKind.AlbumFilled),
            ["Stats"] = (IconKind.Stats, IconKind.Stats1),
            ["Settings"] = (IconKind.SettingsOutline, IconKind.SettingsFilled),
        };

        public bool IsPlayingDetailVisible => PlayingFrame.Visibility == Visibility.Visible;
        //private ToolTip _progressToolTip = new();
        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = this;
            var navigationServiceFactory = App.Services.GetRequiredService<INavigationServiceFactory>();
            _playingNavigation = navigationServiceFactory.CreateNavigationService(PlayingFrame);
            _playingNavigation.RegisterPage<PlayingDetailPage>();
            ViewModel.MusicBrowseVM.SetMainPage(this);
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
            ViewModel.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
        }

        private void DesktopLyricsButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DesktopLyrics.IsEnabled = !ViewModel.DesktopLyrics.IsEnabled;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationViewControl.IsPaneOpen = false;
            // IsPaneOpen 在点击瞬间就翻转；PaneOpened/PaneClosed 要等开合动画结束才触发（图标会明显滞后），
            // 因此改为监听属性变化，覆盖点击/选中项自动收起/轻触消失等所有入口。
            _isPaneOpenCallbackToken = NavigationViewControl.RegisterPropertyChangedCallback(
                NavigationView.IsPaneOpenProperty, OnIsPaneOpenChanged);
            ViewModel.AreOtherButtonsVisible = NavigationViewControl.IsPaneOpen;
            CollapsePaneCloseButton();
            SetupSettingsItemIcon();
            NavigateToDefaultPage();
            InitiaizeEqualizerDialog();
            SetSettingsDialog();
            AddPlayListDialog ??= new AddPlayListDialog(ViewModel.AppViewModel);
            NavigationViewControl.Visibility = Visibility.Visible;
            if (App.MainWindow is { } mainWindow)
            {
                mainWindow.AppWindow.Changed += MainPage_AppWindow_Changed;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
                SetTitleBarArea(TitleBarArea.Top);
            }
            ViewModel.AppViewModel.UpdateMaximizeState();
            UpdateAlbumCoverSpin(ViewModel.AppViewModel.IsPlaying);
            Loaded -= MainPage_Loaded;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.MainWindow is { } mw)
                    mw.AppWindow.Changed -= MainPage_AppWindow_Changed;
            }
            catch
            {
            }
            if (_isPaneOpenCallbackToken != 0)
            {
                NavigationViewControl.UnregisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, _isPaneOpenCallbackToken);
                _isPaneOpenCallbackToken = 0;
            }
            Unloaded -= MainPage_Unloaded;
        }

        /// <summary>播放时让底栏圆形封面持续旋转；暂停/停止时暂停（保留当前角度，恢复时无缝衔接）。</summary>
        private void OnAppViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppViewModel.IsPlaying))
            {
                UpdateAlbumCoverSpin(ViewModel.AppViewModel.IsPlaying);
            }
        }

        private Storyboard _albumCoverSpinStoryboard;
        private DoubleAnimation _albumCoverSpinAnimation;
        private bool _albumCoverSpinInitialized;

        private void EnsureAlbumCoverSpinStoryboard()
        {
            if (_albumCoverSpinInitialized) return;
            _albumCoverSpinAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(90)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(_albumCoverSpinAnimation, AlbumCoverRotateTransform);
            Storyboard.SetTargetProperty(_albumCoverSpinAnimation, "Angle");
            _albumCoverSpinStoryboard = new Storyboard();
            _albumCoverSpinStoryboard.Children.Add(_albumCoverSpinAnimation);
            _albumCoverSpinInitialized = true;
        }

        private void UpdateAlbumCoverSpin(bool isPlaying)
        {
            EnsureAlbumCoverSpinStoryboard();
            if (isPlaying)
            {
                if (_albumCoverSpinStoryboard.GetCurrentState() == ClockState.Stopped)
                    _albumCoverSpinStoryboard.Begin();
                else
                    _albumCoverSpinStoryboard.Resume();
            }
            else
            {
                _albumCoverSpinStoryboard.Pause();
            }
        }

        private void MainPage_AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {            
            if (args.DidPresenterChange)
            {
                ViewModel.AppViewModel.SyncFullScreenStateFromWindow();
            }
            if (args.DidPositionChange || args.DidSizeChange)
            {
                ViewModel.AppViewModel.UpdateMaximizeState();
                SetTitleBarArea(TitleBarArea.Top);
            }
        }

        private void AppTitleBar_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!ViewModel.AppViewModel.IsPlayingDetailVisible) return;
            ViewModel.AppViewModel.IsPointerOverTitleBar = true;
        }

        private void AppTitleBar_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!ViewModel.AppViewModel.IsPlayingDetailVisible) return;
            ViewModel.AppViewModel.IsPointerOverTitleBar = false;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindow?.Minimize();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            var window = App.MainWindow;
            if (window?.AppWindow.Presenter is OverlappedPresenter overlapped)
            {
                if (overlapped.State == OverlappedPresenterState.Maximized)
                    window.Restore();
                else
                    window.Maximize();
            }
            ViewModel.AppViewModel.UpdateMaximizeState();
        }

        private void FullscreenTitleBarButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AppViewModel.ToggleFullScreen();
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetTitleBarArea(TitleBarArea.Top);
        }

        private static readonly RectInt32[] _dragRectNone = [new(0, 0, 0, 0)];
        private static readonly RectInt32[] _dragRectTop = [new()];

        private void SetTitleBarArea(TitleBarArea titleBarArea)
        {
            if (App.MainWindow?.AppWindow?.TitleBar is null) return;

            var scale = AppTitleBar.XamlRoot?.RasterizationScale ?? 1.0;

            switch (titleBarArea)
            {
                case TitleBarArea.None:
                    App.MainWindow.AppWindow.TitleBar.SetDragRectangles(_dragRectNone);
                    break;

                case TitleBarArea.Top:
                    _dragRectTop[0] = new RectInt32(
                        0, 0,
                        (int)(AppTitleBar.ActualWidth * scale),
                        (int)(AppTitleBar.ActualHeight * scale)
                    );
                    App.MainWindow.AppWindow.TitleBar.SetDragRectangles(_dragRectTop);
                    break;
            }
        }

        private void CloseTitleBarButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is null) return;
            if (AppSettings.IsRunningBackend)
            {
                App.MainWindow.Hide();
                if (AppSettings.IsTrimOnHideEnabled)
                    _ = WorkingSetCompressor.TrimSelfAsync();
            }
            else
            {
                _ = App.Current_Exit();
            }
        }

        private void NavigateTo(Type pageType, object? parameter = null, NavigationTransitionInfo? navigationTransitionInfo = null)
        {
            MainFrame.Navigate(pageType, parameter, navigationTransitionInfo);
            MainFrame.BackStack.Clear();
            // 浏览页的返回目标由 NavigateToMusicBrowseSubPage 记录；离开浏览页即失效。
            if (pageType != typeof(MusicBrowsePage)) _browsePageReturnToLibrary = false;
        }

        private void InitiaizeEqualizerDialog()
        {
            if (EqualizerDialog is null)
            {
                EqualizerDialog = new EqualizerDialog();
                EqualizerDialog.EqualizerCommitted += (s, e) =>
                {
                    // Fire-and-forget full state sync: idempotent, latest state wins;
                    // the dialog already debounces rapid slider drags before committing.
                    ViewModel.PlayerCommandService.EqUpdate();
                };
            }
        }

        private void SetSettingsDialog()
        {
            SettingsDialog ??= new SettingsDialog(ViewModel.AppViewModel);
        }

        private void NavigateToDefaultPage()
        {
            // 旧版启动页 tag 兼容：AddFolder/PlayLists 已并入音乐库，MusicBrowse 即全部歌曲
            var tag = ViewModel.AppViewModel.DefaultEntryComboBoxTag switch
            {
                "AddFolder" or "PlayLists" => "MusicLibrary",
                "MusicBrowse" => "AllSongs",
                var t => t
            };
            switch (tag)
            {
                case "MusicLibrary":
                    SetSelectedNavItemByTag("MusicLibrary");
                    NavigateTo(typeof(MusicLibraryPage), null, new EntranceNavigationTransitionInfo());
                    break;
                case "ArtistList":
                    NavigateToMusicBrowseSubPage("artist");
                    break;
                case "AlbumList":
                    NavigateToMusicBrowseSubPage("album");
                    break;
                case "Stats":
                    SetSelectedNavItemByTag("Stats");
                    NavigateTo(typeof(StatsPage), null, new EntranceNavigationTransitionInfo());
                    break;
                default:
                    NavigateToMusicBrowseSubPage("song");
                    break;
            }

            // 默认导航后刷新侧边栏图标（选中项字形可能已随 SetSelectedNavItemByTag 改变）
            ApplyNavIconSelectionStates();
        }

        public void NavigateToSettingsPage()
        {
            if (PlayingFrame.Visibility is Visibility.Visible)
            {
                NavigationViewControl.Visibility = Visibility.Visible;
                _playingNavigation.Dismiss(300);
                ViewModel.AppViewModel.IsPlayingDetailVisible = false;
                ViewModel.AppViewModel.IsPointerOverTitleBar = true;
            }
            if (MainFrame.Content is not SettingsPage)
            {
                NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                NavigateTo(typeof(SettingsPage), null, new EntranceNavigationTransitionInfo());
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                if (MainFrame.Content is not SettingsPage)
                {
                    NavigateTo(typeof(SettingsPage), null, new EntranceNavigationTransitionInfo());
                }
                return;
            }

            switch (args.InvokedItemContainer.Tag.ToString())
            {
                case "AllSongs":
                    NavigateToMusicBrowseSubPage("song");
                    break;
                case "MusicLibrary":
                    if (MainFrame.Content is not MusicLibraryPage)
                    {
                        NavigateTo(typeof(MusicLibraryPage), null, new EntranceNavigationTransitionInfo());
                    }
                    break;
                case "ArtistList":
                    NavigateToMusicBrowseSubPage("artist");
                    break;
                case "AlbumList":
                    NavigateToMusicBrowseSubPage("album");
                    break;
                case "Stats":
                    if (MainFrame.Content?.GetType() != typeof(StatsPage))
                    {
                        NavigateTo(typeof(StatsPage), null, new EntranceNavigationTransitionInfo());
                    }
                    break;
            }
        }

        /// <summary>导航到 MusicBrowsePage 并切换到指定子页（tag：song/album/artist/folder/favourite）。</summary>
        public void NavigateToMusicBrowseSubPage(string tag)
        {
            // 记录子页来源：从音乐库页进入的子页（最爱等）返回时要回到音乐库页，
            // 侧栏导航进入的浏览页没有上层，返回键仍由浏览页自行处理。
            bool fromMusicLibrary = MainFrame.Content is MusicLibraryPage;
            if (MainFrame.Content is not MusicBrowsePage)
            {
                NavigateTo(typeof(MusicBrowsePage), null, new EntranceNavigationTransitionInfo());
            }
            if (MainFrame.Content is MusicBrowsePage page)
            {
                page.SelectBarItem(tag);
            }
            _browsePageReturnToLibrary = fromMusicLibrary;
            if (fromMusicLibrary) ViewModel.AppViewModel.IsBackBtnEnable = true;
        }

        /// <summary>进入播放列表页（音乐库页歌单入口使用；页面归属音乐库，导航栏保持"音乐库"选中）。</summary>
        public void NavigateToPlayListPage()
        {
            NavigateTo(typeof(PlayListPage), null, new EntranceNavigationTransitionInfo());
        }

        /// <summary>
        /// MusicBrowsePage 子页切换后同步侧边导航选中项；
        /// folder/favourite 无独立导航项（入口在音乐库页），保持"音乐库"选中与入口一致。
        /// </summary>
        public void SyncNavigationSelection(string tag)
        {
            var navTag = tag switch
            {
                "song" => "AllSongs",
                "album" => "AlbumList",
                "artist" => "ArtistList",
                "folder" or "favourite" => "MusicLibrary",
                _ => null
            };
            if (navTag is not null)
            {
                SetSelectedNavItemByTag(navTag);
            }
        }

        private void SetSelectedNavItemByTag(string tag)
        {
            foreach (var item in NavigationViewControl.MenuItems)
            {
                if (item is NavigationViewItem navigationViewItem && navigationViewItem.Tag?.ToString() == tag)
                {
                    NavigationViewControl.SelectedItem = navigationViewItem;
                    return;
                }
            }
        }

        /// <summary>根据当前选中项统一刷新侧边栏所有导航图标（选中态显示对应配对字形），实现图标随选中态变化。</summary>
        private void ApplyNavIconSelectionStates()
        {
            var selected = NavigationViewControl.SelectedItem as NavigationViewItem;

            foreach (var item in NavigationViewControl.MenuItems)
            {
                if (item is not NavigationViewItem navItem || navItem.Icon is not FontIcon fontIcon)
                {
                    continue;
                }
                if (NavIconMap.TryGetValue(navItem.Tag?.ToString() ?? string.Empty, out var pair))
                {
                    var kind = ReferenceEquals(navItem, selected) ? pair.Selected : pair.Unselected;
                    fontIcon.Glyph = IconService.GetIconChar(kind);
                }
            }

            if (NavigationViewControl.SettingsItem is NavigationViewItem settingsItem
                && settingsItem.Icon is FontIcon settingsFont
                && NavIconMap.TryGetValue("Settings", out var settingsPair))
            {
                var kind = ReferenceEquals(settingsItem, selected) ? settingsPair.Selected : settingsPair.Unselected;
                settingsFont.Glyph = IconService.GetIconChar(kind);
            }
        }

        /// <summary>SettingsItem 为只读属性，无法在 XAML 中赋值；改为在加载后为自动生成的设置项设置 Fluent 图标以支持选中态切换。</summary>
        private void SetupSettingsItemIcon()
        {
            if (NavigationViewControl.SettingsItem is NavigationViewItem settingsItem)
            {
                settingsItem.Icon = CreateFluentFontIcon(NavIconMap.TryGetValue("Settings", out var pair) ? pair.Unselected : IconKind.SettingsOutline);
            }
        }

        private static FontIcon CreateFluentFontIcon(IconKind kind) => new()
        {
            Glyph = IconService.GetIconChar(kind),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(IconGlyph.SegoeFluentIconsFontFamily),
        };

        private void NavigationViewControl_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            ApplyNavIconSelectionStates();
        }

        public void NavigateToMusicBrowsePage()
        {
            if (MainFrame.Content is not MusicBrowsePage)
            {
                NavigateTo(typeof(MusicBrowsePage), null, new EntranceNavigationTransitionInfo());
            }
            // 交叉链接可能不触发子页切换（已在目标子页仅进入详情），这里兜底同步导航选中态
            SyncNavigationSelection(App.Services.GetRequiredService<MusicBrowseViewModel>().SelectedPageTag);
        }

        public void NavigateToPlayingDetailPage()
        {
            if (_isPageTransitioning) return;
            _isPageTransitioning = true;

            if (PlayingFrame.Visibility is Visibility.Collapsed)
            {
                var pendingCount = 1;
                void OnOneCompleted()
                {
                    if (Interlocked.Decrement(ref pendingCount) == 0)
                        _isPageTransitioning = false;
                }
                _playingNavigation.Show(typeof(PlayingDetailPage), 300, onCompleted: OnOneCompleted);
                NavigationViewControl.Visibility = Visibility.Collapsed;
                ViewModel.AppViewModel.IsPlayingDetailVisible = true;
                ViewModel.AppViewModel.IsPointerOverTitleBar = false;
            }
            else
            {
                _isPageTransitioning = false;
            }
        }

        public void NavigatebackToMusicBrowsePage()
        {
            if (_isPageTransitioning) return;
            _isPageTransitioning = true;

            if (PlayingFrame.Visibility is Visibility.Visible)
            {
                var pendingCount = 1;
                void OnOneCompleted()
                {
                    if (Interlocked.Decrement(ref pendingCount) == 0)
                        _isPageTransitioning = false;
                }
                _playingNavigation.Dismiss(300, onCompleted: OnOneCompleted);
                NavigationViewControl.Visibility = Visibility.Visible;
                ViewModel.AppViewModel.IsPlayingDetailVisible = false;
                ViewModel.AppViewModel.IsPointerOverTitleBar = true;
            }
            else
            {
                _isPageTransitioning = false;
            }
        }

        public void HandleBackNavigation()
        {
            if (MainFrame.Content is PlayListPage playListPage)
            {
                // 歌单详情由音乐库页进入，歌单列表也在音乐库页：一步返回音乐库页，
                // 避免先退回本页的歌单浏览态（BackStack 已清空，退到那里就无法再返回）。
                playListPage.LeaveDetailForLibrary();
                ReturnToMusicLibraryPage();
                return;
            }
            // 返回键/全局热键在任意主页面都会进入这里。MusicBrowsePage 离开视觉树后，
            // 其 ContentFrame.Content 仍持有缓存页且 IsInDetailMode 为恢复详情态而刻意保留，
            // 继续下发会在已脱离视觉树的页面上触发 ConnectedAnimation 并抛异常。
            if (MainFrame.Content is MusicBrowsePage browsePage)
            {
                if (browsePage.IsSubPageInDetailMode)
                {
                    App.Services.GetRequiredService<MusicBrowseViewModel>().BackButton();
                    return;
                }
                if (_browsePageReturnToLibrary)
                {
                    ReturnToMusicLibraryPage();
                }
            }
        }

        /// <summary>回到音乐库页（浏览子页 / 歌单详情返回用）：BackStack 已被清空，只能重新导航。</summary>
        private void ReturnToMusicLibraryPage()
        {
            _browsePageReturnToLibrary = false;
            ViewModel.AppViewModel.IsBackBtnEnable = false;
            SetSelectedNavItemByTag("MusicLibrary");
            NavigateTo(typeof(MusicLibraryPage), null, new EntranceNavigationTransitionInfo());
        }

        private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            HandleBackNavigation();
        }

        /// <summary>标题栏“返回上一页”按钮：复用现有返回导航逻辑。</summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            HandleBackNavigation();
        }

        /// <summary>IsPaneOpen 变化时同步 AreOtherButtonsVisible（汉堡按钮图标经模板绑定自动切换）并折叠默认的收起箭头按钮。</summary>
        private void OnIsPaneOpenChanged(DependencyObject sender, DependencyProperty dp)
        {
            ViewModel.AreOtherButtonsVisible = NavigationViewControl.IsPaneOpen;
            CollapsePaneCloseButton();
        }

        /// <summary>通过视觉树查找并折叠 NavigationView 默认的收起箭头按钮（PaneCloseButton），避免与自定义按钮重复。</summary>
        private void CollapsePaneCloseButton()
        {
            if (FindVisualChildByName(NavigationViewControl, "PaneCloseButton") is Button closeButton)
            {
                closeButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>BFS 在视觉树中按 Name 查找首个 FrameworkElement。</summary>
        private static DependencyObject? FindVisualChildByName(DependencyObject parent, string name)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(parent);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int count = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    if (child is FrameworkElement fe && fe.Name == name)
                    {
                        return child;
                    }
                    queue.Enqueue(child);
                }
            }
            return null;
        }

        /// <summary>BFS 在视觉树中按类型查找首个匹配元素。</summary>
        private static T? FindVisualChildByType<T>(DependencyObject parent) where T : DependencyObject
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(parent);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int count = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    if (child is T t)
                    {
                        return t;
                    }
                    queue.Enqueue(child);
                }
            }
            return null;
        }

        private void ProgressSlider_Loaded(object sender, RoutedEventArgs e)
        {
            var thumb = FindVisualChild<Thumb>(ProgressSlider);
            if (thumb is not null)
            {
                thumb.DragStarted += Thumb_DragStarted;
                thumb.DragCompleted += Thumb_DragCompleted;
                //thumb.DragDelta += (s, e) =>
                //{
                //    _progressToolTip?.Content = ViewModel.AppViewModel.ProgressSliderThumbTipText;
                //};
                //ToolTipService.SetToolTip(thumb, _progressToolTip);
                //_progressToolTip.Opened += (s, e) =>
                //    _progressToolTip.Content = ViewModel.AppViewModel.ProgressSliderThumbTipText;
            }
        }

        private void ProgressSlider_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ViewModel.AppViewModel.IsMouseOverProgressBar = true;
        }

        private void ProgressSlider_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ViewModel.AppViewModel.IsMouseOverProgressBar = false;
        }

        private void VolumeSlider_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ViewModel.AppViewModel.IsMouseOverVolumeSlider = true;
        }

        private void VolumeButton_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var delta = e.GetCurrentPoint(VolumeButton).Properties.MouseWheelDelta;
            if (delta > 0)
            {
                ViewModel.AppViewModel.AdjustVolume(5);
            }
            else if (delta < 0)
            {
                ViewModel.AppViewModel.AdjustVolume(-5);
            }
            e.Handled = true;
        }

        private void VolumeSlider_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ViewModel.AppViewModel.IsMouseOverVolumeSlider = false;
        }

        private void VolumeSlider_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel.AppViewModel.IsMouseOverVolumeSlider)
            {
                var delta = e.GetCurrentPoint(VolumeSlider).Properties.MouseWheelDelta;
                if (delta > 0)
                {
                    ViewModel.AppViewModel.AdjustVolume(1);
                }
                else if (delta < 0)
                {
                    ViewModel.AppViewModel.AdjustVolume(-1);
                }
                e.Handled = true;
            }
        }

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            ViewModel.AppViewModel.IsUserDraggingProgressSlider = true;
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ViewModel.AppViewModel.IsUserDraggingProgressSlider = false;
            var (_, totalMs) = ViewModel.AppViewModel.GetTimeProgressCache();
            long newPosMs = Math.Max(0, Math.Min((long)(ViewModel.AppViewModel.ProgressSlider * 1000), totalMs));
            ViewModel.AppViewModel.IsManualSelect = true;
            try
            {
                ViewModel.PlayerCommandService.ChangeWaveChannelTime(newPosMs);
            }
            finally { ViewModel.AppViewModel.IsManualSelect = false; }
        }

        private void CurrentPlayListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            _ = App.Services.GetRequiredService<PlaybackCoordinator>().PlayAtAsync(CurrentPlayListView.SelectedIndex);
        }

        private void AutoScrollHover_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is AutoScrollView autoScrollView)
            {
                autoScrollView.IsPlaying = true;
            }
        }

        private void AutoScrollHover_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (sender is AutoScrollView autoScrollView)
            {
                autoScrollView.IsPlaying = false;
            }
        }

        private void AutoScrollHover_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is AutoScrollView autoScrollView)
            {
                autoScrollView.IsPlaying = false;
            }
        }

        private void CurrentPlayListButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentPlayListTeachingTip.IsOpen = true;
            UpdateCurrentPlayList();
        }

        private void CurrentPlayListTeachingTipCloseButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentPlayListTeachingTip.IsOpen = false;
        }

        public void UpdateCurrentPlayList()
        {
            int index = ViewModel.AppViewModel.GetSelectedPlaybackIndex();
            CurrentPlayListView.SelectedIndex = index;
            if (index < 0) return;
            if (CurrentPlayListView.ContainerFromIndex(index) is FrameworkElement container)
                container.StartBringIntoView();
            else CurrentPlayListView.ScrollIntoView(ViewModel.AppViewModel.CurrentPlayingList[index]);
        }

        private void CancelPlayingDetailButton_Click(object sender, RoutedEventArgs e)
        {
            NavigatebackToMusicBrowsePage();
        }
    }
}
