using System.Runtime.InteropServices;

namespace TuSeEditor.App.Services;

/// <summary>
/// 输入服务:SendInput 模拟真实硬件输入。
/// 键盘默认走扫描码模式(兼容 DirectInput 类网游),鼠标用绝对坐标。
/// </summary>
public static class InputService
{
    /// <summary>true=键盘用扫描码发送(游戏兼容性好);false=虚拟键码</summary>
    public static bool ScanCodeMode { get; set; } = true;

    // ---------- 键名 → VK ----------
    static readonly Dictionary<string, uint> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["space"] = 0x20, ["enter"] = 0x0D, ["return"] = 0x0D, ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["tab"] = 0x09, ["backspace"] = 0x08, ["delete"] = 0x2E, ["del"] = 0x2E,
        ["insert"] = 0x2D, ["ins"] = 0x2D, ["home"] = 0x24, ["end"] = 0x23,
        ["pageup"] = 0x21, ["pgup"] = 0x21, ["pagedown"] = 0x22, ["pgdn"] = 0x22,
        ["up"] = 0x26, ["down"] = 0x28, ["left"] = 0x25, ["right"] = 0x27,
        ["shift"] = 0x10, ["ctrl"] = 0x11, ["control"] = 0x11, ["alt"] = 0x12, ["win"] = 0x5B,
        ["capslock"] = 0x14, ["numlock"] = 0x90, ["scrolllock"] = 0x91,
        ["printscreen"] = 0x2C,
    };

    static readonly HashSet<uint> ExtendedKeys = new() { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E };

    static uint ResolveVk(string name)
    {
        name = name.Trim();
        if (KeyMap.TryGetValue(name, out var vk)) return vk;
        if (name.Length == 1)
        {
            char c = char.ToUpperInvariant(name[0]);
            if (c >= '0' && c <= '9') return (uint)c;
            if (c >= 'A' && c <= 'Z') return (uint)c;
        }
        if (name.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name[1..], out var f) && f >= 1 && f <= 24)
            return (uint)(0x70 + f - 1);
        if ((name.StartsWith("num", StringComparison.OrdinalIgnoreCase) || name.StartsWith("numpad", StringComparison.OrdinalIgnoreCase)) &&
            int.TryParse(name.Replace("numpad", "").Replace("num", ""), out var n) && n >= 0 && n <= 9)
            return (uint)(0x60 + n);
        if (name.StartsWith("vk", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(name[2..], System.Globalization.NumberStyles.HexNumber, null, out var custom))
            return custom;
        throw new ArgumentException($"无法识别的按键名:{name}");
    }

    static void Send(NativeMethods.INPUT[] inputs)
    {
        if (NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>()) == 0)
            throw new InvalidOperationException("SendInput 被拒绝(可能被安全软件或反作弊拦截)");
    }

    static NativeMethods.INPUT KeyInput(uint vk, bool down)
    {
        var input = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
        var flags = down ? 0u : NativeMethods.KEYEVENTF_KEYUP;
        if (ScanCodeMode)
        {
            var scan = NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
            input.u.ki.wScan = (ushort)scan;
            input.u.ki.dwFlags = flags | NativeMethods.KEYEVENTF_SCANCODE;
            if (ExtendedKeys.Contains(vk)) input.u.ki.dwFlags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }
        else
        {
            input.u.ki.wVk = (ushort)vk;
            input.u.ki.dwFlags = flags;
        }
        return input;
    }

    // ---------- 鼠标 ----------
    public static void MoveTo(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
            throw new InvalidOperationException("SetCursorPos 失败");
    }

    public static (int X, int Y) CurrentPos()
    {
        NativeMethods.GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    /// <summary>action:单击 / 双击 / 右键 / 中键</summary>
    public static void Click(string action)
    {
        switch (action)
        {
            case "双击":
                SendClick(true); System.Threading.Thread.Sleep(30); SendClick(true);
                break;
            case "右键":
                SendClick(false, right: true);
                break;
            case "中键":
                SendClick(false, middle: true);
                break;
            default:
                SendClick(true);
                break;
        }
    }

    static void SendClick(bool left, bool right = false, bool middle = false)
    {
        uint down = right ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : middle ? NativeMethods.MOUSEEVENTF_MIDDLEDOWN : NativeMethods.MOUSEEVENTF_LEFTDOWN;
        uint up = right ? NativeMethods.MOUSEEVENTF_RIGHTUP : middle ? NativeMethods.MOUSEEVENTF_MIDDLEUP : NativeMethods.MOUSEEVENTF_LEFTUP;
        Send(new[]
        {
            new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new NativeMethods.INPUTUNION { mi = new NativeMethods.MOUSEINPUT { dwFlags = down } } },
            new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new NativeMethods.INPUTUNION { mi = new NativeMethods.MOUSEINPUT { dwFlags = up } } },
        });
    }

    public static void Wheel(int delta)
    {
        Send(new[]
        {
            new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new NativeMethods.INPUTUNION { mi = new NativeMethods.MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_WHEEL, mouseData = unchecked((uint)delta) } } },
        });
    }

    public static void Drag(int fromX, int fromY, int toX, int toY, double seconds)
    {
        MoveTo(fromX, fromY);
        Thread.Sleep(60);
        SendClickDown();
        try
        {
            int steps = Math.Max(8, (int)(seconds * 120));
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                MoveTo(fromX + (int)((toX - fromX) * t), fromY + (int)((toY - fromY) * t));
                Thread.Sleep(Math.Max(1, (int)(seconds * 1000 / steps)));
            }
            Thread.Sleep(60);
        }
        finally
        {
            SendClickUp();
        }
    }

    static void SendClickDown()
    {
        Send(new[] { new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new NativeMethods.INPUTUNION { mi = new NativeMethods.MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTDOWN } } } });
    }

    static void SendClickUp()
    {
        Send(new[] { new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new NativeMethods.INPUTUNION { mi = new NativeMethods.MOUSEINPUT { dwFlags = NativeMethods.MOUSEEVENTF_LEFTUP } } } });
    }

    // ---------- 键盘 ----------
    /// <summary>combo 如 "ctrl+s"、"a"、"F5"、"ctrl+shift+esc"</summary>
    public static void PressKeys(string combo)
    {
        var keys = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keys.Length == 0) throw new ArgumentException("按键为空");
        var vks = keys.Select(ResolveVk).ToArray();
        foreach (var vk in vks) Send(new[] { KeyInput(vk, true) });
        foreach (var vk in vks.Reverse()) Send(new[] { KeyInput(vk, false) });
    }

    /// <summary>Unicode 文本输入,支持中文(编辑框场景;DirectInput 游戏不适用)</summary>
    public static void TypeText(string text, double intervalSec)
    {
        foreach (var ch in text)
        {
            if (ch == '\r') continue;
            SendChar(ch);
            if (intervalSec > 0) Thread.Sleep((int)(intervalSec * 1000));
        }
    }

    static void SendChar(char ch)
    {
        if (ch == '\n')
        {
            PressKeys("enter");
            return;
        }
        var down = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
        down.u.ki.wScan = ch;
        down.u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE;
        var up = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD };
        up.u.ki.wScan = ch;
        up.u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP;
        Send(new[] { down, up });
    }
}
