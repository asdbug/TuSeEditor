using System.Runtime.InteropServices;

namespace TuSeEditor.App.Services;

/// <summary>一帧屏幕画面:BGRA 像素 + 在桌面坐标系中的原点</summary>
public sealed class CaptureFrame
{
    public int Width;
    public int Height;
    public byte[] Bgra = Array.Empty<byte>();
    /// <summary>帧左上角在虚拟桌面坐标(物理像素)中的位置</summary>
    public int OriginX;
    public int OriginY;
    /// <summary>抓图引擎名,用于日志</summary>
    public string Engine = "";

    /// <summary>全黑帧检测(GDI 对部分独占全屏/受保护窗口会返回黑屏)</summary>
    public bool IsBlank()
    {
        var b = Bgra;
        for (int i = 0; i < b.Length; i += 64)
            if (b[i] != 0) return false;
        return true;
    }

    public System.Windows.Media.Imaging.BitmapSource ToBitmapSource()
        => System.Windows.Media.Imaging.BitmapSource.Create(Width, Height, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null, Bgra, Width * 4);
}

/// <summary>像素矩形</summary>
public readonly record struct PixelRect(int X, int Y, int W, int H)
{
    public static PixelRect Parse(string s)
    {
        // "full" 或 "x,y,w,h"
        var parts = s.Split(',');
        if (parts.Length == 4 &&
            int.TryParse(parts[0].Trim(), out var x) && int.TryParse(parts[1].Trim(), out var y) &&
            int.TryParse(parts[2].Trim(), out var w) && int.TryParse(parts[3].Trim(), out var h))
            return new PixelRect(x, y, w, h);
        return default;
    }

    public bool IsFull => W <= 0 || H <= 0;
}

/// <summary>抓图引擎选择</summary>
public enum EngineKind { Auto, Dxgi, Gdi }

/// <summary>
/// 抓图服务:DXGI 桌面复制(兼容大多数 DirectX 网游)优先,失败/黑屏自动降级 GDI。
/// 所有返回帧的坐标都是桌面物理像素(进程为 PerMonitorV2 DPI 感知)。
/// </summary>
public sealed class CaptureService : IDisposable
{
    readonly object _lock = new();
    DxgiEngine? _dxgi;
    int _dxgiConsecutiveFails;
    public EngineKind Mode { get; set; } = EngineKind.Auto;
    public Action<string>? Log;

    /// <summary>为图色匹配抓一帧(全屏/整个虚拟桌面,由各引擎决定范围)</summary>
    public CaptureFrame CaptureForMatch()
    {
        lock (_lock)
        {
            if (Mode != EngineKind.Gdi)
            {
                try
                {
                    var f = DxgiCaptureFrame();
                    if (f != null && !f.IsBlank())
                    {
                        _dxgiConsecutiveFails = 0;
                        return f;
                    }
                    Log?.Invoke("DXGI 抓到黑屏,本次改用 GDI");
                }
                catch (Exception ex)
                {
                    _dxgiConsecutiveFails++;
                    var hint = ex.Message.Contains("0x80070005") || ex.Message.Contains("拒绝访问")
                        ? "(可能被其他程序占用桌面复制或权限不足,已自动改用 GDI)"
                        : "";
                    Log?.Invoke($"DXGI 抓图失败({_dxgiConsecutiveFails}):{ex.Message}{hint}");
                    DisposeDxgi();
                }
                if (Mode == EngineKind.Dxgi && _dxgiConsecutiveFails < 6)
                {
                    // 指定 DXGI 模式时多给几次机会,期间返回 GDI 结果但引擎标记为 GDI
                }
            }
            var gdi = CaptureGdiVirtualScreen();
            gdi.Engine = "GDI";
            return gdi;
        }
    }

    /// <summary>覆盖层用:抓主显示器(GDI 足够,截图一次用于框选/取色)</summary>
    public static CaptureFrame CapturePrimary()
    {
        var r = NativeMethods.GetPrimaryMonitorRect();
        var f = CaptureGdiRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        f.Engine = "GDI";
        return f;
    }

