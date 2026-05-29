namespace DirectoryChangeApp.Services;

public class DirectoryAnalyzerService(
    IStateRepository stateRepository,
    IRuntimePathResolver runtimePathResolver,
    ILogger<DirectoryAnalyzerService> logger) : IDirectoryAnalyzerService
{
    public AnalysisReport Analyze(string directoryPath)
    {
        logger.LogInformation("Starting file analysis for directory: {Path}", directoryPath);
        var runtimePath = runtimePathResolver.Resolve(directoryPath);

        if (string.IsNullOrWhiteSpace(runtimePath) || !Directory.Exists(runtimePath))
        {
            throw new ArgumentException("Bad or not a valid directory path in current runtime.");
        }

        var currentState = stateRepository.LoadState(directoryPath);
        var newState = new Dictionary<string, FileItem>();
        var report = new AnalysisReport();

        var allFiles = new List<string>();
        EnumerateFiles(runtimePath, allFiles, isRoot: true);

        var potentialAddedFiles = ProcessFiles(runtimePath, allFiles, currentState, newState, report);
        ProcessDeletedItems(currentState, newState, report, potentialAddedFiles);

        stateRepository.SaveState(directoryPath, newState);

        logger.LogInformation(
            "File analysis finished. Changes - New: {Added}, Modified: {Mod}, Deleted: {Del}",
            report.Added.Count, report.Modified.Count, report.Deleted.Count);
        return report;
    }

    private List<string> ProcessFiles(
        string basePath,
        IEnumerable<string> files,
        Dictionary<string, FileItem> currentState,
        Dictionary<string, FileItem> newState,
        AnalysisReport report)
    {
        var potentialAddedFiles = new List<string>();

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

            if (currentState.TryGetValue(relativePath, out var oldState))
            {
                if (oldState.Hash == currentHash)
                {
                    newState[relativePath] = oldState;
                }
                else
                {
                    var updatedItem = new FileItem { Hash = currentHash, Version = oldState.Version + 1 };
                    newState[relativePath] = updatedItem;
                    report.Modified.Add($"{relativePath} (Version {updatedItem.Version})");
                }
            }
            else
            {
                newState[relativePath] = new FileItem { Hash = currentHash, Version = 1 };
                potentialAddedFiles.Add(relativePath);
            }
        }

        return potentialAddedFiles;
    }

    private void ProcessDeletedItems(
        Dictionary<string, FileItem> currentState,
        Dictionary<string, FileItem> newState,
        AnalysisReport report,
        List<string> potentialAddedFiles)
    {
        var potentialDeletedFiles = currentState.Keys
            .Where(path => !newState.ContainsKey(path))
            .ToList();

        var actuallyAddedFiles = new List<string>();
        foreach (var addedFile in potentialAddedFiles)
        {
            var addedHash = newState[addedFile].Hash;
            var matchedDeletedFiles = potentialDeletedFiles
                .Where(path => currentState[path].Hash == addedHash)
                .ToList();
            var matchedAddedFilesCount = potentialAddedFiles.Count(path => newState[path].Hash == addedHash);

            if (matchedDeletedFiles.Count == 1 && matchedAddedFilesCount == 1)
            {
                var matchedDeletedFile = matchedDeletedFiles.First();
                potentialDeletedFiles.Remove(matchedDeletedFile);
                var oldVersion = currentState[matchedDeletedFile].Version;
                newState[addedFile].Version = oldVersion;
                report.Modified.Add($"{addedFile} (Version {oldVersion})");
            }
            else
            {
                actuallyAddedFiles.Add(addedFile);
            }
        }

        foreach (var added in actuallyAddedFiles)
        {
            report.Added.Add($"{added} (Version 1)");
        }

        foreach (var deleted in potentialDeletedFiles)
        {
            report.Deleted.Add($"{deleted} (Last version {currentState[deleted].Version})");
        }
    }

    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private void EnumerateFiles(string directoryPath, List<string> files, bool isRoot = false)
    {
        string[] childFiles;
        string[] childDirectories;

        try
        {
            childFiles = Directory.GetFiles(directoryPath)
                .Where(f => !Path.GetFileName(f).StartsWith('.'))
                .ToArray();
            childDirectories = Directory.GetDirectories(directoryPath)
                .Where(d => !Path.GetFileName(d).StartsWith('.'))
                .ToArray();
        }
        catch (UnauthorizedAccessException ex)
        {
            if (isRoot)
            {
                throw;
            }

            logger.LogWarning(ex, "Skipping inaccessible directory: {Path}", directoryPath);
            return;
        }
        catch (IOException ex)
        {
            if (isRoot)
            {
                throw;
            }

            logger.LogWarning(ex, "Skipping unreadable directory: {Path}", directoryPath);
            return;
        }

        files.AddRange(childFiles);

        foreach (var childDirectory in childDirectories)
        {
            EnumerateFiles(childDirectory, files);
        }
    }
}
