using AnimatedWin2dControls.Controls.AnimatedLyricsLineControl;
using AnimatedWin2dControls.Messages;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;
using WinUIMusicPlayer.Helper;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.Services;
using WinUIMusicPlayer.ViewModel;
using Windows.UI;

namespace WinUIMusicPlayer.DesktopLyrics
{
    /// <summary>
    /// 任务栏歌词条带：透明、置顶、不进任务栏/Alt-Tab，位置固定贴靠主任务栏（固定尺寸 400×60）。
    /// 基类与 spectrum 一致使用 WinUIEx.WindowEx。
    /// 贴靠：FindWindow(Shell_TrayWnd) + GetWindowRect 取任务栏矩形，横向任务栏才贴靠其左侧，
    ///       否则回退主屏工作区底部；750ms 定时比对几何/HWND，变化才重新定位（参考 MusicBar）。
    /// 置顶：任务栏是特殊的 topmost band 窗口，单次设置不保证始终盖住，故 750ms 定时重申
    ///       SetWindowPos(HWND_TOPMOST, NOMOVE|NOSIZE|NOACTIVATE|NOOWNERZORDER)（MusicBar 同款）。
    /// 窗口样式：GWL_STYLE 移除标题栏/边框位 + OR-in WS_POPUP（WinUIEx ToggleWindowStyle，
    ///       含 SWP_FRAMECHANGED）+ 不可调整大小 + 去掉 DWM 系统投影/圆角/细边框
    ///       （WindowHelper.RemoveWindowDecoration，否则轮廓外仍有阴影、无法融入任务栏）；
    ///       整窗点击穿透常开（WS_EX_LAYERED 一次性设置常驻，
    ///       运行期只切 WS_EX_TRANSPARENT），光标移到条带右侧按钮区才临时取消穿透供点击
    ///       （游标轮询两档：悬停窗口期 50ms 快轮询保证跟手，其余 200ms 慢轮询只做进窗检测与自愈，
    ///       BetterLyrics OverlayInputHelper 思路），慢轮询附带自愈：窗口被前后台切换偶发
    ///       置为不可见/最小化时无焦点拉回并重申置顶。
    /// 自动隐藏：系统开启「自动隐藏任务栏」且任务栏当前收起时条带一并隐藏，弹出后恢复。
    /// </summary>
    public sealed partial class DesktopLyricsWindow : WinUIEx.WindowEx, IDisposable
    {
        /// <summary>条带固定宽度（不随任务栏宽度变化）。</summary>
        private const int FixedWidth = 600;
        /// <summary>贴靠任务栏左侧的外边距（与 MusicBar 一致）。</summary>
        private const int LeftMargin = 20;
        /// <summary>条带固定高度（底边对齐任务栏底部，不再随任务栏高度变化）。</summary>
        private const int BarHeight = 70;
        /// <summary>贴靠刷新节拍：重申置顶 + 任务栏几何/自动隐藏变化检测（MusicBar 同款 750ms）。</summary>
        private const double DockPollingIntervalMs = 750;
        private const double HoverPollingIntervalMs = 50;    // 悬停窗口期间：穿透切换要跟手
        private const double IdlePollingIntervalMs = 200;    // 静默期：进窗检测 + 自愈
        private const double ControlPanelHoverMargin = 6.0;
        private const double AdaptiveSamplingIntervalMs = 1000;   // 环境取色轮询周期（BetterLyrics 同款 1s）
        private const double AdaptiveSwitchThreshold = 128;       // 环境 YIQ 亮度中位数阈值：低于视为暗背景（白字）
        private const double AdaptiveHysteresis = 16;             // 切换滞回带：边界附近的采样抖动不引起黑白来回闪

        private IDesktopLyricsRenderer? _renderer;
        private readonly IntPtr _hwnd;
        private ThemeStyleHelper? _themeStyleHelper;

        /// <summary>桌面歌词状态源（开关 / 逐字 / 样式）。</summary>
        public DesktopLyricsViewModel ViewModel { get; } = App.Services.GetRequiredService<DesktopLyricsViewModel>();

