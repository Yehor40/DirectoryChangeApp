namespace DirectoryChangeApp.Repository;

public interface IStateRepository
{
    Dictionary<string, FileItem> LoadState(string directoryPath);
    void SaveState(string directoryPath, Dictionary<string, FileItem> state);
}
