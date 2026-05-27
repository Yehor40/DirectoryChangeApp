namespace DirectoryChangeApp.Data;

public record AnalysisReportDto
{
    public List<string> Added { get; set; } = new();
    public List<string> Modified { get; set; } = new();
    public List<string> Deleted { get; set; } = new();
}