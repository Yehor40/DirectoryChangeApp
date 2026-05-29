namespace DirectoryChangeApp.Data;

public class AnalysisReport
{
    public List<string> Added { get; set; } = new();
    public List<string> Modified { get; set; } = new();
    public List<string> MetadataChanged { get; set; } = new();
    public List<string> Removed { get; set; } = new();
    public List<string> RemovedDirectories { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
    public List<string> SkippedDirectories { get; set; } = new();
    public List<string> UnstableFiles { get; set; } = new();
    public bool IsPartial { get; set; }
    public double ScanDurationMs { get; set; }
    public DateTime ScanTimestampUtc { get; set; }
}