        /// <summary>条带按钮的播放控制命令（上一曲 / 播放暂停 / 下一曲），自带可用态守卫。</summary>
        public PlaybackCommands Playback { get; } = App.Services.GetRequiredService<PlaybackCommands>();

        private bool _clickThrough;              // 当前穿透样式状态（false = 尚未设置）
        private bool _cursorOverPanel;
        private DispatcherQueueTimer? _hoverTimer;   // 50ms，仅光标悬停窗口期间运行
        private DispatcherQueueTimer? _idleTimer;    // 200ms，常驻：进窗检测 + 自愈
        private DispatcherQueueTimer? _dockTimer;    // 750ms：贴靠刷新 + 重申置顶 + 自动隐藏同步
        private bool _disposed;
        private bool _isOverlayVisible = true;

        private IntPtr _lastTaskbarHwnd;            // 上次贴靠的任务栏（HWND 变化 = explorer 重启）
        private WindowHelper.RECT _lastTaskbarRect; // 上次贴靠的任务栏矩形，变化才重新定位
        private bool _taskbarHidden;                // 任务栏自动隐藏且当前收起 → 条带一并隐藏
        private RectInt32? _panelScreenRectCache;   // 按钮区屏幕矩形缓存（含悬停外扩）；窗口位置/尺寸变化时失效
        private DispatcherQueueTimer? _adaptiveColorTimer;
        private bool? _adaptiveIsDarkBackground;    // 上次明暗判定（null=未判定），滞回切换的基准
        private Color? _lastAdaptiveTextColor;      // 当前应用的取色文字色（判定不变则跳过重绘）

        public DesktopLyricsWindow()
        {
            InitializeComponent();
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 渲染器按"逐字效果"开关选择：CanvasLyricsRenderer（Win2D 逐字扫光）或
            // TextBlockLyricsRenderer（文本描边）。开关变化经 PropertyChanged 热切换（EnsureRenderer）。
            EnsureRenderer(ViewModel.IsKaraokeEnabled);

            // 复用 WinUIEx 自带的完全透明背景（与主程序"透明"样式同源）
            SystemBackdrop = new TransparentTintBackdrop();
            ConfigureWindow();
            UpdateAdaptiveColorMode();

            // 贴靠任务栏：先立窗口样式（无边框/穿透/置顶）再定位，最后起 750ms 刷新节拍
            ApplyOverlayStyle();
            DockToTaskbar();
            StartDockTimer();
            StartCursorPolling();

            // 跟随主程序明暗主题（MusicDetailsWindow 同款）：只转发 themeChanged，
            // 不调用 SetAppStyle——覆盖窗口需保持全透明背景，不能被换成亚克力/云母
            _themeStyleHelper = new ThemeStyleHelper(this, AppWindow);
            _themeStyleHelper.SetAppTheme();
            if (App.MainWindow is not null)
            {
                App.MainWindow.themeChanged += MainWindow_themeChanged;
            }

            UILyricsBus.Changed += OnUILyricsChanged;
            TimeProgressBus.CurrentPlayingTimeChanged += OnTimeProgressChanged;
            OffsetMsBus.Changed += OnOffsetChanged;
            IsPlayingBus.Changed += OnIsPlayingChanged;
            AppWindow.Changed += OnAppWindowChanged;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            Closed += OnWindowClosed;

            // 拉取全量歌词/进度/样式状态（AppViewModel.SendFullLyricsSync）
            LyricsSyncRequestBus.Request();
        }

        /// <summary>复用窗口和渲染器；隐藏时停止采样、自愈及渲染，恢复时重拉状态。</summary>
        public void SetOverlayVisible(bool visible)
        {
            if (_disposed || _isOverlayVisible == visible) return;
            _isOverlayVisible = visible;
            if (!visible)
            {
                StopHoverTimer();
                StopIdleTimer();
                StopDockTimer();
                StopAdaptiveColorTimer();
                _renderer?.SetSuspended(true);
                AppWindow.Hide();
                return;
            }

            LyricsSyncRequestBus.Request();
            _renderer?.SetSuspended(false);
            WindowHelper.RestoreOverlay(_hwnd);
            ApplyOverlayStyle();
            DockToTaskbar();
            StartDockTimer();
            UpdateAdaptiveColorMode();
        }

