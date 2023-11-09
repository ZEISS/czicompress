// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Windows.Input;

using netczicompress.Models;
using netczicompress.ViewModels;
using netczicompressTests.Customizations;
using netczicompressTests.Models.Mocks;
using ReactiveUI;
using static netczicompress.Models.CompressorMessage;

/// <summary>
/// Tests for <see cref="ErrorListViewModel"/>.
/// </summary>
public class ErrorListViewModelTests
{
    [Fact]
    public void SelectedErrorItem_WhenSet_RaisesPropertyChanged()
    {
        var sut = CreateSut();
        var fixture = new Fixture();

        using (var m = sut.Monitor())
        {
            sut.SelectedErrorItem = fixture.Create<ErrorItem>();
            m.Should().RaisePropertyChangeFor(x => x.SelectedErrorItem);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(20)]
    [InlineData(100)]
    public void ObserveRun_WhenItemsAddedBelowBulkAddThreshold_PerformsSingleAdds(int bufferCap)
    {
        // ARRANGE
        const int MaxItemsToShow = 10;
        ErrorListViewModel sut = CreateSut(bufferCap, MaxItemsToShow);
        Debug.Assert(
            sut.BulkAddThreshold > Math.Min(bufferCap, MaxItemsToShow),
            "ItemsAddedBelowResetThreshold");

        var errorListChangesRecorder = ObserveErrorListChanges(sut).StartRecording();
        IFixture fixture = CreateFixture();
        var input = GenerateMessages(fixture).Take(54).ToArray();
        var run = input.ToObservableAction();

        // ACT
        sut.ObserveRun(
            fixture.Create<FolderCompressorParameters>(),
            run.Output);
        run.Start();

        // ASSERT
        // Number of changes: 1 Reset + 1 Add per Item + 1 Add of Truncation Notice
        // Changes
        var expectedChanges = GetExpectedErrorListChangesForSingleAdds(input, MaxItemsToShow);
        errorListChangesRecorder.Data.Count.Should().Be(expectedChanges.Length);
        errorListChangesRecorder.Data.Should().BeEquivalentTo(expectedChanges);

        // Final state
        sut.Errors.Should().BeEquivalentTo(expectedChanges[^1].SnapShot);
    }

    [Fact]
    public void ObserveRun_WhenItemsAddedExceedBulkAddThreshold_PerformsBulkAdd()
    {
        // ARRANGE
        const int MaxItemsToShow = 30;
        const int BufferCapacity = 30;
        ErrorListViewModel sut = CreateSut(BufferCapacity, MaxItemsToShow);
        Debug.Assert(
            sut.BulkAddThreshold < Math.Min(MaxItemsToShow, BufferCapacity),
            "ItemsAddedExceedResetThreshold");

        var errorListChangesRecorder = ObserveErrorListChanges(sut).StartRecording();
        var fixture = CreateFixture();
        var input = GenerateMessages(fixture).Take(61).ToArray();
        var run = input.ToObservableAction();

        // ACT
        sut.ObserveRun(
            fixture.Create<FolderCompressorParameters>(),
            run.Output);
        run.Start();

        // ASSERT
        // Changes
        var expectedRealErrorItems = GetExpectedErrorItems(input).Take(MaxItemsToShow).ToArray();
        var expectedErrorItems = expectedRealErrorItems.Append(GetExpectedTruncationItem()).ToArray();
        var expectation = new[]
        {
            (Array.Empty<ErrorItem>(), "Reset"),
            (expectedRealErrorItems, "Reset"),
            GetExpectedAddLastChange(expectedErrorItems),
        };

        errorListChangesRecorder.Data.Count.Should().Be(expectation.Length);
        errorListChangesRecorder.Data.Should().BeEquivalentTo(expectation);

        // Final state
        sut.Errors.Should().BeEquivalentTo(expectedErrorItems);
        sut.SelectedErrorItem.Should().BeEquivalentTo(expectedRealErrorItems[^1]);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(5)]
    public void ObserveRun_WhenRunIsErrored_HasCorrectFinalState(double bufferTimeMillis)
    {
        // ARRANGE
        ErrorListViewModel sut = CreateSut(
            bufferCap: 30,
            maxItemsToShow: int.MaxValue,
            bufferTimeout: TimeSpan.FromMilliseconds(bufferTimeMillis),
            scheduler: ImmediateScheduler.Instance);

        var fixture = CreateFixture();
        var input = GenerateMessages(fixture).Take(125).ToArray();
        var observableMock = new Mock<IObservable<FileFinished>>();
        IObserver<FileFinished>? subscriber = null;
        observableMock
            .Setup(x => x.Subscribe(It.IsAny<IObserver<FileFinished>>()))
            .Callback<IObserver<FileFinished>>(obs => subscriber = obs);

        // ACT
        sut.ObserveRun(
            fixture.Create<FolderCompressorParameters>(),
            observableMock.Object);
        subscriber?.OnNextAll(input);
        subscriber?.OnError(new Exception());

        // ASSERT
        var expectedErrorItems = GetExpectedErrorItems(input).ToArray();
        sut.Errors.Should().BeEquivalentTo(expectedErrorItems);
        sut.SelectedErrorItem.Should().BeEquivalentTo(expectedErrorItems[^1]);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(5)]
    public void ObserveRun_WhenSchedulerIsSlow_HasCorrectFinalState(double bufferTimeMillis)
    {
        // ARRANGE
        var scheduler = new SchedulerMock();
        ErrorListViewModel sut = CreateSut(
            bufferCap: 30,
            maxItemsToShow: int.MaxValue,
            bufferTimeout: TimeSpan.FromMilliseconds(bufferTimeMillis),
            scheduler: scheduler);

        var fixture = CreateFixture();
        var input = GenerateMessages(fixture).Take(125).ToArray();
        var run = input.ToObservableAction();

        // ACT
        sut.ObserveRun(
            fixture.Create<FolderCompressorParameters>(),
            run.Output);
        run.Start();

        scheduler.RunQueuedActions();

        // ASSERT
        var expectedErrorItems = GetExpectedErrorItems(input).ToArray();
        sut.Errors.Should().BeEquivalentTo(expectedErrorItems);
        sut.SelectedErrorItem.Should().BeEquivalentTo(expectedErrorItems[^1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(100)]
    public void ObserveRun_AsItemsAreDelivered_SelectsLastRealItem(int bufferCap)
    {
        // ARRANGE
        const int MaxItemsToShow = 10;
        ErrorListViewModel sut = CreateSut(bufferCap, MaxItemsToShow);

        var recorder = ObserveSelectedItemChanges(sut).StartRecording();
        var fixture = CreateFixture();
        var input = GenerateMessages(fixture, 54).ToArray();
        var run = input.ToObservableAction();

        // ACT
        sut.ObserveRun(
            fixture.Create<FolderCompressorParameters>(),
            run.Output);
        run.Start();

        // ASSERT
        // Number of changes: we expect only one update per buffer
        int expectedNumberOfUpdates = (int)Math.Ceiling(1.0 * MaxItemsToShow / bufferCap);
        recorder.Data.Count.Should().Be(expectedNumberOfUpdates);

        // State after each change
        foreach (var (snapshot, selectedItem) in recorder.Data)
        {
            selectedItem.Should().BeSameAs(snapshot.LastOrDefault());
        }

        // Final state: Truncation Notice is not selected, last 'real' error remains selected
        sut.SelectedErrorItem.Should().BeSameAs(sut.Errors[^2]);
    }

    [Fact]
    public void ShowSelectedFileCommand_CanExecuteWhenSelectedErrorItemIsNotNull()
    {
        var sut = CreateSut();
        Command_CanExecuteWhenSelectedErrorItemIsNotNull(sut, sut.ShowSelectedFileCommand);
    }

    [Fact]
    public void ShowSelectedFileCommand_WhenSelectedErrorItemIsNotNull_RevealsFile()
    {
        // ARRANGE
        var launcherMock = new Mock<IFileLauncher>();
        var sut = CreateSut(launcher: launcherMock.Object);
        var fixture = new Fixture();
        var errorItem = fixture.Create<ErrorItem>();
        sut.SelectedErrorItem = errorItem;

        // ACT
        sut.ShowSelectedFileCommand.Execute(null);

        // ASSERT
        launcherMock.Verify(x => x.Reveal(errorItem.File), Times.Once);
        launcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void ShowSelectedFileCommand_WhenSelectedErrorItemIsNull_NoError()
    {
        // ARRANGE
        var launcherMock = new Mock<IFileLauncher>();
        var sut = CreateSut(launcher: launcherMock.Object);
        sut.SelectedErrorItem = null;

        // ACT
        sut.ShowSelectedFileCommand.Execute(null);

        // ASSERT
        launcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void OpenSelectedFileCommand_WhenSelectedErrorItemIsNull_NoError()
    {
        // ARRANGE
        var launcherMock = new Mock<IFileLauncher>();
        var sut = CreateSut(launcher: launcherMock.Object);
        sut.SelectedErrorItem = null;

        // ACT
        sut.OpenSelectedFileCommand.Execute(null);

        // ASSERT
        launcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void OpenSelectedFileCommand_WhenSelectedErrorItemIsNotNull_OpensFile()
    {
        // ARRANGE
        var launcherMock = new Mock<IFileLauncher>();
        var sut = CreateSut(launcher: launcherMock.Object);
        var fixture = new Fixture();
        var errorItem = fixture.Create<ErrorItem>();
        sut.SelectedErrorItem = errorItem;

        // ACT
        sut.OpenSelectedFileCommand.Execute(null);

        // ASSERT
        launcherMock.Verify(x => x.Launch(errorItem.File), Times.Once);
        launcherMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void OpenSelectedFileCommand_CanExecuteWhenSelectedErrorItemIsNotNull()
    {
        var sut = CreateSut();
        Command_CanExecuteWhenSelectedErrorItemIsNotNull(sut, sut.OpenSelectedFileCommand);
    }

    private static void Command_CanExecuteWhenSelectedErrorItemIsNotNull(ErrorListViewModel sut, ICommand command)
    {
        sut.SelectedErrorItem.Should().BeNull();
        command.CanExecute(null).Should().BeFalse();

        using (var m = command.Monitor())
        {
            var fixture = new Fixture();
            sut.SelectedErrorItem = fixture.Create<ErrorItem>();
            command.CanExecute(null).Should().BeTrue();
            m.Should().Raise(nameof(ICommand.CanExecuteChanged));
        }

        using (var m = command.Monitor())
        {
            sut.SelectedErrorItem = null;
            command.CanExecute(null).Should().BeFalse();
            m.Should().Raise(nameof(ICommand.CanExecuteChanged));
        }
    }

    private static IFixture CreateFixture()
    {
        var f = new Fixture().Customize(new AutoMoqCustomization());
        f.Customizations.Add(new ThreadCountSpecimenBuilder());
        return f;
    }

    private static ErrorListViewModel CreateSut(
        int bufferCap = 50,
        int maxItemsToShow = int.MaxValue,
        TimeSpan? bufferTimeout = null,
        IScheduler? scheduler = null,
        IFileLauncher? launcher = null)
    {
        return new ErrorListViewModel(
            maxNumberOfErrorsToShow: maxItemsToShow,
            bufferInterval: bufferTimeout ?? TimeSpan.FromMinutes(5),
            bufferCapacity: bufferCap,
            scheduler: scheduler ?? ImmediateScheduler.Instance,
            launcher: launcher ?? Mock.Of<IFileLauncher>());
    }

    private static IEnumerable<FileFinished> GenerateMessages(IFixture fixture, int count = int.MaxValue)
    {
        return from i in Enumerable.Range(0, count)
               let error = i % 2 == 0 ? $"E{i}" : null
               select new FileFinished(
                   Mock.Of<IFileInfo>(f => f.FullName == $"F{i}"),
                   fixture.Create<long>(),
                   fixture.Create<long>(),
                   fixture.Create<TimeSpan?>(),
                   error);
    }

    private static IObservable<(ErrorItem[] Snapshot, string Change)> ObserveErrorListChanges(
        ErrorListViewModel sut)
    {
        INotifyCollectionChanged errorsColl = sut.Errors;
        var changes = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
            e => errorsColl.CollectionChanged += e,
            e => errorsColl.CollectionChanged -= e);
        var resultSnapshotObservable = from change in changes
                                       select (sut.Errors.ToArray(), ToString(change.EventArgs));
        return resultSnapshotObservable;
    }

    private static (IReadOnlyList<ErrorItem> SnapShot, string Change)[] GetExpectedErrorListChangesForSingleAdds(
        IEnumerable<FileFinished> inputData,
        int maxItemsToShow)
    {
        int expectedChangeCount = maxItemsToShow + 2;
        var changeIndices = Enumerable.Range(0, expectedChangeCount);

        // Changes
        var expectedChanges = from changeIndex in changeIndices
                              select GetExpectedErrorListChangeForSingleAdds(changeIndex, inputData, maxItemsToShow);
        return expectedChanges.ToArray();
    }

    private static (IReadOnlyList<ErrorItem> SnapShot, string Change) GetExpectedErrorListChangeForSingleAdds(
        int changeIndex,
        IEnumerable<FileFinished> inputData,
        int maxItemsToShow)
    {
        if (changeIndex == 0)
        {
            // First snapshot is the clearing of the list.
            return (Array.Empty<ErrorItem>(), "Reset");
        }

        IEnumerable<ErrorItem> allExpected = GetExpectedErrorItems(inputData);
        var truncatedExpectation = allExpected.Take(maxItemsToShow).Append(GetExpectedTruncationItem());
        var list = truncatedExpectation.Take(changeIndex).ToList();
        return GetExpectedAddLastChange(list);
    }

    private static ErrorItem GetExpectedTruncationItem()
    {
        return new ErrorItem { File = "…", ErrorMessage = "See log file for further errors." };
    }

    private static IEnumerable<ErrorItem> GetExpectedErrorItems(IEnumerable<FileFinished> inputData)
    {
        return from m in inputData
               let e = m.ErrorMessage
               where e != null
               select new ErrorItem { File = m.InputFile.FullName, ErrorMessage = e };
    }

    private static IObservable<(ErrorItem[] Snapshot, ErrorItem? SelectedItem)> ObserveSelectedItemChanges(ErrorListViewModel sut)
    {
        var changes = sut.ObservableForProperty(vm => vm.SelectedErrorItem);

        return from change in changes select (sut.Errors.ToArray(), change.Value);
    }

    private static string ToString(NotifyCollectionChangedEventArgs e)
    {
        return e.Action switch
        {
            NotifyCollectionChangedAction.Add => FormatAdd(e.NewStartingIndex, ToString(e.NewItems)),
            _ => e.Action.ToString(),
        };

        static string ToString(IEnumerable? newItems)
        {
            var errorItems = newItems!.Cast<ErrorItem>();
            return string.Join("\t", errorItems);
        }
    }

    private static string FormatAdd(int addIndex, object? newItems)
    {
        return $"Add {newItems} at {addIndex}";
    }

    private static (IReadOnlyList<T> SnapShot, string Change) GetExpectedAddLastChange<T>(IReadOnlyList<T> list)
    {
        string addLastChange = FormatAdd(list.Count - 1, list[^1]);
        return (list, addLastChange);
    }
}
