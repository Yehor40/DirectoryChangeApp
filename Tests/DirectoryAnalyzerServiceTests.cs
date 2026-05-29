using Moq;
using Xunit;

namespace DirectoryChangeApp.Tests;

public class DirectoryAnalyzerServiceTests : IDisposable
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
    public async Task AnalyzeAsync_WhenFileIsNew_ShouldAddToAddedListWithVersion1()
    {
        string testFileName = "test.txt";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        await File.WriteAllTextAsync(testFilePath, "Test file contents");

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns((DirectorySnapshot?)null);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        Assert.NotNull(report);
        Assert.Single(report.Added);
        Assert.Contains("test.txt (Version 1)", report.Added);
        Assert.Empty(report.Modified);
        Assert.Empty(report.Removed);

        _stateRepositoryMock.Verify(
            repo => repo.SaveSnapshot(_tempDirPath, It.Is<DirectorySnapshot>(s => s.Files.ContainsKey("test.txt"))),
            Times.Once
        );
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFileWasChanged_ShouldAddToModifiedListWithIncrementedVersion()
    {
        string testFileName = "test.txt";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        await File.WriteAllTextAsync(testFilePath, "Changed file contents");

        var oldSnapshot = new DirectorySnapshot
        {
            RootPath = _tempDirPath,
            LastScanTimeUtc = DateTime.UtcNow.AddMinutes(-5),
            Files = new Dictionary<string, FileSnapshotItem>
            {
                [testFileName] = new()
                {
                    RelativePath = testFileName,
                    Hash = "old-hash",
                    Version = 2,
                    Length = 10,
                    LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-10)
                }
            }
        };

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns(oldSnapshot);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Single(report.Modified);
        Assert.Contains("test.txt (Version 3)", report.Modified);
        Assert.Empty(report.Removed);

        _stateRepositoryMock.Verify(
            repo => repo.SaveSnapshot(_tempDirPath, It.Is<DirectorySnapshot>(s =>
                s.Files.ContainsKey(testFileName) && s.Files[testFileName].Version == 3)),
            Times.Once
        );
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFileWasTouched_ShouldAddToMetadataChangedListWithSameVersion()
    {
        string testFileName = "test.txt";
        string testFilePath = Path.Combine(_tempDirPath, testFileName);
        await File.WriteAllTextAsync(testFilePath, "Unchanged content");
        
        // Setup historical state with same content hash but older modified time
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Unchanged content"))).ToLower();

        var oldSnapshot = new DirectorySnapshot
        {
            RootPath = _tempDirPath,
            LastScanTimeUtc = DateTime.UtcNow.AddMinutes(-5),
            Files = new Dictionary<string, FileSnapshotItem>
            {
                [testFileName] = new()
                {
                    RelativePath = testFileName,
                    Hash = hash,
                    Version = 4,
                    Length = 17,
                    LastWriteTimeUtc = DateTime.UtcNow.AddHours(-1)
                }
            }
        };

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns(oldSnapshot);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.MetadataChanged);
        Assert.Contains("test.txt (Version 4)", report.MetadataChanged);
        Assert.Empty(report.Removed);

        _stateRepositoryMock.Verify(
            repo => repo.SaveSnapshot(_tempDirPath, It.Is<DirectorySnapshot>(s =>
                s.Files.ContainsKey(testFileName) && s.Files[testFileName].Version == 4)),
            Times.Once
        );
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFileWasRemoved_ShouldAddToRemovedList()
    {
        var oldSnapshot = new DirectorySnapshot
        {
            RootPath = _tempDirPath,
            LastScanTimeUtc = DateTime.UtcNow.AddMinutes(-5),
            Files = new Dictionary<string, FileSnapshotItem>
            {
                ["removed.txt"] = new()
                {
                    RelativePath = "removed.txt",
                    Hash = "old-hash",
                    Version = 4,
                    Length = 100,
                    LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(-10)
                }
            }
        };

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns(oldSnapshot);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        Assert.NotNull(report);
        Assert.Empty(report.Added);
        Assert.Empty(report.Modified);
        Assert.Single(report.Removed);
        Assert.Contains("removed.txt (Last version 4)", report.Removed);

        _stateRepositoryMock.Verify(
            repo => repo.SaveSnapshot(_tempDirPath, It.Is<DirectorySnapshot>(s => !s.Files.ContainsKey("removed.txt"))),
            Times.Once
        );
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFileInSubfolderIsNew_ShouldReportRelativePath()
    {
        var subfolder = Path.Combine(_tempDirPath, "docs");
        Directory.CreateDirectory(subfolder);
        await File.WriteAllTextAsync(Path.Combine(subfolder, "readme.txt"), "hello");

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns((DirectorySnapshot?)null);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        Assert.Single(report.Added);
        Assert.Contains("docs/readme.txt (Version 1)", report.Added);
    }

    [Fact]
    public async Task JsonStateRepository_LoadAndSaveSnapshot_ShouldPersistStateCorrectly()
    {
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(_tempDirPath);
            var repository = new JsonStateRepository();
            var expectedSnapshot = new DirectorySnapshot
            {
                FormatVersion = 1,
                RootPath = _tempDirPath,
                LastScanTimeUtc = DateTime.UtcNow,
                Files = new Dictionary<string, FileSnapshotItem>
                {
                    ["tracked.txt"] = new()
                    {
                        RelativePath = "tracked.txt",
                        Hash = "saved-hash",
                        Version = 5,
                        Length = 120,
                        LastWriteTimeUtc = DateTime.UtcNow
                    }
                },
                Directories = new HashSet<string> { "docs" }
            };

            repository.SaveSnapshot(_tempDirPath, expectedSnapshot);

            var snapshot = repository.LoadSnapshot(_tempDirPath);

            Assert.NotNull(snapshot);
            Assert.True(snapshot.Files.ContainsKey("tracked.txt"));
            Assert.Equal("saved-hash", snapshot.Files["tracked.txt"].Hash);
            Assert.Equal(5, snapshot.Files["tracked.txt"].Version);
            Assert.Contains("docs", snapshot.Directories);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldSkipAppDataAndDotFilesSilently()
    {
        // Setup hidden Git directory & App_Data directory inside tempDirPath
        var gitDir = Path.Combine(_tempDirPath, ".git");
        var appDataDir = Path.Combine(_tempDirPath, "App_Data");
        
        Directory.CreateDirectory(gitDir);
        Directory.CreateDirectory(appDataDir);
        
        await File.WriteAllTextAsync(Path.Combine(gitDir, "config"), "git config");
        await File.WriteAllTextAsync(Path.Combine(appDataDir, "snapshots.json"), "app state data");

        // Add a normal file to scan
        await File.WriteAllTextAsync(Path.Combine(_tempDirPath, "normal.txt"), "hello");

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns((DirectorySnapshot?)null);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        // Verify normal file is added, but hidden files/App_Data folder files are skipped
        Assert.Single(report.Added);
        Assert.Contains("normal.txt (Version 1)", report.Added);
        Assert.Empty(report.SkippedFiles);
        Assert.Empty(report.SkippedDirectories);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFileIsRenamed_ShouldReportAsModifiedWithPreservedVersion()
    {
        const string oldName = "old_name.txt";
        const string newName = "new_name.txt";
        var content = "same content for rename test";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        await File.WriteAllTextAsync(Path.Combine(_tempDirPath, newName), content);

        var oldSnapshot = new DirectorySnapshot
        {
            RootPath = _tempDirPath,
            Files = new Dictionary<string, FileSnapshotItem>
            {
                [oldName] = new()
                {
                    RelativePath = oldName,
                    Hash = hash,
                    Version = 3,
                    Length = content.Length,
                    LastWriteTimeUtc = DateTime.UtcNow.AddHours(-1)
                }
            }
        };

        _stateRepositoryMock
            .Setup(repo => repo.LoadSnapshot(_tempDirPath))
            .Returns(oldSnapshot);

        var report = await _service.AnalyzeAsync(_tempDirPath);

        Assert.Empty(report.Added);
        Assert.Empty(report.Removed);
        Assert.Single(report.Modified);
        Assert.Contains("new_name.txt (Version 3)", report.Modified);
    }

    [Fact]
    public async Task AnalyzeAsync_WhenRootDirectoryNameStartsWithDotOrRelativePath_ShouldNotSkipRootDirectory()
    {
        var dotFolder = Path.Combine(_tempDirPath, ".test_root");
        Directory.CreateDirectory(dotFolder);
        await File.WriteAllTextAsync(Path.Combine(dotFolder, "normal.txt"), "hello");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dotFolder);
            
            _stateRepositoryMock
                .Setup(repo => repo.LoadSnapshot(dotFolder))
                .Returns((DirectorySnapshot?)null);

            // Audit the relative folder "." (dot)
            var report = await _service.AnalyzeAsync(".");

            Assert.Single(report.Added);
            Assert.Contains("normal.txt (Version 1)", report.Added);
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
            try
            {
                Directory.Delete(_tempDirPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
