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
/// <content>Tests for <see cref="MainViewModel.StopCommand"/>.</content>
public partial class MainViewModelTests
{
    [Fact]
    public async Task StopCommand_WhenProcessing_CanExecuteHasExpectedValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithWaitForCancellation();

        var sut = fixture.Create<MainViewModel>();
        ICommand start = sut.StartCommand;
        ICommand stop = sut.StopCommand;
        SetFolders(sut, fs);

        Dictionary<string, bool> canStopWhen = new();

        void AddDataPoint(string timePoint)
        {
            var canStop = stop.CanExecute(null);
            canStopWhen[timePoint] = stop.CanExecute(null);
            sut.CanStop.Should().Be(canStop);
        }

        // ACT
        AddDataPoint("ready to run");

        Task startCommandTask;
        using (var monitor = sut.Monitor())
        {
            startCommandTask = sut.StartCommand.Execute(default).ToTask();
            monitor.Should().RaisePropertyChangeFor(mvm => mvm.CanStop);
            AddDataPoint("running");
        }

        Task stopCommandTask;
        using (var monitor = sut.Monitor())
        {
            stopCommandTask = sut.StopCommand.Execute(default).ToTask();
            monitor.Should().RaisePropertyChangeFor(mvm => mvm.CanStop);
            AddDataPoint("stopping");
        }

        await startCommandTask;
        await stopCommandTask;
        AddDataPoint("stopped");

        // ASSERT
        canStopWhen["ready to run"].Should().BeFalse();
        canStopWhen["running"].Should().BeTrue();
        canStopWhen["stopping"].Should().BeFalse();
        canStopWhen["stopped"].Should().BeFalse();
    }

    [Fact]
    public async Task StartCommands_WhenProcessingCompletes_CanExecuteIsFalse()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        _ = fixture.Freeze<Mock<IFolderCompressor>>().WithAutoData(fixture);

        var sut = fixture.Create<MainViewModel>();
        ICommand stop = sut.StopCommand;
        SetFolders(sut, fs);

        // ACT
        var startCommandTask = sut.StartCommand.Execute(default).ToTask();
        await startCommandTask;

        // ASSERT
        stop.CanExecute(null).Should().BeFalse();
    }
}
