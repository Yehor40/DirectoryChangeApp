namespace DirectoryChangeApp.Services;

public class RuntimePathResolver(IOptions<PathMappingOptions> options) : IRuntimePathResolver
{
    private readonly PathMappingOptions _options = options.Value;

    public string Resolve(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return inputPath;
        }

        var hostPrefix = TrimTrailingSeparators(_options.HostPathPrefix);
        var containerPrefix = TrimTrailingSeparators(_options.ContainerPathPrefix);

        if (string.IsNullOrWhiteSpace(hostPrefix) || string.IsNullOrWhiteSpace(containerPrefix))
        {
            return inputPath;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!inputPath.StartsWith(hostPrefix, comparison))
        {
            return inputPath;
        }

        var suffix = inputPath[hostPrefix.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return containerPrefix;
        }

        var pathSegments = suffix
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        return Path.Combine([containerPrefix, .. pathSegments]);
    }

    private static string TrimTrailingSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
