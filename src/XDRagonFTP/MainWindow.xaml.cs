using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using XDRagonFTP.Services;

namespace XDRagonFTP;

public partial class MainWindow : Window
{
    private readonly Backend _backend = new();
    private readonly ObservableCollection<SharedFile> _files = new();
    private readonly DispatcherTimer _statusTimer;

    private bool _running;
    private bool _busy;
    private bool _closeConfirmed;
    private string _mode = "FTP";

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = _files;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) => RefreshStatus();

        Loaded += (_, _) =>
        {
            CheckPrerequisites();
            RefreshFiles();
            RefreshStatus();
            _statusTimer.Start();
        };
    }

    // ------------------------------------------------------------------
    // Forudsætninger
    // ------------------------------------------------------------------
    private void CheckPrerequisites()
    {
        var hasFtp = Backend.ServiceExists("ftpsvc");
        PrereqBanner.Visibility = hasFtp ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        await RunBackendAsync("Install", "Installerer Windows' FTP-server – vent venligst…");
        CheckPrerequisites();
        RefreshStatus();
    }

    // ------------------------------------------------------------------
    // Filhåndtering (kopiér ind i Share)
    // ------------------------------------------------------------------
    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        if (ok)
        {
            DropOutline.Stroke = (Brush)FindResource("Brush.Green");
            DropOutline.Fill = (Brush)FindResource("Brush.GreenPale");
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e) => ResetDropZone();

    private void ResetDropZone()
    {
        DropOutline.Stroke = (Brush)FindResource("Brush.Border");
        DropOutline.Fill = (Brush)FindResource("Brush.Bg");
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        ResetDropZone();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            AddPaths(paths);
    }

    private void BtnBrowseFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Vælg filer der skal deles",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
            AddPaths(dlg.FileNames);
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Vælg mappe der skal deles", Multiselect = true };
        if (dlg.ShowDialog(this) == true)
            AddPaths(dlg.FolderNames);
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        try
        {
            Directory.CreateDirectory(Backend.SharePath);
        }
        catch (Exception ex)
        {
            Log($"Kunne ikke oprette {Backend.SharePath}: {ex.Message}");
            MessageBox.Show(this,
                $"Mappen {Backend.SharePath} kunne ikke oprettes.\nKør 'Installér nu' først.\n\n{ex.Message}",
                "XDRagon FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int copied = 0, skipped = 0;
        bool? overwriteAll = null;

        foreach (var src in paths)
        {
            var name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(name)) continue;
            var dst = Path.Combine(Backend.SharePath, name);

            // Allerede i Share? (fx bruger trækker fra selve mappen)
            if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(dst) || Directory.Exists(dst))
            {
                if (overwriteAll is null)
                {
                    var r = MessageBox.Show(this,
                        $"\"{name}\" findes allerede i XDRagon FTP.\n\nSkal den overskrives? (Gælder for alle i denne omgang)",
                        "Overskriv?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Cancel) break;
                    overwriteAll = r == MessageBoxResult.Yes;
                }
                if (overwriteAll == false) { skipped++; continue; }
            }

            try
            {
                if (Directory.Exists(src))
                {
                    if (Directory.Exists(dst)) Directory.Delete(dst, true);
                    CopyDirectory(src, dst);
                }
                else if (File.Exists(src))
                {
                    File.Copy(src, dst, true);
                }
                else continue;
                copied++;
            }
            catch (Exception ex)
            {
                skipped++;
                Log($"Fejl ved kopiering af {name}: {ex.Message}");
            }
        }

        RefreshFiles();
        Log(skipped == 0
            ? $"{copied} element(er) kopieret til XDRagon FTP."
            : $"{copied} kopieret, {skipped} sprunget over.");
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    private void RefreshFiles()
    {
        _files.Clear();
        if (Directory.Exists(Backend.SharePath))
        {
            var di = new DirectoryInfo(Backend.SharePath);
            foreach (var d in di.GetDirectories().OrderBy(x => x.Name))
                _files.Add(new SharedFile
                {
                    Name = d.Name, FullPath = d.FullName, IsDirectory = true,
                    Size = SharedFile.DirectorySize(d.FullName), Added = d.CreationTime
                });
            foreach (var f in di.GetFiles().OrderBy(x => x.Name))
                _files.Add(new SharedFile
                {
                    Name = f.Name, FullPath = f.FullName, IsDirectory = false,
                    Size = f.Length, Added = f.CreationTime
                });
        }
        EmptyHint.Visibility = _files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var total = _files.Sum(x => x.Size);
        FileStatus.Text = _files.Count == 0 ? "0" : $"{_files.Count} · {SharedFile.FormatSize(total)}";
    }

    private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SharedFile sf) return;
        var r = MessageBox.Show(this, $"Fjern \"{sf.Name}\" fra XDRagon FTP?\n\nOriginalen andre steder på disken røres ikke.",
            "Fjern", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            if (sf.IsDirectory) Directory.Delete(sf.FullPath, true);
            else File.Delete(sf.FullPath);
        }
        catch (Exception ex)
        {
            Log($"Kunne ikke fjerne {sf.Name}: {ex.Message}");
        }
        RefreshFiles();
    }

    private void ClearShare()
    {
        if (!Directory.Exists(Backend.SharePath)) return;
        var di = new DirectoryInfo(Backend.SharePath);
        foreach (var f in di.GetFiles()) { try { f.Delete(); } catch { } }
        foreach (var d in di.GetDirectories()) { try { d.Delete(true); } catch { } }
        RefreshFiles();
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Backend.SharePath);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Backend.SharePath}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Log(ex.Message); }
    }

    // ------------------------------------------------------------------
    // Start / Stop / Luk
    // ------------------------------------------------------------------
    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            var r = MessageBox.Show(this, "Der er ingen filer i XDRagon FTP endnu.\n\nVil du starte serveren alligevel?",
                "Ingen filer", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        _mode = ModeSftp.IsChecked == true ? "SFTP" : "FTP";
        var ok = await RunBackendAsync("Start",
            _mode == "FTP" ? "Starter FTP-server og åbner port 21…" : "Starter SFTP (OpenSSH) og åbner port 22…",
            _mode);

        var st = _backend.ReadState();
        if (ok && st is { Running: true })
        {
            _running = true;
            ApplyState(st);
            Log($"{_mode} kører. Giv modtageren adresse, brugernavn og adgangskode.");
        }
        else
        {
            _running = false;
            Log("Start mislykkedes.");
        }
        RefreshStatus();
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        await StopAsync();
        AskAboutFiles();
    }

    private async Task<bool> StopAsync()
    {
        var ok = await RunBackendAsync("Stop", "Lukker port og stopper server…", _mode);
        if (ok)
        {
            _running = false;
            PasswordBox.Text = "••••••••••••";
            IpList.ItemsSource = null;
            IpHint.Visibility = Visibility.Visible;
            Log("Stoppet – porten er lukket og kontoen deaktiveret.");
        }
        RefreshStatus();
        return ok;
    }

    private void AskAboutFiles()
    {
        if (_files.Count == 0) return;
        var r = MessageBox.Show(this,
            $"Der ligger {_files.Count} element(er) i XDRagon FTP.\n\n" +
            "Skal de slettes nu?\n\n" +
            "Ja = slet filerne fra XDRagon FTP\nNej = lad dem blive til næste gang",
            "Slet filer?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (r == MessageBoxResult.Yes)
        {
            ClearShare();
            Log("Filerne er slettet fra XDRagon FTP.");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_closeConfirmed) return;
        if (_busy) { e.Cancel = true; return; }

        e.Cancel = true;

        // Luk kører altid stop-rutinen, hvis der er noget at stoppe
        if (_running || Backend.ServiceRunning("ftpsvc") || Backend.ServiceRunning("sshd"))
        {
            var ok = await StopAsync();
            if (!ok)
            {
                var r = MessageBox.Show(this,
                    "Serveren kunne ikke stoppes (måske afviste du administrator-prompten).\n\nLuk alligevel og lad porten stå åben?",
                    "Advarsel", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (r != MessageBoxResult.Yes) return;
            }
        }

        AskAboutFiles();

        _closeConfirmed = true;
        _statusTimer.Stop();
        Close();
    }

    // ------------------------------------------------------------------
    // Backend-kald + status
    // ------------------------------------------------------------------
    private async Task<bool> RunBackendAsync(string action, string message, string mode = "FTP")
    {
        if (_busy) return false;
        SetBusy(true, message);
        try
        {
            var outcome = await _backend.RunAsync(action, mode);
            switch (outcome)
            {
                case BackendOutcome.Ok:
                    return true;
                case BackendOutcome.Cancelled:
                    Log("Afbrudt – administrator-godkendelse blev afvist.");
                    return false;
                case BackendOutcome.ScriptMissing:
                    MessageBox.Show(this, $"Backend-scriptet mangler:\n{_backend.ScriptPath}\n\nGeninstallér XDRagon FTP.",
                        "XDRagon FTP", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                default:
                    var st = _backend.ReadState();
                    var err = string.IsNullOrWhiteSpace(st?.LastError) ? "Ukendt fejl." : st!.LastError;
                    Log("Fejl: " + err);
                    MessageBox.Show(this,
                        $"Handlingen '{action}' mislykkedes:\n\n{err}\n\nSe loggen i:\n{Backend.LogDir}",
                        "XDRagon FTP", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyState(BackendState st)
    {
        PasswordBox.Text = string.IsNullOrEmpty(st.Password) ? "••••••••••••" : st.Password;
        UserBox.Text = string.IsNullOrEmpty(st.User) ? "xdragonftp" : st.User;
        var ips = st.Ips?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        IpList.ItemsSource = ips.Length == 0 ? null : ips.Select(ip => $"{ip}  :{st.Port}").ToArray();
        IpHint.Visibility = ips.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (ips.Length == 0) IpHint.Text = "Ingen aktiv netværksadresse fundet";
    }

    private void RefreshStatus()
    {
        var ftpUp = Backend.ServiceRunning("ftpsvc");
        var sshUp = Backend.ServiceRunning("sshd");
        var st = _backend.ReadState();
        var stateRunning = st?.Running == true;

        _running = stateRunning && ((st!.Mode == "SFTP" && sshUp) || (st.Mode != "SFTP" && ftpUp));
        var port = st?.Port > 0 ? st!.Port : (ModeSftp.IsChecked == true ? 22 : 21);

        if (_running)
        {
            StatusDot.Fill = (Brush)FindResource("Brush.Green");
            StatusText.Text = $"Kører · port {port}";
            SvcStatus.Text = st!.Mode == "SFTP" ? "OpenSSH (sshd) kører" : "IIS FTP (ftpsvc) kører";
            FwStatus.Text = $"Port {port} åben";
            if (st.Mode == "SFTP") ModeSftp.IsChecked = true; else ModeFtp.IsChecked = true;
            if (IpList.ItemsSource is null)      // fx efter genstart af GUI'en mens serveren kørte
                ApplyState(st);
        }
        else
        {
            StatusDot.Fill = (ftpUp || sshUp) ? (Brush)FindResource("Brush.Amber") : new SolidColorBrush(Color.FromRgb(0x8A, 0x96, 0xA3));
            StatusText.Text = (ftpUp || sshUp) ? "Tjeneste kører – port lukket" : "Stoppet";
            SvcStatus.Text = Backend.ServiceExists("ftpsvc") ? (ftpUp ? "ftpsvc kører (ikke af XDRagon)" : "Stoppet") : "Ikke installeret";
            FwStatus.Text = "Lukket";
        }

        BtnStart.IsEnabled = !_busy && !_running;
        BtnStop.IsEnabled = !_busy && _running;
        ModeFtp.IsEnabled = ModeSftp.IsEnabled = !_running && !_busy;
        BtnStart.Content = ModeSftp.IsChecked == true ? "▶  Start SFTP" : "▶  Start FTP";
        BtnStop.Content = ModeSftp.IsChecked == true ? "■  Stop SFTP" : "■  Stop FTP";
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null) Log(message);
        BtnStart.IsEnabled = !busy && !_running;
        BtnStop.IsEnabled = !busy && _running;
        BtnClose.IsEnabled = !busy;
        BtnInstall.IsEnabled = !busy;
        DropZone.IsEnabled = !busy;
    }

    private void Log(string message)
    {
        LogLine.Text = $"{DateTime.Now:HH:mm}  {message}";
    }

    // ------------------------------------------------------------------
    // Kopiér-knapper
    // ------------------------------------------------------------------
    private void BtnCopyUser_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(UserBox.Text);
        Log("Brugernavn kopieret.");
    }

    private void BtnCopyPassword_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Text.StartsWith('•')) { Log("Ingen aktiv adgangskode – start serveren først."); return; }
        Clipboard.SetText(PasswordBox.Text);
        Log("Adgangskode kopieret.");
    }
}
