using System.Windows;

namespace TuSeEditor.App.Views;

public partial class SettingsWindow : Window
{
    /// <summary>初始值(打开前设置)</summary>
    public string InitialEngine { get; set; } = "auto";
    public bool InitialScanCode { get; set; } = true;
    public int InitialLoopCount { get; set; } = 1;

    /// <summary>保存后的结果</summary>
    public string CaptureEngine { get; private set; } = "auto";
    public bool ScanCodeMode { get; private set; } = true;
    public int ScriptLoopCount { get; private set; } = 1;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            EngineBox.SelectedValue = InitialEngine;
            ScanCodeCheck.IsChecked = InitialScanCode;
            LoopCountBox.Text = InitialLoopCount.ToString();
        };
    }

    void OnSave(object sender, RoutedEventArgs e)
    {
        CaptureEngine = EngineBox.SelectedValue as string ?? "auto";
        ScanCodeMode = ScanCodeCheck.IsChecked == true;
        if (!int.TryParse(LoopCountBox.Text, out var n) || n < 0) n = 1;
        ScriptLoopCount = n;
        DialogResult = true;
    }
}
