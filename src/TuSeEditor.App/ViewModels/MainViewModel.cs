using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using TuSeEditor.App.Models;
using TuSeEditor.App.Services;
using TuSeEditor.App.Views;

namespace TuSeEditor.App.ViewModels;

public class ToolboxItemVM
{
    public StepCatalog.StepInfo Info { get; }
    public string Display => $"{Info.Icon}  {Info.Name}";
    public string Category => Info.Category;
    public ToolboxItemVM(StepCatalog.StepInfo info) => Info = info;
}

/// <summary>主窗口 ViewModel</summary>
public partial class MainViewModel : ObservableObject
{
    ProjectDoc _project = new();
    string? _projectPath;
    AppSettings _settings = AppSettings.Load();

    public CaptureService Capture { get; } = new();
    readonly EngineService _engine = new();
    public HotkeyService Hotkey { get; } = new();

    public ObservableCollection<StepNodeVM> Steps { get; } = new();
    public ObservableCollection<ToolboxItemVM> Toolbox { get; } = new();

    [ObservableProperty]
    private object? _selectedItem; // StepNodeVM 或 BranchVM

    [ObservableProperty]
    private StepNodeVM? _selectedNode;

    [ObservableProperty]
    private BranchVM? _selectedBranch;

    [ObservableProperty]
    private ObservableCollection<PropertyRowVM> _propertyRows = new();

    [ObservableProperty]
    private string _logText = "欢迎使用图色脚本编辑器。左侧双击添加步骤,右侧配置参数。\r\n";

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _title = "图色脚本编辑器 - 未命名脚本";

    [ObservableProperty]
    private bool _isRunning;

    /// <summary>状态栏显示的工程路径</summary>
    public string ProjectPathDisplay =>
        IsRunning ? "● 运行中" :
        string.IsNullOrEmpty(_projectPath) ? "未保存" : _projectPath;

    /// <summary>标题变化后通知窗口(由 VM 内部触发)</summary>
    public event Action? UpdateTitleExternal;

    public RelayCommand NewProjectCmd { get; }
    public RelayCommand OpenProjectCmd { get; }
    public RelayCommand SaveProjectCmd { get; }
    public RelayCommand RunScriptCmd { get; }
    public RelayCommand StopScriptCmd { get; }
    public RelayCommand ExportPythonCmd { get; }
    public RelayCommand ShowSettingsCmd { get; }
    public RelayCommand AddStepCmd { get; }
    public RelayCommand DeleteStepCmd { get; }
    public RelayCommand CopyStepCmd { get; }
    public RelayCommand MoveUpCmd { get; }
    public RelayCommand MoveDownCmd { get; }

    public MainViewModel()
    {
        foreach (var info in StepCatalog.All)
            Toolbox.Add(new ToolboxItemVM(info));

        Capture.Log = m => Ui(() => AppendLog(m));
        Capture.Mode = _settings.CaptureEngine?.ToLowerInvariant() switch
        {
            "dxgi" => EngineKind.Dxgi,
            "gdi" => EngineKind.Gdi,
            _ => EngineKind.Auto,
        };

        WireEngine();

        NewProjectCmd = new RelayCommand(_ => NewProject());
        OpenProjectCmd = new RelayCommand(_ => OpenProject());
        SaveProjectCmd = new RelayCommand(_ => SaveProject());
        RunScriptCmd = new RelayCommand(_ => RunScript(), _ => !IsRunning && _project.Steps.Count > 0);
        StopScriptCmd = new RelayCommand(_ => _engine.Stop(), _ => IsRunning);
        ExportPythonCmd = new RelayCommand(_ => ExportPython(), _ => _project.Steps.Count > 0);
        ShowSettingsCmd = new RelayCommand(_ => ShowSettings());
        AddStepCmd = new RelayCommand(p => { if (p is ToolboxItemVM t) AddStep(t); });
        DeleteStepCmd = new RelayCommand(_ => { if (SelectedNode != null) DeleteStep(SelectedNode); });
        CopyStepCmd = new RelayCommand(_ => { if (SelectedNode != null) CopyStep(SelectedNode); });
        MoveUpCmd = new RelayCommand(_ => { if (SelectedNode != null) MoveStep(SelectedNode, -1); });
        MoveDownCmd = new RelayCommand(_ => { if (SelectedNode != null) MoveStep(SelectedNode, 1); });

        RebuildTree();
    }

