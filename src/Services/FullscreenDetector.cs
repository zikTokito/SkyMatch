using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SkyMatch
{
    internal static class FullscreenDetector
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static bool IsFullscreenApp()
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) == 0 && (state == 3 || state == 4))
                    return true;

                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero
                    || foreground == GetDesktopWindow()
                    || foreground == GetShellWindow())
                    return false;

                uint processId;
                GetWindowThreadProcessId(foreground, out processId);
                if (processId == (uint)Process.GetCurrentProcess().Id) return false;

                RECT window;
                if (!GetWindowRect(foreground, out window)) return false;
                IntPtr monitor = MonitorFromWindow(foreground, 2);
                if (monitor == IntPtr.Zero) return false;
                MONITORINFO info = new MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetMonitorInfo(monitor, ref info)) return false;

                int monitorWidth = info.rcMonitor.Right - info.rcMonitor.Left;
                int monitorHeight = info.rcMonitor.Bottom - info.rcMonitor.Top;
                if (monitorWidth <= 0 || monitorHeight <= 0) return false;
                if (Math.Abs(window.Left - info.rcMonitor.Left) > 3
                    || Math.Abs(window.Top - info.rcMonitor.Top) > 3
                    || Math.Abs(window.Right - info.rcMonitor.Right) > 3
                    || Math.Abs(window.Bottom - info.rcMonitor.Bottom) > 3)
                    return false;

                int style = GetWindowLong(foreground, -16);
                bool hasCaption = (style & 0x00C00000) == 0x00C00000;
                bool hasThickFrame = (style & 0x00040000) != 0;
                return !hasCaption && !hasThickFrame;
            }
            catch
            {
                return false;
            }
        }
    }
}
