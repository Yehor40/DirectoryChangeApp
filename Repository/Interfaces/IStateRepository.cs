namespace DirectoryChangeApp.Repository;

public interface IStateRepository
{
    Dictionary<string, FileItem> LoadState();
    void SaveState(Dictionary<string, FileItem> state);
}