        /// <summary>
        /// 条带窗口样式：无边框无标题栏、不可调整大小、置顶、整窗点击穿透常开。
        /// 替代原「锁定/解锁」两套样式——位置固定贴靠任务栏后不再需要解锁态。
        /// </summary>
        private void ApplyOverlayStyle()
        {
            // LAYERED 常驻且只设置一次（穿透开关只切 TRANSPARENT）：
            // 运行期反复增删 LAYERED 会与 DWM 分层合成竞态，前后台切换时偶发整窗隐身
            WindowHelper.EnsureLayered(_hwnd);
            ApplyClickThrough(true);

            // GWL_STYLE：移除标题栏/边框位 + OR-in WS_POPUP（均含 SWP_FRAMECHANGED 立即重算）
            this.ToggleWindowStyle(false, WindowStyle.Caption | WindowStyle.ThickFrame);
            this.ToggleWindowStyle(true, WindowStyle.Popup | WindowStyle.Visible);
            // presenter 意图同步为无边框，防止其簿记在后续事件中重放 WS_THICKFRAME
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
            }
            // 去掉 DWM 系统投影/圆角/细边框（无边框样式仍会残留系统阴影，仅靠 XAML 去不掉）；
            // 需在 GWL_STYLE 变更（SWP_FRAMECHANGED）之后调用，避免被重算覆盖
            WindowHelper.RemoveWindowDecoration(_hwnd);
            InvalidatePanelScreenRect();   // 边框样式切换可能改变客户区原点
        }

    /// <summary>
    /// 把有效样式推给渲染器：自定义颜色覆盖开启时用样式原色，
    /// 否则用环境取色结果（黑/白）覆盖样式颜色（悬浮窗默认跟随背景）。
    /// </summary>
    private void ApplyEffectiveStyle()
    {
        if (_renderer is null) return;
        DesktopLyricsStyle style = ViewModel.Style;
        if (!style.UseCustomColor && _lastAdaptiveTextColor is { } adaptive)
        {
            style = style with { Color = adaptive };
        }
        _renderer.SetStyle(style);
    }

    /// <summary>按样式快照的自定义颜色覆盖开关启停环境取色轮询；任何样式变化都全量推送渲染器。
    /// 注意自适应模式下不能只刷新取色：字号/字重/阴影强度等非颜色样式改动若不显式推送，
    /// 要等到下一次黑白翻转或渲染器切换才生效（曾表现为阴影滑块拖动无效）。</summary>
    private void UpdateAdaptiveColorMode()
    {
        if (!_isOverlayVisible || ViewModel.Style.UseCustomColor)
        {
            StopAdaptiveColorTimer();
            _adaptiveIsDarkBackground = null;
            _lastAdaptiveTextColor = null;
        }
        else
        {
            StartAdaptiveColorTimer();
        }

        // 无条件全量推送（幂等、轻量）：自适应模式下颜色由 _lastAdaptiveTextColor 覆盖
        ApplyEffectiveStyle();

        if (_isOverlayVisible && !ViewModel.Style.UseCustomColor)
            RefreshAdaptiveColor();
    }

    private void StartAdaptiveColorTimer()
    {
        if (_adaptiveColorTimer is null)
        {
            _adaptiveColorTimer = DispatcherQueue.CreateTimer();
            _adaptiveColorTimer.Interval = TimeSpan.FromMilliseconds(AdaptiveSamplingIntervalMs);
            _adaptiveColorTimer.Tick += (_, _) => RefreshAdaptiveColor();
        }
        _adaptiveColorTimer.Start();
    }

    private void StopAdaptiveColorTimer() => _adaptiveColorTimer?.Stop();

