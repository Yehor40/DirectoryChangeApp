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
                [testFileName] = new() { Hash = "old-hash", Version = 2, IsDirectory = false }
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
                ["removed.txt"] = new() { Hash = "old-hash", Version = 4, IsDirectory = false }
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
    public void SqliteStateRepository_LoadState_WhenStateWasSaved_ShouldReturnSavedState()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var repository = new SqliteStateRepository(context);
        var expectedState = new Dictionary<string, FileItem>
        {
            ["tracked.txt"] = new() { Hash = "saved-hash", Version = 5, IsDirectory = false }
        };

        repository.SaveState(_tempDirPath, expectedState);

        var state = repository.LoadState(_tempDirPath);

        Assert.True(state.ContainsKey("tracked.txt"));
        Assert.Equal("saved-hash", state["tracked.txt"].Hash);
        Assert.Equal(5, state["tracked.txt"].Version);
        Assert.False(state["tracked.txt"].IsDirectory);

        connection.Dispose();
    }

    [Fact]
    public void SqliteStateRepository_LoadState_WhenDifferentDirectoryWasSaved_ShouldReturnEmptyState()
    {
        var otherDirectoryPath = Path.Combine(_tempDirPath, "other");
        Directory.CreateDirectory(otherDirectoryPath);

        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        var repository = new SqliteStateRepository(context);
        var otherState = new Dictionary<string, FileItem>
        {
            ["project-file.txt"] = new() { Hash = "old-hash", Version = 1, IsDirectory = false }
        };

        repository.SaveState(otherDirectoryPath, otherState);

        var state = repository.LoadState(_tempDirPath);

        Assert.Empty(state);

        connection.Dispose();
    }

    [Fact]
    public void Analyze_WhenFileReplacedByDirectory_ShouldReportDeletedAndAdded()
    {
        string testFileName = "test_item";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        Directory.CreateDirectory(testFilePath);

        _stateRepositoryMock
            .Setup(repo => repo.LoadState(_tempDirPath))
            .Returns(new Dictionary<string, FileItem>
            {
                [testFileName] = new() { Hash = "old-hash", Version = 2, IsDirectory = false }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.NotNull(report);
        Assert.Single(report.Added);
        Assert.Contains("[Catalog] test_item", report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.Deleted);
        Assert.Contains("test_item (Last version 2)", report.Deleted);
    }

    [Fact]
    public void Analyze_WhenDirectoryReplacedByFile_ShouldReportDeletedAndAdded()
    {
        string testFileName = "test_item";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        File.WriteAllText(testFilePath, "New file contents");

        _stateRepositoryMock
            .Setup(repo => repo.LoadState(_tempDirPath))
            .Returns(new Dictionary<string, FileItem>
            {
                [testFileName] = new() { IsDirectory = true, Version = 1 }
            });

        var report = _service.Analyze(_tempDirPath);

        Assert.NotNull(report);
        Assert.Single(report.Added);
        Assert.Contains("test_item (Version 1)", report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.Deleted);
        Assert.Contains("[Catalog] test_item (Last version 1)", report.Deleted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirPath))
        {
            Directory.Delete(_tempDirPath, true);
        }
    }
}
