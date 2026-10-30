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
                "Der opstod en uventet fejl:\n\n" + args.Exception.Message,
                "Wi24rd FTP", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
