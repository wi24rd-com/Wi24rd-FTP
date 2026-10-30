using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Wi24rdFTP.Services;

namespace Wi24rdFTP;

public partial class MainWindow : Window
{
    private readonly Backend _backend = new();
    private readonly ObservableCollection<SharedFile> _files = new();
    private readonly DispatcherTimer _statusTimer;

    private bool _running;
    private bool _busy;
    private bool _closeConfirmed;
    private string _mode = "FTP";
    private string _password = NewPassword();

    public MainWindow()
    {
        InitializeComponent();
        FileList.ItemsSource = _files;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) => RefreshStatus();

        ShowPassword(_password);

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
        ElevatedBanner.Visibility = IsElevated() ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsElevated()
    {
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(id)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (BtnStart is null) return;   // kaldes under InitializeComponent
        BtnStartText.Text = ModeSftp.IsChecked == true ? "Start SFTP" : "Start FTP";
        BtnStopText.Text  = ModeSftp.IsChecked == true ? "Stop SFTP"  : "Stop FTP";
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
        var ok = !_busy && e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        if (ok)
        {
            DropOutline.BorderBrush = (Brush)FindResource("Brush.Accent");
            DropOutline.Background = (Brush)FindResource("Brush.AccentPale");
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e) => ResetDropZone();

    private void ResetDropZone()
    {
        DropOutline.BorderBrush = (Brush)FindResource("Brush.Border");
        DropOutline.Background = (Brush)FindResource("Brush.Field");
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        ResetDropZone();
        if (_busy) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            AddPaths(paths);
        e.Handled = true;
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
                "Wi24rd FTP", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        $"\"{name}\" findes allerede i Wi24rd FTP.\n\nSkal den overskrives? (Gælder for alle i denne omgang)",
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
            ? $"{copied} element(er) kopieret til Wi24rd FTP."
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
        var r = MessageBox.Show(this, $"Fjern \"{sf.Name}\" fra Wi24rd FTP?\n\nOriginalen andre steder på disken røres ikke.",
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
            var r = MessageBox.Show(this, "Der er ingen filer i Wi24rd FTP endnu.\n\nVil du starte serveren alligevel?",
                "Ingen filer", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        _mode = ModeSftp.IsChecked == true ? "SFTP" : "FTP";
        var ok = await RunBackendAsync("Start",
            _mode == "FTP" ? "Starter FTP-server og åbner port 21…" : "Starter SFTP (OpenSSH) og åbner port 22…",
            _mode, _password);

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
            _password = NewPassword();
            ShowPassword(_password);
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
            $"Der ligger {_files.Count} element(er) i Wi24rd FTP.\n\n" +
            "Skal de slettes nu?\n\n" +
            "Ja = slet filerne fra Wi24rd FTP\nNej = lad dem blive til næste gang",
            "Slet filer?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (r == MessageBoxResult.Yes)
        {
            ClearShare();
            Log("Filerne er slettet fra Wi24rd FTP.");
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
    private async Task<bool> RunBackendAsync(string action, string message, string mode = "FTP", string? password = null)
    {
        if (_busy) return false;
        SetBusy(true, message);
        try
        {
            var outcome = await _backend.RunAsync(action, mode, password);
            switch (outcome)
            {
                case BackendOutcome.Ok:
                    return true;
                case BackendOutcome.Cancelled:
                    Log("Afbrudt – administrator-godkendelse blev afvist.");
                    return false;
                case BackendOutcome.ScriptMissing:
                    MessageBox.Show(this, $"Backend-scriptet mangler:\n{_backend.ScriptPath}\n\nGeninstallér Wi24rd FTP.",
                        "Wi24rd FTP", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                default:
                    var st = _backend.ReadState();
                    var err = string.IsNullOrWhiteSpace(st?.LastError) ? "Ukendt fejl." : st!.LastError;
                    Log("Fejl: " + err);
                    MessageBox.Show(this,
                        $"Handlingen '{action}' mislykkedes:\n\n{err}\n\nSe loggen i:\n{Backend.LogDir}",
                        "Wi24rd FTP", MessageBoxButton.OK, MessageBoxImage.Error);
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
        if (!string.IsNullOrEmpty(st.Password)) { _password = st.Password; ShowPassword(_password); }
        UserBox.Text = string.IsNullOrEmpty(st.User) ? "wi24rdftp" : st.User;
        var ips = st.Ips?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        IpList.ItemsSource = ips.Length == 0 ? null : ips.Select(ip => $"{ip}:{st.Port}").ToArray();
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
            StatusDot.Fill = (Brush)FindResource("Brush.Ok");
            SvcDot.Fill = (Brush)FindResource("Brush.Ok");
            FwDot.Fill = (Brush)FindResource("Brush.Ok");
            StatusText.Text = $"Kører · port {port}";
            LogHint.Text = "Tryk Stop FTP for at lukke porten igen.";
            SvcStatus.Text = st!.Mode == "SFTP" ? "OpenSSH (sshd) kører" : "IIS FTP (ftpsvc) kører";
            FwStatus.Text = $"Port {port} åben";
            if (st.Mode == "SFTP") ModeSftp.IsChecked = true; else ModeFtp.IsChecked = true;
            if (IpList.ItemsSource is null)      // fx efter genstart af GUI'en mens serveren kørte
                ApplyState(st);
        }
        else
        {
            StatusDot.Fill = (ftpUp || sshUp) ? (Brush)FindResource("Brush.Amber") : (Brush)FindResource("Brush.Grey");
            SvcDot.Fill = (ftpUp || sshUp) ? (Brush)FindResource("Brush.Amber") : (Brush)FindResource("Brush.Grey");
            FwDot.Fill = (Brush)FindResource("Brush.Red");
            StatusText.Text = (ftpUp || sshUp) ? "Tjeneste kører – port lukket" : "Stoppet";
            LogHint.Text = "Start FTP for at dele filer og mapper.";
            SvcStatus.Text = Backend.ServiceExists("ftpsvc") ? (ftpUp ? "ftpsvc kører (ikke af Wi24rd)" : "Stoppet") : "Ikke installeret";
            FwStatus.Text = "Lukket";
        }

        BtnStart.IsEnabled = !_busy && !_running;
        BtnStop.IsEnabled = !_busy && _running;
        ModeFtp.IsEnabled = ModeSftp.IsEnabled = !_running && !_busy;
        BtnNewPassword.IsEnabled = !_running && !_busy;
        BtnStartText.Text = ModeSftp.IsChecked == true ? "Start SFTP" : "Start FTP";
        BtnStopText.Text  = ModeSftp.IsChecked == true ? "Stop SFTP"  : "Stop FTP";
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

    private const string PasswordChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    /// <summary>12 tegn, kun A-Z/a-z/2-9 uden 0,O,1,l,I – kan læses op i telefonen.</summary>
    private static string NewPassword()
    {
        var bytes = new byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => PasswordChars[b % PasswordChars.Length]).ToArray());
    }

    /// <summary>Farvekodet: tal = rød, STORE bogstaver = blå, små bogstaver = grøn.</summary>
    private void ShowPassword(string password)
    {
        var digit = (Brush)FindResource("Brush.PwDigit");
        var upper = (Brush)FindResource("Brush.PwUpper");
        var lower = (Brush)FindResource("Brush.PwLower");

        PasswordText.Inlines.Clear();
        foreach (var c in password)
        {
            PasswordText.Inlines.Add(new System.Windows.Documents.Run(c.ToString())
            {
                Foreground = char.IsDigit(c) ? digit : char.IsUpper(c) ? upper : lower
            });
        }
    }

    private void BtnNewPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _password = NewPassword();
        ShowPassword(_password);
        Log("Ny adgangskode genereret.");
    }

    private void Log(string message)
    {
        LogLine.Text = message;
        LogHint.Text = $"Kl. {DateTime.Now:HH:mm}";
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
        Clipboard.SetText(_password);
        Log("Adgangskode kopieret.");
    }
}
