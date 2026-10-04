using Microsoft.Extensions.DependencyInjection;
using System;

namespace WinUIMusicPlayer.DesktopLyrics
{
    /// <summary>
    /// 桌面歌词窗口生命周期服务：创建 / 关闭 / 重置边界 / 启动恢复 / 退出清理。
    /// 开关、锁定、样式、边界等状态由 <see cref="DesktopLyricsViewModel"/> 持有（INPC 绑定源），
    /// 本类仅保存窗口生命周期状态：初始锁定态取自 VM，后续变化由窗口经 VM.PropertyChanged 感知。
    /// 所有方法须在 UI 线程调用。
    /// </summary>
    public static class DesktopLyricsManager
    {
        private static DesktopLyricsWindow? _window;
        private static bool _isShuttingDown;

        private static DesktopLyricsViewModel ViewModel =>
            App.Services.GetRequiredService<DesktopLyricsViewModel>();

        /// <summary>自动隐藏复用窗口；首次被抑制时延迟创建，恢复显示不抢主窗口焦点。</summary>
        public static void SetWindowVisible(bool visible)
        {
            if (_isShuttingDown) return;
            if (visible) CreateWindow();
            _window?.SetOverlayVisible(visible);
        }

        private static void CreateWindow()
        {
            if (_window is not null) return;
            _window = new DesktopLyricsWindow();
            // 必须先显示再应用窗口样式：对未激活的窗口做 GWL_STYLE 切 Popup / 加 WS_EX_LAYERED
            // 会破坏 XAML 岛的呈现与输入管线，后续窗口无响应且内容丢失。
            _window.AppWindow.Show(false);
        }

        public static void CloseWindow()
        {
            var window = _window;
            _window = null;
            if (window is null) return;
            try
            {
                window.Close();
            }
            catch
            {
                // 退出过程中窗口可能已释放
            }
        }

        /// <summary>应用启动时按设置恢复（AppInitializerService 调用）。</summary>
        public static void RestoreFromSettings() => ViewModel.RestoreFromSettings();

        /// <summary>应用退出清理（App.Current_Exit 调用）。</summary>
        public static void Shutdown()
        {
            _isShuttingDown = true;
            CloseWindow();
        }
    }
}
