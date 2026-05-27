namespace DirectoryChangeApp.Data;

public record AnalyzeRequestDto
{
    public string DirectoryPath { get; set; } = string.Empty;
}