    void Ui(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) a();
        else d.BeginInvoke(a);
    }

    void WireEngine()
    {
        _engine.Log += m => Ui(() => AppendLog(m));
        _engine.CurrentStepChanged += id => Ui(() => HighlightStep(id));
        _engine.RunStateChanged += running => Ui(() =>
        {
            IsRunning = running;
            StatusText = running ? "● 运行中(F10 停止)" : "就绪";
            if (!running) HighlightStep(null);
        });
    }

    void HighlightStep(string? id)
    {
        foreach (var n in EnumerateNodes())
            n.IsRunning = id != null && n.Model.Id == id;
    }

    // ---------------- 日志 ----------------
    readonly object _logLock = new();

    public void AppendLog(string msg)
    {
        lock (_logLock)
        {
            LogText += msg + "\r\n";
            if (LogText.Length > 65536)
                LogText = LogText[^32768..];
        }
        OnPropertyChanged(nameof(LogText));
    }

    // ---------------- 步骤树构建 ----------------
    public StepNodeVM WrapNode(Step model, StepNodeVM? parent)
    {
        var vm = new StepNodeVM(model) { Parent = parent, IsEnabled = model.Enabled };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StepNodeVM.IsEnabled))
            {
                model.Enabled = vm.IsEnabled;
            }
        };
        vm.RebuildChildren(this);
        return vm;
    }

    void RebuildTree()
    {
        Steps.Clear();
        foreach (var s in _project.Steps)
            Steps.Add(WrapNode(s, null));
    }

    public IEnumerable<StepNodeVM> EnumerateNodes()
    {
        IEnumerable<StepNodeVM> Walk(ObservableCollection<object>? items)
        {
            if (items == null) yield break;
            foreach (var item in items)
            {
                if (item is StepNodeVM n)
                {
                    yield return n;
                    foreach (var c in Walk(n.NodeItems)) yield return c;
                }
                else if (item is BranchVM b)
                {
                    foreach (var c in Walk(b.Items)) yield return c;
                }
            }
        }
        foreach (var n in Steps)
        {
            yield return n;
            foreach (var c in Walk(n.NodeItems)) yield return c;
        }
    }

    public StepNodeVM? FindNode(string id) => EnumerateNodes().FirstOrDefault(n => n.Model.Id == id);

    partial void OnSelectedItemChanged(object? value)
    {
        SelectedNode = value as StepNodeVM;
        SelectedBranch = value as BranchVM;
        BuildPropertyRows();
    }

    // ---------------- 属性面板 ----------------
    void BuildPropertyRows()
    {
        PropertyRows = new ObservableCollection<PropertyRowVM>();
        var node = SelectedNode;
        if (node == null) return;
        var info = StepCatalog.Get(node.Model.Type);
        if (info == null) return;

        foreach (var def in info.Params)
        {
            PropertyRowVM row = def.Kind switch
            {
                ParamKind.Template => new TemplateRowVM(),
                ParamKind.Region => new RegionRowVM(),
                ParamKind.Color => new ColorRowVM(),
                ParamKind.MultiText => new MultiTextRowVM(),
                ParamKind.Combo => new ComboRowVM(),
                ParamKind.Check => new CheckRowVM(),
                ParamKind.Keys => new KeysRowVM(),
                ParamKind.Int or ParamKind.Double => new NumberRowVM(),
                _ => new TextRowVM(),
            };
            row.Def = def;
            row.Value = node.Model.Str(def.Key);
            row.ParamsGetter = () => node.Model.Params;
            row.Changed = () => OnRowChanged(node);
            switch (row)
            {
                case TemplateRowVM t:
                    t.Capture = r => CaptureTemplate(r);
                    t.Browse = r => BrowseTemplate(r);
                    t.Test = r => TestFind(r);
                    break;
                case ColorRowVM c:
                    c.PickColor = r => PickColor(r);
                    c.TestColor = r => TestColor(r);
                    break;
                case RegionRowVM rg:
                    rg.PickRegion = r => PickRegion(r);
                    break;
            }
            row.RefreshVisible();
            PropertyRows.Add(row);
        }
    }

    void OnRowChanged(StepNodeVM node)
    {
        foreach (var r in PropertyRows)
        {
            r.WriteTo(node.Model);
            r.RefreshVisible();
        }
        node.RefreshTitle();
    }

    // ---------------- 树操作 ----------------
    List<Step>? ModelListOf(StepNodeVM node)
    {
        if (node.Parent == null) return _project.Steps;
        var p = node.Parent.Model;
        foreach (var list in new[] { p.Children, p.ElseChildren })
            if (list.Any(s => s.Id == node.Model.Id)) return list;
        return p.Children;
    }

    /// <summary>工具箱双击添加步骤</summary>
    public void AddStep(ToolboxItemVM item)
    {
        var step = StepCatalog.Create(item.Info.Type);
        var node = WrapNode(step, null);

        if (SelectedBranch != null)
        {
            // 添加进选中分支
            var owner = SelectedBranch.Owner;
            var list = SelectedBranch.IsElse ? owner.Model.ElseChildren : owner.Model.Children;
            list.Add(step);
            SelectedBranch.Items.Add(node);
            node.Parent = owner;
            AppendLog($"已添加步骤:{item.Info.Name}(到分支 {SelectedBranch.Label})");
        }
        else if (SelectedNode != null)
        {
            var sel = SelectedNode;
            var selInfo = StepCatalog.Get(sel.Model.Type);
            if (selInfo is { HasChildren: true, HasElse: false })
            {
                // 选中循环:加入循环体
                sel.Model.Children.Add(step);
                node.Parent = sel;
                sel.RebuildChildren(this);
                AppendLog($"已添加步骤:{item.Info.Name}(到循环体)");
            }
            else if (selInfo is { HasElse: true })
            {
                sel.Model.Children.Add(step);
                node.Parent = sel;
                sel.RebuildChildren(this);
                AppendLog($"已添加步骤:{item.Info.Name}(到满足分支)");
            }
            else
            {
                var list = ModelListOf(sel);
                var idx = list!.FindIndex(s => s.Id == sel.Model.Id);
                list.Insert(idx + 1, step);
                node.Parent = sel.Parent;
                RebuildTree();
                AppendLog($"已添加步骤:{item.Info.Name}");
            }
        }
        else
        {
            _project.Steps.Add(step);
            node.Parent = null;
            Steps.Add(node);
            AppendLog($"已添加步骤:{item.Info.Name}");
        }
    }

    public void DeleteStep(StepNodeVM node)
    {
        var list = ModelListOf(node);
        list?.RemoveAll(s => s.Id == node.Model.Id);
        RebuildTree();
        AppendLog($"已删除步骤:{node.TypeName}");
    }

    public void CopyStep(StepNodeVM node)
    {
        var clone = node.Model.DeepClone();
        var list = ModelListOf(node);
        var idx = list!.FindIndex(s => s.Id == node.Model.Id);
        list.Insert(idx + 1, clone);
        RebuildTree();
        AppendLog($"已复制步骤:{node.TypeName}");
    }

    public void MoveStep(StepNodeVM node, int dir)
    {
        var list = ModelListOf(node);
        var idx = list!.FindIndex(s => s.Id == node.Model.Id);
        int target = idx + dir;
        if (target < 0 || target >= list.Count) return;
        (list[idx], list[target]) = (list[target], list[idx]);
        RebuildTree();
    }

    /// <summary>拖拽移动:target 为 StepNodeVM 时放到其后面;为 BranchVM 时放入分支末尾</summary>
    public void DropStep(StepNodeVM dragged, object target)
    {
        // 禁止拖到自身子树内
        for (var p = target as StepNodeVM; p != null; p = p.Parent)
            if (p.Model.Id == dragged.Model.Id) return;
        if (target is StepNodeVM tn && tn.Model.Id == dragged.Model.Id) return;

        var list = ModelListOf(dragged);
        list?.RemoveAll(s => s.Id == dragged.Model.Id);

        if (target is BranchVM branch)
        {
            var owner = branch.Owner;
            var blist = branch.IsElse ? owner.Model.ElseChildren : owner.Model.Children;
            blist.Add(dragged.Model);
            dragged.Parent = owner;
        }
        else if (target is StepNodeVM t)
        {
            var tlist = ModelListOf(t)!;
            var idx = tlist.FindIndex(s => s.Id == t.Model.Id);
            tlist.Insert(idx + 1, dragged.Model);
            dragged.Parent = t.Parent;
        }
        RebuildTree();
    }

    // ---------------- 工程文件 ----------------
    void UpdateTitle()
    {
        Title = $"图色脚本编辑器 - {(string.IsNullOrEmpty(_projectPath) ? "未命名脚本" : Path.GetFileName(_projectPath))}{(IsRunning ? "  [运行中]" : "")}";
        OnPropertyChanged(nameof(ProjectPathDisplay));
        UpdateTitleExternal?.Invoke();
    }

    void NewProject()
    {
        if (IsRunning) { AppendLog("请先停止脚本"); return; }
        _project = new ProjectDoc();
        _projectPath = null;
        RebuildTree();
        UpdateTitle();
        AppendLog("已新建脚本");
    }

    void OpenProject()
    {
        if (IsRunning) { AppendLog("请先停止脚本"); return; }
        var dlg = new OpenFileDialog { Filter = "图色脚本 (*.tsproj)|*.tsproj|所有文件|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _project = ProjectService.Load(dlg.FileName);
            _projectPath = dlg.FileName;
            RebuildTree();
            UpdateTitle();
            AppendLog($"已打开脚本:{dlg.FileName}({CountSteps()} 个步骤)");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开失败:{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    int CountSteps()
    {
        int n = 0;
        void Walk(List<Step> list) { foreach (var s in list) { n++; Walk(s.Children); Walk(s.ElseChildren); } }
        Walk(_project.Steps);
        return n;
    }

    bool SaveProject()
    {
        if (_projectPath == null)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "图色脚本 (*.tsproj)|*.tsproj",
                FileName = "我的脚本.tsproj",
            };
            if (dlg.ShowDialog() != true) return false;
            _projectPath = dlg.FileName;
        }
        try
        {
            ProjectService.Save(_project, _projectPath);
            UpdateTitle();
            AppendLog($"已保存:{_projectPath}");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败:{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>确保工程已保存(抓模板图前调用,模板需要存放目录)</summary>
    bool EnsureProjectSaved()
    {
        if (_projectPath != null) return true;
        AppendLog("请先保存脚本(模板图会存放在脚本同目录的 templates 下)");
        return SaveProject();
    }

    // ---------------- 抓图 / 取色 / 区域 / 测试 ----------------
    void CaptureTemplate(TemplateRowVM row)
    {
        if (!EnsureProjectSaved()) return;
        var frame = CaptureService.CapturePrimary();
        var overlay = new CaptureOverlayWindow(frame, OverlayMode.Template);
        overlay.ShowDialog();
        if (overlay.TemplateResult == null) return;
        var (bgra, rect) = overlay.TemplateResult.Value;
        string dir = ProjectService.EnsureTemplatesDir(_projectPath!);
        string path = Path.Combine(dir, $"tpl_{DateTime.Now:HHmmss_fff}.png");
        ImageUtil.SavePng(bgra, rect.W, rect.H, path);
        row.Value = path;
        if (SelectedNode != null) OnRowChanged(SelectedNode);
        AppendLog($"模板已保存:{path}");
    }

    void BrowseTemplate(TemplateRowVM row)
    {
        var dlg = new OpenFileDialog { Filter = "图片 (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|所有文件|*.*" };
        if (dlg.ShowDialog() == true)
        {
            row.Value = dlg.FileName;
            if (SelectedNode != null) OnRowChanged(SelectedNode);
        }
    }

    void PickColor(ColorRowVM row)
    {
        var frame = CaptureService.CapturePrimary();
        var overlay = new CaptureOverlayWindow(frame, OverlayMode.Color);
        overlay.ShowDialog();
        if (overlay.ColorResult == null) return;
        row.Value = MatcherService.ToHex(overlay.ColorResult.Value.R, overlay.ColorResult.Value.G, overlay.ColorResult.Value.B);
        if (SelectedNode != null) OnRowChanged(SelectedNode);
        AppendLog($"已取色:{row.Value}");
    }

    void PickRegion(RegionRowVM row)
    {
        var frame = CaptureService.CapturePrimary();
        var overlay = new CaptureOverlayWindow(frame, OverlayMode.Region);
        overlay.ShowDialog();
        if (overlay.RegionResult == null) return;
        var r = overlay.RegionResult.Value;
        // 覆盖层帧原点即主显示器左上角,换算为桌面绝对坐标
        int x = frame.OriginX + r.X, y = frame.OriginY + r.Y;
        row.Value = $"{x},{y},{r.W},{r.H}";
        if (SelectedNode != null) OnRowChanged(SelectedNode);
        AppendLog($"搜索区域:{row.Value}");
    }

    /// <summary>测试找图:立即抓屏匹配并高亮结果</summary>
    void TestFind(TemplateRowVM row)
    {
        var node = SelectedNode;
        if (node == null) return;
        string template = row.Value;
        string region = node.Model.Str("region");
        double sim = node.Model.Dbl("similarity");
        if (sim <= 0) sim = 0.85;
        if (string.IsNullOrEmpty(template) || !File.Exists(template))
        {
            AppendLog("⚠ 请先抓取或选择模板图");
            return;
        }
        RunTest(() =>
        {
            var frame = Capture.CaptureForMatch();
            var hit = MatcherService.FindImage(frame, template, sim, region);
            return (hit.Found, hit.X + frame.OriginX, hit.Y + frame.OriginY, hit.Score, frame);
        }, "找图");
    }

    void TestColor(ColorRowVM row)
    {
        var node = SelectedNode;
        if (node == null) return;
        RunTest(() =>
        {
            var frame = Capture.CaptureForMatch();
            var hit = MatcherService.FindColor(frame, row.Value, node.Model.Int("tolerance"), node.Model.Str("region"));
            return (hit.Found, hit.X + frame.OriginX, hit.Y + frame.OriginY, hit.Score, frame);
        }, "找色");
    }

    void RunTest(Func<(bool Found, int X, int Y, double Score, CaptureFrame Frame)> action, string what)
    {
        try
        {
            var (found, x, y, score, frame) = action();
            if (found)
            {
                AppendLog($"✔ {what}成功:({x},{y}) 相似度 {score:F2}");
                ScreenHighlighter.Highlight(x - 4, y - 4, 8, 8);
            }
            else
            {
                AppendLog($"✖ {what}失败:未找到目标(引擎:{frame.Engine})");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"❌ 测试出错:{ex.Message}");
        }
    }

    // ---------------- 运行 / 停止 / 导出 / 设置 ----------------
    public void RunScript()
    {
        if (IsRunning) return;
        if (_project.Steps.Count == 0) { AppendLog("脚本为空,请先添加步骤"); return; }
        AppendLog($"抓图引擎:{Capture.Mode}");
        _engine.Start(_project, Capture);
    }

    public void StopScript() => _engine.Stop();

    void ExportPython()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Python 脚本 (*.py)|*.py",
            FileName = "script.py",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var dir = Path.GetDirectoryName(dlg.FileName)!;
            PythonExporter.Export(_project, _project.Steps, dir);
            AppendLog($"🐍 已导出 Python 脚本:{dlg.FileName}");
            AppendLog("   运行方式:先安装依赖 pip install opencv-python mss numpy pydirectinput,然后 python script.py");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败:{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ShowSettings()
    {
        var win = new SettingsWindow
        {
            Owner = Application.Current.MainWindow,
            InitialEngine = _settings.CaptureEngine,
            InitialScanCode = _project.ScanCodeMode,
            InitialLoopCount = _project.ScriptLoopCount,
        };
        if (win.ShowDialog() == true)
        {
            _settings.CaptureEngine = win.CaptureEngine;
            _settings.Save();
            Capture.Mode = win.CaptureEngine.ToLowerInvariant() switch
            {
                "dxgi" => EngineKind.Dxgi,
                "gdi" => EngineKind.Gdi,
                _ => EngineKind.Auto,
            };
            _project.ScanCodeMode = win.ScanCodeMode;
            _project.ScriptLoopCount = Math.Max(0, win.ScriptLoopCount);
            AppendLog($"设置已保存:抓图引擎={win.CaptureEngine},扫描码模式={(win.ScanCodeMode ? "开" : "关")},整体循环={(win.ScriptLoopCount == 0 ? "无限" : win.ScriptLoopCount + " 次")}");
        }
    }

    // ---------------- 供 MainWindow 调用的公开包装 ----------------
    public void CaptureTemplatePublic(TemplateRowVM row) => CaptureTemplate(row);
    public void BrowseTemplatePublic(TemplateRowVM row) => BrowseTemplate(row);
    public void TestFindPublic(TemplateRowVM row) => TestFind(row);
    public void PickColorPublic(ColorRowVM row) => PickColor(row);
    public void TestColorPublic(ColorRowVM row) => TestColor(row);
    public void PickRegionPublic(RegionRowVM row) => PickRegion(row);

    /// <summary>区域改回全屏后同步参数</summary>
    public void ForceRowSync()
    {
        if (SelectedNode != null) OnRowChanged(SelectedNode);
    }

    /// <summary>拖到树空白处:移动到根列表末尾</summary>
    public void DropStepToRootEnd(StepNodeVM dragged)
    {
        ModelListOf(dragged)?.RemoveAll(s => s.Id == dragged.Model.Id);
        _project.Steps.Add(dragged.Model);
        dragged.Parent = null;
        RebuildTree();
    }
}
