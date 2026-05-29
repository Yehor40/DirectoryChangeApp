using Moq;
using Microsoft.Extensions.Options;
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
        var pathResolver = new RuntimePathResolver(Options.Create(new PathMappingOptions()));
        _service = new DirectoryAnalyzerService(_stateRepositoryMock.Object, pathResolver, _loggerMock.Object);
    }
    
    [Fact]
    public void Analyze_WhenFileIsNew_ShouldAddToAddedListWithVersion1()
    {
      
        string testFileName = "test.txt";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        File.WriteAllText(testFilePath, "Test file contents");

        _stateRepositoryMock
        .Setup(repo => repo.LoadState(_tempDirPath))
        .Returns(new Dictionary<string, FileItem>());

        //
        var report = _service.Analyze(_tempDirPath);

       
        Assert.NotNull(report);
        Assert.Single(report.Added); 
        Assert.Contains("test.txt (Version 1)", report.Added);
        Assert.Empty(report.Modified);
        Assert.Empty(report.Deleted);

        _stateRepositoryMock.Verify(
            repo => repo.SaveState(_tempDirPath, It.Is<Dictionary<string, FileItem>>(d => d.ContainsKey("test.txt"))), 
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
            .Setup(repo => repo.LoadState(_tempDirPath))
            .Returns(new Dictionary<string, FileItem>
            {
                [testFileName] = new() { Hash = "old-hash", Version = 2 }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Single(report.Modified);
        Assert.Contains("test.txt (Version 3)", report.Modified);
        Assert.Empty(report.Deleted);

        _stateRepositoryMock.Verify(
            repo => repo.SaveState(_tempDirPath, It.Is<Dictionary<string, FileItem>>(d =>
                d.ContainsKey(testFileName) && d[testFileName].Version == 3)),
            Times.Once
        );
    }

    [Fact]
    public void Analyze_WhenFileWasRemoved_ShouldAddToDeletedList()
    {
        _stateRepositoryMock
            .Setup(repo => repo.LoadState(_tempDirPath))
            .Returns(new Dictionary<string, FileItem>
            {
                ["removed.txt"] = new() { Hash = "old-hash", Version = 4 }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.Deleted);
        Assert.Contains("removed.txt (Last version 4)", report.Deleted);

        _stateRepositoryMock.Verify(
            repo => repo.SaveState(_tempDirPath, It.Is<Dictionary<string, FileItem>>(d => !d.ContainsKey("removed.txt"))),
            Times.Once
        );
    }

    [Fact]
    public void JsonStateRepository_LoadState_WhenStateWasSaved_ShouldReturnSavedState()
    {
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_tempDirPath);
            var repository = new JsonStateRepository();
            var expectedState = new Dictionary<string, FileItem>
            {
                ["tracked.txt"] = new() { Hash = "saved-hash", Version = 5 }
            };

            repository.SaveState(_tempDirPath, expectedState);

            var state = repository.LoadState(_tempDirPath);

            Assert.True(state.ContainsKey("tracked.txt"));
            Assert.Equal("saved-hash", state["tracked.txt"].Hash);
            Assert.Equal(5, state["tracked.txt"].Version);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    [Fact]
    public void JsonStateRepository_LoadState_WhenDifferentDirectoryWasSaved_ShouldReturnEmptyState()
    {
        var otherDirectoryPath = Path.Combine(_tempDirPath, "other");
        Directory.CreateDirectory(otherDirectoryPath);
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_tempDirPath);
            var repository = new JsonStateRepository();
            var otherState = new Dictionary<string, FileItem>
            {
                ["project-file.txt"] = new() { Hash = "old-hash", Version = 1 }
            };

            repository.SaveState(otherDirectoryPath, otherState);

            var state = repository.LoadState(_tempDirPath);

            Assert.Empty(state);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    [Fact]
    public void Analyze_WhenFileInSubfolderIsNew_ShouldReportRelativePath()
    {
        var subfolder = Path.Combine(_tempDirPath, "docs");
        Directory.CreateDirectory(subfolder);
        File.WriteAllText(Path.Combine(subfolder, "readme.txt"), "hello");

        _stateRepositoryMock
            .Setup(repo => repo.LoadState(_tempDirPath))
            .Returns(new Dictionary<string, FileItem>());

        var report = _service.Analyze(_tempDirPath);

        Assert.Single(report.Added);
        Assert.Contains("docs/readme.txt (Version 1)", report.Added);
    }

    [Fact]
    public void Analyze_WhenFileReplacedByDirectory_ShouldReportFileAsDeleted()
    {
        const string testFileName = "test_item";
        Directory.CreateDirectory(Path.Combine(_tempDirPath, testFileName));

        _stateRepositoryMock
            .Setup(repo => repo.LoadState(_tempDirPath))
            .Returns(new Dictionary<string, FileItem>
            {
                [testFileName] = new() { Hash = "old-hash", Version = 2 }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.Empty(report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.Deleted);
        Assert.Contains("test_item (Last version 2)", report.Deleted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirPath))
        {
            Directory.Delete(_tempDirPath, true);
        }
    }
}
