using System;
using WinUIMusicPlayer.Helper;

namespace WinUIMusicPlayer.DesktopLyrics
{
    /// <summary>任务栏所在的屏幕边缘。</summary>
    public enum TaskbarEdge
    {
        Bottom,
        Top,
        Left,
        Right,
    }

    /// <summary>
    /// 主任务栏的几何与状态快照（矩形均为物理像素，与 AppWindow.Move/Resize 同一坐标系）。
    /// 参考 MusicBar 的做法：只用 FindWindow(Shell_TrayWnd) + GetWindowRect 取矩形，
    /// 自动隐藏开关额外用 SHAppBarMessage(ABM_GETSTATE)。
    /// </summary>
    public readonly record struct TaskbarInfo(
        bool Found,                 // 是否探测到主任务栏（Shell_TrayWnd）
        IntPtr Hwnd,
        int X,
        int Y,
        int Width,
        int Height,
        TaskbarEdge Edge,
        bool AutoHideEnabled,       // 系统「自动隐藏任务栏」开关已打开
        bool IsHidden)              // 开关已打开且当前处于收起态（滑出屏幕）
    {
        /// <summary>横向任务栏（底/顶）才适合贴靠条带；竖直任务栏（左/右）宽度不足。</summary>
        public bool IsHorizontal => Width >= Height;
    }

    /// <summary>任务栏探测入口：纯 Win32 调用，不持有状态、不做缓存，每次调用重新取值。</summary>
    public static class TaskbarInfoProvider
    {
        private const string PrimaryTaskbarClassName = "Shell_TrayWnd";

        /// <summary>自动隐藏时任务栏留在屏幕上的可见像素（用于判定"已滑出"）。</summary>
        private const int AutoHideVisibleSliver = 4;

        public static TaskbarInfo GetPrimary()
        {
            var hwnd = WindowHelper.FindWindow(PrimaryTaskbarClassName, null);
            if (hwnd == IntPtr.Zero ||
                !WindowHelper.GetWindowRect(hwnd, out var rect) ||
                rect.Width <= 0 || rect.Height <= 0)
            {
                return default;   // Found = false：调用方走工作区底部回退
            }

            var autoHide = WindowHelper.IsTaskbarAutoHideEnabled();
            var monitor = WindowHelper.GetMonitorRect(hwnd);
            bool hidden = autoHide && monitor is { } m && IsSlidOutOfScreen(rect, m);
            var edge = InferEdge(rect, monitor);

            return new TaskbarInfo(
                Found: true,
                Hwnd: hwnd,
                X: rect.Left,
                Y: rect.Top,
                Width: rect.Width,
                Height: rect.Height,
                Edge: edge,
                AutoHideEnabled: autoHide,
                IsHidden: hidden);
        }

        /// <summary>
        /// 自动隐藏且已收起时，任务栏窗口被推到屏幕外，只剩边缘一条（约 2px）。
        /// 用矩形与所在显示器矩形的相对位置判定，不去猜动画状态。
        /// </summary>
        private static bool IsSlidOutOfScreen(WindowHelper.RECT rect, WindowHelper.RECT monitor)
            => rect.Bottom <= monitor.Top + AutoHideVisibleSliver
               || rect.Top >= monitor.Bottom - AutoHideVisibleSliver
               || rect.Right <= monitor.Left + AutoHideVisibleSliver
               || rect.Left >= monitor.Right - AutoHideVisibleSliver;

        private static TaskbarEdge InferEdge(WindowHelper.RECT rect, WindowHelper.RECT? monitor)
        {
            if (monitor is not { } m)
            {
                return rect.Width >= rect.Height ? TaskbarEdge.Bottom : TaskbarEdge.Left;
            }

            int toTop = Math.Abs(rect.Top - m.Top);
            int toBottom = Math.Abs(m.Bottom - rect.Bottom);
            int toLeft = Math.Abs(rect.Left - m.Left);
            int toRight = Math.Abs(m.Right - rect.Right);

            return rect.Width >= rect.Height
                ? (toBottom <= toTop ? TaskbarEdge.Bottom : TaskbarEdge.Top)
                : (toRight <= toLeft ? TaskbarEdge.Right : TaskbarEdge.Left);
        }
    }
}
