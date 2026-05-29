using System.Collections.Concurrent;

namespace DirectoryChangeApp.Services;

public class DirectoryAnalyzerService(
    IStateRepository stateRepository,
    IRuntimePathResolver runtimePathResolver,
    ILogger<DirectoryAnalyzerService> logger) : IDirectoryAnalyzerService
{
    public async Task<AnalysisReport> AnalyzeAsync(string directoryPath)
    {
        logger.LogInformation("Starting parallel analysis of directory: {Path}", directoryPath);
        var runtimePath = runtimePathResolver.Resolve(directoryPath);

        if (string.IsNullOrWhiteSpace(runtimePath) || !Directory.Exists(runtimePath))
        {
            throw new ArgumentException("Bad or not a valid catalog path in current runtime.");
        }

        var currentState = stateRepository.LoadState(directoryPath);
        var newState = new Dictionary<string, FileItem>();
        var report = new AnalysisReport();

        // Thread-safe collections for parallel disk scanning
        var allFiles = new ConcurrentBag<string>();

        // Launch parallel async directory tree enumeration
        await EnumerateFilesAsync(runtimePath, allFiles);

        logger.LogDebug("Disk scan complete. Found {FileCount} files to hash.", allFiles.Count);

        // Sequential processing phase (state dictionaries are not thread-safe)
        var potentialAddedFiles = ProcessFiles(runtimePath, allFiles, currentState, newState, report);
        ProcessDeletedItems(currentState, newState, report, potentialAddedFiles);

        stateRepository.SaveState(directoryPath, newState);

        logger.LogInformation(
            "Analysis finished. Changes — New: {Added}, Modified: {Mod}, Deleted: {Del}",
            report.Added.Count, report.Modified.Count, report.Deleted.Count);

        return report;
    }

    // ── File processing (sequential, after scan) ────────────────────────

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

    // ── Deletion & rename detection ─────────────────────────────────────

    private void ProcessDeletedItems(
        Dictionary<string, FileItem> currentState,
        Dictionary<string, FileItem> newState,
        AnalysisReport report,
        List<string> potentialAddedFiles)
    {
        var potentialDeletedFiles = currentState.Keys
            .Where(path => !newState.ContainsKey(path))
            .ToList();

        // Strict 1-to-1 rename detection by hash
        var actuallyAddedFiles = new List<string>();
        foreach (var addedFile in potentialAddedFiles)
        {
            var addedHash = newState[addedFile].Hash;
            var matchedDeletedFiles = potentialDeletedFiles
                .Where(path => currentState[path].Hash == addedHash)
                .ToList();
            var matchedAddedFilesCount = potentialAddedFiles
                .Count(path => newState[path].Hash == addedHash);

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

    // ── Hashing ─────────────────────────────────────────────────────────

    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    // ── Parallel async directory tree enumeration ───────────────────────

    private async Task EnumerateFilesAsync(
        string directoryPath,
        ConcurrentBag<string> collectedFiles)
    {
        try
        {
            // Collect files in the current directory (skip hidden/dot-files)
            foreach (var file in Directory.EnumerateFiles(directoryPath))
            {
                if (!Path.GetFileName(file).StartsWith('.'))
                {
                    collectedFiles.Add(file);
                }
            }

            // Recurse into subdirectories in parallel (skip hidden/dot-dirs)
            var subDirs = Directory.EnumerateDirectories(directoryPath)
                .Where(d => !Path.GetFileName(d).StartsWith('.'));

            var tasks = subDirs.Select(subDir =>
                Task.Run(() => EnumerateFilesAsync(subDir, collectedFiles)));

            await Task.WhenAll(tasks);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Skipping inaccessible directory: {Path}. Reason: {Message}",
                directoryPath, ex.Message);
        }
        catch (IOException ex)
        {
            logger.LogWarning("Skipping unreadable directory: {Path}. Reason: {Message}",
                directoryPath, ex.Message);
        }
    }
}
