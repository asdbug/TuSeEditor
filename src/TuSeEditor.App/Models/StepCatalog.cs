using System.IO;
using System.Text;

namespace TuSeEditor.App.Models;

/// <summary>属性编辑器类型</summary>
public enum ParamKind
{
    Text,        // 单行文本
    MultiText,   // 多行文本
    Int,         // 整数
    Double,      // 小数
    Combo,       // 下拉选择
    Check,       // 勾选
    Color,       // 颜色 #RRGGBB
    Template,    // 模板图路径
    Region,      // 搜索区域 "full" 或 "x,y,w,h"
    Keys,        // 按键组合 如 ctrl+s
}

/// <summary>参数定义(驱动右侧动态属性面板)</summary>
public class ParamDef
{
    public string Key = "";
    public string Label = "";
    public ParamKind Kind;
    public object? Default;
    public string[] Options = Array.Empty<string>();
    public double Min, Max;
    /// <summary>仅当另一个参数等于指定值时显示(如"随机模式"才显示上限)</summary>
    public string? DependsOnKey;
    public string? DependsOnValue;
    public string Tooltip = "";
}

/// <summary>积木目录:每种步骤的名称、分类、图标、参数模式与默认值</summary>
public static class StepCatalog
{
    public class StepInfo
    {
        public StepType Type;
        public string Name = "";
        public string Category = "";
        public string Icon = "";
        public string Desc = "";
        public bool HasChildren;
        public bool HasElse;
        public ParamDef[] Params = Array.Empty<ParamDef>();
    }

    static ParamDef P(string key, string label, ParamKind kind, object? def = null,
        string[]? options = null, string? dependsKey = null, string? dependsValue = null, string tooltip = "")
        => new() { Key = key, Label = label, Kind = kind, Default = def, Options = options ?? Array.Empty<string>(), DependsOnKey = dependsKey, DependsOnValue = dependsValue, Tooltip = tooltip };

    static readonly ParamDef[] FindParams =
    {
        P("template", "模板图", ParamKind.Template, ""),
        P("similarity", "相似度", ParamKind.Double, 0.85, tooltip: "0.5~1.0,匹配不到就调低,误匹配就调高"),
        P("region", "搜索区域", ParamKind.Region, "full", tooltip: "限制搜索范围可大幅提速"),
        P("action", "点击方式", ParamKind.Combo, "单击", new[] { "单击", "双击", "右键", "不点击" }),
        P("offsetX", "X偏移", ParamKind.Int, 0),
        P("offsetY", "Y偏移", ParamKind.Int, 0),
        P("timeout", "等待超时(秒)", ParamKind.Double, 5),
        P("interval", "检测间隔(秒)", ParamKind.Double, 0.5),
        P("failPolicy", "超时后", ParamKind.Combo, "继续执行", new[] { "继续执行", "停止脚本" }),
    };

    static readonly ParamDef[] ColorParams =
    {
        P("color", "颜色", ParamKind.Color, "#FFFFFF"),
        P("tolerance", "容差", ParamKind.Int, 15, tooltip: "每个通道允许的色值偏差 0~100"),
        P("region", "搜索区域", ParamKind.Region, "full"),
        P("action", "点击方式", ParamKind.Combo, "单击", new[] { "单击", "双击", "右键", "不点击" }),
        P("offsetX", "X偏移", ParamKind.Int, 0),
        P("offsetY", "Y偏移", ParamKind.Int, 0),
        P("timeout", "等待超时(秒)", ParamKind.Double, 5),
        P("interval", "检测间隔(秒)", ParamKind.Double, 0.5),
        P("failPolicy", "超时后", ParamKind.Combo, "继续执行", new[] { "继续执行", "停止脚本" }),
    };

    static readonly ParamDef[] WaitParams =
    {
        P("template", "模板图", ParamKind.Template, ""),
        P("similarity", "相似度", ParamKind.Double, 0.85),
        P("region", "搜索区域", ParamKind.Region, "full"),
        P("timeout", "超时(秒)", ParamKind.Double, 10),
        P("interval", "检测间隔(秒)", ParamKind.Double, 0.5),
        P("failPolicy", "超时后", ParamKind.Combo, "继续执行", new[] { "继续执行", "停止脚本" }),
    };

