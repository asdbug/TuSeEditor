using System.Windows;
using TuSeEditor.App.ViewModels;

namespace TuSeEditor.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"发生未处理的错误:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "图色脚本编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
