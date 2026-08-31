using Newtonsoft.Json;

namespace TuSeEditor.App.Models;

/// <summary>脚本积木类型</summary>
public enum StepType
{
    FindImageClick,   // 找图点击
    FindColorClick,   // 找色点击
    WaitImage,        // 等待图出现
    WaitImageGone,    // 等待图消失
    IfImage,          // 判断图存在
    IfColor,          // 判断色存在
    MouseClick,       // 鼠标点击
    MouseMove,        // 鼠标移动
    MouseWheel,       // 滚轮
    MouseDrag,        // 拖拽
    KeyPress,         // 按键
    InputText,        // 输入文本
    Delay,            // 延时
    Loop,             // 循环
    BreakLoop,        // 跳出循环
    Comment,          // 注释
    StopScript,       // 停止脚本
}

/// <summary>步骤:积木节点,参数用字符串键值存放,便于 JSON 序列化与动态表单</summary>
public class Step
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public StepType Type { get; set; }
    public bool Enabled { get; set; } = true;

    public Dictionary<string, object> Params { get; set; } = new();

    /// <summary>容器步骤的子步骤(循环体 / 条件"满足"分支)</summary>
    public List<Step> Children { get; set; } = new();

    /// <summary>条件步骤"不满足"分支</summary>
    public List<Step> ElseChildren { get; set; } = new();

    public Step() { }

    public Step(StepType type) => Type = type;

    public Step DeepClone()
    {
        var s = JsonConvert.DeserializeObject<Step>(JsonConvert.SerializeObject(this))!;
        s.Id = Guid.NewGuid().ToString("N");
        return s;
    }
}
