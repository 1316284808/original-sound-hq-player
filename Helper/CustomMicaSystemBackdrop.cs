using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;
using WinRT.Interop;

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

        // Mica 效果属性
        // BaseAlt 对桌面背景着色更强、对比更明显，视觉上更"浓"；
        // Base 则更轻、更中性、壁纸采样更淡。默认用 BaseAlt 以获得更浓郁的质感。
        public MicaKind MicaKind { get; set; } = MicaKind.BaseAlt;
        public Color TintColor { get; set; } = Color.FromArgb(255, 32, 32, 32);
        // Mica 的标准通透感来自 TintOpacity < 1.0；Windows 默认值为 0.8。
        // 这里默认 0.5，配合从壁纸提取的主色，使亮色云母既有壁纸色相又不过分实；
        // 想要更实就往 0.8 调。注意：Color 自带的 alpha 不参与运算，透明度只由 TintOpacity 控制。
        public float TintOpacity { get; set; } = 0.1f;
        // Mica 不支持/未激活时回退的纯色（仅在系统无法提供材质时生效，不参与正常渲染），避免回退到透明而显得"淡"。
        public Color FallbackColor { get; set; } = Color.FromArgb(255, 32, 32, 32);
        public bool IsInputActive = false;

        // 壁纸主色取色相关
        private DispatcherQueue? _dispatcherQueue;
        private ElementTheme _currentTheme = ElementTheme.Default;
        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private WinProcDelegate? _wndProcDelegate;
        private bool _subclassed = false;

        public CustomMicaSystemBackdrop(Window window = null)
        {
            _window = window;
        }

        protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
        {
            _currentTarget = connectedTarget;
            _isConnected = true;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _backdropConfiguration = new SystemBackdropConfiguration();

            // 根据应用当前主题设置背景配置
            FrameworkElement rootElement = xamlRoot.Content as FrameworkElement;
            SetConfigurationSourceTheme(rootElement);

            // 监听主题变更事件
            rootElement.ActualThemeChanged += RootElement_ActualThemeChanged;

            // 监听窗口状态变更
            if (_window is not null)
            {
                _window.Closed += Window_Closed;
                _window.Activated += Window_Activated;
            }

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

            // 子类化窗口以监听壁纸变更（WM_SETTINGCHANGE / SPI_SETDESKWALLPAPER）
            InstallWallpaperWatcher();
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

            RemoveWallpaperWatcher();

            if (disconnectedTarget is FrameworkElement element)
            {
                element.ActualThemeChanged -= RootElement_ActualThemeChanged;
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
            if (_backdropConfiguration is null) return;
            _backdropConfiguration.IsInputActive = IsInputActive
                ? args.WindowActivationState != WindowActivationState.Deactivated
                : true;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            RemoveWallpaperWatcher();
            _micaController?.Dispose();
            _micaController = null;
            _backdropConfiguration = null;
            if (_window is not null)
            {
                _window.Closed -= Window_Closed;
                _window.Activated -= Window_Activated;
            }
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

        // ===== 壁纸主色取色：自己读壁纸算主色，反哺 TintColor，弥补亮色 Mica 被 DWM 去饱和的"寡淡" =====

        private void UpdateUiColor(ElementTheme elementTheme)
        {
            _currentTheme = elementTheme;
            bool isDark = elementTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                ElementTheme.Default => Application.Current.RequestedTheme == ApplicationTheme.Dark,
                _ => true
            };

            // 先给一个即时默认值，避免异步取色期间的闪烁/空白
            TintColor = isDark
                ? Color.FromArgb(255, 32, 32, 32)
                : Color.FromArgb(255, 235, 235, 235);
            SetMicaProperties();

            // 异步从壁纸提取主色并叠加更浓郁的着色
            RefreshWallpaperTint();
        }

        private void RefreshWallpaperTint()
        {
            if (!_isConnected || _micaController is null || _dispatcherQueue is null) return;
            var path = GetWallpaperPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            Task.Run(async () =>
            {
                var dominant = await GetDominantColorAsync(path);
                _dispatcherQueue.TryEnqueue(() => ApplyWallpaperTint(dominant));
            });
        }

        private void ApplyWallpaperTint(Color wallpaperColor)
        {
            if (!_isConnected || _micaController is null) return;
            bool isDark = _currentTheme switch
            {
                ElementTheme.Dark => true,
                ElementTheme.Light => false,
                ElementTheme.Default => Application.Current.RequestedTheme == ApplicationTheme.Dark,
                _ => true
            };

            if (isDark)
            {
                // 暗色：保留暗底，仅借壁纸色相，避免整体发灰
                TintColor = Darken(wallpaperColor, 0.25f);
                TintOpacity = 0.6f;
            }
            else
            {
                // 亮色：提升饱和度 + 中等不透明度，让壁纸色透出来又不至于太实
                TintColor = BoostSaturation(wallpaperColor, 0.55f);
                TintOpacity = 0.5f;
            }
            SetMicaProperties();
        }

        private static string GetWallpaperPath()
        {
            try
            {
                const int MAX_PATH = 260;
                var sb = new StringBuilder(MAX_PATH);
                const uint SPI_GETDESKWALLPAPER = 0x0073;
                if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)MAX_PATH, sb, 0))
                {
                    var p = sb.ToString();
                    return string.IsNullOrWhiteSpace(p) ? string.Empty : p;
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        // 读取壁纸图，缩小后做 4-4-4 量化的主导色统计，返回代表性颜色。失败返回中性灰兜底。
        private static async Task<Color> GetDominantColorAsync(string filePath)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                uint maxDim = 64;
                var transform = new BitmapTransform();
                if (decoder.PixelWidth >= decoder.PixelHeight)
                {
                    transform.ScaledWidth = maxDim;
                    transform.ScaledHeight = (uint)(maxDim * ((double)decoder.PixelHeight / decoder.PixelWidth));
                }
                else
                {
                    transform.ScaledWidth = (uint)(maxDim * ((double)decoder.PixelWidth / decoder.PixelHeight));
                    transform.ScaledHeight = maxDim;
                }
                transform.InterpolationMode = BitmapInterpolationMode.Linear;

                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var bytes = pixelData.DetachPixelData();
                int width = (int)transform.ScaledWidth;
                int height = (int)transform.ScaledHeight;
                if (width <= 0 || height <= 0) return FallbackTint;

                // 16^3 = 4096 个量化桶
                var buckets = new long[4096];
                var sumR = new long[4096];
                var sumG = new long[4096];
                var sumB = new long[4096];

                int stride = width * 4;
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int i = row + x * 4;
                        int r = bytes[i + 2];
                        int g = bytes[i + 1];
                        int b = bytes[i];
                        int idx = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
                        buckets[idx]++;
                        sumR[idx] += r;
                        sumG[idx] += g;
                        sumB[idx] += b;
                    }
                }

                int bestIdx = 0;
                long bestCount = -1;
                for (int idx = 0; idx < buckets.Length; idx++)
                {
                    if (buckets[idx] > bestCount)
                    {
                        bestCount = buckets[idx];
                        bestIdx = idx;
                    }
                }

                if (bestCount <= 0) return FallbackTint;
                return Color.FromArgb(255,
                    (byte)(sumR[bestIdx] / bestCount),
                    (byte)(sumG[bestIdx] / bestCount),
                    (byte)(sumB[bestIdx] / bestCount));
            }
            catch
            {
                return FallbackTint;
            }
        }

        private static readonly Color FallbackTint = Color.FromArgb(255, 200, 200, 200);

        // 提升饱和度到至少 minSaturation，让亮色云母更"浓郁"
        private static Color BoostSaturation(Color color, float minSaturation)
        {
            float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float l = (max + min) / 2f;
            float h = 0f, s = 0f;
            float d = max - min;
            if (d > 1e-4f)
            {
                s = l > 0.5f ? d / (2f - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6f : 0f);
                else if (max == g) h = (b - r) / d + 2f;
                else h = (r - g) / d + 4f;
                h /= 6f;
            }
            s = Math.Min(1f, Math.Max(s, minSaturation));
            return HslToRgb(h, s, l);
        }

        private static Color Darken(Color color, float factor)
        {
            return Color.FromArgb(255,
                (byte)(color.R * factor),
                (byte)(color.G * factor),
                (byte)(color.B * factor));
        }

        private static Color HslToRgb(float h, float s, float l)
        {
            if (s <= 1e-4f)
            {
                byte v = (byte)(l * 255f);
                return Color.FromArgb(255, v, v, v);
            }
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            float r = HueToChannel(p, q, h + 1f / 3f);
            float g = HueToChannel(p, q, h);
            float b = HueToChannel(p, q, h - 1f / 3f);
            return Color.FromArgb(255,
                (byte)(r * 255f),
                (byte)(g * 255f),
                (byte)(b * 255f));
        }

        private static float HueToChannel(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        // ===== 壁纸变更监听：子类化窗口 WndProc，捕获 WM_SETTINGCHANGE(SPI_SETDESKWALLPAPER) =====

        private const int GWL_WNDPROC = -4;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const int SPI_SETDESKWALLPAPER = 0x0014;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WinProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void InstallWallpaperWatcher()
        {
            if (_subclassed || _window is null) return;
            try
            {
                _hwnd = WindowNative.GetWindowHandle(_window);
                _wndProcDelegate = new WinProcDelegate(WndProc);
                var newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
                _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, newProc);
                _subclassed = true;
            }
            catch
            {
                _subclassed = false;
            }
        }

        private void RemoveWallpaperWatcher()
        {
            if (!_subclassed || _hwnd == IntPtr.Zero) return;
            try
            {
                SetWindowLongPtr(_hwnd, GWL_WNDPROC, _oldWndProc);
            }
            catch
            {
            }
            _oldWndProc = IntPtr.Zero;
            _wndProcDelegate = null;
            _subclassed = false;
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_SETTINGCHANGE && wParam.ToInt32() == SPI_SETDESKWALLPAPER)
            {
                RefreshWallpaperTint();
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }
    }
}
