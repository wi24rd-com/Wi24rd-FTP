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
    private UpdateInfo? _update;

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
            _ = CheckForUpdateAsync();
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
        BtnStartText.Text = ModeSftp.IsChecked == true ? L.StartSftp : L.StartFtp;
        BtnStopText.Text  = ModeSftp.IsChecked == true ? L.StopSftp  : L.StopFtp;
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        await RunBackendAsync("Install", L.InstallingMsg);
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
            Title = L.DlgChooseFiles,
            Multiselect = true,
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
            AddPaths(dlg.FileNames);
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = L.DlgChooseFolder, Multiselect = true };
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
            Log(L.F(L.ErrCreateFolder, Backend.SharePath, ex.Message));
            MessageBox.Show(this,
                L.F(L.ErrCreateFolderBox, Backend.SharePath, ex.Message),
                L.AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        L.F(L.OverwriteQ, name),
                        L.OverwriteTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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
                Log(L.F(L.ErrCopy, name, ex.Message));
            }
        }

        RefreshFiles();
        var msg = skipped == 0
            ? L.F(L.CopiedN, copied)
            : L.F(L.CopiedSkipped, copied, skipped);
        Log(msg);
        if (_running) LogHint.Text = L.RunningFilesHint;
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    /// <summary>Navn-kolonnen fylder den plads, de faste kolonner ikke bruger.</summary>
    private void FileList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        const double fixedCols = 36 + 40;                 // ikon + fjern-knap
        var scroll = SystemParameters.VerticalScrollBarWidth + 12;
        var w = FileList.ActualWidth - fixedCols - ColSize.Width - ColAdded.Width - scroll;
        ColName.Width = Math.Max(120, w);
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
        var r = MessageBox.Show(this, L.F(L.RemoveQ, sf.Name),
            L.RemoveTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            if (sf.IsDirectory) Directory.Delete(sf.FullPath, true);
            else File.Delete(sf.FullPath);
        }
        catch (Exception ex)
        {
            Log(L.F(L.ErrRemove, sf.Name, ex.Message));
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
            var r = MessageBox.Show(this, L.NoFilesQ,
                L.NoFilesTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        _mode = ModeSftp.IsChecked == true ? "SFTP" : "FTP";
        var ok = await RunBackendAsync("Start",
            _mode == "FTP" ? L.StartingFtp : L.StartingSftp,
            _mode, _password);

        var st = _backend.ReadState();
        if (ok && st is { Running: true })
        {
            _running = true;
            ApplyState(st);
            Log(L.F(L.RunningMsg, _mode));
        }
        else
        {
            _running = false;
            Log(L.StartFailed);
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
        var ok = await RunBackendAsync("Stop", L.StoppingMsg, _mode);
        if (ok)
        {
            _running = false;
            _password = NewPassword();
            ShowPassword(_password);
            IpList.ItemsSource = null;
            IpHint.Visibility = Visibility.Visible;
            Log(L.StoppedMsg);
        }
        RefreshStatus();
        return ok;
    }

    private void AskAboutFiles()
    {
        if (_files.Count == 0) return;
        var r = MessageBox.Show(this,
            L.F(L.DeleteFilesQ, _files.Count),
            L.DeleteFilesTitle, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (r == MessageBoxResult.Yes)
        {
            ClearShare();
            Log(L.FilesDeleted);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private async Task CheckForUpdateAsync()
    {
        var current = Version.TryParse(AboutWindow.AppVersion, out var v) ? v : new Version(0, 0, 0);
        _update = await UpdateChecker.CheckAsync(current);
        if (_update is null) return;
        UpdateText.Text = L.F(L.UpdateAvailable, _update.Version.ToString(3));
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        var url = _update?.InstallerUrl ?? _update?.ReleaseUrl;
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_closeConfirmed) return;
        e.Cancel = true;                 // vi lukker selv, når stop-rutinen er kørt
        if (_busy) return;
        _ = Dispatcher.InvokeAsync(ShutdownAsync);
    }

    /// <summary>Luk = Stop-rutine (hvis noget kører) → spørg om filer → luk.</summary>
    private async Task ShutdownAsync()
    {
        if (_running || Backend.ServiceRunning("ftpsvc") || Backend.ServiceRunning("sshd"))
        {
            var ok = await StopAsync();
            if (!ok)
            {
                var r = MessageBox.Show(this,
                    L.StopFailedQ,
                    L.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
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
        var poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        var started = DateTime.Now;
        poll.Tick += (_, _) =>
        {
            var last = Backend.ReadLastLogLine();
            if (!string.IsNullOrWhiteSpace(last) && !last.StartsWith("==="))
                LogHint.Text = last;
            LogLine.Text = $"{message}  ({(int)(DateTime.Now - started).TotalSeconds}s)";
        };
        poll.Start();
        try
        {
            var outcome = await _backend.RunAsync(action, mode, password);
            switch (outcome)
            {
                case BackendOutcome.Ok:
                    return true;
                case BackendOutcome.Cancelled:
                    Log(L.Cancelled);
                    return false;
                case BackendOutcome.ScriptMissing:
                    MessageBox.Show(this, L.F(L.ScriptMissing, _backend.ScriptPath),
                        L.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                default:
                    var st = _backend.ReadState();
                    var err = string.IsNullOrWhiteSpace(st?.LastError) ? L.UnknownError : st!.LastError;
                    Log(L.ErrorPrefix + err);
                    MessageBox.Show(this,
                        L.F(L.ActionFailed, action, err, Backend.LogDir),
                        L.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
            }
        }
        finally
        {
            poll.Stop();
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
        if (ips.Length == 0) IpHint.Text = L.NoIp;
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
            StatusText.Text = L.F(L.RunningPort, port);
            LogHint.Text = L.HintStop;
            SvcStatus.Text = st!.Mode == "SFTP" ? L.SshdRunning : L.FtpsvcRunning;
            FwStatus.Text = L.F(L.PortOpen, port);
            if (st.Mode == "SFTP") ModeSftp.IsChecked = true; else ModeFtp.IsChecked = true;
            if (IpList.ItemsSource is null)      // fx efter genstart af GUI'en mens serveren kørte
                ApplyState(st);
        }
        else
        {
            StatusDot.Fill = (ftpUp || sshUp) ? (Brush)FindResource("Brush.Amber") : (Brush)FindResource("Brush.Grey");
            SvcDot.Fill = (ftpUp || sshUp) ? (Brush)FindResource("Brush.Amber") : (Brush)FindResource("Brush.Grey");
            FwDot.Fill = (Brush)FindResource("Brush.Red");
            StatusText.Text = (ftpUp || sshUp) ? L.ServiceRunningPortClosed : L.Stopped;
            LogHint.Text = L.HintStart;
            SvcStatus.Text = Backend.ServiceExists("ftpsvc") ? (ftpUp ? L.FtpsvcForeign : L.Stopped) : L.NotInstalled;
            FwStatus.Text = L.Closed;
        }

        BtnStart.IsEnabled = !_busy && !_running;
        BtnStop.IsEnabled = !_busy && _running;
        ModeFtp.IsEnabled = ModeSftp.IsEnabled = !_running && !_busy;
        BtnNewPassword.IsEnabled = !_running && !_busy;
        BtnStartText.Text = ModeSftp.IsChecked == true ? L.StartSftp : L.StartFtp;
        BtnStopText.Text  = ModeSftp.IsChecked == true ? L.StopSftp  : L.StopFtp;
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
        Log(L.NewPasswordGenerated);
    }

    private void Log(string message)
    {
        LogLine.Text = message;
        LogHint.Text = L.F(L.AtTime, DateTime.Now.ToString("t"));
    }

    // ------------------------------------------------------------------
    // Kopiér-knapper
    // ------------------------------------------------------------------
    private void BtnCopyUser_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(UserBox.Text);
        Log(L.UsernameCopied);
    }

    private void BtnCopyPassword_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_password);
        Log(L.PasswordCopied);
    }
}
