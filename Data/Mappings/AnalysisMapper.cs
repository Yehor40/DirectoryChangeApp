namespace DirectoryChangeApp.Data.Mappings;

public static class AnalysisMapper
{
    public static AnalysisReportDto ToResponseDto(this AnalysisReport result)
    {
        return new AnalysisReportDto
        {
            Added = result.Added,
            Modified = result.Modified,
            Deleted = result.Deleted
        };
    }
}