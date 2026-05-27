namespace DirectoryChangeApp.Services.Interfaces;

public interface IDirectoryAnalyzerService
{
    AnalysisReport Analyze(string directoryPath);
}