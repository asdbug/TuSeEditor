using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TuSeEditor.App.Services;

/// <summary>图像保存等小工具</summary>
public static class ImageUtil
{
    public static void SavePng(byte[] bgra, int width, int height, string path)
    {
        var bs = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32,
            null, bgra, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bs));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}

/// <summary>直接在屏幕上画红框高亮(用于"测试找图"结果,不受 DPI 影响)</summary>
public static class ScreenHighlighter
{
    public static void Highlight(int x, int y, int w, int h, int durationMs = 1500)
    {
        Task.Run(() =>
        {
            var hdc = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                var pen = CreatePen(0 /*PS_SOLID*/, 3, 0x0000FF /*RGB→COLORREF 0x00BBGGRR 红=0x0000FF*/);
                var old = SelectObject(hdc, pen);
                var oldBrush = SelectObject(hdc, GetStockObject(5 /*NULL_BRUSH*/));
                var deadline = Stopwatch.StartNew();
                while (deadline.ElapsedMilliseconds < durationMs)
                {
                    Rectangle(hdc, x - 4, y - 4, x + w + 4, y + h + 4);
                    Thread.Sleep(60);
                }
                SelectObject(hdc, old);
                SelectObject(hdc, oldBrush);
                DeleteObject(pen);
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, hdc);
            }
        });
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr CreatePen(int iStyle, int cWidth, uint color);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern IntPtr GetStockObject(int i);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
}
