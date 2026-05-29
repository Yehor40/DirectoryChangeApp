namespace DirectoryChangeApp.Data.Models;

public class DirectorySnapshot
{
    public int FormatVersion { get; set; } = 1;
    public string RootPath { get; set; } = string.Empty;
    public DateTime LastScanTimeUtc { get; set; }
    public Dictionary<string, FileSnapshotItem> Files { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> Directories { get; set; } = new(StringComparer.Ordinal);
}
