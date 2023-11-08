// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.IO.Abstractions;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;

using AutoFixture;

using netczicompress.ViewModels;

using netczicompressTests.Models.Mocks;

/// <summary>
/// Tests for <see cref="MainViewModel"/>.
/// </summary>
/// <content>Tests for <see cref="MainViewModel.StartCommand"/>.</content>
public partial class MainViewModelTests
{
    [Fact]
    public void StartCommand_WhenInputFolderNotSet_CannotExecute()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);

        // ACT
        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs, setInput: false);

        // ASSERT
        ICommand command = sut.StartCommand;
        command.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void StartCommand_WhenOutputFolderNotSet_CannotExecute()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);

        // ACT
        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs, setOutput: false);

        // ASSERT
        ICommand command = sut.StartCommand;
        command.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void StartCommand_WhenBothFoldersSet_CanExecute()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);

        bool expectCanExecute = false;
        List<(bool Expectation, bool Actual)> data = new();

        var sut = fixture.Create<MainViewModel>();
        sut.StartCommand.CanExecute.Subscribe(onNext:
            canExec => data.Add((expectCanExecute, canExec)));

        // ACT
        SetFolders(sut, fs, setOutput: false);

        expectCanExecute = true;
        SetFolders(sut, fs, setInput: false);

        // ASSERT
        ICommand start = sut.StartCommand;
        start.CanExecute(null).Should().BeTrue();

        data.Count.Should().Be(2);
        foreach (var (expectation, actual) in data)
        {
            actual.Should().Be(expectation);
        }
    }

    [Fact]
    public async Task StartCommand_WhenProcessing_CanExecuteHasExpectedValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithWaitForCancellation();

        var sut = fixture.Create<MainViewModel>();
        ICommand start = sut.StartCommand;
        SetFolders(sut, fs);

        Dictionary<string, bool> canStartWhen = new();

        void AddDataPoint(string timePoint)
        {
            Thread.MemoryBarrier();
            canStartWhen[timePoint] = start.CanExecute(null);
        }

        // ACT
        AddDataPoint("ready to run");

        var startCommandTask = sut.StartCommand.Execute(default).ToTask();
        AddDataPoint("running");

        var stopCommandTask = sut.StopCommand.Execute(default).ToTask();
        await startCommandTask;
        await stopCommandTask;
        AddDataPoint("stopped");

        for (int k = 0; !canStartWhen["stopped"] && k < 100; k++)
        {
            // canStartWhen["stopped"] has sporadically been false,
            // e. g. in https://github.com/m-ringler/netczicompress/actions/runs/6195449563/job/16820188169?pr=47
            // This is probably due to the fact that ReactiveCommand overrides the passed in CanExecute observable
            // with false while it is executing. So, CanExecute becomes true again only _after_ the command has
            // stopped executing, i. e. _after_ startCommandTask completes. Thus, we have a race condition.
            // => Wait a bit and try again.
            await Task.Delay(10);
            AddDataPoint("stopped");
        }

        // ASSERT
        canStartWhen["ready to run"].Should().BeTrue();
        canStartWhen["running"].Should().BeFalse();
        try
        {
            canStartWhen["stopped"].Should().BeTrue();
        }
        catch (Exception ex)
        {
            throw new Exception(canStartWhen["stopped"].ToString(), ex);
        }
    }

    [Fact]
    public async Task StartCommands_WhenProcessingCompletes_CanExecuteIsTrue()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithAutoData(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);

        // ACT
        var startCommandTask = sut.StartCommand.Execute(default).ToTask();
        await startCommandTask;

        // ASSERT
        ICommand start = sut.StartCommand;
        start.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public async Task StartCommands_WhenProcessingStarts_UsesCorrectParameters(OperationMode selectedMode, bool recursive)
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var folderCompressorMock = fixture.Freeze<Mock<IFolderCompressor>>().WithWaitForCancellation();

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);
        sut.SelectedMode = selectedMode;
        sut.Recursive = recursive;

        // ACT
        var startCommandTask = sut.StartCommand.Execute(default).ToTask();

        // ASSERT
        folderCompressorMock.Verify(
            x => x.PrepareNewRun(
                It.Is<FolderCompressorParameters>(
                    x => x.InputDir.FullName == sut.InputDirectory &&
                        x.OutputDir.FullName == sut.OutputDirectory &&
                        object.ReferenceEquals(x.InputDir.FileSystem, fs) &&
                        object.ReferenceEquals(x.OutputDir.FileSystem, fs) &&
                        x.Recursive == recursive &&
                        x.Mode == selectedMode.Value),
                It.Is<CancellationToken>(t => !t.IsCancellationRequested)),
            Times.Once);

        // CLEANUP
        await sut.StopCommand.Execute(default);
        await startCommandTask;
        folderCompressorMock.VerifyNoOtherCalls();
    }

    [Theory]
    [AutoData]
    public async Task StartCommands_WhenProcessingStarts_LogsCorrectParameters(OperationMode selectedMode, bool recursive)
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithWaitForCancellation();
        var loggerMock = new LoggerMock().Inject(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);
        sut.SelectedMode = selectedMode;
        sut.Recursive = recursive;

        // ACT
        var startCommandTask = sut.StartCommand.Execute(default).ToTask();

        // ASSERT
        loggerMock.VerifySingleEntry(
            Microsoft.Extensions.Logging.LogLevel.Information,
            $"Starting run: FolderCompressorParameters {{ InputDir = {sut.InputDirectory}, OutputDir = {sut.OutputDirectory}, Recursive = {recursive}, Mode = {selectedMode.Value}, ExecutionOptions = ExecutionOptions {{ ThreadCount = {sut.ThreadCount} }}, ProcessingOptions = ProcessingOptions {{ CompressionLevel = {sut.CompressionLevel}, CopyFailedFiles = {sut.CopyFailedFiles} }} }}");

        // CLEANUP
        await sut.StopCommand.Execute(default);
        await startCommandTask;
    }
}
