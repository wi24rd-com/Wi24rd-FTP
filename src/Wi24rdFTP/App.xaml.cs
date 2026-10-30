using System.Windows;

namespace Wi24rdFTP;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                L.UnexpectedError + args.Exception.Message,
                L.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
