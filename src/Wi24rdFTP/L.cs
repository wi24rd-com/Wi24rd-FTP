using System.Globalization;
using System.Resources;

namespace Wi24rdFTP;

/// <summary>
/// Lokaliserede tekster. Engelsk er neutral-sproget (Strings.resx);
/// Strings.da.resx bruges når Windows kører på dansk. Nye sprog = én ekstra .resx.
/// </summary>
public static class L
{
    private static readonly ResourceManager Rm =
        new("Wi24rdFTP.Resources.Strings", typeof(L).Assembly);

    private static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string F(string format, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

    public static string AppTitle => Get("AppTitle");
    public static string Tagline => Get("Tagline");
    public static string Stopped => Get("Stopped");
    public static string InstallNow => Get("InstallNow");
    public static string PrereqBanner => Get("PrereqBanner");
    public static string ElevatedBanner => Get("ElevatedBanner");
    public static string Status => Get("Status");
    public static string Server => Get("Server");
    public static string Firewall => Get("Firewall");
    public static string Files => Get("Files");
    public static string OpenFolder => Get("OpenFolder");
    public static string FilesShared => Get("FilesShared");
    public static string DropTitle => Get("DropTitle");
    public static string DropSub => Get("DropSub");
    public static string BrowseFiles => Get("BrowseFiles");
    public static string ChooseFolder => Get("ChooseFolder");
    public static string ColName => Get("ColName");
    public static string ColSize => Get("ColSize");
    public static string ColAdded => Get("ColAdded");
    public static string RemoveTip => Get("RemoveTip");
    public static string EmptyList => Get("EmptyList");
    public static string Connection => Get("Connection");
    public static string Protocol => Get("Protocol");
    public static string Address => Get("Address");
    public static string AddressHint => Get("AddressHint");
    public static string IpHint => Get("IpHint");
    public static string Username => Get("Username");
    public static string Copy => Get("Copy");
    public static string Password => Get("Password");
    public static string PasswordHint => Get("PasswordHint");
    public static string NewPassword => Get("NewPassword");
    public static string LegendDigit => Get("LegendDigit");
    public static string LegendUpper => Get("LegendUpper");
    public static string LegendLower => Get("LegendLower");
    public static string ReadOnlyInfo => Get("ReadOnlyInfo");
    public static string Ready => Get("Ready");
    public static string HintStart => Get("HintStart");
    public static string HintStop => Get("HintStop");
    public static string StartFtp => Get("StartFtp");
    public static string StartSftp => Get("StartSftp");
    public static string StopFtp => Get("StopFtp");
    public static string StopSftp => Get("StopSftp");
    public static string CloseApp => Get("CloseApp");
    public static string InstallingMsg => Get("InstallingMsg");
    public static string DlgChooseFiles => Get("DlgChooseFiles");
    public static string DlgChooseFolder => Get("DlgChooseFolder");
    public static string ErrCreateFolder => Get("ErrCreateFolder");
    public static string ErrCreateFolderBox => Get("ErrCreateFolderBox");
    public static string OverwriteQ => Get("OverwriteQ");
    public static string OverwriteTitle => Get("OverwriteTitle");
    public static string ErrCopy => Get("ErrCopy");
    public static string CopiedN => Get("CopiedN");
    public static string CopiedSkipped => Get("CopiedSkipped");
    public static string RunningFilesHint => Get("RunningFilesHint");
    public static string RemoveQ => Get("RemoveQ");
    public static string RemoveTitle => Get("RemoveTitle");
    public static string ErrRemove => Get("ErrRemove");
    public static string NoFilesQ => Get("NoFilesQ");
    public static string NoFilesTitle => Get("NoFilesTitle");
    public static string StartingFtp => Get("StartingFtp");
    public static string StartingSftp => Get("StartingSftp");
    public static string RunningMsg => Get("RunningMsg");
    public static string StartFailed => Get("StartFailed");
    public static string StoppingMsg => Get("StoppingMsg");
    public static string StoppedMsg => Get("StoppedMsg");
    public static string DeleteFilesQ => Get("DeleteFilesQ");
    public static string DeleteFilesTitle => Get("DeleteFilesTitle");
    public static string FilesDeleted => Get("FilesDeleted");
    public static string StopFailedQ => Get("StopFailedQ");
    public static string Warning => Get("Warning");
    public static string Cancelled => Get("Cancelled");
    public static string ScriptMissing => Get("ScriptMissing");
    public static string UnknownError => Get("UnknownError");
    public static string ErrorPrefix => Get("ErrorPrefix");
    public static string ActionFailed => Get("ActionFailed");
    public static string NoIp => Get("NoIp");
    public static string RunningPort => Get("RunningPort");
    public static string SshdRunning => Get("SshdRunning");
    public static string FtpsvcRunning => Get("FtpsvcRunning");
    public static string PortOpen => Get("PortOpen");
    public static string ServiceRunningPortClosed => Get("ServiceRunningPortClosed");
    public static string FtpsvcForeign => Get("FtpsvcForeign");
    public static string NotInstalled => Get("NotInstalled");
    public static string Closed => Get("Closed");
    public static string NewPasswordGenerated => Get("NewPasswordGenerated");
    public static string UsernameCopied => Get("UsernameCopied");
    public static string PasswordCopied => Get("PasswordCopied");
    public static string UnexpectedError => Get("UnexpectedError");
    public static string AtTime => Get("AtTime");
    public static string About => Get("About");
    public static string Version => Get("Version");
    public static string License => Get("License");
    public static string ReadLicense => Get("ReadLicense");
    public static string Close => Get("Close");
    public static string AboutDescription => Get("AboutDescription");
    public static string AboutCredits => Get("AboutCredits");
    public static string UpdateAvailable => Get("UpdateAvailable");
    public static string Download => Get("Download");
}