    static readonly ParamDef[] IfImageParams =
    {
        P("template", "模板图", ParamKind.Template, ""),
        P("similarity", "相似度", ParamKind.Double, 0.85),
        P("region", "搜索区域", ParamKind.Region, "full"),
    };

    static readonly ParamDef[] IfColorParams =
    {
        P("color", "颜色", ParamKind.Color, "#FFFFFF"),
        P("tolerance", "容差", ParamKind.Int, 15),
        P("region", "搜索区域", ParamKind.Region, "full"),
    };

    static readonly ParamDef[] ClickParams =
    {
        P("posX", "X坐标", ParamKind.Int, -1, tooltip: "-1 表示当前鼠标位置"),
        P("posY", "Y坐标", ParamKind.Int, -1),
        P("action", "点击方式", ParamKind.Combo, "单击", new[] { "单击", "双击", "右键" }),
    };

    public static readonly List<StepInfo> All = new()
    {
        new StepInfo{ Type=StepType.FindImageClick, Name="找图点击", Category="图色识别", Icon="🖼", Desc="在屏幕上找到模板图并点击", Params=FindParams },
        new StepInfo{ Type=StepType.FindColorClick, Name="找色点击", Category="图色识别", Icon="🎨", Desc="找到指定颜色像素并点击", Params=ColorParams },
        new StepInfo{ Type=StepType.WaitImage, Name="等待图出现", Category="图色识别", Icon="⏳", Desc="循环检测直到模板图出现或超时", Params=WaitParams },
        new StepInfo{ Type=StepType.WaitImageGone, Name="等待图消失", Category="图色识别", Icon="💤", Desc="循环检测直到模板图消失或超时", Params=WaitParams },
        new StepInfo{ Type=StepType.IfImage, Name="判断图存在", Category="图色识别", Icon="❓", Desc="模板图存在时执行「满足」分支,否则执行「否则」分支", Params=IfImageParams, HasChildren=true, HasElse=true },
        new StepInfo{ Type=StepType.IfColor, Name="判断色存在", Category="图色识别", Icon="❓", Desc="指定颜色存在时执行「满足」分支", Params=IfColorParams, HasChildren=true, HasElse=true },

        new StepInfo{ Type=StepType.MouseClick, Name="鼠标点击", Category="鼠标键盘", Icon="🖱", Desc="在指定坐标点击", Params=ClickParams },
        new StepInfo{ Type=StepType.MouseMove, Name="鼠标移动", Category="鼠标键盘", Icon="↗", Desc="移动鼠标到指定坐标",
            Params=new[]{ P("posX","X坐标",ParamKind.Int,0), P("posY","Y坐标",ParamKind.Int,0) } },
        new StepInfo{ Type=StepType.MouseWheel, Name="滚轮", Category="鼠标键盘", Icon="🔃", Desc="滚动滚轮",
            Params=new[]{ P("delta","滚动量",ParamKind.Int,120,tooltip:"正数向上,负数向下,120 约为一格") } },
        new StepInfo{ Type=StepType.MouseDrag, Name="拖拽", Category="鼠标键盘", Icon="✋", Desc="按住左键从起点拖到终点",
            Params=new[]{ P("fromX","起点X",ParamKind.Int,0), P("fromY","起点Y",ParamKind.Int,0),
                P("toX","终点X",ParamKind.Int,0), P("toY","终点Y",ParamKind.Int,0), P("duration","耗时(秒)",ParamKind.Double,0.5) } },
        new StepInfo{ Type=StepType.KeyPress, Name="按键", Category="鼠标键盘", Icon="⌨", Desc="按下并松开按键或组合键",
            Params=new[]{ P("keys","按键",ParamKind.Keys,"space",tooltip:"如 a / F5 / ctrl+s / alt+tab"),
                P("repeat","重复次数",ParamKind.Int,1), P("keyInterval","按键间隔(秒)",ParamKind.Double,0.05) } },
        new StepInfo{ Type=StepType.InputText, Name="输入文本", Category="鼠标键盘", Icon="📝", Desc="以 Unicode 方式输入文本(支持中文)",
            Params=new[]{ P("text","文本内容",ParamKind.MultiText,""), P("charInterval","字符间隔(秒)",ParamKind.Double,0.02) } },

        new StepInfo{ Type=StepType.Delay, Name="延时", Category="流程控制", Icon="⏱", Desc="等待一段时间",
            Params=new[]{ P("mode","方式",ParamKind.Combo,"固定",new[]{ "固定", "随机" }),
                P("seconds","秒数",ParamKind.Double,1.0), P("secondsMax","上限秒数",ParamKind.Double,2.0,dependsKey:"mode",dependsValue:"随机") } },
        new StepInfo{ Type=StepType.Loop, Name="循环", Category="流程控制", Icon="🔁", Desc="重复执行内部步骤",
            Params=new[]{ P("mode","方式",ParamKind.Combo,"固定次数",new[]{ "固定次数", "无限循环" }),
                P("count","循环次数",ParamKind.Int,10,dependsKey:"mode",dependsValue:"固定次数") },
            HasChildren=true },
        new StepInfo{ Type=StepType.BreakLoop, Name="跳出循环", Category="流程控制", Icon="⏏", Desc="结束最近一层循环" },

        new StepInfo{ Type=StepType.Comment, Name="注释", Category="其他", Icon="💬", Desc="备注说明,不执行任何动作",
            Params=new[]{ P("text","内容",ParamKind.Text,"") } },
        new StepInfo{ Type=StepType.StopScript, Name="停止脚本", Category="其他", Icon="🛑", Desc="立即结束整个脚本" },
    };