    /// <summary>采样歌词实际绘制区域（渲染器上报边界，环带 36px）周围的环境亮度，
    /// 无文本时回退窗口外圈；经滞回判定黑/白文字色，判定不变则跳过重绘。</summary>
    private void RefreshAdaptiveColor()
    {
        if (!_isOverlayVisible || ViewModel.Style.UseCustomColor) return;

        bool sampled;
        double luminance = 0;
        if (_renderer?.LastTextBounds is { } bounds && bounds.Width > 1 && bounds.Height > 1)
        {
            (int X, int Y, int Width, int Height) screen = MapToScreen(bounds);
            sampled = screen.Width > 0 &&
                DesktopLyricsAdaptiveColor.TrySampleRing(screen.X, screen.Y, screen.Width, screen.Height, out luminance);
        }
        else
        {
            sampled = DesktopLyricsAdaptiveColor.TrySampleBackgroundLuminance(_hwnd, out luminance);
        }
        if (!sampled) return;

        bool isDark;
        if (_adaptiveIsDarkBackground is { } previous)
        {
            // 滞回：只有明确越过阈值±滞回带才翻转，边界值每秒重采的抖动不切换
            isDark = previous
                ? luminance < AdaptiveSwitchThreshold + AdaptiveHysteresis
                : luminance < AdaptiveSwitchThreshold - AdaptiveHysteresis;
        }
        else
        {
            isDark = luminance < AdaptiveSwitchThreshold;
        }

        if (_adaptiveIsDarkBackground == isDark) return;
        _adaptiveIsDarkBackground = isDark;
        _lastAdaptiveTextColor = isDark ? Colors.White : Colors.Black;
        ApplyEffectiveStyle();
    }

    /// <summary>元素坐标（DIP，相对窗口内容根）→ 屏幕物理像素矩形。
    /// 与 ControlPanel 命中测试同款换算：ClientToScreen 客户区原点 + XamlRoot 光栅化缩放。</summary>
    private (int X, int Y, int Width, int Height) MapToScreen(Rect elementBounds)
    {
        double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        var origin = new WindowHelper.POINT();
        if (!WindowHelper.ClientToScreen(_hwnd, ref origin)) return default;
        return (
            origin.X + (int)Math.Round(elementBounds.X * scale),
            origin.Y + (int)Math.Round(elementBounds.Y * scale),
            (int)Math.Ceiling(elementBounds.Width * scale),
            (int)Math.Ceiling(elementBounds.Height * scale));
    }

