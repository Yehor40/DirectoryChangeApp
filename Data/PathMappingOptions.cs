namespace DirectoryChangeApp.Data;

public class PathMappingOptions
{
    public const string SectionName = "PathMapping";

    public string HostPathPrefix { get; set; } = string.Empty;

    public string ContainerPathPrefix { get; set; } = string.Empty;
}
