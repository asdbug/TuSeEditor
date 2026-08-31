using System.Windows;
using System.Windows.Interop;

namespace TuSeEditor.App.Services;

/// <summary>全局热键(F9 启动 / F10 停止,基于 RegisterHotKey)</summary>
public sealed class HotkeyService : IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const int ID_START = 9001;
    const int ID_STOP = 9002;

    IntPtr _hwnd;
    HwndSource? _source;

    public event Action? StartPressed;
    public event Action? StopPressed;

    public bool Register(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        bool okStart = NativeMethods.RegisterHotKey(_hwnd, ID_START, 0, 0x79); // F9
        bool okStop = NativeMethods.RegisterHotKey(_hwnd, ID_STOP, 0, 0x7A);   // F10
        return okStart && okStop;
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == ID_START) { StartPressed?.Invoke(); handled = true; }
            else if (id == ID_STOP) { StopPressed?.Invoke(); handled = true; }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, ID_START);
            NativeMethods.UnregisterHotKey(_hwnd, ID_STOP);
        }
        _source?.RemoveHook(WndProc);
    }
}
