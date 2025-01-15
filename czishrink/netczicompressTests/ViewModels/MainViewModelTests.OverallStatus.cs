// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive;
using System.Reactive.Threading.Tasks;

using AutoFixture;

using Microsoft.Extensions.Logging;

using netczicompress.Models;
using netczicompress.ViewModels;

using netczicompressTests.Models.Mocks;

/// <summary>
/// Tests for <see cref="MainViewModel"/>.
/// </summary>
/// <content>Tests for members related to overall status.</content>
public partial class MainViewModelTests
{
    [Fact]
    public void OverallStatus_WhenNotRun_IsEmpty()
    {
        // ARRANGE
        var fixture = CreateFixture();

        var sut = fixture.Create<MainViewModel>();

        // ACT
        var actual = sut.OverallStatus;

        // ASSERT
        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task OverallStatus_WhenRunSuccessful_IsExpected()
    {
        // ARRANGE
        var fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithAutoData(fixture);
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);

        // ACT
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        sut.OverallStatus.Should().Be("Finished");
        loggerMock.VerifyEntryCount(2);
        loggerMock.VerifyEntry(^1, LogLevel.Information, "Run finished normally.");
    }

    [Fact]
    public async Task OverallStatus_WhenRunCanceled_IsExceptionMessage()
    {
        // ARRANGE
        var fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var opCanceled = new OperationCanceledException("CaNcElEd");
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithException(opCanceled);
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);

        // ACT
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        sut.OverallStatus.Should().Be(opCanceled.Message);
        loggerMock.VerifyEntryCount(2);
        loggerMock.VerifyEntry(1, LogLevel.Warning, "Run canceled.");
    }

    [Fact]
    public async Task OverallStatus_WhenOtherException_IsExceptionMessageWithWarningSign()
    {
        // ARRANGE
        var fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var invalidOp = new InvalidOperationException("Foo bar baz.");
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithException(invalidOp);
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);

        // ACT
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        sut.OverallStatus.Should().Be("⚠ Foo bar baz.");
        loggerMock.VerifyEntryCount(2);
        loggerMock.VerifyEntry(^1, LogLevel.Error, "Run failed: " + invalidOp);
    }

    [Fact]
    public async Task OverallStatus_WhenInputDirectoryNotSet_IsExpected()
    {
        // ARRANGE
        var fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var compressorMock = fixture.Freeze<Mock<IFolderCompressor>>();
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs, setInput: false);

        // ACT
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        sut.OverallStatus.Should().Be("⚠ Input folder is not set.");

        loggerMock.VerifySingleEntry(LogLevel.Error, "Run failed: System.InvalidOperationException: Input folder is not set.*at*");

        compressorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OverallStatus_WhenOutputDirectoryNotSet_IsExpected()
    {
        // ARRANGE
        var fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var compressorMock = fixture.Freeze<Mock<IFolderCompressor>>();
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs, setOutput: false);

        // ACT
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        sut.OverallStatus.Should().Be("⚠ Output folder is not set.");

        loggerMock.VerifySingleEntry(LogLevel.Error, "Run failed: System.InvalidOperationException: Output folder is not set.*at*");

        compressorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OverallStatus_WhenInputDirectoryDoesNotExist_IsExpected()
    {
        // ARRANGE
        var fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var compressorMock = fixture.Freeze<Mock<IFolderCompressor>>();
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);
        string inputDir = fs.Path.Join(fs.Path.GetTempPath(), "Input");
        sut.InputDirectory = inputDir;

        // ACT
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        sut.OverallStatus.Should().Be($"⚠ Directory {inputDir} does not exist.");
        loggerMock.VerifySingleEntry(LogLevel.Error, $"Run failed: System.IO.DirectoryNotFoundException: Directory {inputDir} does not exist.*at*");
        compressorMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OverallStatus_WhenProcessing_HasExpectedValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithWaitForCancellation();

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);

        Dictionary<string, string> overallStatusWhen = new();

        void AddDataPoint(string timePoint)
        {
            overallStatusWhen[timePoint] = sut.OverallStatus;
        }

        // ACT
        AddDataPoint("ready to run");

        for (int i = 0; i < 2; i++)
        {
            Task startCommandTask;
            using (var monitor = sut.Monitor())
            {
                startCommandTask = sut.StartCommand.Execute(default).ToTask();
                AddDataPoint("running" + i);

                if (i == 0)
                {
                    monitor.Should().NotRaisePropertyChangeFor(mvm => mvm.OverallStatus);
                }
                else
                {
                    monitor.Should().RaisePropertyChangeFor(mvm => mvm.OverallStatus);
                }
            }

            Task stopCommandTask;
            using (var monitor = sut.Monitor())
            {
                stopCommandTask = sut.StopCommand.Execute(default).ToTask();

                await startCommandTask;
                await stopCommandTask;

                AddDataPoint("stopped" + i);
                monitor.Should().RaisePropertyChangeFor(mvm => mvm.OverallStatus);
            }
        }

        // ASSERT
        overallStatusWhen["ready to run"].Should().BeEmpty();
        overallStatusWhen["running0"].Should().BeEmpty();
        overallStatusWhen["stopped0"].Should().Be(new OperationCanceledException().Message);
        overallStatusWhen["running1"].Should().BeEmpty();
        overallStatusWhen["stopped1"].Should().Be(new OperationCanceledException().Message);
    }

    private static MockFileSystem NewMockFileSystem()
    {
        return new MockFileSystem(
            new MockFileSystemOptions { CreateDefaultTempDir = true });
    }

    private static void SetFolders(MainViewModel sut, IFileSystem fs, bool setInput = true, bool setOutput = true)
    {
        string someExistingDir = fs.DirectoryInfo.New(fs.Path.GetTempPath()).FullName;
        if (setInput)
        {
            sut.InputDirectory = someExistingDir;
        }

        if (setOutput)
        {
            sut.OutputDirectory = fs.Path.Join(someExistingDir, "output");
        }
    }

    private class LoggerMock : ILogger<MainViewModel>
    {
        private static readonly EventId Zero = (EventId)0;

        private List<(LogLevel Level, EventId Event, string Message)> LogEntries { get; } = new();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (this.LogEntries)
            {
                this.LogEntries.Add(new(logLevel, eventId, formatter.Invoke(state, exception)));
            }
        }

        public void VerifyEntryCount(int count)
        {
            this.LogEntries.Should().HaveCount(count);
        }

        public void VerifyEntry(Index index, LogLevel expectedLevel, string messageWildcardPattern)
        {
            (LogLevel level, EventId eventId, string message) = this.LogEntries[index];
            level.Should().Be(expectedLevel);
            eventId.Should().Be(Zero);
            message.Should().Match(messageWildcardPattern);
        }

        public void VerifySingleEntry(LogLevel expectedLevel, string messageWildcardPattern)
        {
            this.VerifyEntryCount(1);
            this.VerifyEntry(0, expectedLevel, messageWildcardPattern);
        }

        public LoggerMock Inject(IFixture fixture)
        {
            fixture.Inject<ILogger<MainViewModel>>(this);
            return this;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }
    }
}
