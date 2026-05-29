namespace DirectoryChangeApp.Services.Interfaces;

public interface IDirectoryAnalyzerService
{
    Task<AnalysisReport> AnalyzeAsync(string directoryPath);
}