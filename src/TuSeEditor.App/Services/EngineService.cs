using System.IO;
using TuSeEditor.App.Models;

namespace TuSeEditor.App.Services;

/// <summary>跳出循环信号</summary>
class BreakLoopSignal : Exception { }
/// <summary>停止整个脚本信号</summary>
class StopScriptSignal : Exception { }

/// <summary>脚本执行引擎:后台线程遍历步骤树</summary>
public sealed class EngineService
{
    public event Action<string>? Log;
    public event Action<string?>? CurrentStepChanged; // 步骤 Id(高亮),null=清除
    public event Action<bool>? RunStateChanged;

    public bool IsRunning { get; private set; }
    CancellationTokenSource? _cts;

    CaptureService _capture = new();
    ProjectDoc _project = new();

    public void Start(ProjectDoc project, CaptureService capture)
    {
        if (IsRunning) return;
        _project = project;
        _capture = capture;
        InputService.ScanCodeMode = project.ScanCodeMode;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        RunStateChanged?.Invoke(true);
        var token = _cts.Token;
        Task.Run(() =>
        {
            try
            {
                RunScript(token);
            }
            catch (OperationCanceledException) { }
            catch (StopScriptSignal)
            {
                Emit("⏹ 脚本被 StopScript 步骤结束");
            }
            catch (Exception ex)
            {
                Emit($"❌ 运行出错:{ex.Message}");
            }
            finally
            {
                IsRunning = false;
                CurrentStepChanged?.Invoke(null);
                RunStateChanged?.Invoke(false);
            }
        }, token);
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        Emit("⏹ 正在停止脚本…");
    }

    void Emit(string msg)
    {
        Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }

    void Sleep(double seconds, CancellationToken token)
    {
        if (token.WaitHandle.WaitOne(Math.Max(1, (int)(seconds * 1000))))
            throw new OperationCanceledException(token);
    }

    void RunScript(CancellationToken token)
    {
        var loops = _project.ScriptLoopCount;
        Emit(loops == 0 ? "▶ 脚本开始(无限循环)" : $"▶ 脚本开始(共 {loops} 轮)");
        int round = 0;
        while (true)
        {
            round++;
            if (loops != 0 && round > loops) break;
            if (loops == 0 && round > 1) Emit($"—— 第 {round} 轮 ——");
            ExecuteList(_project.Steps, token, 0);
            if (loops == 0) continue;
        }
        Emit("✔ 脚本执行完毕");
    }

    /// <summary>执行一组步骤(某个列表层级)</summary>
    void ExecuteList(List<Step> steps, CancellationToken token, int depth)
    {
        foreach (var step in steps)
        {
            token.ThrowIfCancellationRequested();
            if (!step.Enabled) continue;
            Execute(step, token, depth);
        }
    }