    // ---------------- GDI ----------------
    public static CaptureFrame CaptureGdiRect(int x, int y, int w, int h)
    {
        var hdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        try
        {
            var hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
            var hbmp = NativeMethods.CreateCompatibleBitmap(hdcScreen, w, h);
            var old = NativeMethods.SelectObject(hdcMem, hbmp);
            try
            {
                NativeMethods.BitBlt(hdcMem, 0, 0, w, h, hdcScreen, x, y, NativeMethods.SRCCOPY);
            }
            finally
            {
                NativeMethods.SelectObject(hdcMem, old); // GetDIBits 前必须取消选中
            }

            var bmi = new NativeMethods.BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = w;
            bmi.bmiHeader.biHeight = -h; // 负数 = 自上而下
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;

            var buf = new byte[w * h * 4];
            NativeMethods.GetDIBits(hdcMem, hbmp, 0, (uint)h, buf, ref bmi, NativeMethods.DIB_RGB_COLORS);

            NativeMethods.DeleteObject(hbmp);
            NativeMethods.DeleteDC(hdcMem);

            return new CaptureFrame { Width = w, Height = h, Bgra = buf, OriginX = x, OriginY = y };
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    static CaptureFrame CaptureGdiVirtualScreen()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        return CaptureGdiRect(x, y, w, h);
    }

    // ---------------- DXGI ----------------
    CaptureFrame? DxgiCaptureFrame()
    {
        if (_dxgi == null)
        {
            _dxgi = new DxgiEngine();
            _dxgi.Log = m => Log?.Invoke(m);
            if (!_dxgi.TryInit())
            {
                DisposeDxgi();
                _dxgiConsecutiveFails = 99; // 环境不支持,直接固定 GDI
                return null;
            }
            Log?.Invoke("DXGI 桌面复制引擎已启动");
        }
        return _dxgi.Capture(200);
    }

    void DisposeDxgi()
    {
        _dxgi?.Dispose();
        _dxgi = null;
    }

    public void Dispose() => DisposeDxgi();
}

/// <summary>DXGI 桌面复制引擎:兼容 DX9~DX12 游戏的窗口化/无边框/全屏画面</summary>
public sealed class DxgiEngine : IDisposable
{
    Vortice.Direct3D11.ID3D11Device? _device;
    Vortice.Direct3D11.ID3D11DeviceContext? _context;
    Vortice.Direct3D11.ID3D11Texture2D? _staging;
    Vortice.DXGI.IDXGIOutputDuplication? _dup;
    Vortice.DXGI.IDXGIOutput? _output;
    Vortice.DXGI.IDXGIAdapter? _adapter;
    Vortice.DXGI.IDXGIFactory1? _factory;

    int _w, _h;
    int _originX, _originY;
    byte[]? _lastFrame;          // 最近一次成功帧(桌面无更新时复用)
    public Action<string>? Log;

    public bool TryInit()
    {
        _factory = Vortice.DXGI.DXGI.CreateDXGIFactory1<Vortice.DXGI.IDXGIFactory1>();

        // 选包含主显示器中心点的输出
        var primary = NativeMethods.GetPrimaryMonitorRect();
        int cx = (primary.Left + primary.Right) / 2, cy = (primary.Top + primary.Bottom) / 2;

        for (int ai = 0; _factory.EnumAdapters((uint)ai, out var adapter).Success; ai++)
        {
            for (int oi = 0; adapter.EnumOutputs((uint)oi, out var output).Success; oi++)
            {
                var desc = output.Description;
                var rc = desc.DesktopCoordinates;
                if (cx >= rc.Left && cx < rc.Right && cy >= rc.Top && cy < rc.Bottom)
                {
                    _adapter = adapter;
                    _output = output;
                    _originX = rc.Left; _originY = rc.Top;
                    goto found;
                }
                output.Dispose();
            }
            adapter.Dispose();
        }
        return false;

    found:
        var hr = Vortice.Direct3D11.D3D11.D3D11CreateDevice(
            _adapter, Vortice.Direct3D.DriverType.Unknown,
            Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,
            Array.Empty<Vortice.Direct3D.FeatureLevel>(),
            out _device);
        if (hr.Failure || _device == null)
        {
            Log?.Invoke($"D3D11 设备创建失败: {hr}");
            return false;
        }
        _context = _device.ImmediateContext;

        var output1 = _output.QueryInterface<Vortice.DXGI.IDXGIOutput1>();
        _dup = output1.DuplicateOutput(_device);
        output1.Dispose();
        return true;
    }

