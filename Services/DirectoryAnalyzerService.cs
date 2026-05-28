namespace DirectoryChangeApp.Services;

public class DirectoryAnalyzerService(IStateRepository stateRepository,ILogger<DirectoryAnalyzerService> logger) : IDirectoryAnalyzerService
{

    public AnalysisReport Analyze(string directoryPath)
    {
        logger.LogInformation("Starting the analysis of the catalog: {Path}", directoryPath);
        
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            throw new ArgumentException("Bad or not a valid catalog path.");
        }

        var currentState = stateRepository.LoadState(directoryPath);
        var newState = new Dictionary<string, FileItem>();
        var report = new AnalysisReport();

        var allFiles = new List<string>();
        var allDirs = new List<string>();
        EnumerateDirectoryTree(directoryPath, allFiles, allDirs, isRoot: true);

        ProcessFiles(directoryPath, allFiles, currentState, newState, report);
        ProcessDirectories(directoryPath, allDirs, currentState, newState, report);
        ProcessDeletedItems(currentState, newState, report);

        stateRepository.SaveState(directoryPath, newState);
        
        logger.LogInformation("Analysis successfully finished. Changes - New: {Added}, Modified: {Mod}, Deleted: {Del}", 
            report.Added.Count, report.Modified.Count, report.Deleted.Count);
        return report;
    }
    
    private void ProcessFiles(string basePath, IEnumerable<string> files, Dictionary<string, FileItem> currentState, Dictionary<string, FileItem> newState, AnalysisReport report)
    {
        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(basePath, filePath);
            string currentHash;

            try
            {
                currentHash = ComputeFileHash(filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Skipping inaccessible file: {Path}", filePath);
                continue;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Skipping unreadable file: {Path}", filePath);
                continue;
            }

            if (currentState.TryGetValue(relativePath, out var oldState) && !oldState.IsDirectory)
            {
                if (oldState.Hash == currentHash)
                {
                    newState[relativePath] = oldState;
                }
                else
                {
                    var updatedItem = new FileItem { Hash = currentHash, Version = oldState.Version + 1, IsDirectory = false };
                    newState[relativePath] = updatedItem;
                    report.Modified.Add($"{relativePath} (Version {updatedItem.Version})");
                }
            }
            else
            {
                newState[relativePath] = new FileItem { Hash = currentHash, Version = 1, IsDirectory = false };
                report.Added.Add($"{relativePath} (Version 1)");
            }
        }
    }
    
    private void ProcessDirectories(string basePath, IEnumerable<string> dirs, Dictionary<string, FileItem> currentState, Dictionary<string, FileItem> newState, AnalysisReport report)
    {
        foreach (var dirPath in dirs)
        {
            var relativePath = Path.GetRelativePath(basePath, dirPath);
            newState[relativePath] = new FileItem { IsDirectory = true, Version = 1 };
            
            if (!currentState.TryGetValue(relativePath, out var oldState) || !oldState.IsDirectory)
            {
                report.Added.Add($"[Catalog] {relativePath}");
            }
        }
    }

    private void ProcessDeletedItems(Dictionary<string, FileItem> currentState, Dictionary<string, FileItem> newState, AnalysisReport report)
    {
        foreach (var oldItem in currentState)
        {
            if (!newState.TryGetValue(oldItem.Key, out var newItem) || newItem.IsDirectory != oldItem.Value.IsDirectory)
            {
                string prefix = oldItem.Value.IsDirectory ? "[Catalog] " : "";
                report.Deleted.Add($"{prefix}{oldItem.Key} (Last version {oldItem.Value.Version})");
            }
        }
    }

    private string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private void EnumerateDirectoryTree(string directoryPath, List<string> files, List<string> directories, bool isRoot = false)
    {
        string[] childFiles;
        string[] childDirectories;

        try
        {
            childFiles = Directory.GetFiles(directoryPath);
            childDirectories = Directory.GetDirectories(directoryPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (isRoot)
            {
                throw;
            }

            logger.LogWarning(ex, "Skipping inaccessible catalog: {Path}", directoryPath);
            return;
        }
        catch (IOException ex)
        {
            if (isRoot)
            {
                throw;
            }

            logger.LogWarning(ex, "Skipping unreadable catalog: {Path}", directoryPath);
            return;
        }

        files.AddRange(childFiles);

        foreach (var childDirectory in childDirectories)
        {
            directories.Add(childDirectory);
            EnumerateDirectoryTree(childDirectory, files, directories);
        }
    }
}