    void Execute(Step step, CancellationToken token, int depth)
    {
        var indent = new string(' ', depth * 2);
        CurrentStepChanged?.Invoke(step.Id);
        var info = StepCatalog.Get(step.Type);

        switch (step.Type)
        {
            case StepType.Comment:
                Emit($"{indent}💬 {step.Str("text")}");
                break;

            case StepType.Delay:
            {
                double s = step.Str("mode") == "随机"
                    ? Random.Shared.NextDouble() * (step.Dbl("secondsMax") - step.Dbl("seconds")) + step.Dbl("seconds")
                    : step.Dbl("seconds");
                Emit($"{indent}⏱ 延时 {s:F2} 秒");
                Sleep(s, token);
                break;
            }

            case StepType.MouseClick:
            {
                int x = step.Int("posX"), y = step.Int("posY");
                if (x < 0 || y < 0) (x, y) = InputService.CurrentPos();
                InputService.MoveTo(x, y);
                InputService.Click(step.Str("action"));
                Emit($"{indent}🖱 ({x},{y}) {step.Str("action")}");
                Sleep(0.05, token);
                break;
            }

            case StepType.MouseMove:
                InputService.MoveTo(step.Int("posX"), step.Int("posY"));
                Emit($"{indent}↗ 移动到 ({step.Str("posX")},{step.Str("posY")})");
                break;

            case StepType.MouseWheel:
                InputService.Wheel(step.Int("delta"));
                Emit($"{indent}🔃 滚轮 {step.Int("delta")}");
                Sleep(0.1, token);
                break;

            case StepType.MouseDrag:
                Emit($"{indent}✋ 拖拽 ({step.Str("fromX")},{step.Str("fromY")})→({step.Str("toX")},{step.Str("toY")})");
                InputService.Drag(step.Int("fromX"), step.Int("fromY"), step.Int("toX"), step.Int("toY"), step.Dbl("duration"));
                break;

            case StepType.KeyPress:
            {
                int repeat = Math.Max(1, step.Int("repeat"));
                Emit($"{indent}⌨ {step.Str("keys")} ×{repeat}");
                for (int i = 0; i < repeat; i++)
                {
                    InputService.PressKeys(step.Str("keys"));
                    if (i < repeat - 1) Sleep(step.Dbl("keyInterval"), token);
                }
                Sleep(0.05, token);
                break;
            }

            case StepType.InputText:
                Emit($"{indent}📝 输入文本:{Truncate(step.Str("text"), 30)}");
                InputService.TypeText(step.Str("text"), step.Dbl("charInterval"));
                break;

            case StepType.FindImageClick:
            case StepType.FindColorClick:
            {
                bool isImage = step.Type == StepType.FindImageClick;
                double timeout = step.Dbl("timeout"), interval = Math.Max(0.1, step.Dbl("interval"));
                Emit($"{indent}{info!.Icon} {info.Name}:{(isImage ? Path.GetFileName(step.Str("template")) : step.Str("color"))}(超时{timeout}s)");
                var deadline = Environment.TickCount64 + (long)(timeout * 1000);
                MatchResult hit = default;
                CaptureFrame frame;
                while (true)
                {
                    frame = _capture.CaptureForMatch();
                    hit = isImage
                        ? MatcherService.FindImage(frame, step.Str("template"), step.Dbl("similarity"), step.Str("region"))
                        : MatcherService.FindColor(frame, step.Str("color"), step.Int("tolerance"), step.Str("region"));
                    if (hit.Found) break;
                    if (Environment.TickCount64 >= deadline) break;
                    Sleep(interval, token);
                }
                if (hit.Found)
                {
                    var (ax, ay) = hit.ToAbsolute(frame);
                    if (step.Str("action") != "不点击")
                    {
                        InputService.MoveTo(ax + step.Int("offsetX"), ay + step.Int("offsetY"));
                        InputService.Click(step.Str("action"));
                    }
                    Emit($"{indent}✔ 找到 ({ax},{ay}) 相似度{hit.Score:F2},{(step.Str("action") == "不点击" ? "未点击" : step.Str("action") + "完成")}");
                    Sleep(0.1, token);
                }
                else
                {
                    Emit($"{indent}✖ 未找到,超时{timeout}秒");
                    if (step.Str("failPolicy") == "停止脚本") throw new StopScriptSignal();
                }
                break;
            }

            case StepType.WaitImage:
            case StepType.WaitImageGone:
            {
                bool wantPresent = step.Type == StepType.WaitImage;
                double timeout = step.Dbl("timeout"), interval = Math.Max(0.1, step.Dbl("interval"));
                Emit($"{indent}{info!.Icon} 等待图{(wantPresent ? "出现" : "消失")}:{Path.GetFileName(step.Str("template"))}(超时{timeout}s)");
                var deadline = Environment.TickCount64 + (long)(timeout * 1000);
                bool ok = false;
                while (true)
                {
                    var frame = _capture.CaptureForMatch();
                    var hit = MatcherService.FindImage(frame, step.Str("template"), step.Dbl("similarity"), step.Str("region"));
                    ok = wantPresent ? hit.Found : !hit.Found;
                    if (ok) break;
                    if (Environment.TickCount64 >= deadline) break;
                    Sleep(interval, token);
                }
                if (ok) Emit($"{indent}✔ 条件满足");
                else
                {
                    Emit($"{indent}✖ 等待超时{timeout}秒");
                    if (step.Str("failPolicy") == "停止脚本") throw new StopScriptSignal();
                }
                break;
            }

            case StepType.IfImage:
            case StepType.IfColor:
            {
                bool isImage = step.Type == StepType.IfImage;
                var frame = _capture.CaptureForMatch();
                var hit = isImage
                    ? MatcherService.FindImage(frame, step.Str("template"), step.Dbl("similarity"), step.Str("region"))
                    : MatcherService.FindColor(frame, step.Str("color"), step.Int("tolerance"), step.Str("region"));
                Emit($"{indent}{info!.Icon} 判断{(isImage ? "图" : "色")} → {(hit.Found ? "满足" : "不满足")}(相似度/命中:{hit.Score:F2})");
                ExecuteList(hit.Found ? step.Children : step.ElseChildren, token, depth + 1);
                break;
            }

            case StepType.Loop:
            {
                bool infinite = step.Str("mode") == "无限循环";
                int count = Math.Max(1, step.Int("count"));
                Emit($"{indent}🔁 循环开始:{(infinite ? "无限" : count + " 次")}");
                try
                {
                    int i = 0;
                    while (infinite || i < count)
                    {
                        token.ThrowIfCancellationRequested();
                        ExecuteList(step.Children, token, depth + 1);
                        i++;
                    }
                }
                catch (BreakLoopSignal)
                {
                    Emit($"{indent}⏏ 跳出循环");
                }
                Emit($"{indent}🔁 循环结束");
                break;
            }

            case StepType.BreakLoop:
                throw new BreakLoopSignal();

            case StepType.StopScript:
                throw new StopScriptSignal();

            default:
                Emit($"{indent}⚠ 未知步骤类型 {step.Type}");
                break;
        }
    }

    static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
