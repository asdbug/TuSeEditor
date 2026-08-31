using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TuSeEditor.App.Models;

namespace TuSeEditor.App.ViewModels;

/// <summary>条件步骤的分支节点("满足时" / "否则")</summary>
public partial class BranchVM : ObservableObject
{
    public string Label { get; }
    public bool IsElse { get; }
    public StepNodeVM Owner { get; }
    public ObservableCollection<object> Items { get; } = new();

    public BranchVM(StepNodeVM owner, string label, bool isElse)
    {
        Owner = owner;
        Label = label;
        IsElse = isElse;
    }
}

/// <summary>步骤树节点 VM,与 Step 模型一一对应</summary>
public partial class StepNodeVM : ObservableObject
{
    public Step Model { get; }
    public StepNodeVM? Parent { get; set; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _title = "";

    public string Icon { get; }
    public string TypeName { get; }

    /// <summary>子节点(Loop=子步骤;If=两个分支组;其他=null)</summary>
    public ObservableCollection<object>? NodeItems { get; private set; }

    /// <summary>该节点在模型中的子列表(If 类型时为"满足"分支)</summary>
    public List<Step> ModelChildren => Model.Children;

    public StepNodeVM(Step model)
    {
        Model = model;
        var info = StepCatalog.Get(model.Type);
        Icon = info?.Icon ?? "▪";
        TypeName = info?.Name ?? model.Type.ToString();
        IsEnabled = model.Enabled;
        Title = StepCatalog.Describe(model);
    }

    public void RefreshTitle()
    {
        Title = StepCatalog.Describe(Model);
        // 同步刷新子节点标题(一般不会变,保险起见)
        if (NodeItems != null)
            foreach (var item in NodeItems.OfType<StepNodeVM>())
                item.RefreshTitle();
    }

    /// <summary>重建 NodeItems(在树结构变化后调用)</summary>
    public void RebuildChildren(MainViewModel vm)
    {
        var info = StepCatalog.Get(Model.Type);
        if (info is { HasChildren: true, HasElse: true })
        {
            var then = new BranchVM(this, "✔ 满足条件时", false);
            var elseB = new BranchVM(this, "✖ 否则", true);
            foreach (var c in Model.Children) then.Items.Add(vm.WrapNode(c, this));
            foreach (var c in Model.ElseChildren) elseB.Items.Add(vm.WrapNode(c, this));
            NodeItems = new ObservableCollection<object> { then, elseB };
        }
        else if (info is { HasChildren: true })
        {
            NodeItems = new ObservableCollection<object>();
            foreach (var c in Model.Children) NodeItems.Add(vm.WrapNode(c, this));
        }
        else
        {
            NodeItems = null;
        }
    }
}