    public static StepInfo? Get(StepType type) => All.FirstOrDefault(x => x.Type == type);

    /// <summary>按目录创建步骤并填入默认参数</summary>
    public static Step Create(StepType type)
    {
        var step = new Step(type);
        var info = Get(type);
        if (info != null)
            foreach (var p in info.Params)
                step.Params[p.Key] = p.Default ?? "";
        return step;
    }

    /// <summary>取参数值(字符串形式,缺省回退目录默认值)</summary>
    public static string Str(this Step step, string key)
    {
        if (step.Params.TryGetValue(key, out var v) && v != null)
            return v.ToString() ?? "";
        return Get(step.Type)?.Params.FirstOrDefault(p => p.Key == key)?.Default?.ToString() ?? "";
    }

    public static int Int(this Step step, string key)
        => int.TryParse(step.Str(key), out var v) ? v : 0;

    public static double Dbl(this Step step, string key)
        => double.TryParse(step.Str(key), System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    /// <summary>生成步骤在列表中的显示文字</summary>
    public static string Describe(Step step)
    {
        var info = Get(step.Type);
        if (info == null) return step.Type.ToString();
        var sb = new StringBuilder();
        switch (step.Type)
        {
            case StepType.FindImageClick:
            case StepType.WaitImage:
            case StepType.WaitImageGone:
            case StepType.IfImage:
                sb.Append(Path.GetFileName(step.Str("template")));
                if (string.IsNullOrEmpty(sb.ToString())) sb.Append("(未选模板)");
                sb.Append($"  相似度{step.Str("similarity")}");
                break;
            case StepType.FindColorClick:
            case StepType.IfColor:
                sb.Append($"{step.Str("color")}  容差{step.Str("tolerance")}");
                break;
            case StepType.MouseClick:
                sb.Append($"{(step.Int("posX") < 0 ? "当前位置" : $"({step.Str("posX")},{step.Str("posY")})")} {step.Str("action")}");
                break;
            case StepType.MouseMove:
                sb.Append($"({step.Str("posX")},{step.Str("posY")})");
                break;
            case StepType.MouseWheel:
                sb.Append(step.Int("delta") > 0 ? "向上滚动" : "向下滚动");
                break;
            case StepType.MouseDrag:
                sb.Append($"({step.Str("fromX")},{step.Str("fromY")}) → ({step.Str("toX")},{step.Str("toY")})");
                break;
            case StepType.KeyPress:
                sb.Append($"{step.Str("keys").ToUpper()} ×{step.Str("repeat")}");
                break;
            case StepType.InputText:
                var t = step.Str("text");
                sb.Append(t.Length > 16 ? t[..16] + "…" : t);
                break;
            case StepType.Delay:
                sb.Append(step.Str("mode") == "随机" ? $"{step.Str("seconds")}~{step.Str("secondsMax")} 秒" : $"{step.Str("seconds")} 秒");
                break;
            case StepType.Loop:
                sb.Append(step.Str("mode") == "无限循环" ? "无限循环" : $"{step.Str("count")} 次");
                break;
            case StepType.Comment:
                sb.Append(step.Str("text"));
                break;
        }
        return $"{info.Name}  {sb}".TrimEnd();
    }
}
