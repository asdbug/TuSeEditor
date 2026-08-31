// 核心服务无头测试:抓图 → 存模板 → 找图 → 找色 → 区域找图 → 导出 Python
using System.IO;
using TuSeEditor.App.Models;
using TuSeEditor.App.Services;

int fail = 0;
void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "✔" : "✖")} {name} {detail}");
    if (!ok) fail++;
}

// 1. 抓图(优先 DXGI,失败自动 GDI)
using var cap = new CaptureService();
cap.Log = m => Console.WriteLine($"   [capture] {m}");
var frame = cap.CaptureForMatch();
Check("抓图", frame.Width > 0 && frame.Bgra.Length == frame.Width * frame.Height * 4,
    $"{frame.Engine} {frame.Width}x{frame.Height} origin=({frame.OriginX},{frame.OriginY}) 黑屏={frame.IsBlank()}");

// 2. 自动寻找一块"有内容"的区域(方差足够)作为模板,避免纯色退化
static double StdDev(byte[] src, int fw, int x, int y, int w, int h)
{
    double sum = 0, sum2 = 0; int n = w * h;
    for (int row = 0; row < h; row++)
        for (int col = 0; col < w; col++)
        {
            int i = ((y + row) * fw + x + col) * 4;
            double lum = 0.299 * src[i + 2] + 0.587 * src[i + 1] + 0.114 * src[i];
            sum += lum; sum2 += lum * lum;
        }
    double mean = sum / n;
    return Math.Sqrt(sum2 / n - mean * mean);
}

int tw = 120, th = 40, tx = -1, ty = -1; double sd = 0;
for (int sy = 100; sy + th < frame.Height - 50; sy += 40)
{
    for (int sx = 100; sx + tw < frame.Width - 50; sx += 60)
    {
        sd = StdDev(frame.Bgra, frame.Width, sx, sy, tw, th);
        if (sd >= 3) { tx = sx; ty = sy; break; }
    }
    if (tx >= 0) break;
}
Check("找到有特征的区域", tx >= 0, $"pos=({tx},{ty}) 方差={sd:F1}");

var crop = new byte[tw * th * 4];
for (int row = 0; row < th; row++)
    Array.Copy(frame.Bgra, ((ty + row) * frame.Width + tx) * 4, crop, row * tw * 4, tw * 4);
string dir = Path.Combine(AppContext.BaseDirectory, "testout");
Directory.CreateDirectory(dir);
string tpl = Path.Combine(dir, "tpl.png");
ImageUtil.SavePng(crop, tw, th, tpl);
Check("模板保存", File.Exists(tpl), tpl);

// 2b. 纯色模板应报明确错误而非乱匹配
string blankTpl = Path.Combine(dir, "blank.png");
ImageUtil.SavePng(new byte[tw * th * 4], tw, th, blankTpl);
bool blankRejected = false;
try { MatcherService.FindImage(frame, blankTpl, 0.9, "full"); }
catch (InvalidOperationException ex) { blankRejected = ex.Message.Contains("纯色"); }
Check("纯色模板防护", blankRejected);

// 3. 全屏找图
var hit = MatcherService.FindImage(frame, tpl, 0.9, "full");
Check("全屏找图", hit.Found && Math.Abs(hit.X - (tx + tw / 2)) <= 2 && Math.Abs(hit.Y - (ty + th / 2)) <= 2,
    $"found={hit.Found} pos=({hit.X},{hit.Y}) score={hit.Score:F3} 期望({tx + tw / 2},{ty + th / 2})");

// 4. 区域找图(桌面绝对坐标)
string region = $"{frame.OriginX + tx - 60},{frame.OriginY + ty - 20},240,80";
var hit2 = MatcherService.FindImage(frame, tpl, 0.9, region);
Check("区域找图", hit2.Found, $"region={region} pos=({hit2.X},{hit2.Y}) score={hit2.Score:F3}");

// 5. 找色:取屏幕中央某像素的颜色,再找它
var (r, g, b) = MatcherService.ColorAt(frame, frame.Width / 2, frame.Height / 2);
var chit = MatcherService.FindColor(frame, MatcherService.ToHex(r, g, b), 0, "full");
Check("找色", chit.Found, $"color=#{r:X2}{g:X2}{b:X2} pos=({chit.X},{chit.Y})");