    /// <summary>
    /// 按逐字效果开关选择/热切换渲染器。切换时旧渲染器销毁（内容从可视树摘除并释放 Win2D 资源），
    /// 新渲染器应用当前样式；歌词/进度快照由调用方经 LyricsSyncRequestBus.Request() 重拉。
    /// </summary>
    private void EnsureRenderer(bool karaoke)
    {
        if (_renderer is not null && (_renderer is CanvasLyricsRenderer) == karaoke) return;

        if (_renderer is not null)
        {
            RendererHost.Content = null;
            _renderer.Dispose();
        }
        _renderer = karaoke ? new CanvasLyricsRenderer() : new TextBlockLyricsRenderer();
        _renderer.SetSuspended(!_isOverlayVisible);
        RendererHost.Content = _renderer.Content;
        ApplyEffectiveStyle();
    }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(DesktopLyricsViewModel.IsKaraokeEnabled):
                    EnsureRenderer(ViewModel.IsKaraokeEnabled);
                    LyricsSyncRequestBus.Request();   // 新渲染器重拉歌词/进度全量快照
                    break;
                case nameof(DesktopLyricsViewModel.Style):
                    UpdateAdaptiveColorMode();
                    break;
            }
        }

        public void Dispose()
        {
            if (!_disposed) Close();
        }

        private void ConfigureWindow()
        {
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            this.SetTitleBarBackgroundColors(Colors.Transparent);
            // 不进 Alt-Tab / 任务栏（MusicBar 用 WS_EX_TOOLWINDOW 达成同样效果）
            AppWindow.IsShownInSwitchers = false;
        }

        /// <summary>
        /// 贴靠主任务栏：宽度固定 FixedWidth，高度取任务栏高度，贴任务栏左侧（留 LeftMargin）。
        /// 仅横向任务栏（底/顶）贴靠；未探测到任务栏或竖直任务栏时回退主屏工作区底部（MusicBar 同款策略）。
        /// 矩形均为物理像素，与 GetWindowRect / AppWindow.Move 同坐标系，无需 DPI 换算。
        /// </summary>
        private void DockToTaskbar()
        {
            var info = TaskbarInfoProvider.GetPrimary();
            int x, y, height;

            if (info.Found && info.IsHorizontal)
            {
                height = BarHeight;
                x = info.X + LeftMargin;
                // 底边对齐任务栏底部（条带可能高于任务栏，向上溢出）
                y = info.Y + info.Height - BarHeight;
                _lastTaskbarHwnd = info.Hwnd;
                _lastTaskbarRect = new WindowHelper.RECT
                {
                    Left = info.X,
                    Top = info.Y,
                    Right = info.X + info.Width,
                    Bottom = info.Y + info.Height,
                };
            }
            else
            {
                var work = DisplayArea.Primary.WorkArea;
                height = BarHeight;
                x = work.X + LeftMargin;
                y = work.Y + work.Height - height;
                _lastTaskbarHwnd = IntPtr.Zero;
                _lastTaskbarRect = default;
            }

            WindowSizeHelper.MoveAndResizeExact(AppWindow, x, y, FixedWidth, height);
            InvalidatePanelScreenRect();   // 位置/尺寸变化后按钮区屏幕矩形需重算
            KeepAboveTaskbar();
        }

        /// <summary>重申置顶（不动位置/尺寸/焦点）：任务栏是特殊的 topmost band 窗口，单次设置不足以保证层级。</summary>
        private void KeepAboveTaskbar() => WindowHelper.KeepTopmost(_hwnd);

        private void StartDockTimer()
        {
            if (_dockTimer is null)
            {
                _dockTimer = DispatcherQueue.CreateTimer();
                _dockTimer.Interval = TimeSpan.FromMilliseconds(DockPollingIntervalMs);
                _dockTimer.Tick += OnDockTimerTick;
            }
            _dockTimer.Start();
        }

        private void StopDockTimer() => _dockTimer?.Stop();

        /// <summary>750ms 节拍：任务栏自动隐藏同步 → 几何/HWND 变化重新贴靠 → 重申置顶。</summary>
        private void OnDockTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_disposed || !_isOverlayVisible)
            {
                sender.Stop();
                return;
            }

            var info = TaskbarInfoProvider.GetPrimary();
            bool hidden = info.Found && info.AutoHideEnabled && info.IsHidden;
            if (hidden != _taskbarHidden)
            {
                _taskbarHidden = hidden;
                if (hidden) HideForHiddenTaskbar();
                else RestoreFromHiddenTaskbar();
                return;
            }
            if (hidden) return;

            // 矩形或 HWND 变化才重新定位：HWND 变化覆盖 explorer 重启，矩形变化覆盖改高度/分辨率/缩放；
            // 也顺带处理窗口被前后台切换偶发置为不可见/最小化（自愈）
            bool geometryChanged = !info.Found
                ? _lastTaskbarHwnd != IntPtr.Zero
                : info.Hwnd != _lastTaskbarHwnd
                  || info.X != _lastTaskbarRect.Left
                  || info.Y != _lastTaskbarRect.Top
                  || info.Width != _lastTaskbarRect.Width
                  || info.Height != _lastTaskbarRect.Height;

            if (geometryChanged || !WindowHelper.IsWindowVisible(_hwnd) || WindowHelper.IsIconic(_hwnd))
            {
                DockToTaskbar();
                return;
            }

            KeepAboveTaskbar();
        }

        /// <summary>任务栏收起：条带一并隐藏并停下所有轮询与渲染。</summary>
        private void HideForHiddenTaskbar()
        {
            StopHoverTimer();
            StopIdleTimer();
            StopAdaptiveColorTimer();
            _renderer?.SetSuspended(true);
            AppWindow.Hide();
        }

        /// <summary>任务栏弹出：恢复显示并重拉全量歌词快照。</summary>
        private void RestoreFromHiddenTaskbar()
        {
            LyricsSyncRequestBus.Request();
            _renderer?.SetSuspended(false);
            WindowHelper.RestoreOverlay(_hwnd);
            DockToTaskbar();
            StartCursorPolling();
            UpdateAdaptiveColorMode();
            // 切样式会写入 WS_VISIBLE；若期间用户已从托盘关闭歌词，隐藏优先
            if (!_isOverlayVisible) AppWindow.Hide();
        }

        private void ApplyClickThrough(bool enable)
        {
            if (_clickThrough == enable) return;
            _clickThrough = enable;
            WindowHelper.SetClickThrough(_hwnd, enable);
        }

        /// <summary>开启光标轮询（慢轮询常驻：进窗检测 + 自愈；快轮询在光标进入窗口时才起）。</summary>
        private void StartCursorPolling()
        {
            if (!_isOverlayVisible) return;
            _cursorOverPanel = false;
            StartIdleTimer();
        }

        private void StartHoverTimer()
        {
            if (_hoverTimer is null)
            {
                _hoverTimer = DispatcherQueue.CreateTimer();
                _hoverTimer.Interval = TimeSpan.FromMilliseconds(HoverPollingIntervalMs);
                _hoverTimer.Tick += OnHoverTimerTick;
            }
            _hoverTimer.Start();
        }

        private void StopHoverTimer()
        {
            _hoverTimer?.Stop();
        }

        private void StartIdleTimer()
        {
            if (_idleTimer is null)
            {
                _idleTimer = DispatcherQueue.CreateTimer();
                _idleTimer.Interval = TimeSpan.FromMilliseconds(IdlePollingIntervalMs);
                _idleTimer.Tick += OnIdleTimerTick;
            }
            _idleTimer.Start();
        }

        private void StopIdleTimer()
        {
            _idleTimer?.Stop();
        }

        /// <summary>静默期轮询（200ms）：自愈 + 进窗检测；一旦发现光标悬停窗口即切入 50ms 快轮询。</summary>
        private void OnIdleTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (!_isOverlayVisible)
            {
                sender.Stop();
                return;
            }
            // 自愈：被前后台切换偶发置为不可见/最小化的穿透窗口不进任务栏、无恢复入口（表现为歌词消失），
            // 检测到即无焦点拉回并重申置顶
            if (!WindowHelper.IsWindowVisible(_hwnd) || WindowHelper.IsIconic(_hwnd))
            {
                WindowHelper.RestoreOverlay(_hwnd);
            }
            if (WindowHelper.GetCursorPos(out WindowHelper.POINT cursor) && IsCursorOverWindow(cursor))
            {
                StartHoverTimer();
            }
        }

        private void OnHoverTimerTick(DispatcherQueueTimer sender, object args)
        {
            // 离开窗口：恢复穿透、停快轮询，回到慢速自愈轮询
            if (!_isOverlayVisible || !WindowHelper.GetCursorPos(out WindowHelper.POINT cursor) || !IsCursorOverWindow(cursor))
            {
                sender.Stop();
                _cursorOverPanel = false;
                ApplyClickThrough(true);
                return;
            }
            // 光标移到按钮上 = 临时取消穿透供点击；离开按钮立即恢复穿透
            bool overPanel = IsCursorOverControlPanel(cursor);
            if (overPanel != _cursorOverPanel)
            {
                _cursorOverPanel = overPanel;
                ApplyClickThrough(!overPanel);
            }
        }

        /// <summary>游标是否悬停在本窗口上（AppWindow.Position/Size 与 GetCursorPos 同为物理像素）。</summary>
        private bool IsCursorOverWindow(WindowHelper.POINT cursor)
        {
            PointInt32 pos = AppWindow.Position;
            SizeInt32 size = AppWindow.Size;
            return cursor.X >= pos.X && cursor.X < pos.X + size.Width
                && cursor.Y >= pos.Y && cursor.Y < pos.Y + size.Height;
        }

        /// <summary>
        /// 游标是否悬停在条带右侧的按钮区上。屏幕矩形按窗口位置/尺寸变化缓存
        /// （见 <see cref="InvalidatePanelScreenRect"/>）：贴靠后窗口静止，
        /// 命中缓存时纯数值比较，避免每 tick 的 TransformToVisual 分配与 XAML 调用。
        /// </summary>
        private bool IsCursorOverControlPanel(WindowHelper.POINT cursor)
        {
            if (_panelScreenRectCache is { } cached)
            {
                return cursor.X >= cached.X && cursor.X <= cached.X + cached.Width
                    && cursor.Y >= cached.Y && cursor.Y <= cached.Y + cached.Height;
            }
            if (ControlPanel.ActualWidth <= 0 || RootGrid.XamlRoot is null) return false;
            double scale = RootGrid.XamlRoot.RasterizationScale;
            Rect bounds = ControlPanel.TransformToVisual(null)
                .TransformBounds(new Rect(0, 0, ControlPanel.ActualWidth, ControlPanel.ActualHeight));
            var origin = new WindowHelper.POINT();
            if (!WindowHelper.ClientToScreen(_hwnd, ref origin)) return false;
            int left = origin.X + (int)((bounds.X - ControlPanelHoverMargin) * scale);
            int top = origin.Y + (int)((bounds.Y - ControlPanelHoverMargin) * scale);
            int right = origin.X + (int)((bounds.X + bounds.Width + ControlPanelHoverMargin) * scale);
            int bottom = origin.Y + (int)((bounds.Y + bounds.Height + ControlPanelHoverMargin) * scale);
            _panelScreenRectCache = new RectInt32(left, top, right - left, bottom - top);
            return cursor.X >= left && cursor.X <= right && cursor.Y >= top && cursor.Y <= bottom;
        }

        /// <summary>按钮组屏幕矩形依赖窗口位置/尺寸/DPI，变化后需重算。</summary>
        private void InvalidatePanelScreenRect() => _panelScreenRectCache = null;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsEnabled = false;
        }

        // ==== 数据总线转发 ====

        private void OnUILyricsChanged(IList<LyricLine>? value)
        {
            if (_isOverlayVisible) _renderer?.SetLyrics(value);
        }

        private void OnTimeProgressChanged(long totalMs)
        {
            if (_isOverlayVisible) _renderer?.SetPlaybackTime(totalMs);
        }

        private void OnOffsetChanged(double value)
        {
            if (_isOverlayVisible) _renderer?.SetOffset(value);
        }

        private void OnIsPlayingChanged(bool value)
        {
            if (_isOverlayVisible) _renderer?.SetIsPlaying(value);
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            // z 序变动后若被挤出置顶层（其他置顶窗口切换可致），幂等重申，防"被盖住"表现为消失
            if (_isOverlayVisible && args.DidZOrderChange) WindowHelper.EnsureTopmost(_hwnd);
            // 位置/尺寸只影响按钮区屏幕矩形缓存（贴靠由 750ms 定时器负责，不再记边界）
            if (args.DidPositionChange || args.DidSizeChange) InvalidatePanelScreenRect();
        }

        private void MainWindow_themeChanged(object? sender, EventArgs e)
        {
            _themeStyleHelper?.SetAppTheme();
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (App.MainWindow is not null)
            {
                App.MainWindow.themeChanged -= MainWindow_themeChanged;
            }
            UILyricsBus.Changed -= OnUILyricsChanged;
            TimeProgressBus.CurrentPlayingTimeChanged -= OnTimeProgressChanged;
            OffsetMsBus.Changed -= OnOffsetChanged;
            IsPlayingBus.Changed -= OnIsPlayingChanged;
            AppWindow.Changed -= OnAppWindowChanged;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Closed -= OnWindowClosed;
            StopHoverTimer();
            StopIdleTimer();
            StopDockTimer();
            StopAdaptiveColorTimer();
            _renderer?.Dispose();
            _renderer = null;
            _disposed = true;
        }
    }
}
