using Moq;
using Xunit;

namespace DirectoryChangeApp.Tests;

public class DirectoryAnalyzerServiceTests:IDisposable
{
    private readonly string _tempDirPath;
    private readonly Mock<IStateRepository> _stateRepositoryMock;
    private readonly DirectoryAnalyzerService _service;
    private readonly Mock<ILogger<DirectoryAnalyzerService>> _loggerMock;

    public DirectoryAnalyzerServiceTests()
    {
        _tempDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirPath);
        _stateRepositoryMock = new Mock<IStateRepository>();
        _loggerMock = new Mock<ILogger<DirectoryAnalyzerService>>();
        _service = new DirectoryAnalyzerService(_stateRepositoryMock.Object, _loggerMock.Object);
    }
    
    [Fact]
    public void Analyze_WhenFileIsNew_ShouldAddToAddedListWithVersion1()
    {
      
        string testFileName = "test.txt";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        File.WriteAllText(testFilePath, "Test file contents");

        _stateRepositoryMock
        .Setup(repo => repo.LoadState())
        .Returns(new Dictionary<string, FileItem>());

        //
        var report = _service.Analyze(_tempDirPath);

       
        Assert.NotNull(report);
        Assert.Single(report.Added); 
        Assert.Contains("test.txt (Version 1)", report.Added);
        Assert.Empty(report.Modified);
        Assert.Empty(report.Deleted);

        _stateRepositoryMock.Verify(
            repo => repo.SaveState(It.Is<Dictionary<string, FileItem>>(d => d.ContainsKey("test.txt"))), 
            Times.Once
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirPath))
        {
            Directory.Delete(_tempDirPath, true);
        }
    }
}