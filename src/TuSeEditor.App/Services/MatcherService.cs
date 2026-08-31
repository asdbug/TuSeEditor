using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace TuSeEditor.App.Services;

/// <summary>找图/找色结果(坐标为帧内像素)</summary>
public readonly record struct MatchResult(bool Found, int X, int Y, double Score)
{
    /// <summary>换算为桌面绝对坐标</summary>
    public (int X, int Y) ToAbsolute(CaptureFrame frame) => (X + frame.OriginX, Y + frame.OriginY);
}

/// <summary>图色匹配:OpenCV 模板匹配 + 容差颜色扫描</summary>
public static class MatcherService
{
    /// <summary>
    /// 模板匹配。region 为帧内区域("full" 表示全帧)。
    /// 返回匹配中心点(帧内坐标)。
    /// </summary>
    public static MatchResult FindImage(CaptureFrame frame, string templatePath, double similarity, string region)
    {
        if (!File.Exists(templatePath))
            return new MatchResult(false, 0, 0, 0);

        using var template = Cv2.ImRead(templatePath, ImreadModes.Color);
        if (template.Empty())
            throw new InvalidOperationException($"模板图无法读取:{templatePath}");

        // 纯色模板会让归一化相关系数退化(0/0),匹配结果无意义,给出明确错误
        Cv2.MeanStdDev(template, out var tMean, out var tStd);
        if (tStd.Val0 < 0.5 && tStd.Val1 < 0.5 && tStd.Val2 < 0.5)
            throw new InvalidOperationException(
                "模板图是纯色/无变化区域,无法用于找图;请重新框选一个有文字或图案的目标,或改用「找色点击」。");

        using var screen = FrameToBgrMat(frame);
        using var search = CropRegion(screen, region, frame);

        if (search.Width < template.Width || search.Height < template.Height)
            return new MatchResult(false, 0, 0, 0);

        using var result = new Mat();
        Cv2.MatchTemplate(search, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);

        int cx = maxLoc.X + template.Width / 2;
        int cy = maxLoc.Y + template.Height / 2;
        // 匹配点在裁剪区域内,换算回全帧坐标
        var (ox, oy) = RegionOffset(region, frame);
        return new MatchResult(maxVal >= similarity, ox + cx, oy + cy, maxVal);
    }

    /// <summary>容差找色。返回第一个匹配点(自上而下扫描,帧内坐标)。</summary>
    public static MatchResult FindColor(CaptureFrame frame, string hexColor, int tolerance, string region)
    {
        var (r, g, b) = ParseHex(hexColor);
        int tol = Math.Clamp(tolerance, 0, 100);

        var rect = RegionRect(region, frame);
        int x0 = Math.Max(0, rect.X), y0 = Math.Max(0, rect.Y);
        int x1 = Math.Min(frame.Width, rect.X + rect.W), y1 = Math.Min(frame.Height, rect.Y + rect.H);
        if (x1 <= x0 || y1 <= y0) return new MatchResult(false, 0, 0, 0);

        var data = frame.Bgra;
        for (int y = y0; y < y1; y++)
        {
            int row = y * frame.Width * 4;
            for (int x = x0; x < x1; x++)
            {
                int i = row + x * 4;
                if (Math.Abs(data[i] - b) <= tol &&
                    Math.Abs(data[i + 1] - g) <= tol &&
                    Math.Abs(data[i + 2] - r) <= tol)
                    return new MatchResult(true, x, y, 1.0);
            }
        }
        return new MatchResult(false, 0, 0, 0);
    }

    /// <summary>取帧内某点颜色</summary>
    public static (byte R, byte G, byte B) ColorAt(CaptureFrame frame, int x, int y)
    {
        int i = (y * frame.Width + x) * 4;
        return (frame.Bgra[i + 2], frame.Bgra[i + 1], frame.Bgra[i]);
    }

    // ---------- 内部 ----------
    static Mat FrameToBgrMat(CaptureFrame frame)
    {
        var handle = GCHandle.Alloc(frame.Bgra, GCHandleType.Pinned);
        try
        {
            using var bgra = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC4,
                handle.AddrOfPinnedObject(), frame.Width * 4);
            var bgr = new Mat();
            Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
            return bgr;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>按区域串裁剪("full" 返回原图)。region 为桌面绝对坐标,需换算成帧内坐标。</summary>
    static Mat CropRegion(Mat src, string region, CaptureFrame frame)
    {
        var rect = RegionRect(region, frame);
        if (rect.W <= 0 || rect.H <= 0) return src.Clone();
        int x = Math.Clamp(rect.X, 0, frame.Width - 1);
        int y = Math.Clamp(rect.Y, 0, frame.Height - 1);
        int w = Math.Min(rect.W, frame.Width - x);
        int h = Math.Min(rect.H, frame.Height - y);
        return src[new Rect(x, y, w, h)].Clone();
    }

    static (int X, int Y) RegionOffset(string region, CaptureFrame frame)
    {
        var rect = RegionRect(region, frame);
        return (Math.Max(0, rect.X), Math.Max(0, rect.Y));
    }

    /// <summary>把桌面绝对坐标的区域换算为帧内像素矩形</summary>
    static PixelRect RegionRect(string region, CaptureFrame frame)
    {
        var rect = PixelRect.Parse(region ?? "full");
        if (rect.IsFull)
            return new PixelRect(0, 0, frame.Width, frame.Height);
        return new PixelRect(rect.X - frame.OriginX, rect.Y - frame.OriginY, rect.W, rect.H);
    }

    public static (byte R, byte G, byte B) ParseHex(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return (r, g, b);
        return (255, 255, 255);
    }

    public static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
}
