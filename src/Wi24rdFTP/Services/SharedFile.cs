using System.IO;

namespace Wi24rdFTP.Services;

/// <summary>En fil eller mappe i Share-mappen, som vist i GUI'ens liste.</summary>
public sealed class SharedFile
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime Added { get; init; }

    public string Icon => IsDirectory ? "\uED25" : "\uE8A5";          // Segoe MDL2: Folder / Document
    public string SizeText => IsDirectory ? $"{FormatSize(Size)} (mappe)" : FormatSize(Size);
    public string AddedText => Added.ToString("dd-MM-yyyy HH:mm");

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} B" : $"{v:0.#} {units[i]}";
    }

    public static long DirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch { return 0; }
    }
}
