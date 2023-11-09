// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

/// <summary>
/// Tests for <see cref="CsvLoggingStrategy"/>.
/// </summary>
public class CsvLoggingStrategyTests
{
    [Fact]
    public void LoggerScheduler_WhenNotSet_ReturnsDefaultScheduler()
    {
        var sut = new CsvLoggingStrategy(Mock.Of<IFileLauncher>());

        sut.LoggerScheduler.Should().BeSameAs(DefaultScheduler.Instance);
    }

    [Theory]
    [AutoData]
    public void CreateLogFile_WhenCalled_ProducesExpectedFileName(bool recursive, CompressionMode mode)
    {
        // ARRANGE
        var schedulerMock = new Mock<IScheduler>(MockBehavior.Strict);

        var sut = new CsvLoggingStrategy(Mock.Of<IFileLauncher>())
        {
            LoggerScheduler = schedulerMock.Object,
        };

        var fs = new MockFileSystem(new MockFileSystemOptions { CreateDefaultTempDir = true });

        var outputDir = fs.Path.Combine(fs.Path.GetTempPath(), "foo");
        var runParameters = new FolderCompressorParameters(
            new Mock<IDirectoryInfo>(MockBehavior.Strict).Object,
            fs.DirectoryInfo.New(outputDir),
            recursive,
            mode,
            new ExecutionOptions(ThreadCount.Default),
            new ProcessingOptions(CompressionLevel.Default));

        schedulerMock.SetupGet(x => x.Now).Returns(
            DateTimeOffset.Parse($"3067-11-14T23:59:59.1234"));

        // ACT
        var actual = sut.CreateLogFile(runParameters);

        // ASSERT
        var expected = fs.Path.Combine(outputDir, $"CziShrink_30671114T235959_{mode}.csv");
        actual.FullName.Should().Be(expected);
        actual.Exists.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public async Task CreateLogFile_WhenCalled_ProducesUniqueFileName(bool recursive, CompressionMode mode)
    {
        // ARRANGE
        DateTimeOffset now = DateTimeOffset.Parse($"3067-11-14T23:59:59.1234");
        var schedulerMock = new Mock<IScheduler>(MockBehavior.Strict);
        schedulerMock.SetupGet(x => x.Now).Returns(() =>
        {
            lock (schedulerMock)
            {
                return now;
            }
        });

        var sut = new CsvLoggingStrategy(Mock.Of<IFileLauncher>())
        {
            LoggerScheduler = schedulerMock.Object,
        };

        var fs = new MockFileSystem(new MockFileSystemOptions { CreateDefaultTempDir = true });

        var outputDir = fs.Path.Combine(fs.Path.GetTempPath(), "foo");
        var runParameters = new FolderCompressorParameters(
            new Mock<IDirectoryInfo>(MockBehavior.Strict).Object,
            fs.DirectoryInfo.New(outputDir),
            recursive,
            mode,
            new ExecutionOptions(ThreadCount.Default),
            new ProcessingOptions(CompressionLevel.Default));
        CreateFile(sut.CreateLogFile(runParameters));

        // ACT
        Task<IFileInfo> actTask = Task.Run(() => sut.CreateLogFile(runParameters));
        SpinWait.SpinUntil(() => actTask.Status == TaskStatus.Running);
        await Task.Delay(10);
        actTask.Status.Should().Be(TaskStatus.Running);

        lock (schedulerMock)
        {
            now += TimeSpan.FromSeconds(1.5);
        }

        // ASSERT
        var expected = fs.Path.Combine(outputDir, $"CziShrink_30671115T000000_{mode}.csv");
        var actual = await actTask;
        actual.FullName.Should().Be(expected);
        actual.Exists.Should().BeFalse();
    }

    [Fact]
    public void CreateLogger_WhenCalled_CreatesCsvLogWriter()
    {
        // ARRANGE
        var sut = new CsvLoggingStrategy(Mock.Of<IFileLauncher>());

        using var writer = Mock.Of<TextWriter>();

        // ACT
        var actual = sut.CreateLogger(writer);

        // ASSERT
        var result = actual.Should().BeOfType<CsvLogFileWriter>().Subject;
        result.Writer.Should().BeSameAs(writer);
    }

    [Fact]
    public void OpenLogFile_WhenCalled_UsesFileLauncher()
    {
        // ARRANGE
        var fileLauncherMock = new Mock<IFileLauncher>();
        var sut = new CsvLoggingStrategy(fileLauncherMock.Object);
        var fileInfo = Mock.Of<IFileInfo>(f => f.FullName == "/foo/bar/baz.CSV");

        // ACT
        sut.OpenLogFile(fileInfo);

        // ASSERT
        fileLauncherMock.Verify(l => l.Launch("/foo/bar/baz.CSV"), Times.Once);
        fileLauncherMock.VerifyNoOtherCalls();
    }

    private static void CreateFile(IFileInfo firstFile)
    {
        firstFile.Directory!.Create();
        using (var firstFileHandle = firstFile.AppendText())
        {
            firstFileHandle.WriteLine("foo");
        }

        firstFile.Refresh();
        Debug.Assert(firstFile.Exists, "Error in ARRANGE");
    }
}
