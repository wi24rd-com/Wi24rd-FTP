using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Wi24rdFTP.Services;

public sealed record UpdateInfo(Version Version, string ReleaseUrl, string? InstallerUrl);

/// <summary>
/// Spørger GitHub Releases om nyeste version. Ét anonymt GET, ingen telemetri.
/// Returnerer null ved fejl, offline eller hvis vi allerede er nyeste.
/// </summary>
public static class UpdateChecker
{
    private const string LatestApi = "https://api.github.com/repos/wi24rd-com/Wi24rd-FTP/releases/latest";

    public static async Task<UpdateInfo?> CheckAsync(Version current)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Wi24rdFTP", current.ToString(3)));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var doc = JsonDocument.Parse(await http.GetStringAsync(LatestApi));
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
            if (latest <= current) return null;

            var releaseUrl = root.GetProperty("html_url").GetString() ?? "";
            string? installer = null;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.GetProperty("name").GetString() ?? "";
                    if (name.Equals("Wi24rdFTP-Setup.exe", StringComparison.OrdinalIgnoreCase))
                    { installer = a.GetProperty("browser_download_url").GetString(); break; }
                }
            return new UpdateInfo(latest, releaseUrl, installer);
        }
        catch
        {
            return null;
        }
    }
}
