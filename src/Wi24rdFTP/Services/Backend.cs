using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wi24rdFTP.Services;

/// <summary>Spejler state.json som backend-scriptet skriver.</summary>
public sealed class BackendState
{
    [JsonPropertyName("timestamp")]     public string Timestamp { get; set; } = "";
    [JsonPropertyName("action")]        public string Action { get; set; } = "";
    [JsonPropertyName("mode")]          public string Mode { get; set; } = "FTP";
    [JsonPropertyName("share")]         public string Share { get; set; } = "";
    [JsonPropertyName("user")]          public string User { get; set; } = "";
    [JsonPropertyName("ips")]           public string[] Ips { get; set; } = Array.Empty<string>();
    [JsonPropertyName("ftpInstalled")]  public bool FtpInstalled { get; set; }
    [JsonPropertyName("sftpInstalled")] public bool SftpInstalled { get; set; }
    [JsonPropertyName("running")]       public bool Running { get; set; }
    [JsonPropertyName("port")]          public int Port { get; set; }
    [JsonPropertyName("password")]      public string Password { get; set; } = "";
    [JsonPropertyName("lastError")]     public string LastError { get; set; } = "";
}

public enum BackendOutcome { Ok, Failed, Cancelled, ScriptMissing }

/// <summary>
/// Kører Wi24rdFtp.ps1 eleveret (UAC) og læser resultatet fra state.json.
/// GUI'en selv kører uden admin-rettigheder, så drag-and-drop fra Explorer virker.
/// </summary>
public sealed class Backend
{
    public const string RootPath  = @"C:\Wi24rdFTP";
    public const string SharePath = @"C:\Wi24rdFTP\Share";

    public static readonly string DataDir   = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Wi24rdFTP");
    public static readonly string StateFile = Path.Combine(DataDir, "state.json");
    public static readonly string LogDir    = Path.Combine(DataDir, "log");

    public string ScriptPath { get; } = Path.Combine(AppContext.BaseDirectory, "Scripts", "Wi24rdFtp.ps1");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<BackendOutcome> RunAsync(string action, string mode = "FTP", string? password = null)
    {
        if (!File.Exists(ScriptPath))
            return BackendOutcome.ScriptMissing;

        // Adgangskoden er altid kun [A-Za-z2-9], så den kan sendes sikkert som argument
        var pwArg = string.IsNullOrEmpty(password) ? "" : $" -Password {password}";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden " +
                        $"-File \"{ScriptPath}\" -Action {action} -Mode {mode}{pwArg}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return BackendOutcome.Failed;
            await p.WaitForExitAsync();
            return p.ExitCode == 0 ? BackendOutcome.Ok : BackendOutcome.Failed;
        }
        catch (Win32Exception)
        {
            // Brugeren klikkede "Nej" i UAC-dialogen
            return BackendOutcome.Cancelled;
        }
    }

    /// <summary>Sidste log-linje fra dagens backend-log (uden tidsstempel), til statusfeltet.</summary>
    public static string? ReadLastLogLine()
    {
        try
        {
            var path = Path.Combine(LogDir, $"backend-{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? last = null;
            while (sr.ReadLine() is { } line)
                if (line.StartsWith('[') && line.Length > 11 && line[9] == ']') last = line[11..];
            return last;
        }
        catch { return null; }
    }

    public BackendState? ReadState()
    {
        try
        {
            if (!File.Exists(StateFile)) return null;
            var json = File.ReadAllText(StateFile);
            return JsonSerializer.Deserialize<BackendState>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static bool ServiceExists(string name) =>
        ServiceController.GetServices().Any(s =>
            string.Equals(s.ServiceName, name, StringComparison.OrdinalIgnoreCase));

    public static bool ServiceRunning(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }
}
