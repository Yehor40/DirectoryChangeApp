using DirectoryChangeApp.Data;
using DirectoryChangeApp.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectoryChangeApp.Repository;

public class SqliteStateRepository : IStateRepository
{
    private readonly AppDbContext _dbContext;

    public SqliteStateRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Dictionary<string, FileItem> LoadState(string directoryPath)
    {
        var normalizedPath = GetStateKey(directoryPath);

        var directoryState = _dbContext.DirectoryStates
            .Include(d => d.FileItems)
            .FirstOrDefault(d => d.DirectoryPath == normalizedPath);

        if (directoryState == null)
        {
            return new Dictionary<string, FileItem>();
        }

        var stateDict = new Dictionary<string, FileItem>();
        foreach (var item in directoryState.FileItems)
        {
            stateDict[item.RelativePath] = new FileItem
            {
                Hash = item.Hash,
                Version = item.Version,
                IsDirectory = item.IsDirectory
            };
        }

        return stateDict;
    }

    public void SaveState(string directoryPath, Dictionary<string, FileItem> state)
    {
        var normalizedPath = GetStateKey(directoryPath);

        var directoryState = _dbContext.DirectoryStates
            .Include(d => d.FileItems)
            .FirstOrDefault(d => d.DirectoryPath == normalizedPath);

        if (directoryState == null)
        {
            directoryState = new DirectoryState
            {
                DirectoryPath = normalizedPath
            };
            _dbContext.DirectoryStates.Add(directoryState);
        }
        else
        {
            // Remove existing items and add new ones (or update them)
            // The simplest approach is to clear and re-add to match the Dictionary replacement behavior.
            _dbContext.FileItems.RemoveRange(directoryState.FileItems);
        }

        foreach (var kvp in state)
        {
            directoryState.FileItems.Add(new FileItemEntity
            {
                RelativePath = kvp.Key,
                Hash = kvp.Value.Hash,
                Version = kvp.Value.Version,
                IsDirectory = kvp.Value.IsDirectory
            });
        }

        _dbContext.SaveChanges();
    }

    private static string GetStateKey(string directoryPath)
    {
        return Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
