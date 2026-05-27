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

    [Fact]
    public void Analyze_WhenFileWasChanged_ShouldAddToModifiedListWithIncrementedVersion()
    {
        string testFileName = "test.txt";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        File.WriteAllText(testFilePath, "Changed file contents");

        _stateRepositoryMock
            .Setup(repo => repo.LoadState())
            .Returns(new Dictionary<string, FileItem>
            {
                [testFileName] = new() { Hash = "old-hash", Version = 2, IsDirectory = false }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Single(report.Modified);
        Assert.Contains("test.txt (Version 3)", report.Modified);
        Assert.Empty(report.Deleted);

        _stateRepositoryMock.Verify(
            repo => repo.SaveState(It.Is<Dictionary<string, FileItem>>(d =>
                d.ContainsKey(testFileName) && d[testFileName].Version == 3)),
            Times.Once
        );
    }

    [Fact]
    public void Analyze_WhenFileWasRemoved_ShouldAddToDeletedList()
    {
        _stateRepositoryMock
            .Setup(repo => repo.LoadState())
            .Returns(new Dictionary<string, FileItem>
            {
                ["removed.txt"] = new() { Hash = "old-hash", Version = 4, IsDirectory = false }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.Deleted);
        Assert.Contains("removed.txt (Last version 4)", report.Deleted);

        _stateRepositoryMock.Verify(
            repo => repo.SaveState(It.Is<Dictionary<string, FileItem>>(d => !d.ContainsKey("removed.txt"))),
            Times.Once
        );
    }

    [Fact]
    public void JsonStateRepository_LoadState_WhenStateFileExists_ShouldReturnSavedState()
    {
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_tempDirPath);
            var repository = new JsonStateRepository();
            var expectedState = new Dictionary<string, FileItem>
            {
                ["tracked.txt"] = new() { Hash = "saved-hash", Version = 5, IsDirectory = false }
            };

            repository.SaveState(expectedState);

            var state = repository.LoadState();

            Assert.True(state.ContainsKey("tracked.txt"));
            Assert.Equal("saved-hash", state["tracked.txt"].Hash);
            Assert.Equal(5, state["tracked.txt"].Version);
            Assert.False(state["tracked.txt"].IsDirectory);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirPath))
        {
            Directory.Delete(_tempDirPath, true);
        }
    }
}
