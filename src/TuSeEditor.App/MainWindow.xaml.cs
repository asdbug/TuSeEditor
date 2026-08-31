using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TuSeEditor.App.Models;
using TuSeEditor.App.ViewModels;
using TuSeEditor.App.Views;

namespace TuSeEditor.App;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // 工具箱按分类分组
        var view = (System.ComponentModel.ICollectionView)CollectionViewSource.GetDefaultView(_vm.Toolbox);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolboxItemVM.Category)));

        _vm.PropertyChanged += Vm_PropertyChanged;
        SourceInitialized += (_, _) =>
        {
            _vm.Hotkey.StartPressed += () => Dispatcher.BeginInvoke(_vm.RunScript);
            _vm.Hotkey.StopPressed += () => Dispatcher.BeginInvoke(_vm.StopScript);
            if (!_vm.Hotkey.Register(this))
                _vm.AppendLog("⚠ 全局热键 F9/F10 注册失败(可能被其他软件占用)");
            else
                _vm.AppendLog("全局热键已就绪:F9 运行 / F10 停止(在任何界面下都有效)");
        };
        _vm.UpdateTitleExternal += () => Title = _vm.Title;
        Title = _vm.Title;
    }

    void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LogText))
            LogBox.ScrollToEnd();
        else if (e.PropertyName == nameof(MainViewModel.SelectedNode))
            PropTitle.Text = _vm.SelectedNode == null ? "步骤属性" : $"步骤属性  ·  {_vm.SelectedNode.TypeName}";
        else if (e.PropertyName == nameof(MainViewModel.Title))
        {
            Title = _vm.Title;
            ProjectPathText.Text = _vm.ProjectPathDisplay;
        }
        else if (e.PropertyName == nameof(MainViewModel.IsRunning))
            ProjectPathText.Text = _vm.ProjectPathDisplay;
    }

    // ---------------- 工具栏 ----------------
    void OnNew(object s, RoutedEventArgs e) => _vm.NewProjectCmd.Execute(null);
    void OnOpen(object s, RoutedEventArgs e) => _vm.OpenProjectCmd.Execute(null);
    void OnSave(object s, RoutedEventArgs e) => _vm.SaveProjectCmd.Execute(null);
    void OnRun(object s, RoutedEventArgs e) => _vm.RunScript();
    void OnStop(object s, RoutedEventArgs e) => _vm.StopScript();
    void OnExport(object s, RoutedEventArgs e) => _vm.ExportPythonCmd.Execute(null);
    void OnSettings(object s, RoutedEventArgs e) => _vm.ShowSettingsCmd.Execute(null);

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            OpenHelp();
            e.Handled = true;
        }
    }

    void OnHelp(object s, RoutedEventArgs e) => OpenHelp();

    void OpenHelp()
    {
        var win = new Views.HelpWindow { Owner = this };
        win.Show();
    }

    void Toolbox_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.AddStepCmd.CanExecute(null))
            _vm.AddStepCmd.Execute(ToolboxList.SelectedItem);
    }

    void ToolboxAdd_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ToolboxItemVM item)
            _vm.AddStep(item);
    }

    // ---------------- 步骤树 ----------------
    void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        => _vm.SelectedItem = e.NewValue;

    void Tree_RightDown(object sender, MouseButtonEventArgs e)
    {
        // 右键先选中,再弹出菜单
        if (e.OriginalSource is DependencyObject d && FindAncestor<TreeViewItem>(d) is { } item)
            item.IsSelected = true;
    }

    void OnCopyStep(object s, RoutedEventArgs e) => _vm.CopyStepCmd.Execute(null);
    void OnDeleteStep(object s, RoutedEventArgs e) => _vm.DeleteStepCmd.Execute(null);
    void OnMoveUp(object s, RoutedEventArgs e) => _vm.MoveUpCmd.Execute(null);
    void OnMoveDown(object s, RoutedEventArgs e) => _vm.MoveDownCmd.Execute(null);

    // ---- 拖拽排序 ----
    Point? _dragStart;
    object? _dragData;

    void Tree_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart == null) return;
        var pos = e.GetPosition(StepTree);
        if (Math.Abs(pos.X - _dragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (_dragData is StepNodeVM)
        {
            DragDrop.DoDragDrop(StepTree, new DataObject("tuse-step", _dragData), DragDropEffects.Move);
            _dragStart = null;
        }
    }

    void Tree_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(StepTree);
        _dragData = null;
        // 记录按下位置所在节点,供拖拽启动判断
        if (e.OriginalSource is DependencyObject d && FindAncestor<TreeViewItem>(d) is { } item)
            _dragData = item.DataContext;
    }

    void Tree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("tuse-step") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    void Tree_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("tuse-step")) return;
        if (e.Data.GetData("tuse-step") is not StepNodeVM dragged) return;

        object? target = null;
        if (FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) is { } item)
            target = item.DataContext;
        if (target == null)
        {
            // 落在空白处:移到根列表末尾
            _vm.DropStepToRootEnd(dragged);
            return;
        }
        _vm.DropStep(dragged, target);
    }

    static TreeViewItem? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is TreeViewItem item) return item;
            current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    // ---------------- 属性面板按钮 ----------------
    T? RowOf<T>(object sender) where T : class => (sender as FrameworkElement)?.DataContext as T;

    void OnCaptureTemplate(object sender, RoutedEventArgs e)
    {
        if (RowOf<TemplateRowVM>(sender) is { } r) _vm.CaptureTemplatePublic(r);
    }

    void OnBrowseTemplate(object sender, RoutedEventArgs e)
    {
        if (RowOf<TemplateRowVM>(sender) is { } r) _vm.BrowseTemplatePublic(r);
    }

    void OnTestTemplate(object sender, RoutedEventArgs e)
    {
        if (RowOf<TemplateRowVM>(sender) is { } r) _vm.TestFindPublic(r);
    }

    void OnPickColor(object sender, RoutedEventArgs e)
    {
        if (RowOf<ColorRowVM>(sender) is { } r) _vm.PickColorPublic(r);
    }

    void OnTestColor(object sender, RoutedEventArgs e)
    {
        if (RowOf<ColorRowVM>(sender) is { } r) _vm.TestColorPublic(r);
    }

    void OnPickRegion(object sender, RoutedEventArgs e)
    {
        if (RowOf<RegionRowVM>(sender) is { } r) _vm.PickRegionPublic(r);
    }

    void OnRegionFull(object sender, RoutedEventArgs e)
    {
        if (RowOf<RegionRowVM>(sender) is { } r)
        {
            r.Value = "full";
            if (_vm.SelectedNode != null) _vm.ForceRowSync();
        }
    }

    void LogBox_TextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();
}
