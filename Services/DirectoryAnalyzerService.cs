namespace DirectoryChangeApp.Services;

public class DirectoryAnalyzerService(
    IStateRepository stateRepository,
    IRuntimePathResolver runtimePathResolver,
    ILogger<DirectoryAnalyzerService> logger) : IDirectoryAnalyzerService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<AnalysisReport> AnalyzeAsync(string directoryPath)
    {
        var timestampStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        logger.LogInformation("[AUDIT] [{Timestamp}] Starting file analysis for directory: {Path}", timestampStr, directoryPath);
        
        var runtimePath = runtimePathResolver.Resolve(directoryPath);
        if (!string.IsNullOrWhiteSpace(runtimePath))
        {
            runtimePath = Path.GetFullPath(runtimePath);
        }

        if (string.IsNullOrWhiteSpace(runtimePath) || !Directory.Exists(runtimePath))
        {
            throw new ArgumentException("Bad or not a valid directory path in current runtime.");
        }

        var rootKey = NormalizeRootKey(runtimePath);
        var semaphore = Locks.GetOrAdd(rootKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        
        var totalSw = Stopwatch.StartNew();
        try
        {
            // Measure loading previous snapshot
            var loadSw = Stopwatch.StartNew();
            var oldSnapshot = stateRepository.LoadSnapshot(directoryPath) ?? new DirectorySnapshot
            {
                RootPath = directoryPath
            };
            loadSw.Stop();
            
            logger.LogInformation(
                "[AUDIT] [{Timestamp}] Loaded previous snapshot in {DurationMs:F2} ms. Contains {FileCount} files, {DirCount} directories.",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                loadSw.Elapsed.TotalMilliseconds,
                oldSnapshot.Files.Count,
                oldSnapshot.Directories.Count);

            var scannedFiles = new List<string>();
            var scannedDirectories = new HashSet<string>(StringComparer.Ordinal);
            var skippedFiles = new List<string>();
            var skippedDirectories = new List<string>();
            var skippedDirectoryPaths = new HashSet<string>(StringComparer.Ordinal);
            bool isPartial = false;

            // Step 1: Recursive scan (traversal)
            var scanSw = Stopwatch.StartNew();
            ScanDirectory(
                runtimePath,
                runtimePath,
                scannedFiles,
                scannedDirectories,
                skippedFiles,
                skippedDirectories,
                skippedDirectoryPaths,
                ref isPartial);
            scanSw.Stop();

            logger.LogInformation(
                "[AUDIT] [{Timestamp}] Traversed directory tree in {DurationMs:F2} ms. Discovered {FileCount} files, {DirCount} folders.",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                scanSw.Elapsed.TotalMilliseconds,
                scannedFiles.Count,
                scannedDirectories.Count);

            // Step 2: Bounded Parallel Hashing & Metadata extraction
            var hashSw = Stopwatch.StartNew();
            var fileScanResults = new ConcurrentBag<FileScanResult>();
            var maxDegree = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };

            await Parallel.ForEachAsync(scannedFiles, parallelOptions, async (filePath, cancellationToken) =>
            {
                var relativePath = GetPortableRelativePath(runtimePath, filePath);

                try
                {
                    // Read metadata before hashing
                    var fileInfoBefore = new FileInfo(filePath);
                    var lengthBefore = fileInfoBefore.Length;
                    var writeTimeBefore = fileInfoBefore.LastWriteTimeUtc;

                    // Compute SHA-256 hash (hashing large files on separate threadpool threads)
                    string hash = await Task.Run(() => ComputeFileHash(filePath), cancellationToken);

                    // Read metadata after hashing
                    var fileInfoAfter = new FileInfo(filePath);
                    var lengthAfter = fileInfoAfter.Length;
                    var writeTimeAfter = fileInfoAfter.LastWriteTimeUtc;

                    // Check for instability (modification during read)
                    if (lengthBefore != lengthAfter || writeTimeBefore != writeTimeAfter)
                    {
                        fileScanResults.Add(new FileScanResult
                        {
                            RelativePath = relativePath,
                            IsUnstable = true,
                            UnstableReason = "Modified during hashing"
                        });
                    }
                    else
                    {
                        fileScanResults.Add(new FileScanResult
                        {
                            RelativePath = relativePath,
                            Hash = hash,
                            Length = lengthAfter,
                            LastWriteTimeUtc = writeTimeAfter
                        });
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    fileScanResults.Add(new FileScanResult
                    {
                        RelativePath = relativePath,
                        IsSkipped = true,
                        SkipReason = ex.Message
                    });
                }
            });
            hashSw.Stop();

            logger.LogInformation(
                "[AUDIT] [{Timestamp}] Completed parallel hashing of {FileCount} files in {DurationMs:F2} ms (MaxDegreeOfParallelism={MaxDegree}).",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                fileScanResults.Count(f => !f.IsSkipped && !f.IsUnstable),
                hashSw.Elapsed.TotalMilliseconds,
                maxDegree);

            // Step 3: Comparison logic
            var compareSw = Stopwatch.StartNew();
            var report = new AnalysisReport();
            var newStateFiles = new Dictionary<string, FileSnapshotItem>(StringComparer.Ordinal);
            var newStateDirs = new HashSet<string>(scannedDirectories, StringComparer.Ordinal);

            // Process scanned files
            foreach (var result in fileScanResults.OrderBy(r => r.RelativePath))
            {
                ProcessScannedFile(result, oldSnapshot, newStateFiles, report, ref isPartial);
            }

            // Process deleted files
            var scannedFilePaths = new HashSet<string>(fileScanResults.Select(r => r.RelativePath), StringComparer.Ordinal);
            foreach (var oldFile in oldSnapshot.Files.Values)
            {
                if (!scannedFilePaths.Contains(oldFile.RelativePath))
                {
                    if (IsInSkippedDirectory(oldFile.RelativePath, skippedDirectoryPaths))
                    {
                        newStateFiles[oldFile.RelativePath] = oldFile;
                        report.SkippedFiles.Add($"{oldFile.RelativePath} (Reason: Parent directory skipped due to access errors)");
                    }
                    else
                    {
                        report.Removed.Add($"{oldFile.RelativePath} (Last version {oldFile.Version})");
                    }
                }
            }

            // Process deleted directories
            foreach (var oldDir in oldSnapshot.Directories)
            {
                if (!scannedDirectories.Contains(oldDir))
                {
                    if (IsInSkippedDirectory(oldDir, skippedDirectoryPaths))
                    {
                        newStateDirs.Add(oldDir);
                        skippedDirectories.Add($"{oldDir} (Reason: Parent directory skipped due to access errors)");
                    }
                    else
                    {
                        report.RemovedDirectories.Add(oldDir);
                    }
                }
            }

            // Populate skipped directories in report
            foreach (var skippedDir in skippedDirectories.OrderBy(d => d))
            {
                report.SkippedDirectories.Add(skippedDir);
            }

            // Populate skipped files from recursive scan phase
            foreach (var skippedFile in skippedFiles.OrderBy(f => f))
            {
                report.SkippedFiles.Add(skippedFile);
            }

            report.IsPartial = isPartial || skippedFiles.Count > 0 || skippedDirectories.Count > 0;
            DetectFileRenames(report, oldSnapshot, newStateFiles);
            compareSw.Stop();

            logger.LogInformation(
                "[AUDIT] [{Timestamp}] Completed comparison analysis in {DurationMs:F2} ms.",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                compareSw.Elapsed.TotalMilliseconds);

            // Step 4: Save new snapshot
            var saveSw = Stopwatch.StartNew();
            var newSnapshot = new DirectorySnapshot
            {
                FormatVersion = 1,
                RootPath = directoryPath,
                LastScanTimeUtc = DateTime.UtcNow,
                Files = newStateFiles,
                Directories = newStateDirs
            };
            stateRepository.SaveSnapshot(directoryPath, newSnapshot);
            saveSw.Stop();

            logger.LogInformation(
                "[AUDIT] [{Timestamp}] Saved updated snapshot atomically to disk in {DurationMs:F2} ms.",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                saveSw.Elapsed.TotalMilliseconds);

            totalSw.Stop();
            report.ScanDurationMs = totalSw.Elapsed.TotalMilliseconds;
            report.ScanTimestampUtc = newSnapshot.LastScanTimeUtc;

            logger.LogInformation(
                "[AUDIT] [{Timestamp}] File analysis completed successfully in {DurationMs:F2} ms. Results - Added: {Added}, Modified: {Mod}, Touched: {Touch}, Removed: {Rem}, Partial: {Partial}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                report.ScanDurationMs,
                report.Added.Count,
                report.Modified.Count,
                report.MetadataChanged.Count,
                report.Removed.Count,
                report.IsPartial);

            return report;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private void ProcessScannedFile(
        FileScanResult result,
        DirectorySnapshot oldSnapshot,
        Dictionary<string, FileSnapshotItem> newStateFiles,
        AnalysisReport report,
        ref bool isPartial)
    {
        if (result.IsSkipped)
        {
            HandleSkippedFile(result, oldSnapshot, newStateFiles, report);
            isPartial = true;
            return;
        }

        if (result.IsUnstable)
        {
            HandleUnstableFile(result, oldSnapshot, newStateFiles, report);
            isPartial = true;
            return;
        }

        HandleSuccessfullyHashedFile(result, oldSnapshot, newStateFiles, report);
    }

    private static void HandleSkippedFile(
        FileScanResult result,
        DirectorySnapshot oldSnapshot,
        Dictionary<string, FileSnapshotItem> newStateFiles,
        AnalysisReport report)
    {
        report.SkippedFiles.Add($"{result.RelativePath} (Reason: {result.SkipReason})");
        
        // Preserve old snapshot entry if it existed to avoid losing history
        if (oldSnapshot.Files.TryGetValue(result.RelativePath, out var oldItem))
        {
            newStateFiles[result.RelativePath] = oldItem;
        }
    }

    private static void HandleUnstableFile(
        FileScanResult result,
        DirectorySnapshot oldSnapshot,
        Dictionary<string, FileSnapshotItem> newStateFiles,
        AnalysisReport report)
    {
        report.UnstableFiles.Add($"{result.RelativePath} (Reason: {result.UnstableReason})");

        // Preserve old snapshot entry to avoid updating version during unstable state
        if (oldSnapshot.Files.TryGetValue(result.RelativePath, out var oldItem))
        {
            newStateFiles[result.RelativePath] = oldItem;
        }
    }

    private static void HandleSuccessfullyHashedFile(
        FileScanResult result,
        DirectorySnapshot oldSnapshot,
        Dictionary<string, FileSnapshotItem> newStateFiles,
        AnalysisReport report)
    {
        if (oldSnapshot.Files.TryGetValue(result.RelativePath, out var historicalItem))
        {
            CompareAndAddExistingFile(result, historicalItem, newStateFiles, report);
        }
        else
        {
            AddNewFile(result, newStateFiles, report);
        }
    }

    private static void CompareAndAddExistingFile(
        FileScanResult result,
        FileSnapshotItem historicalItem,
        Dictionary<string, FileSnapshotItem> newStateFiles,
        AnalysisReport report)
    {
        if (historicalItem.Hash == result.Hash)
        {
            if (historicalItem.Length != result.Length || historicalItem.LastWriteTimeUtc != result.LastWriteTimeUtc)
            {
                // Metadata changed, content identical
                var updatedItem = new FileSnapshotItem
                {
                    RelativePath = result.RelativePath,
                    Hash = result.Hash,
                    Length = result.Length,
                    LastWriteTimeUtc = result.LastWriteTimeUtc,
                    Version = historicalItem.Version
                };
                newStateFiles[result.RelativePath] = updatedItem;
                report.MetadataChanged.Add($"{result.RelativePath} (Version {historicalItem.Version})");
            }
            else
            {
                // Unchanged
                newStateFiles[result.RelativePath] = historicalItem;
            }
        }
        else
        {
            // Content changed, increment version
            var updatedItem = new FileSnapshotItem
            {
                RelativePath = result.RelativePath,
                Hash = result.Hash,
                Length = result.Length,
                LastWriteTimeUtc = result.LastWriteTimeUtc,
                Version = historicalItem.Version + 1
            };
            newStateFiles[result.RelativePath] = updatedItem;
            report.Modified.Add($"{result.RelativePath} (Version {updatedItem.Version})");
        }
    }

    private static void AddNewFile(
        FileScanResult result,
        Dictionary<string, FileSnapshotItem> newStateFiles,
        AnalysisReport report)
    {
        var newItem = new FileSnapshotItem
        {
            RelativePath = result.RelativePath,
            Hash = result.Hash,
            Length = result.Length,
            LastWriteTimeUtc = result.LastWriteTimeUtc,
            Version = 1
        };
        newStateFiles[result.RelativePath] = newItem;
        report.Added.Add($"{result.RelativePath} (Version 1)");
    }

    private void ScanDirectory(
        string currentPath,
        string basePath,
        List<string> scannedFiles,
        HashSet<string> scannedDirectories,
        List<string> skippedFiles,
        List<string> skippedDirectories,
        HashSet<string> skippedDirectoryPaths,
        ref bool isPartial)
    {
        var name = Path.GetFileName(currentPath);
        if (currentPath != basePath)
        {
            if (name.StartsWith('.') || string.Equals(name, "App_Data", StringComparison.OrdinalIgnoreCase))
            {
                return; // silently skip hidden directories and the snapshot App_Data directory
            }
        }

        if (currentPath != basePath)
        {
            var relDir = GetPortableRelativePath(basePath, currentPath);
            scannedDirectories.Add(relDir);
        }

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(currentPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            isPartial = true;
            var relDir = currentPath == basePath ? "" : GetPortableRelativePath(basePath, currentPath);
            skippedDirectories.Add($"{relDir} (Reason: {ex.Message})");
            skippedDirectoryPaths.Add(relDir);
            return;
        }

        foreach (var entry in entries)
        {
            var entryName = Path.GetFileName(entry);
            if (entryName.StartsWith('.') || string.Equals(entryName, "App_Data", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileAttributes attr;
            try
            {
                attr = File.GetAttributes(entry);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                isPartial = true;
                var relEntry = GetPortableRelativePath(basePath, entry);
                skippedFiles.Add($"{relEntry} (Reason: {ex.Message})");
                continue;
            }

            // Do not follow symlinks/reparse points to avoid directory loops
            if ((attr & FileAttributes.ReparsePoint) != 0)
            {
                isPartial = true;
                var relEntry = GetPortableRelativePath(basePath, entry);
                skippedFiles.Add($"{relEntry} (Reason: Symbolic link or reparse point skipped)");
                continue;
            }

            if ((attr & FileAttributes.Directory) != 0)
            {
                ScanDirectory(
                    entry,
                    basePath,
                    scannedFiles,
                    scannedDirectories,
                    skippedFiles,
                    skippedDirectories,
                    skippedDirectoryPaths,
                    ref isPartial);
            }
            else
            {
                scannedFiles.Add(entry);
            }
        }
    }

    private static string NormalizeRootKey(string directoryPath)
    {
        var key = Path.GetFullPath(directoryPath).Replace('\\', '/').TrimEnd('/');
        return OperatingSystem.IsWindows() ? key.ToLowerInvariant() : key;
    }

    private static string GetPortableRelativePath(string basePath, string fullPath)
    {
        var relative = Path.GetRelativePath(basePath, fullPath);
        return relative.Replace('\\', '/');
    }

    private static bool IsInSkippedDirectory(string relativePath, HashSet<string> skippedDirectoryPaths)
    {
        if (skippedDirectoryPaths.Contains("")) // Root was skipped
        {
            return true;
        }

        var parts = relativePath.Split('/');
        var currentPath = "";
        for (int i = 0; i < parts.Length - 1; i++)
        {
            currentPath = (i == 0) ? parts[0] : currentPath + "/" + parts[i];
            if (skippedDirectoryPaths.Contains(currentPath))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Treat a strict 1-to-1 add/remove pair with the same content hash as a rename (reported as modified, version preserved).
    /// </summary>
    private static void DetectFileRenames(
        AnalysisReport report,
        DirectorySnapshot oldSnapshot,
        Dictionary<string, FileSnapshotItem> newStateFiles)
    {
        var addedEntries = report.Added
            .Select(line => (Line: line, Path: ExtractReportPath(line)))
            .Where(x => newStateFiles.ContainsKey(x.Path))
            .ToList();

        var removedEntries = report.Removed
            .Select(line => (Line: line, Path: ExtractReportPath(line)))
            .Where(x => oldSnapshot.Files.ContainsKey(x.Path))
            .ToList();

        var matchedRemoved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var added in addedEntries)
        {
            var addedHash = newStateFiles[added.Path].Hash;
            var hashMatches = removedEntries
                .Where(r => !matchedRemoved.Contains(r.Path) && oldSnapshot.Files[r.Path].Hash == addedHash)
                .ToList();

            var addedWithSameHash = addedEntries.Count(a => newStateFiles[a.Path].Hash == addedHash);

            if (hashMatches.Count != 1 || addedWithSameHash != 1)
            {
                continue;
            }

            var removed = hashMatches[0];
            matchedRemoved.Add(removed.Path);

            var version = oldSnapshot.Files[removed.Path].Version;
            newStateFiles[added.Path].Version = version;

            report.Added.Remove(added.Line);
            report.Removed.Remove(removed.Line);
            report.Modified.Add($"{added.Path} (Version {version})");
        }
    }

    private static string ExtractReportPath(string reportLine)
    {
        var index = reportLine.IndexOf(" (", StringComparison.Ordinal);
        return index < 0 ? reportLine : reportLine[..index];
    }

    private class FileScanResult
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public bool IsUnstable { get; set; }
        public string? UnstableReason { get; set; }
        public bool IsSkipped { get; set; }
        public string? SkipReason { get; set; }
    }
}
