// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;

using netczicompress.ViewModels;
using netczicompressTests.Customizations;
using netczicompressTests.Models.Mocks;

/// <summary>
/// Tests for <see cref="LogFileViewModel"/>.
/// </summary>
public class LogFileViewModelTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObserveRun_WhenCalled_LogsMessages(bool exception)
    {
        // ARRANGE
        var fixture = CreateFixture();
        var p = fixture.Create<FolderCompressorParameters>();

        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);

        var strategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        var recorder = new Recorder<CompressorMessage.FileFinished>();
        using var writer = new StreamWriter(new MemoryStream());
        strategyMock.Setup(x => x.CreateLogFile(p)).Returns(Mock.Of<IFileInfo>(x => x.CreateText() == writer));
        strategyMock.Setup(x => x.CreateLogger(writer)).Returns(recorder);
        strategyMock.Setup(x => x.LoggerScheduler).Returns(ImmediateScheduler.Instance);

        var sut = fixture.Create<LogFileViewModel>();

        var subject = new Subject<CompressorMessage.FileFinished>();

        // ACT
        sut.ObserveRun(p, subject);

        var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
        subject.OnNextAll(messages);

        var messagesLoggedBeforeTermination = recorder.Data.ToImmutableArray();
        Complete(subject, exception);

        // ASSERT
        recorder.Completed.Should().BeTrue();
        recorder.Data.Should().BeEquivalentTo(messages);
        messagesLoggedBeforeTermination.Should().BeEquivalentTo(messages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObserveRun_UsesLoggerSchedulerToLogMessages(bool exception)
    {
        // ARRANGE
        var fixture = CreateFixture();
        var p = fixture.Create<FolderCompressorParameters>();

        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);

        var strategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        var recorder = new Recorder<CompressorMessage.FileFinished>();
        var loggerScheduler = new SchedulerMock();
        using var writer = new StreamWriter(new MemoryStream());
        strategyMock.Setup(x => x.CreateLogFile(p)).Returns(Mock.Of<IFileInfo>(x => x.CreateText() == writer));
        strategyMock.Setup(x => x.CreateLogger(writer)).Returns(recorder);
        strategyMock.Setup(x => x.LoggerScheduler).Returns(loggerScheduler);

        var sut = fixture.Create<LogFileViewModel>();

        List<(string, ImmutableArray<CompressorMessage.FileFinished> LoggedMessages, bool Completed)> data = new();
        void StoreDataPoint(string name)
        {
            recorder.Error.Should().BeNull();
            data.Add((name, recorder.Data.ToImmutableArray(), recorder.Completed));
        }

        // ACT
        var subject = new Subject<CompressorMessage.FileFinished>();
        sut.ObserveRun(p, subject.AsObservable());
        var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
        subject.OnNextAll(messages);

        Complete(subject, exception);
        StoreDataPoint("Completed");

        loggerScheduler.RunQueuedActions();
        StoreDataPoint("Completed and Scheduler Queue Run");

        // ASSERT
        var allMessages = messages.ToImmutableArray();
        var noMessages = ImmutableArray<CompressorMessage.FileFinished>.Empty;

        data.Should().BeEquivalentTo(
            new[]
            {
                ("Completed", noMessages, false),
                ("Completed and Scheduler Queue Run", allMessages, true),
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObserveRun_WhenCalled_CorrectlyPublishesLogFile(bool exception)
    {
        // ARRANGE
        var fixture = CreateFixture();
        var p = fixture.Create<FolderCompressorParameters>();

        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);

        var fs = new MockFileSystem(new MockFileSystemOptions { CreateDefaultTempDir = true });
        IFileInfo? logFile = null;
        var strategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        strategyMock.Setup(x => x.CreateLogFile(p)).Returns<FolderCompressorParameters>(_ =>
        {
            var result = fs.FileInfo.New(fs.Path.GetTempFileName());
            logFile = result;
            return result;
        });
        strategyMock.Setup(x => x.LoggerScheduler).Returns(ImmediateScheduler.Instance);

        var sut = fixture.Create<LogFileViewModel>();

        Dictionary<string, (IFileInfo? LogFileOfLastRun, bool CanShowLogFile)> data = new();
        void LogDataPoint(string name)
        {
            data[name] = (sut.LogFileOfLastRun, sut.OpenLogFileCommand.CanExecute(null));
        }

        LogDataPoint("Never Run");
        for (int i = 0; i < 2; i++)
        {
            // ACT
            var subject = new Subject<CompressorMessage.FileFinished>();
            sut.ObserveRun(p, subject);
            LogDataPoint("Before Run");

            var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
            subject.OnNextAll(messages);

            LogDataPoint("During Run");

            using (var monitor = sut.Monitor())
            {
                Complete(subject, exception);
                monitor.Should().RaisePropertyChangeFor(x => x.LogFileOfLastRun);
            }

            LogDataPoint("After Run");

            // ASSERT
            data["Never Run"].Should().Be((null, false));
            data["Before Run"].Should().Be((null, false));
            data["During Run"].Should().Be((null, false));
            data["After Run"].Should().Be((logFile, true));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObserveRun_WhenCalled_UsesGuiSchedulerToPublishLogFile(bool exception)
    {
        // ARRANGE
        var fixture = CreateFixture();
        var p = fixture.Create<FolderCompressorParameters>();
        var guiScheduler = new SchedulerMock();
        fixture.Inject<IScheduler>(guiScheduler);

        var fs = new MockFileSystem(new MockFileSystemOptions { CreateDefaultTempDir = true });
        var strategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        strategyMock.Setup(x => x.CreateLogFile(p)).Returns<FolderCompressorParameters>(_ =>
        {
            return fs.FileInfo.New(fs.Path.GetTempFileName());
        });
        strategyMock.Setup(x => x.LoggerScheduler).Returns(ImmediateScheduler.Instance);

        var sut = fixture.Create<LogFileViewModel>();

        Dictionary<string, (IFileInfo? LogFileOfLastRun, bool CanShowLogFile)> data = new();
        void LogDataPoint(string name)
        {
            data[name] = (sut.LogFileOfLastRun, sut.OpenLogFileCommand.CanExecute(null));
        }

        for (int i = 0; i < 2; i++)
        {
            // ACT
            var subject = new Subject<CompressorMessage.FileFinished>();
            sut.ObserveRun(p, subject);

            var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
            subject.OnNextAll(messages);

            using (var monitor = sut.Monitor())
            {
                Complete(subject, exception);
                monitor.Should().NotRaisePropertyChangeFor(x => x.LogFileOfLastRun);
                LogDataPoint("Before scheduler queue has run");

                guiScheduler.RunQueuedActions();
                monitor.Should().RaisePropertyChangeFor(x => x.LogFileOfLastRun);
                LogDataPoint("After scheduler queue has run");
            }

            // ASSERT
            data["Before scheduler queue has run"].Should().Be((null, false));
            var after = data["After scheduler queue has run"];
            after.LogFileOfLastRun.Should().NotBeNull();
            after.CanShowLogFile.Should().BeTrue();
        }
    }

    [Fact]
    public void ShowLogFileCommand_WhenCannotExecute_ExecuteDoesNothing()
    {
        // ARRANGE
        var fixture = CreateFixture();
        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);
        var loggingStrategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        var sut = fixture.Create<LogFileViewModel>();

        // ACT
        sut.OpenLogFileCommand.Execute(null);

        // ASSERT
        loggingStrategyMock.Verify(x => x.OpenLogFile(It.IsAny<IFileInfo>()), Times.Never);
    }

    [Fact]
    public void ShowLogFile_WhenLogFileExists_OpensLogFile()
    {
        // ARRANGE
        var fixture = CreateFixture();
        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);
        var loggingStrategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        var logFileMock = new Mock<IFileInfo>();
        var sut = fixture.Create<LogFileViewModel>();

        var p = fixture.Create<FolderCompressorParameters>();

        loggingStrategyMock.Setup(x => x.CreateLogFile(p)).Returns(logFileMock.Object);
        var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
        var run = messages.ToObservableAction();

        sut.ObserveRun(p, run.Output);
        run.Start();
        logFileMock
            .Setup(
                x => x.Refresh())
            .Callback(
                () => logFileMock
                .SetupGet(
                    x => x.Exists)
                .Returns(
                    true));
        sut.LogFileOfLastRun.Should().BeSameAs(logFileMock.Object);
        sut.LogFileOfLastRun!.Exists.Should().BeFalse();
        sut.OpenLogFileCommand.CanExecute(null).Should().BeTrue();

        // ACT
        sut.OpenLogFileCommand.Execute(null);

        // ASSERT
        loggingStrategyMock.Verify(x => x.OpenLogFile(logFileMock.Object), Times.Once);
        sut.LogFileOfLastRun.Should().BeSameAs(logFileMock.Object);
        sut.LogFileOfLastRun!.Exists.Should().BeTrue();
        sut.OpenLogFileCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ShowLogFile_WhenLogFileDoesNotExist_UpdatesCanExecute()
    {
        // ARRANGE
        var fixture = CreateFixture();
        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);
        var loggingStrategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        var logFileMock = new Mock<IFileInfo>();
        var sut = fixture.Create<LogFileViewModel>();

        var p = fixture.Create<FolderCompressorParameters>();

        loggingStrategyMock.Setup(x => x.CreateLogFile(p)).Returns(logFileMock.Object);
        var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
        var run = messages.ToObservableAction();

        sut.ObserveRun(p, run.Output);
        run.Start();
        sut.LogFileOfLastRun.Should().BeSameAs(logFileMock.Object);
        sut.LogFileOfLastRun!.Exists.Should().BeFalse();
        sut.OpenLogFileCommand.CanExecute(null).Should().BeTrue();

        // ACT
        sut.OpenLogFileCommand.Execute(null);

        // ASSERT
        loggingStrategyMock.Verify(x => x.OpenLogFile(logFileMock.Object), Times.Never);
        sut.LogFileOfLastRun.Should().BeNull();
        sut.OpenLogFileCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ShowLogFile_WhenOpeningTheLogFileFails_UpdatesCanExecute()
    {
        // ARRANGE
        var fixture = CreateFixture();
        fixture.Inject<IScheduler>(ImmediateScheduler.Instance);
        var loggingStrategyMock = fixture.Freeze<Mock<ILoggingStrategy>>();
        var logFile = Mock.Of<IFileInfo>(x => x.Exists == true);
        var sut = fixture.Create<LogFileViewModel>();

        var p = fixture.Create<FolderCompressorParameters>();

        loggingStrategyMock.Setup(x => x.CreateLogFile(p)).Returns(logFile);
        var messages = fixture.CreateMany<CompressorMessage.FileFinished>();
        var run = messages.ToObservableAction();

        sut.ObserveRun(p, run.Output);
        run.Start();
        sut.LogFileOfLastRun.Should().BeSameAs(logFile);
        sut.LogFileOfLastRun!.Exists.Should().BeTrue();
        sut.OpenLogFileCommand.CanExecute(null).Should().BeTrue();
        loggingStrategyMock
            .Setup(
                x => x.OpenLogFile(logFile))
            .Throws<IOException>();

        // ACT
        sut.OpenLogFileCommand.Execute(null);

        // ASSERT
        loggingStrategyMock.Verify(x => x.OpenLogFile(logFile), Times.Once);
        sut.LogFileOfLastRun.Should().BeNull();
        sut.OpenLogFileCommand.CanExecute(null).Should().BeFalse();
    }

    private static IFixture CreateFixture()
    {
        var result = new Fixture().Customize(new AutoMoqCustomization());
        result.Customizations.Add(new ThreadCountSpecimenBuilder());
        return result;
    }

    private static void Complete<T>(IObserver<T> subject, bool exception)
    {
        if (exception)
        {
            var ex = new Exception();
            subject.OnError(ex);
        }
        else
        {
            subject.OnCompleted();
        }
    }
}
