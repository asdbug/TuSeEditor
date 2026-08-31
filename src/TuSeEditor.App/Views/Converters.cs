using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Media;

namespace TuSeEditor.App.Views;

/// <summary>"#RRGGBB" → 画刷</summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string s && s.StartsWith("#") && s.Length == 7)
                return (Brush)new BrushConverter().ConvertFromString(s);
        }
        catch { }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => "";
}

/// <summary>图片路径 → 缩略图(失败返回 null)</summary>
public class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string s && File.Exists(s))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(s, UriKind.Absolute);
                bmp.DecodePixelWidth = 240;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
        catch { }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => "";
}
