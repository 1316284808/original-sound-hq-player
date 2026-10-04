using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WinUIMusicPlayer.Helper
{
    public static class WindowHelper
    {
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public const int SW_RESTORE = 9;

        public const int GWLP_WNDPROC = -4;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        // ==== 任务栏探测（贴靠任务栏的歌词条带用） ====

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>窗口所在显示器的 DPI（96 = 100%）。仅用于校验；WinUI 的 AppWindow 与 GetWindowRect 同为物理像素，不需要换算。</summary>
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        private const uint ABM_GETSTATE = 0x00000004;
        private const int ABS_AUTOHIDE = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        /// <summary>系统「自动隐藏任务栏」开关是否打开（ABM_GETSTATE + ABS_AUTOHIDE）。</summary>
        public static bool IsTaskbarAutoHideEnabled()
        {
            var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
            try
            {
                return ((int)SHAppBarMessage(ABM_GETSTATE, ref data) & ABS_AUTOHIDE) != 0;
            }
            catch
            {
                return false;
            }
        }

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        /// <summary>取窗口所在显示器矩形（物理像素）；失败返回 null。用于判断自动隐藏的任务栏是否已滑出屏幕。</summary>
        public static RECT? GetMonitorRect(IntPtr hwnd)
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return null;
            var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            return GetMonitorInfo(monitor, ref info) ? info.rcMonitor : null;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int SW_SHOWNOACTIVATE = 4;
        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const uint LWA_ALPHA = 0x00000002;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        /// <summary>
        /// 为覆盖层窗口一次性设置 WS_EX_LAYERED 与不透明属性（幂等，已分层则跳过）。
        /// 穿透开关 <see cref="SetClickThrough"/> 只切 WS_EX_TRANSPARENT，LAYERED 必须常驻：
        /// 运行期增删 LAYERED 会在前后台切换时与 DWM 分层合成竞态，偶发整窗隐身。
        /// </summary>
        public static void EnsureLayered(IntPtr hWnd)
        {
            int exStyle = (int)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_LAYERED) != 0) return;
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, (IntPtr)(exStyle | WS_EX_LAYERED));
            SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
        }

        /// <summary>
        /// 整窗点击穿透开关（只切 WS_EX_TRANSPARENT，需配合 <see cref="EnsureLayered"/> 的常驻 LAYERED），
        /// 供置顶覆盖层窗口（如桌面歌词悬浮窗）使用。
        /// </summary>
        public static void SetClickThrough(IntPtr hWnd, bool enable)
        {
            int exStyle = (int)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            exStyle = enable
                ? exStyle | WS_EX_TRANSPARENT
                : exStyle & ~WS_EX_TRANSPARENT;
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>覆盖层自愈：不抢焦点地恢复显示并重申置顶。锁定态歌词窗不进任务栏/Alt-Tab，
        /// 一旦被前后台切换偶发置为不可见/最小化即无恢复入口（表现为歌词"消失"）。</summary>
        public static void RestoreOverlay(IntPtr hWnd)
        {
            ShowWindow(hWnd, SW_SHOWNOACTIVATE);
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>窗口不在置顶层时幂等重申置顶（不动位置/尺寸/焦点）。</summary>
        public static void EnsureTopmost(IntPtr hWnd)
        {
            if (((int)GetWindowLongPtr(hWnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0) return;
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// 无条件重申置顶（不动位置/尺寸/焦点）。任务栏是特殊的 band 窗口，即便 WS_EX_TOPMOST 已在位，
        /// z 序仍可能被系统重排而落到任务栏之下，故贴靠任务栏的窗口需周期性调用本方法（MusicBar 同款）。
        /// </summary>
        public static void KeepTopmost(IntPtr hWnd)
            => SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);

        // ==== DWM 窗口装饰（去掉系统投影/圆角/边框，供透明覆盖层融入任务栏） ====

        private const int DWMWA_NCRENDERING_POLICY = 2;      // 非客户区渲染策略
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;  // Win11 圆角偏好
        private const int DWMWA_BORDER_COLOR = 34;           // Win11 边框颜色
        private const int DWMNCRP_DISABLED = 1;              // 关闭非客户区渲染（连带去掉 DWM 投影）
        private const int DWMWCP_DONOTROUND = 1;             // 不做圆角
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);  // 「无边框」哨兵色

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        /// <summary>
        /// 去掉窗口的系统级装饰：DWM 投影（非客户区渲染）、Win11 圆角、1px 边框。
        /// 透明覆盖层（如贴靠任务栏的歌词条带）即便已置无边框样式，DWM 仍会在轮廓外合成一层柔和阴影、
        /// 并按系统默认加圆角与细边框，仅靠 XAML 无法去除，表现为"无法融入任务栏"。
        /// 幂等；在样式被重置后（如切换窗口样式）可重复调用。低版本系统不支持的属性会返回失败，忽略即可。
        /// </summary>
        public static void RemoveWindowDecoration(IntPtr hWnd)
        {
            int policy = DWMNCRP_DISABLED;
            DwmSetWindowAttribute(hWnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));

            int corner = DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            int borderNone = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, ref borderNone, sizeof(int));
        }

    }
}
