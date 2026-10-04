using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinUIMusicPlayer.Helper
{
    public partial class CustomMicaSystemBackdrop : SystemBackdrop
    {
        private MicaController _micaController;
        private SystemBackdropConfiguration _backdropConfiguration;

        // 保存 window 对象引用
        private ICompositionSupportsSystemBackdrop _currentTarget;
        private bool _isConnected = false;
        private Window _window;
        // Mica效果属性
        // Base 比 BaseAlt 更浓郁（BaseAlt 是给侧栏等次级表面用的轻量变体，壁纸透得更多）。
        public MicaKind MicaKind { get; set; } = MicaKind.BaseAlt;
        public Color TintColor { get; set; } = Color.FromArgb(255, 32, 32, 32);
        // Mica 的标准通透感来自 TintOpacity < 1.0；Windows 默认值为 0.8。
        // 值越大着色越浓；需要更浓郁就往 1.0 调。注意：Mica 本身仍会掺入壁纸，
        // 想要纯实心自定义色请用 Acrylic（CustomAcrylicStyle）。
        public float TintOpacity { get; set; } = 0.1f;
        // Mica 不支持/未激活时回退的纯色，避免回退到透明而显得"淡"。
        public Color FallbackColor { get; set; } = Color.FromArgb(255, 32, 32, 32);
        public bool IsInputActive = false;

        public CustomMicaSystemBackdrop(Window window = null)
        {
            _window = window;
        }

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            _currentTarget = connectedTarget;
            _isConnected = true;

            _backdropConfiguration = new SystemBackdropConfiguration();

            // 根据应用当前主题设置背景配置
            Microsoft.UI.Xaml.FrameworkElement rootElement = xamlRoot.Content as Microsoft.UI.Xaml.FrameworkElement;
            SetConfigurationSourceTheme(rootElement);

            // 监听主题变更事件
            rootElement.ActualThemeChanged += RootElement_ActualThemeChanged;

            // 监听窗口状态变更
            _window?.Closed += Window_Closed;
            _window?.Activated += Window_Activated;

            // 创建并初始化云母控制器
            _micaController = new MicaController();

            // 设置云母效果属性（在激活目标前同步应用，避免异步时序导致属性未生效）
            SetMicaProperties();

            // 激活云母效果
            if (_micaController is not null)
            {
                // 设置配置
                _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);

                // 添加目标
                _micaController.AddSystemBackdropTarget(connectedTarget);
            }
        }

        private void RootElement_ActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_isConnected)
            {
                SetConfigurationSourceTheme(sender);
            }
        }

        protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
        {
            // 清理资源和事件监听
            _isConnected = false;
            _currentTarget = null;


            if (disconnectedTarget is FrameworkElement element)
            {
                element.ActualThemeChanged -= RootElement_ActualThemeChanged;

                if (_currentTarget is Window window)
                {
                    window.Closed += Window_Closed;
                }
            }

            if (_micaController is not null)
            {
                _micaController.RemoveSystemBackdropTarget(disconnectedTarget);
                _micaController.Dispose();
                _micaController = null;
            }

            _backdropConfiguration = null;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (IsInputActive)
            {
                _backdropConfiguration?.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            }
            else
            {
                _backdropConfiguration?.IsInputActive = true;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _micaController?.Dispose();
            _micaController = null;
            _backdropConfiguration = null;
            _window.Closed -= Window_Closed;
            _window.Activated -= Window_Activated;
        }

        private void SetConfigurationSourceTheme(FrameworkElement element)
        {
            if (_backdropConfiguration is null) return;

            _backdropConfiguration.Theme = element.ActualTheme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                ElementTheme.Default => SystemBackdropTheme.Default,
                _ => SystemBackdropTheme.Default
            };
            UpdateUiColor(element.ActualTheme);
        }

        // 设置云母效果的属性（调用方均在 UI 线程，故直接同步应用）
        private void SetMicaProperties()
        {
            if (_micaController is null || !_isConnected)
            {
                return;
            }

            try
            {
                // 设置云母效果的类型和颜色
                _micaController.Kind = MicaKind;
                _micaController.TintColor = TintColor;
                _micaController.TintOpacity = TintOpacity;
                _micaController.FallbackColor = FallbackColor;
            }
            catch
            {
            }
        }

        // 提供一个公共方法，用于动态更新云母效果的属性
        public void UpdateProperties(MicaKind micaKind, float tintOpacity, Color tintColor)
        {
            MicaKind = micaKind;
            TintOpacity = tintOpacity;
            TintColor = tintColor;

            SetMicaProperties();
        }

        // 检查系统是否支持Mica效果
        public static bool IsSupported()
        {
            return MicaController.IsSupported();
        }

        private void UpdateUiColor(ElementTheme elementTheme)
        {
            var isDarkTheme = elementTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                ElementTheme.Default => Application.Current.RequestedTheme == ApplicationTheme.Dark,
                _ => true
            };
            TintColor = isDarkTheme
                ? Color.FromArgb(255, 32, 32, 32)
                : Color.FromArgb(220, 255, 255, 255);
            SetMicaProperties();
        }
    }
}
