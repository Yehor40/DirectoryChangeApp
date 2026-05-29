namespace DirectoryChangeApp.Repository;

public class JsonStateRepository : IStateRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public DirectorySnapshot? LoadSnapshot(string directoryPath)
    {
        var filePath = GetSnapshotFilePath(directoryPath);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DirectorySnapshot>(json);
        }
        catch (Exception)
        {
            // If the snapshot is corrupted or unreadable, return null to fall back to clean scan
            return null;
        }
    }

    public void SaveSnapshot(string directoryPath, DirectorySnapshot snapshot)
    {
        var filePath = GetSnapshotFilePath(directoryPath);
        var tempPath = filePath + ".tmp";

        try
        {
            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            
            // Atomic write: Write to a temporary file, flush it, then replace the original
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempPath, filePath);
        }
        catch (Exception ex)
        {
            // Clean up temp file if something failed
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
            throw new IOException($"Failed to write snapshot atomically for directory: {directoryPath}", ex);
        }
    }

    private static string GetSnapshotFilePath(string directoryPath)
    {
        var normalizedPath = Path.GetFullPath(directoryPath)
            .Replace('\\', '/')
            .TrimEnd('/');

        if (OperatingSystem.IsWindows())
        {
            normalizedPath = normalizedPath.ToLowerInvariant();
        }

        byte[] pathBytes = Encoding.UTF8.GetBytes(normalizedPath);
        byte[] hashBytes = SHA256.HashData(pathBytes);
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Save snapshots in App_Data/snapshots relative to application directory
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var snapshotsDir = Path.Combine(baseDir, "App_Data", "snapshots");

        if (!Directory.Exists(snapshotsDir))
        {
            Directory.CreateDirectory(snapshotsDir);
        }

        return Path.Combine(snapshotsDir, $"{hashString}.json");
    }
}
