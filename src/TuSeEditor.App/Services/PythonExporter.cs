using System.IO;
using System.Text;
using TuSeEditor.App.Models;

namespace TuSeEditor.App.Services;

/// <summary>把积木脚本编译为独立可运行的 Python 脚本(依赖:opencv-python mss numpy pydirectinput)</summary>
public static class PythonExporter
{
    public static void Export(ProjectDoc project, IEnumerable<Step> steps, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var templatesDir = Path.Combine(outputDir, "templates");
        Directory.CreateDirectory(templatesDir);

        var py = new StringBuilder();
        py.Append(BuildHeader());
        py.AppendLine("# ===== 脚本主体 =====");
        py.AppendLine("def main():");
        var body = new StringBuilder();
        var em = new Emitter(body, 1);

        int loops = project.ScriptLoopCount;
        if (loops == 0) em.Line("while True:  # 无限循环,按 Ctrl+C 停止");
        else if (loops > 1) em.Line($"for _round in range({loops}):  # 整体循环 {loops} 轮");
        var inner = new Emitter(body, em.Indent + (loops != 1 ? 1 : 0));
        inner.EmitSteps(steps);
        py.Append(body);
        if (loops != 1 && inner.IsEmpty) em.Line("pass");

        py.AppendLine();
        py.AppendLine("if __name__ == \"__main__\":");
        py.AppendLine("    try:");
        py.AppendLine("        main()");
        py.AppendLine("    except KeyboardInterrupt:");
        py.AppendLine("        print(\"\\n已手动停止\")");

        File.WriteAllText(Path.Combine(outputDir, "script.py"), py.ToString(), new UTF8Encoding(false));

        // 复制模板图
        CopyTemplates(steps, templatesDir);

        File.WriteAllText(Path.Combine(outputDir, "运行说明.txt"),
            "使用说明\r\n========\r\n" +
            "1. 安装 Python 3.8+ 与依赖:\r\n   pip install opencv-python mss numpy pydirectinput\r\n" +
            "2. 双击运行或在命令行执行:\r\n   python script.py\r\n" +
            "3. 停止:在窗口按 Ctrl+C 或关闭命令行窗口。\r\n\r\n" +
            "注意:templates 文件夹内的模板图必须与 script.py 保持相对位置。\r\n" +
            "本脚本为前台自动化脚本,请遵守目标软件的用户协议,风险自负。\r\n",
            new UTF8Encoding(false));
    }

    static void CopyTemplates(IEnumerable<Step> steps, string templatesDir)
    {
        void Walk(List<Step> list)
        {
            foreach (var s in list)
            {
                var tpl = s.Str("template");
                if (!string.IsNullOrEmpty(tpl) && File.Exists(tpl) &&
                    s.Type is StepType.FindImageClick or StepType.WaitImage or StepType.WaitImageGone or StepType.IfImage)
                {
                    var dest = Path.Combine(templatesDir, Path.GetFileName(tpl));
                    File.Copy(tpl, dest, overwrite: true);
                }
                Walk(s.Children);
                Walk(s.ElseChildren);
            }
        }
        Walk(steps.ToList());
    }

    // ---------------- 代码生成 ----------------
    class Emitter
    {
        readonly StringBuilder _sb;
        public int Indent;
        public bool IsEmpty => _sb.Length == 0;

        public Emitter(StringBuilder sb, int indent) { _sb = sb; Indent = indent; }

        public void Line(string s) => _sb.AppendLine(new string(' ', Indent * 4) + s);

        public void EmitSteps(IEnumerable<Step> steps)
        {
            bool any = false;
            foreach (var s in steps) { EmitStep(s); any = true; }
            if (!any) Line("pass  # (空)");
        }

