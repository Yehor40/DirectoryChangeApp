namespace DirectoryChangeApp.Repository;

public interface IStateRepository
{
    DirectorySnapshot? LoadSnapshot(string directoryPath);
    void SaveSnapshot(string directoryPath, DirectorySnapshot snapshot);
}