    public CaptureFrame Capture(int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (true)
        {
            var r = _dup!.AcquireNextFrame((uint)Math.Max(1, (int)(deadline - Environment.TickCount64)),
                out var info, out var resource);
            if (r.Success)
            {
                try
                {
                    using var tex = resource.QueryInterface<Vortice.Direct3D11.ID3D11Texture2D>();
                    CopyToStaging(tex);
                }
                finally
                {
                    resource.Dispose();
                    _dup.ReleaseFrame();
                }
                return MapStagingToFrame();
            }

            if (r.Code == Vortice.DXGI.ResultCode.WaitTimeout.Code)
            {
                // 桌面无更新:用最近一帧(内容仍准确)
                if (_lastFrame != null)
                    return new CaptureFrame
                    {
                        Width = _w, Height = _h, Bgra = _lastFrame,
                        OriginX = _originX, OriginY = _originY, Engine = "DXGI"
                    };
                if (Environment.TickCount64 >= deadline)
                    throw new TimeoutException("DXGI 等待桌面帧超时");
                continue;
            }

            if (r.Code == Vortice.DXGI.ResultCode.AccessLost.Code)
            {
                RecreateDuplication();
                continue;
            }

            throw new InvalidOperationException($"AcquireNextFrame 失败: {r}");
        }
    }

    void RecreateDuplication()
    {
        try { _dup?.Dispose(); } catch { }
        _dup = null;
        try { _staging?.Dispose(); _staging = null; _lastFrame = null; } catch { }
        var output1 = _output!.QueryInterface<Vortice.DXGI.IDXGIOutput1>();
        _dup = output1.DuplicateOutput(_device!);
        output1.Dispose();
    }

    unsafe void CopyToStaging(Vortice.Direct3D11.ID3D11Texture2D tex)
    {
        var desc = tex.Description;
        if (_staging == null || _w != desc.Width || _h != desc.Height)
        {
            _w = (int)desc.Width; _h = (int)desc.Height;
            _staging?.Dispose();
            _staging = _device!.CreateTexture2D(new Vortice.Direct3D11.Texture2DDescription(
                desc.Format, (uint)_w, (uint)_h, 1, 1,
                Vortice.Direct3D11.BindFlags.None,
                Vortice.Direct3D11.ResourceUsage.Staging,
                Vortice.Direct3D11.CpuAccessFlags.Read,
                1, 0,
                Vortice.Direct3D11.ResourceOptionFlags.None));
        }
        _context!.CopyResource(_staging!, tex);
    }

    unsafe CaptureFrame MapStagingToFrame()
    {
        var mapped = _context!.Map(_staging!, 0, Vortice.Direct3D11.MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            int rowBytes = _w * 4;
            var buf = new byte[_w * _h * 4];
            if (mapped.RowPitch == rowBytes)
            {
                new ReadOnlySpan<byte>((void*)mapped.DataPointer, buf.Length).CopyTo(buf);
            }
            else
            {
                for (int y = 0; y < _h; y++)
                    new ReadOnlySpan<byte>((void*)(mapped.DataPointer + (long)y * mapped.RowPitch), rowBytes)
                        .CopyTo(buf.AsSpan(y * rowBytes, rowBytes));
            }
            _lastFrame = buf;
            return new CaptureFrame { Width = _w, Height = _h, Bgra = buf, OriginX = _originX, OriginY = _originY, Engine = "DXGI" };
        }
        finally
        {
            _context.Unmap(_staging!, 0);
        }
    }

    public void Dispose()
    {
        _dup?.Dispose();
        _staging?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _output?.Dispose();
        _adapter?.Dispose();
        _factory?.Dispose();
        _dup = null; _staging = null; _context = null; _device = null;
        _output = null; _adapter = null; _factory = null;
    }
}
