using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TuSeEditor.App.Services;

namespace TuSeEditor.App.Views;

public enum OverlayMode { Template, Region, Color }

/// <summary>
/// 全屏截图覆盖层:框选模板 / 框选区域 / 单击取色,带像素放大镜。
/// 截图按物理像素存储,显示时按窗口尺寸映射,DIP↔像素换算自适应 DPI。
/// </summary>
public partial class CaptureOverlayWindow : Window
{
    readonly CaptureFrame _frame;
    readonly OverlayMode _mode;

    bool _dragging;
    Point _startDip;

    /// <summary>模板结果:(裁剪出的 BGRA 像素, 帧内矩形)</summary>
    public (byte[] Bgra, PixelRect Rect)? TemplateResult { get; private set; }
    /// <summary>区域结果:帧内矩形</summary>
    public PixelRect? RegionResult { get; private set; }
    /// <summary>取色结果</summary>
    public (byte R, byte G, byte B, int X, int Y)? ColorResult { get; private set; }

    public CaptureOverlayWindow(CaptureFrame frame, OverlayMode mode)
    {
        InitializeComponent();
        _frame = frame;
        _mode = mode;
        HintText.Text = mode switch
        {
            OverlayMode.Template => "🖼 拖动框选要找的目标(按钮/图标),松开完成;Esc 或右键取消",
            OverlayMode.Region => "⬚ 拖动框选搜索区域,松开完成;Esc 或右键取消",
            _ => "🎨 移动鼠标查看放大镜,单击取色;Esc 或右键取消",
        };
        Loaded += (_, _) =>
        {
            ScreenImage.Source = frame.ToBitmapSource();
            ScreenImage.Width = Root.ActualWidth;
            ScreenImage.Height = Root.ActualHeight;
            Dim.Width = Root.ActualWidth;
            Dim.Height = Root.ActualHeight;
            // 取消按钮放右上角
            CancelBtn.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(CancelBtn, 14);
            Canvas.SetTop(CancelBtn, 10);
        };
    }

    void CancelBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    // ---- DIP ↔ 像素换算 ----
    double PxPerDipX => _frame.Width / Math.Max(1, Root.ActualWidth);
    double PxPerDipY => _frame.Height / Math.Max(1, Root.ActualHeight);

    (int X, int Y) ToPixel(Point dip) => (
        Math.Clamp((int)(dip.X * PxPerDipX), 0, _frame.Width - 1),
        Math.Clamp((int)(dip.Y * PxPerDipY), 0, _frame.Height - 1));

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    void Root_RightDown(object sender, MouseButtonEventArgs e)
    {
        DialogResult = false;
    }

    void Root_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(Root);
        if (_mode == OverlayMode.Color)
        {
            var p = ToPixel(pos);
            var (r, g, b) = MatcherService.ColorAt(_frame, p.X, p.Y);
            ColorResult = (r, g, b, p.X + _frame.OriginX, p.Y + _frame.OriginY);
            DialogResult = true;
            return;
        }
        _dragging = true;
        _startDip = pos;
        Selection.Visibility = Visibility.Visible;
        UpdateSelection(pos);
    }

    void Root_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(Root);
        UpdateMagnifier(pos);
        if (_dragging) UpdateSelection(pos);
    }

    void Root_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        var rect = CurrentRectDip(e.GetPosition(Root));
        int pxX = (int)(rect.X * PxPerDipX), pxY = (int)(rect.Y * PxPerDipY);
        int pxW = (int)(rect.Width * PxPerDipX), pxH = (int)(rect.Height * PxPerDipY);
        if (pxW < 2 || pxH < 2)
        {
            Selection.Visibility = Visibility.Hidden;
            SizeLabel.Visibility = Visibility.Hidden;
            return;
        }
        pxW = Math.Min(pxW, _frame.Width - pxX);
        pxH = Math.Min(pxH, _frame.Height - pxY);

        if (_mode == OverlayMode.Template)
        {
            TemplateResult = (CropBgra(pxX, pxY, pxW, pxH), new PixelRect(pxX, pxY, pxW, pxH));
        }
        else
        {
            RegionResult = new PixelRect(pxX + _frame.OriginX, pxY + _frame.OriginY, pxW, pxH);
        }
        DialogResult = true;
    }

    void UpdateSelection(Point current)
    {
        var rect = CurrentRectDip(current);
        Canvas.SetLeft(Selection, rect.X);
        Canvas.SetTop(Selection, rect.Y);
        Selection.Width = rect.Width;
        Selection.Height = rect.Height;
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Text = $"{(int)(rect.Width * PxPerDipX)} × {(int)(rect.Height * PxPerDipY)}";
        Canvas.SetLeft(SizeLabel, rect.X);
        Canvas.SetTop(SizeLabel, rect.Y + rect.Height + 2);
    }

    Rect CurrentRectDip(Point current)
    {
        double x = Math.Min(_startDip.X, current.X);
        double y = Math.Min(_startDip.Y, current.Y);
        double w = Math.Abs(current.X - _startDip.X);
        double h = Math.Abs(current.Y - _startDip.Y);
        return new Rect(x, y, w, h);
    }

    void UpdateMagnifier(Point dip)
    {
        var p = ToPixel(dip);
        const int half = 10; // 21×21 像素窗口
        var crop = new byte[21 * 21 * 4];
        for (int dy = 0; dy < 21; dy++)
        {
            int sy = p.Y + dy - half;
            if (sy < 0 || sy >= _frame.Height) continue;
            for (int dx = 0; dx < 21; dx++)
            {
                int sx = p.X + dx - half;
                if (sx < 0 || sx >= _frame.Width) continue;
                int si = (sy * _frame.Width + sx) * 4;
                int di = (dy * 21 + dx) * 4;
                crop[di] = _frame.Bgra[si];
                crop[di + 1] = _frame.Bgra[si + 1];
                crop[di + 2] = _frame.Bgra[si + 2];
                crop[di + 3] = 255;
            }
        }
        MagImage.Source = BitmapSource.Create(21, 21, 96, 96, PixelFormats.Bgra32, null, crop, 21 * 4);

        var (r, g, b) = MatcherService.ColorAt(_frame, p.X, p.Y);
        int absX = p.X + _frame.OriginX, absY = p.Y + _frame.OriginY;
        MagText.Text = $"X:{absX}  Y:{absY}\nRGB({r},{g},{b})  #{r:X2}{g:X2}{b:X2}";

        Magnifier.Visibility = Visibility.Visible;
        double mx = dip.X + 18, my = dip.Y + 18;
        if (mx + 160 > Root.ActualWidth) mx = dip.X - 160;
        if (my + 180 > Root.ActualHeight) my = dip.Y - 180;
        Canvas.SetLeft(Magnifier, mx);
        Canvas.SetTop(Magnifier, my);
    }

    byte[] CropBgra(int x, int y, int w, int h)
    {
        var outBuf = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
        {
            int src = ((y + row) * _frame.Width + x) * 4;
            Array.Copy(_frame.Bgra, src, outBuf, row * w * 4, w * 4);
        }
        return outBuf;
    }
}
