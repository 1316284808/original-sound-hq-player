using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIMusicPlayer.Controls;
using WinUIMusicPlayer.Helper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(initialization =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new WinUIMusicPlayer.App();
        });
    }
}

namespace WinUIMusicPlayer
{
    public partial class App : Application
    {
        public static ILogger<T> GetLogger<T>() => NullLogger<T>.Instance;
        public static Window MainWindow { get; private set; } = null!;
        public App() { InitializeComponent(); }
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var host = new Grid();
            var window = new Window { Content = host };
            MainWindow = window;
            window.AppWindow.Move(new Windows.Graphics.PointInt32(-30000, -30000));
            window.Activate();
            string result;
            try
            {
                var control = new ImageSwitcher { IsActive = false, Width = 300, Height = 300 };
                host.Children.Add(control);
                await WaitAsync(() => control.IsLoaded);
                control.ImageHash = "large";
                control.IsActive = true;
                await WaitAsync(() => Current(control) is { PixelWidth: 1536, PixelHeight: 1536 });
                control.ImageHash = "portrait";
                await WaitAsync(() => Current(control) is { PixelWidth: 512, PixelHeight: 1536 });
                control.ImageHash = "small";
                await WaitAsync(() => Current(control) is { PixelWidth: 320, PixelHeight: 200 });

                await VerifyCacheFallbackAsync(control, "portrait", 800, 2400);
                await VerifyCacheFallbackAsync(control, "large", 3072, 3072);
                await VerifyCacheFallbackAsync(control, "oriented", 2400, 800);

                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    try
                    {
                        await ImageHelper.DecodeFileToBitmapAsync(Utils.ToolUtils.FindRawCachePath("portrait"),
                            cancelled.Token, PlaybackCoverImage.MaxPixelSize);
                        throw new Exception("原图兜底未传播取消");
                    }
                    catch (OperationCanceledException) { }
                }

                control.ImageHash = "large";
                control.ImageHash = "portrait";
                control.ImageHash = "small";
                await WaitAsync(() => Current(control) is { PixelWidth: 320, PixelHeight: 200 });
                await Task.Delay(500);
                if (Current(control) is not { PixelWidth: 320, PixelHeight: 200 })
                    throw new Exception("迟到的大图覆盖小图");
                control.IsActive = false;
                control.ImageHash = "large";
                await Task.Delay(100);
                if (Current(control) is not { PixelWidth: 320, PixelHeight: 200 })
                    throw new Exception("隐藏详情页仍加载新图");
                control.IsActive = true;
                await WaitAsync(() => Current(control) is { PixelWidth: 1536, PixelHeight: 1536 });
                await Task.Delay(500);
                if (((ShadowImage)control.FindName("LastAlbumArtImage")).Source is not null)
                    throw new Exception("动画结束后仍保留旧图片");
                host.Children.Remove(control);
                // IsLoaded 在 Unloaded 回调之前即可变为 false，等待实际资源清理完成。
                await WaitAsync(() => !control.IsLoaded && Current(control) is null);
                await CoverPipelineRegression.RunAsync();
                result = "PASS: 真实 ImageSwitcher 与 CoverPresentation/SMTC 缓存失败兜底、解码限制、连续切歌、取消、退出与句柄释放";
            }
            catch (Exception ex) { result = "FAIL: " + ex; }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "result.txt"), result);
            window.Close();
            Exit();
        }
        private static BitmapImage? Current(ImageSwitcher control)
            => ((ShadowImage)control.FindName("AlbumArtImage")).Source as BitmapImage;

        private static async Task VerifyCacheFallbackAsync(ImageSwitcher control, string source, int width, int height)
        {
            string hash = "blocked_" + source;
            string raw = Utils.ToolUtils.FindRawCachePath(hash);
            File.Copy(Utils.ToolUtils.FindRawCachePath(source), raw, true);
            string display = PlaybackCoverImage.GetCachePath(raw);
            // 纵向图阻止新缓存发布；其余图片锁住过期文件，阻止原子替换。
            FileStream? locked = null;
            if (source == "portrait") Directory.CreateDirectory(display);
            else
            {
                await File.WriteAllTextAsync(display, "stale cache");
                File.SetLastWriteTimeUtc(display, File.GetLastWriteTimeUtc(raw).AddMinutes(-1));
                locked = new FileStream(display, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            try
            {
                var previous = Current(control);
                control.ImageHash = hash;
                await WaitAsync(() => Current(control) is { } image &&
                    !ReferenceEquals(image, previous) &&
                    (width >= height ? image.DecodePixelWidth == 1536 : image.DecodePixelHeight == 1536));
                var decoded = Current(control)!;
                // WinUI 的 PixelWidth/Height 仍报告原始尺寸；解码限制由 DecodePixel* 指定。
                if (decoded.PixelWidth != width || decoded.PixelHeight != height ||
                    decoded.DecodePixelType != DecodePixelType.Physical ||
                    (width >= height ? decoded.DecodePixelHeight : decoded.DecodePixelWidth) != 0)
                    throw new Exception($"{source} 原图兜底尺寸/方向不符: {decoded.PixelWidth}x{decoded.PixelHeight}");
            }
            finally
            {
                locked?.Dispose();
                if (source == "portrait") Directory.Delete(display);
                else File.Delete(display);
            }
        }

        private static async Task WaitAsync(Func<bool> predicate)
        {
            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (!predicate())
            {
                if (DateTime.UtcNow > timeout) throw new TimeoutException("等待生产图片控件超时");
                await Task.Delay(20);
            }
        }
    }
}
namespace WinUIMusicPlayer.Utils
{
    internal static partial class ToolUtils
    {
        internal static string FindRawCachePath(string hash)
            => Path.Combine(AppContext.BaseDirectory, "fixtures", hash + "_raw.bin");
    }
}
