namespace DirectoryChangeApp.Data.Models;

public class DirectoryState
{
    public int Id { get; set; }
    public required string DirectoryPath { get; set; }
    public List<FileItemEntity> FileItems { get; set; } = new();
}
