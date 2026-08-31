using CommunityToolkit.Mvvm.ComponentModel;
using TuSeEditor.App.Models;

namespace TuSeEditor.App.ViewModels;

/// <summary>属性面板行基类:统一持有字符串值,按 Kind 写回参数</summary>
public partial class PropertyRowVM : ObservableObject
{
    public ParamDef Def;

    [ObservableProperty]
    private string _value = "";

    /// <summary>值变化回调(由 MainViewModel 注入)</summary>
    public Action? Changed;

    /// <summary>可见性依赖取值(由 MainViewModel 注入)</summary>
    public Func<Dictionary<string, object>>? ParamsGetter;

    [ObservableProperty]
    private bool _isVisible = true;

    public string Label => Def.Label;

    /// <summary>勾选框绑定</summary>
    public bool BoolValue
    {
        get => Value == "true";
        set { Value = value ? "true" : "false"; }
    }

    partial void OnValueChanged(string value) => Changed?.Invoke();

    public void RefreshVisible()
    {
        if (Def.DependsOnKey == null) { IsVisible = true; return; }
        var v = ParamsGetter?.Invoke();
        IsVisible = v != null && v.TryGetValue(Def.DependsOnKey, out var val) &&
                    string.Equals(val?.ToString(), Def.DependsOnValue, StringComparison.Ordinal);
    }

    /// <summary>把字符串值按定义类型转换后写入步骤参数</summary>
    public void WriteTo(Step step)
    {
        object v = Def.Kind switch
        {
            ParamKind.Int => int.TryParse(Value, out var i) ? i : 0,
            ParamKind.Double => double.TryParse(Value, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0,
            ParamKind.Check => BoolValue,
            _ => (object)Value,
        };
        step.Params[Def.Key] = v;
    }
}

public class TextRowVM : PropertyRowVM { }
public class MultiTextRowVM : PropertyRowVM { }
public class NumberRowVM : PropertyRowVM { }
public class ComboRowVM : PropertyRowVM { }
public class CheckRowVM : PropertyRowVM { }
public class KeysRowVM : PropertyRowVM { }

public class ColorRowVM : PropertyRowVM
{
    /// <summary>打开取色覆盖层(MainViewModel 注入)</summary>
    public Action<ColorRowVM>? PickColor;
    /// <summary>测试找色(注入)</summary>
    public Action<ColorRowVM>? TestColor;
}

public class TemplateRowVM : PropertyRowVM
{
    /// <summary>打开抓图覆盖层截取模板(注入)</summary>
    public Action<TemplateRowVM>? Capture;
    /// <summary>从磁盘选择已有图片(注入)</summary>
    public Action<TemplateRowVM>? Browse;
    /// <summary>测试找图(注入)</summary>
    public Action<TemplateRowVM>? Test;
}

public class RegionRowVM : PropertyRowVM
{
    /// <summary>打开覆盖层框选区域(注入)</summary>
    public Action<RegionRowVM>? PickRegion;
}
