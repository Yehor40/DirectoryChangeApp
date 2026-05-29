namespace DirectoryChangeApp.Data.Models;

public class FileSnapshotItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public int Version { get; set; }
}
