using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TuSeEditor.App.Views;

/// <summary>内置帮助窗口:左侧目录,右侧图文说明(F1 或工具栏"帮助"打开)</summary>
public partial class HelpWindow : Window
{
    readonly (string Title, Action<FlowDocument> Build)[] _sections;

    public HelpWindow()
    {
        InitializeComponent();
        _sections = new (string, Action<FlowDocument>)[]
        {
            ("🚀 快速上手", BuildQuickStart),
            ("🪟 界面总览", BuildUi),
            ("🖼 图色识别步骤", BuildImageSteps),
            ("🖱 鼠标键盘步骤", BuildInputSteps),
            ("🔁 流程控制与其他", BuildFlowSteps),
            ("✂ 抓图 / 取色 / 区域", BuildCapture),
            ("▶ 运行与调试", BuildRun),
            ("⚙ 设置窗口详解", BuildSettings),
            ("🐍 导出 Python", BuildExport),
            ("🎮 网游适配指南", BuildGame),
            ("❓ 常见问题 FAQ", BuildFaq),
            ("📁 文件与目录", BuildFiles),
        };
        foreach (var s in _sections)
            TopicList.Items.Add(new ListBoxItem { Content = s.Title });

        TopicList.SelectedIndex = 0;
    }

    void TopicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int i = TopicList.SelectedIndex;
        if (i < 0 || i >= _sections.Length) return;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 13.5,
            LineHeight = double.NaN,
        };
        _sections[i].Build(doc);
        Doc.Document = doc;
    }

    // ---------------- 排版辅助 ----------------
    static Paragraph Para(string text, double topMargin = 4, double bottom = 6, bool bold = false, double size = 13.5, Brush? color = null)
    {
        var p = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, topMargin, 0, bottom),
            FontSize = size,
        };
        if (bold) p.FontWeight = FontWeights.Bold;
        if (color != null) p.Foreground = color;
        return p;
    }

    static void P(FlowDocument d, string text, double topMargin = 4, double bottom = 6, bool bold = false, double size = 13.5, Brush? color = null)
        => d.Blocks.Add(Para(text, topMargin, bottom, bold, size, color));

    static void H(FlowDocument d, string text, double size = 17, Brush? color = null)
        => d.Blocks.Add(Para(text, 14, 6, true, size, color ?? new SolidColorBrush(Color.FromRgb(0x1B, 0x6D, 0xC8))));

    static void H3(FlowDocument d, string text)
        => d.Blocks.Add(Para(text, 10, 4, true, 14.5, new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))));

    static void B(FlowDocument d, string text)
    {
        var p = Para(text, 1, 1);
        p.TextIndent = 14;
        d.Blocks.Add(p);
    }

    static void Note(FlowDocument d, string text)
        => d.Blocks.Add(Para(text, 6, 8, false, 12.5, new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E))));

    static void Table(FlowDocument d, string[] headers, string[][] rows)
    {
        var t = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 10) };
        int cols = headers.Length;
        for (int i = 0; i < cols; i++)
        {
            bool narrow = cols == 2 && i == 0;
            t.Columns.Add(new TableColumn { Width = new GridLength(narrow ? 0.28 : 0.72, GridUnitType.Star) });
        }
        var hg = new TableRowGroup();
        var hr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xEC, 0xFF)) };
        foreach (var h in headers)
            hr.Cells.Add(new TableCell(new Paragraph(new Run(h)) { Margin = new Thickness(0), FontWeight = FontWeights.Bold }) { Padding = new Thickness(7, 4, 7, 4) });
        hg.Rows.Add(hr);
        t.RowGroups.Add(hg);
        var bg = new TableRowGroup();
        foreach (var r in rows)
        {
            var tr = new TableRow();
            foreach (var c in r)
                tr.Cells.Add(new TableCell(new Paragraph(new Run(c)) { Margin = new Thickness(0) }) { Padding = new Thickness(7, 4, 7, 4) });
            bg.Rows.Add(tr);
        }
        t.RowGroups.Add(bg);
        d.Blocks.Add(t);
    }

    static void Code(FlowDocument d, string text)
        => d.Blocks.Add(new Paragraph(new Run(text))
        {
            Margin = new Thickness(10, 4, 0, 10),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x51, 0x23)),
        });

    // ---------------- 各节内容 ----------------
    void BuildQuickStart(FlowDocument d)
    {
        H(d, "快速上手(5 分钟做出第一个脚本)");
        P(d, "目标:屏幕上出现『开始』按钮就自动点击。");
        H3(d, "第 1 步 · 添加步骤");
        B(d, "左侧工具箱「图色识别」分类,双击 🖼 找图点击(或点行尾的 ＋ 按钮);");
        B(d, "中间步骤树出现“找图点击 (未选模板) 相似度0.85”。");
        H3(d, "第 2 步 · 截取模板图");
        B(d, "选中步骤 → 右侧点「🖼 抓模板图」(未保存过脚本会先提示保存,保存到任意文件夹即可);");
        B(d, "屏幕变暗,按住左键拖框套住目标(如游戏里的“开始”按钮),松开完成;");
        B(d, "按 Esc / 右键 / 右上角 ✕ 可取消;移动鼠标时放大镜显示坐标与颜色;");
        B(d, "模板自动保存到工程 templates/ 文件夹,面板出现缩略图。");
        H3(d, "第 3 步 · 验证");
        B(d, "点「🔍 测试找图」:找到时日志显示 ✔ 并在屏幕上闪红框;找不到按 FAQ 调整。");
        H3(d, "第 4 步 · 运行");
        B(d, "按 F9(或点 ▶ 运行)开始;F10 / ■ 停止随时停下;日志实时输出每一步。");
        H3(d, "第 5 步 · 完善");
        B(d, "后面加一个 ⏱ 延时(方式“随机”,1~2 秒)更自然;");
        B(d, "外面包一层 🔁 循环实现持续挂机;💾 保存脚本,下次 📂 打开继续。");
        Note(d, "提示:禁用某一步但不删除,直接取消该行勾选框即可。");
    }

    void BuildUi(FlowDocument d)
    {
        H(d, "界面总览");
        Table(d, new[] { "区域", "作用" }, new[]
        {
            new[] { "工具栏", "新建 / 打开 / 保存脚本;运行(F9) / 停止(F10);导出 Python;设置" },
            new[] { "命令工具箱", "17 种积木按「图色识别 / 鼠标键盘 / 流程控制 / 其他」分组;双击或点“＋”添加" },
            new[] { "脚本步骤树", "拖动排序;行首勾选框启用/禁用;右键复制、删除、上移、下移;运行时当前步骤橙色高亮" },
            new[] { "步骤属性面板", "显示选中步骤的全部参数,修改即时生效;含抓模板图/取色/测试找图等按钮" },
            new[] { "运行日志", "带时间戳实时输出每步结果,如 [14:30:05] ✔ 找到 (720,450) 相似度0.97" },
            new[] { "状态栏", "左侧运行状态;右侧当前工程文件路径" },
        });
        H3(d, "添加步骤会加到哪里?");
        B(d, "选中了循环/条件步骤 → 加入它的内部(循环体或“满足条件时”分支);");
        B(d, "选中了普通步骤 → 插到它后面;");
        B(d, "没选中 → 追加到脚本末尾。");
        H3(d, "条件步骤的两个分支");
        B(d, "「判断图/色存在」在树里展开为“✔ 满足条件时”和“✖ 否则”两组,把步骤拖进对应分支即可。");
    }

    void BuildImageSteps(FlowDocument d)
    {
        H(d, "图色识别步骤");
        H3(d, "找图点击 ⭐ 最常用");
        P(d, "在屏幕上找模板图,找到后移过去点击。超时前每隔“检测间隔”找一次,找到立即继续;超时按“超时后”策略处理。");
        Table(d, new[] { "参数", "说明" }, new[]
        {
            new[] { "模板图", "要找的目标截图;抓模板图截取或选择图片导入" },
            new[] { "相似度", "0.5~1.0,默认 0.85。找不到→调低(0.8/0.75);误匹配→调高(0.9/0.95)" },
            new[] { "搜索区域", "默认全屏;强烈建议框选小区域,速度快 10 倍且防误匹配" },
            new[] { "点击方式", "单击 / 双击 / 右键 / 不点击(只找不点)" },
            new[] { "X偏移 / Y偏移", "相对目标中心点的偏移像素" },
            new[] { "等待超时(秒)", "最等多久,默认 5 秒" },
            new[] { "检测间隔(秒)", "两次找图的间隔,默认 0.5 秒" },
            new[] { "超时后", "继续执行(跳过)或 停止脚本" },
        });
        Note(d, "调优:模板只截目标本身;目标有动画就截静止画面;分辨率/画质变了要重抓;纯色块不能当模板(会提示),改用找色。");
        H3(d, "找色点击");
        P(d, "找指定颜色的像素并点击。适合纯色按钮、进度条、血条、技能图标底色等颜色固定但形状会变的目标——比找图更快更稳。");
        Table(d, new[] { "参数", "说明" }, new[]
        {
            new[] { "颜色", "点「取色」从屏幕吸取,格式 #RRGGBB" },
            new[] { "容差", "每通道允许偏差 0~100,默认 15;画面有压缩噪点就调大到 20~30" },
        });
        H3(d, "等待图出现 / 等待图消失");
        P(d, "反复检测直到模板出现(或消失),超时按策略处理。典型:等加载界面消失、等“领取奖励”亮起。");
        H3(d, "判断图存在 / 判断色存在");
        P(d, "立即检测一次,满足走“✔ 满足条件时”分支,否则走“✖ 否则”分支,可实现“有怪打怪、没怪挂机”逻辑。");
    }

    void BuildInputSteps(FlowDocument d)
    {
        H(d, "鼠标键盘步骤");
        Table(d, new[] { "步骤", "说明" }, new[]
        {
            new[] { "鼠标点击", "在指定坐标点击;X/Y = -1 表示当前位置;单击/双击/右键" },
            new[] { "鼠标移动", "移动到指定坐标(屏幕绝对像素,主显示器左上角为 0,0)" },
            new[] { "滚轮", "正数向上、负数向下,120 约为一格" },
            new[] { "拖拽", "按住左键从起点平滑拖到终点,可设耗时" },
            new[] { "按键", "单键或组合键,可设重复次数与间隔" },
            new[] { "输入文本", "Unicode 逐字输入,支持中文;适用于聊天框/输入框等标准控件" },
        });
        H3(d, "按键写法速查");
        Code(d, "a  5  F5  space  enter  esc  tab\nup  down  left  right\nhome  end  pageup  pagedown  delete  insert\nshift  ctrl  alt  win\nctrl+s        ← 组合键用加号连接\nnumpad0 ~ numpad9");
        Note(d, "游戏内 DirectInput 不支持中文 Unicode 输入,游戏里打字请用「按键」模拟英文数字。");
    }

    void BuildFlowSteps(FlowDocument d)
    {
        H(d, "流程控制与其他");
        Table(d, new[] { "步骤", "说明" }, new[]
        {
            new[] { "⏱ 延时", "固定:睡指定秒数;随机:在[秒数,上限]内随机——推荐随机,行为更自然" },
            new[] { "🔁 循环", "固定次数 / 无限循环;把子步骤拖进循环体;无限循环用 F10 停止" },
            new[] { "⏏ 跳出循环", "立即结束最近一层循环,继续执行循环后面的步骤" },
            new[] { "💬 注释", "只写备注,运行时打印到日志" },
            new[] { "🛑 停止脚本", "立即结束整个脚本" },
        });
        H3(d, "常用组合");
        B(d, "无限循环 + 判断图存在(否则跳出循环)= 挂机到目标出现;");
        B(d, "找图点击(超时后=继续执行)在循环里 = 反复等按钮出现就点;");
        B(d, "关键步骤(如进副本)设“超时后=停止脚本”,防止超时后瞎点。");
    }

    void BuildCapture(FlowDocument d)
    {
        H(d, "抓模板图 / 取色 / 框选区域");
        H3(d, "🖼 抓模板图");
        B(d, "属性面板点「抓模板图」(未保存工程会先让你保存脚本);");
        B(d, "屏幕变暗,按住左键拖框,松开完成;Esc / 右键 / ✕ 取消;");
        B(d, "放大镜实时显示坐标(屏幕绝对像素)、RGB、#RRGGBB;");
        B(d, "模板自动存到工程 templates/ 文件夹并填入参数。");
        P(d, "好模板的标准:小而独特——只截按钮/图标本身,包含文字或边框等细节,不要带大片背景。", 2, 8, true);
        H3(d, "🎨 取色");
        P(d, "点「取色」→ 移动鼠标看放大镜 → 单击取色。找色容差建议 10~30。");
        H3(d, "⬚ 框选区域");
        P(d, "点「框选区域」拖出矩形,或手填 x,y,w,h;点「全屏」恢复。限制区域 = 速度提升 10 倍以上 + 避免屏幕其他相似内容被误点。");
    }

    void BuildRun(FlowDocument d)
    {
        H(d, "运行与调试");
        Table(d, new[] { "功能", "说明" }, new[]
        {
            new[] { "F9 / F10", "全局热键:运行 / 停止,游戏全屏时也有效" },
            new[] { "步骤高亮", "运行中当前步骤橙色显示,一眼看出卡在哪" },
            new[] { "日志", "每步都记录:坐标、相似度、延时等;出问题先看最后一行" },
            new[] { "失败策略", "图色步骤超时后可选“继续执行”(挂机常用)或“停止脚本”(关键步骤)" },
            new[] { "整体循环", "设置里可让整个脚本跑 N 轮,0 = 无限" },
        });
    }

    void BuildSettings(FlowDocument d)
    {
        H(d, "设置窗口详解");
        Table(d, new[] { "设置项", "说明" }, new[]
        {
            new[] { "抓图引擎", "自动(推荐):DXGI 优先,失败/黑屏自动转 GDI;仅 DXGI:强制桌面复制,适合 DirectX 网游;仅 GDI:最保守,独占全屏游戏可能黑屏" },
            new[] { "键盘扫描码模式", "开(默认):扫描码发送,兼容 DirectInput 类游戏;关:虚拟键码。一般保持开启" },
            new[] { "脚本整体循环次数", "整个脚本跑几轮,0 = 无限循环" },
            new[] { "热键", "当前版本固定 F9 = 运行,F10 = 停止" },
        });
        Note(d, "日志会写明实际使用的抓图引擎及降级原因,排查黑屏问题先看日志。");
    }

    void BuildExport(FlowDocument d)
    {
        H(d, "导出 Python 脚本");
        P(d, "点工具栏「🐍 导出 Python」,选 .py 文件名即可。导出内容包括:");
        B(d, "script.py:积木编译成的带缩进可读 Python(内嵌找图/找色/点击引擎);");
        B(d, "templates/:所有模板图(必须与 script.py 保持相对位置);");
        B(d, "运行说明.txt。");
        H3(d, "运行导出的脚本");
        Code(d, "pip install opencv-python mss numpy pydirectinput\npython script.py");
        B(d, "停止:命令行按 Ctrl+C;");
        B(d, "目标机器无需安装本编辑器,只需 Python 3.8+;");
        B(d, "导出脚本同样是前台模拟输入,风险与编辑器内运行相同。");
    }

    void BuildGame(FlowDocument d)
    {
        H(d, "网游适配指南");
        Table(d, new[] { "环节", "方案" }, new[]
        {
            new[] { "抓图", "DXGI 桌面复制可抓 DX9~DX12 游戏的窗口化/无边框/全屏画面;失败或全黑自动降级 GDI" },
            new[] { "输入", "SendInput 硬件级模拟;键盘默认扫描码模式,兼容 DirectInput 游戏" },
            new[] { "坐标", "全程物理像素 + PerMonitorV2 DPI 感知,高 DPI 屏不偏移" },
        });
        P(d, "建议把游戏设为窗口化或无边框窗口,抓图兼容性最好。", 2, 8, true);
        Note(d, "重要边界:① 前台自动化,不支持后台多开;② 不做内核驱动注入、不做反反作弊规避;③ 强反作弊(ACE/TP/EAC/BattlEye)可能拦截输入或屏蔽截图——表现是“点了没反应”“截图全黑”;④ 网游脚本通常违反用户协议,有封号风险,自行评估。");
    }

    void BuildFaq(FlowDocument d)
    {
        H(d, "常见问题 FAQ");
        H3(d, "Q1 测试找图总是失败?");
        B(d, "相似度调低到 0.8 试试;重新抓一个更小更独特的模板(不带大片背景);");
        B(d, "检查分辨率/画质(亮度、特效、HDR)是否与截模板时一致;");
        B(d, "用「框选区域」缩小搜索范围;纯色目标改用「找色点击」。");
        H3(d, "Q2 截图全黑?");
        B(d, "设置→抓图引擎改「仅 GDI」;游戏改窗口化/无边框;");
        B(d, "确认没有 OBS 等软件占用桌面复制;日志会写明当前引擎与降级原因。");
        H3(d, "Q3 找到也点了,游戏没反应?");
        B(d, "反作弊拦截模拟输入(见网游适配);");
        B(d, "目标窗口是管理员权限 → 以管理员身份运行编辑器;");
        B(d, "先加一个「鼠标点击」点游戏窗口任意处获取焦点。");
        H3(d, "Q4 中文输入不进去?");
        B(d, "输入文本走 Unicode,仅标准输入框有效;游戏内请用「按键」输英文数字。");
        H3(d, "Q5 鼠标位置不对?");
        B(d, "坐标是屏幕绝对像素(主显示器左上角 0,0);重抓模板即可自动对齐,不要手抄其他设备的坐标。");
        H3(d, "Q6 F9 没反应?");
        B(d, "热键被其他软件占用(输入法/录屏/游戏),关掉冲突软件后重启编辑器。");
        H3(d, "Q7 CPU 高/卡?");
        B(d, "缩小搜索区域;加大检测间隔;能用找色就别找图。");
        H3(d, "Q8 多显示器?");
        B(d, "抓图覆盖主显示器;找图/找色覆盖整个虚拟桌面;建议游戏放主显示器。");
    }

    void BuildFiles(FlowDocument d)
    {
        H(d, "文件与目录说明");
        Table(d, new[] { "路径", "内容" }, new[]
        {
            new[] { "你的脚本.tsproj", "脚本工程(JSON),含全部步骤与参数,可文本编辑/版本管理" },
            new[] { "templates/(工程同目录)", "所有抓取的模板图" },
            new[] { "%APPDATA%\\TuSeEditor\\settings.json", "编辑器全局设置" },
            new[] { "导出目录/script.py + templates/", "导出的 Python 脚本及模板" },
        });
        P(d, "免责声明:本工具仅供学习与个人自动化使用,请遵守目标软件的用户协议与当地法律法规,使用风险自负。", 12, 4, true, 12.5, new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)));
    }
}
