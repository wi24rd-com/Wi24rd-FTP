using System.Windows;

namespace XDRagonFTP;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                "Der opstod en uventet fejl:\n\n" + args.Exception.Message,
                "XDRagon FTP", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