        void EmitStep(Step s)
        {
            switch (s.Type)
            {
                case StepType.Comment:
                    Line($"# {s.Str("text")}");
                    break;

                case StepType.Delay:
                    if (s.Str("mode") == "随机")
                        Line($"time.sleep(random.uniform({D(s, "seconds")}, {D(s, "secondsMax")}))");
                    else
                        Line($"time.sleep({D(s, "seconds")})");
                    break;

                case StepType.MouseClick:
                    if (s.Int("posX") >= 0)
                        Line($"click_at({s.Int("posX")}, {s.Int("posY")}, {Q(s, "action")})");
                    else
                        Line($"click_current({Q(s, "action")})");
                    break;

                case StepType.MouseMove:
                    Line($"move_to({s.Int("posX")}, {s.Int("posY")})");
                    break;

                case StepType.MouseWheel:
                    Line($"wheel({s.Int("delta")})");
                    break;

                case StepType.MouseDrag:
                    Line($"drag({s.Int("fromX")}, {s.Int("fromY")}, {s.Int("toX")}, {s.Int("toY")}, {D(s, "duration")})");
                    break;

                case StepType.KeyPress:
                    var keys = s.Str("keys").Replace("'", "\\'");
                    Line($"for _k in range({Math.Max(1, s.Int("repeat"))}):");
                    Line($"    press_keys('{keys}')");
                    if (s.Int("repeat") > 1) Line($"    time.sleep({D(s, "keyInterval")})");
                    break;

                case StepType.InputText:
                    Line($"type_text({JsonEncode(s.Str("text"))}, {D(s, "charInterval")})");
                    break;

                case StepType.FindImageClick:
                {
                    Line($"# 找图点击:{Path.GetFileName(s.Str("template"))}");
                    Line($"_hit = _wait_for(lambda: find_image({Tpl(s)}, {D(s, "similarity")}, {Region(s)}), {D(s, "timeout")}, {D(s, "interval")})");
                    Line("if _hit:");
                    Line($"    click_at(_hit[0] + {s.Int("offsetX")}, _hit[1] + {s.Int("offsetY")}, {Q(s, "action")})");
                    Line("else:");
                    if (s.Str("failPolicy") == "停止脚本")
                        Line("    print('找图超时,停止脚本'); sys.exit(1)");
                    else
                        Line("    print('找图超时,跳过')");
                    break;
                }

                case StepType.FindColorClick:
                {
                    Line($"# 找色点击:{s.Str("color")}");
                    Line($"_hit = _wait_for(lambda: find_color('{s.Str("color")}', {s.Int("tolerance")}, {Region(s)}), {D(s, "timeout")}, {D(s, "interval")})");
                    Line("if _hit:");
                    Line($"    click_at(_hit[0] + {s.Int("offsetX")}, _hit[1] + {s.Int("offsetY")}, {Q(s, "action")})");
                    Line("else:");
                    if (s.Str("failPolicy") == "停止脚本")
                        Line("    print('找色超时,停止脚本'); sys.exit(1)");
                    else
                        Line("    print('找色超时,跳过')");
                    break;
                }

                case StepType.WaitImage:
                {
                    Line($"# 等待图出现:{Path.GetFileName(s.Str("template"))}");
                    Line($"if _wait_for(lambda: find_image({Tpl(s)}, {D(s, "similarity")}, {Region(s)}), {D(s, "timeout")}, {D(s, "interval")}) is None:");
                    if (s.Str("failPolicy") == "停止脚本")
                        Line("    print('等待图片超时,停止脚本'); sys.exit(1)");
                    else
                        Line("    print('等待图片超时,跳过')");
                    break;
                }

                case StepType.WaitImageGone:
                {
                    Line($"# 等待图消失:{Path.GetFileName(s.Str("template"))}");
                    Line($"if _wait_for(lambda: find_image({Tpl(s)}, {D(s, "similarity")}, {Region(s)}) is None, {D(s, "timeout")}, {D(s, "interval")}) is None:");
                    if (s.Str("failPolicy") == "停止脚本")
                        Line("    print('等待图片消失超时,停止脚本'); sys.exit(1)");
                    else
                        Line("    print('等待图片消失超时,跳过')");
                    break;
                }

                case StepType.IfImage:
                {
                    Line($"if find_image({Tpl(s)}, {D(s, "similarity")}, {Region(s)}):");
                    var inner = new Emitter(_sb, Indent + 1);
                    inner.EmitSteps(s.Children);
                    Line("else:");
                    inner = new Emitter(_sb, Indent + 1);
                    inner.EmitSteps(s.ElseChildren);
                    break;
                }

                case StepType.IfColor:
                {
                    Line($"if find_color('{s.Str("color")}', {s.Int("tolerance")}, {Region(s)}):");
                    var inner = new Emitter(_sb, Indent + 1);
                    inner.EmitSteps(s.Children);
                    Line("else:");
                    inner = new Emitter(_sb, Indent + 1);
                    inner.EmitSteps(s.ElseChildren);
                    break;
                }

                case StepType.Loop:
                {
                    if (s.Str("mode") == "无限循环")
                        Line("while True:  # 无限循环,按 Ctrl+C 停止");
                    else
                        Line($"for _loop{LoopSeq()} in range({Math.Max(1, s.Int("count"))}):");
                    var inner = new Emitter(_sb, Indent + 1);
                    inner.EmitSteps(s.Children);
                    break;
                }

                case StepType.BreakLoop:
                    Line("break");
                    break;

                case StepType.StopScript:
                    Line("sys.exit(0)");
                    break;
            }
        }

