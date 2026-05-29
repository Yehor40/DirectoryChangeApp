namespace DirectoryChangeApp.Data.Mappings;

public static class AnalysisMapper
{
    public static AnalysisReportDto ToResponseDto(this AnalysisReport result)
    {
        return new AnalysisReportDto
        {
            Added = result.Added,
            Modified = result.Modified,
            MetadataChanged = result.MetadataChanged,
            Removed = result.Removed,
            RemovedDirectories = result.RemovedDirectories,
            SkippedFiles = result.SkippedFiles,
            SkippedDirectories = result.SkippedDirectories,
            UnstableFiles = result.UnstableFiles,
            IsPartial = result.IsPartial,
            ScanDurationMs = result.ScanDurationMs,
            ScanTimestampUtc = result.ScanTimestampUtc
        };
    }
}
