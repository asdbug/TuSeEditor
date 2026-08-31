using System.Runtime.InteropServices;

namespace TuSeEditor.App.Services;

/// <summary>Win32 API 封装:GDI 抓屏、SendInput 输入、热键、窗口信息</summary>
internal static partial class NativeMethods
{
    // ---------- user32 ----------
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKey(uint uCode, uint uMapType);
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;
    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;

    // ---------- gdi32 ----------
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] internal static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);
    [DllImport("gdi32.dll")] internal static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[]? lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    internal const int SRCCOPY = 0x00CC0020;
    internal const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;      // 负数 = 自上而下
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;    // 调色板占位
    }

    // ---------- SendInput ----------
    internal const uint INPUT_MOUSE = 0;
    internal const uint INPUT_KEYBOARD = 1;

    internal const uint MOUSEEVENTF_MOVE = 0x0001;
    internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    internal const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    internal const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    internal const uint MOUSEEVENTF_WHEEL = 0x0800;

    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;
    internal const uint KEYEVENTF_SCANCODE = 0x0008;

    internal const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL, wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    // ---------- 显示器枚举 ----------
    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>主显示器的桌面坐标矩形</summary>
    internal static RECT GetPrimaryMonitorRect()
    {
        RECT result = default;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, _, _, _) =>
        {
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf<MONITORINFO>();
            if (GetMonitorInfo(hMon, ref mi) && (mi.dwFlags & 1) != 0) // MONITORINFOF_PRIMARY
            {
                result = mi.rcMonitor;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        if (result.Right == 0 && result.Bottom == 0)
            return new RECT { Left = 0, Top = 0, Right = GetSystemMetrics(SM_CXSCREEN), Bottom = GetSystemMetrics(SM_CYSCREEN) };
        return result;
    }

    /// <summary>窗口的可见区域(去掉不可见阴影边框)</summary>
    internal static bool TryGetWindowBounds(IntPtr hwnd, out RECT rect)
    {
        rect = default;
        if (!IsWindow(hwnd)) return false;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var dwmRect, Marshal.SizeOf<RECT>()) == 0)
        {
            rect = dwmRect;
            return true;
        }
        return GetWindowRect(hwnd, out rect);
    }
}
