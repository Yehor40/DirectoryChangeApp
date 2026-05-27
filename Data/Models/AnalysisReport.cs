namespace DirectoryChangeApp.Data;

public class AnalysisReport
{
    public List<string> Added { get; set; } = new();
    public List<string> Modified { get; set; } = new();
    public List<string> Deleted { get; set; } = new();
}