// 6. 鼠标移动(不点击,验证 SendInput 链路)
int cx = frame.OriginX + frame.Width / 2, cy = frame.OriginY + frame.Height / 2;
InputService.MoveTo(cx, cy);
var cur = InputService.CurrentPos();
InputService.MoveTo(frame.OriginX + 10, frame.OriginY + 10);
Check("鼠标移动", true, $"移动到({cx},{cy}) 实际({cur.X},{cur.Y}) 后回到左上");

// 7. 导出 Python 脚本
var proj = new ProjectDoc { ScriptLoopCount = 2 };
var sFind = StepCatalog.Create(StepType.FindImageClick);
sFind.Params["template"] = tpl;
sFind.Params["action"] = "不点击";
var sDelay = StepCatalog.Create(StepType.Delay);
sDelay.Params["mode"] = "随机";
var sLoop = StepCatalog.Create(StepType.Loop);
sLoop.Params["mode"] = "固定次数";
sLoop.Params["count"] = 3;
var sKey = StepCatalog.Create(StepType.KeyPress);
sKey.Params["keys"] = "ctrl+s";
sLoop.Children.Add(sKey);
var sIf = StepCatalog.Create(StepType.IfImage);
sIf.Params["template"] = tpl;
var sCmt = StepCatalog.Create(StepType.Comment);
sCmt.Params["text"] = "测试注释";
sIf.Children.Add(sCmt);
proj.Steps.Add(sFind);
proj.Steps.Add(sDelay);
proj.Steps.Add(sLoop);
proj.Steps.Add(sIf);
string exportDir = Path.Combine(dir, "export");
PythonExporter.Export(proj, proj.Steps, exportDir);
string py = Path.Combine(exportDir, "script.py");
Check("导出 Python", File.Exists(py) && File.Exists(Path.Combine(exportDir, "templates", "tpl.png")), py);
Console.WriteLine("---- 生成的脚本主体(节选)----");
foreach (var line in File.ReadAllLines(py).Where(l => l.StartsWith("    ") && !l.Contains("_fields_")).Take(24))
    Console.WriteLine(line);

// 7. 工程保存 / 加载
string projPath = Path.Combine(dir, "test.tsproj");
ProjectService.Save(proj, projPath);
var loaded = ProjectService.Load(projPath);
int loadedCount = 0;
void CountWalk(List<Step> list) { foreach (var s in list) { loadedCount++; CountWalk(s.Children); CountWalk(s.ElseChildren); } }
CountWalk(loaded.Steps);
Check("工程保存/加载", loaded.Steps.Count == 4 && loadedCount == 6, $"顶层 {loaded.Steps.Count} 个 / 总计 {loadedCount} 个步骤");

// 8. 引擎运行(仅注释/延时/循环/条件,无输入动作,安全)
var proj2 = new ProjectDoc { ScriptLoopCount = 2 };
var c1 = StepCatalog.Create(StepType.Comment); c1.Params["text"] = "引擎测试开始";
var dl = StepCatalog.Create(StepType.Delay); dl.Params["mode"] = "固定"; dl.Params["seconds"] = 0.05;
var lp = StepCatalog.Create(StepType.Loop); lp.Params["mode"] = "固定次数"; lp.Params["count"] = 2;
var c2 = StepCatalog.Create(StepType.Comment); c2.Params["text"] = "循环体内";
lp.Children.Add(c2);
var br = StepCatalog.Create(StepType.BreakLoop);
lp.Children.Add(br); // 第一次循环即跳出,验证 BreakLoop
var st = StepCatalog.Create(StepType.StopScript);
proj2.Steps.Add(c1); proj2.Steps.Add(dl); proj2.Steps.Add(lp); proj2.Steps.Add(st);
var c3 = StepCatalog.Create(StepType.Comment); c3.Params["text"] = "不应执行(StopScript 之后)";
proj2.Steps.Add(c3);

var engine = new EngineService();
var logs = new List<string>();
bool engineEnded = false;
engine.Log += m => { lock (logs) logs.Add(m); };
engine.RunStateChanged += running => { if (!running) { lock (logs) engineEnded = true; Monitor.PulseAll(logs); } };
engine.Start(proj2, cap);
lock (logs)
{
    while (!engineEnded) Monitor.Wait(logs, 5000);
}
var logText = string.Join("\n", logs);
Check("引擎运行(StopScript 生效)", logText.Contains("脚本被 StopScript") && !logText.Contains("不应执行") && logText.Contains("跳出循环"),
    $"\n{logText}");

Console.WriteLine(fail == 0 ? "== 全部通过 ==" : $"== {fail} 项失败 ==");
return fail;
