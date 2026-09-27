using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIMusicPlayer.Controls;

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
        public App() { InitializeComponent(); }
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var host = new Grid();
            var window = new Window { Content = host };
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
                result = "PASS: 真实 ImageSwitcher 文件流解码尺寸、连续切歌、隐藏/恢复、过渡层和卸载释放";
            }
            catch (Exception ex) { result = "FAIL: " + ex; }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "result.txt"), result);
            window.Close();
            Exit();
        }
        private static BitmapImage? Current(ImageSwitcher control)
            => ((ShadowImage)control.FindName("AlbumArtImage")).Source as BitmapImage;
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
    internal static class ToolUtils
    {
        internal static string FindRawCachePath(string hash)
            => Path.Combine(AppContext.BaseDirectory, "fixtures", hash + "_raw.bin");
    }
}
