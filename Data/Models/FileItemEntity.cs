namespace DirectoryChangeApp.Data.Models;

public class FileItemEntity
{
    public int Id { get; set; }
    public required string RelativePath { get; set; }
    public string? Hash { get; set; }
    public int Version { get; set; }
    public bool IsDirectory { get; set; }

    public int DirectoryStateId { get; set; }
    public DirectoryState? DirectoryState { get; set; }
}