        int _loopSeq;
        string LoopSeq() => (++_loopSeq).ToString();

        static string Q(Step s, string key) => $"'{s.Str(key)}'";

        static string D(Step s, string key)
            => s.Dbl(key).ToString(System.Globalization.CultureInfo.InvariantCulture);

        static string Tpl(Step s) => $"\"templates/{Path.GetFileName(s.Str("template")).Replace("\\", "/")}\"";

        static string Region(Step s)
        {
            var region = s.Str("region");
            if (string.IsNullOrWhiteSpace(region) || region == "full") return "None";
            var parts = region.Split(',');
            if (parts.Length == 4) return $"({parts[0].Trim()}, {parts[1].Trim()}, {parts[2].Trim()}, {parts[3].Trim()})";
            return "None";
        }

        static string JsonEncode(string text)
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(text);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    static string BuildHeader() => """
        # -*- coding: utf-8 -*-
        '''
        图色脚本编辑器 - 导出脚本
        运行前请安装依赖:  pip install opencv-python mss numpy pydirectinput
        本脚本与编辑器内逻辑一致:找图/找色 → 模拟点击/按键。
        '''

        import sys
        import time
        import random
        import ctypes

        import numpy as np
        import cv2
        import mss
        import pydirectinput

        pydirectinput.FAILSAFE = False
        pydirectinput.PAUSE = 0

        _MSS = mss.mss()


        def capture(region=None):
            '''截屏,返回 BGR 图像。region=(x, y, w, h) 相对主显示器,None 表示全屏。'''
            if region:
                mon = {"left": region[0], "top": region[1], "width": region[2], "height": region[3]}
            else:
                mon = _MSS.monitors[1]
            img = np.array(_MSS.grab(mon))
            return cv2.cvtColor(img, cv2.COLOR_BGRA2BGR)


        def find_image(path, similarity=0.85, region=None):
            '''找图,返回屏幕绝对坐标(中心点);找不到返回 None。'''
            tpl = cv2.imread(path)
            if tpl is None:
                print("[错误] 模板图不存在:", path)
                return None
            screen = capture(region)
            if screen.shape[0] < tpl.shape[0] or screen.shape[1] < tpl.shape[1]:
                return None
            result = cv2.matchTemplate(screen, tpl, cv2.TM_CCOEFF_NORMED)
            _, max_val, _, max_loc = cv2.minMaxLoc(result)
            if max_val >= similarity:
                ox, oy = (region[0], region[1]) if region else (0, 0)
                return (max_loc[0] + tpl.shape[1] // 2 + ox,
                        max_loc[1] + tpl.shape[0] // 2 + oy)
            return None


        def find_color(hex_color, tolerance=15, region=None):
            '''找色,返回屏幕绝对坐标;找不到返回 None。'''
            r, g, b = int(hex_color[1:3], 16), int(hex_color[3:5], 16), int(hex_color[5:7], 16)
            screen = capture(region)
            lower = np.array([max(0, b - tolerance), max(0, g - tolerance), max(0, r - tolerance)])
            upper = np.array([min(255, b + tolerance), min(255, g + tolerance), min(255, r + tolerance)])
            mask = cv2.inRange(screen, lower, upper)
            ys, xs = np.where(mask > 0)
            if len(xs) == 0:
                return None
            ox, oy = (region[0], region[1]) if region else (0, 0)
            return (int(xs[0]) + ox, int(ys[0]) + oy)


        def click_at(x, y, action="单击"):
            move_to(x, y)
            time.sleep(0.05)
            click_current(action)


        def click_current(action="单击"):
            if action == "双击":
                pydirectinput.click(clicks=2, interval=0.06)
            elif action == "右键":
                pydirectinput.click(button="right")
            elif action != "不点击":
                pydirectinput.click()


        def move_to(x, y):
            pydirectinput.moveTo(x, y)


        def wheel(delta):
            pydirectinput.scroll(delta)


        def press_keys(combo):
            keys = [k.strip() for k in combo.split("+") if k.strip()]
            for k in keys:
                pydirectinput.keyDown(k)
            for k in reversed(keys):
                pydirectinput.keyUp(k)


        def drag(x1, y1, x2, y2, seconds=0.5):
            move_to(x1, y1)
            time.sleep(0.05)
            pydirectinput.mouseDown()
            steps = max(8, int(seconds * 60))
            for i in range(1, steps + 1):
                t = i / steps
                move_to(int(x1 + (x2 - x1) * t), int(y1 + (y2 - y1) * t))
                time.sleep(seconds / steps)
            time.sleep(0.05)
            pydirectinput.mouseUp()


        # ---------- 中文/Unicode 文本输入( ctypes SendInput ) ----------
        PUL = ctypes.POINTER(ctypes.c_ulong)


        class _KeyBdInput(ctypes.Structure):
            _fields_ = [("wVk", ctypes.c_ushort), ("wScan", ctypes.c_ushort),
                        ("dwFlags", ctypes.c_ulong), ("time", ctypes.c_ulong),
                        ("dwExtraInfo", PUL)]


        class _HardwareInput(ctypes.Structure):
            _fields_ = [("uMsg", ctypes.c_ulong), ("wParamL", ctypes.c_short),
                        ("wParamH", ctypes.c_ubyte)]


        class _MouseInput(ctypes.Structure):
            _fields_ = [("dx", ctypes.c_long), ("dy", ctypes.c_long),
                        ("mouseData", ctypes.c_ulong), ("dwFlags", ctypes.c_ulong),
                        ("time", ctypes.c_ulong), ("dwExtraInfo", PUL)]


        class _InputI(ctypes.Union):
            _fields_ = [("ki", _KeyBdInput), ("mi", _MouseInput), ("hi", _HardwareInput)]


        class _Input(ctypes.Structure):
            _fields_ = [("type", ctypes.c_ulong), ("ii", _InputI)]


        def _send_unicode_char(ch, up=False):
            extra = ctypes.c_ulong(0)
            ii = _InputI()
            flags = 0x0002 | 0x0004 if up else 0x0004  # KEYUP | UNICODE
            ii.ki = _KeyBdInput(0, ord(ch), flags, 0, ctypes.pointer(extra))
            x = _Input(ctypes.c_ulong(1), ii)
            ctypes.windll.user32.SendInput(1, ctypes.pointer(x), ctypes.sizeof(x))


        def type_text(text, interval=0.02):
            '''Unicode 输入,支持中文(适合聊天框、输入框)。'''
            for ch in text:
                if ch == "\r":
                    continue
                if ch == "\n":
                    pydirectinput.press("enter")
                    time.sleep(interval)
                    continue
                _send_unicode_char(ch)
                _send_unicode_char(ch, up=True)
                time.sleep(interval)


        def _wait_for(fn, timeout, interval=0.5):
            '''循环执行 fn,返回首个非 None 结果;超时返回 None。'''
            deadline = time.time() + timeout
            while True:
                r = fn()
                if r is not None:
                    return r
                if time.time() >= deadline:
                    return None
                time.sleep(interval)

        """;
}
