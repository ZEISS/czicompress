// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.Diagnostics;
using System.IO.Abstractions;
using System.Reactive.Subjects;

using netczicompress.ViewModels;

using netczicompressTests.Models.Mocks;

using static netczicompress.Models.CompressorMessage;

/// <summary>
/// Tests for <see cref="CurrentTasksViewModel"/>.
/// </summary>
public class CurrentTasksViewModelTests
{
    [Fact]
    public void OnNext_WhenTaskNotFinished_AddsTaskToList()
    {
        // ARRANGE
        var sut = new CurrentTasksViewModel(ImmediateScheduler.Instance);

        for (int i = 0; i < 10; i++)
        {
            var message = CreateFixture().Create<FileStarting>();

            // ACT
            sut.OnNext(message);

            // ASSERT
            sut.CompressionTasks.Count().Should().Be(i + 1);
            sut.CompressionTasks[i].FileName.Should().Be(message.InputFile.FullName);
        }
    }

    [Fact]
    public void OnNext_WhenTaskFinished_DoesNotAddTaskToList()
    {
        // ARRANGE
        var sut = new CurrentTasksViewModel(ImmediateScheduler.Instance);

        using var progress = new BehaviorSubject<int>(100);
        var message = new FileStarting(Mock.Of<IFileInfo>(), progress);

        // ACT
        sut.OnNext(message);

        // ASSERT
        sut.CompressionTasks.Should().BeEmpty();
    }

    [Fact]
    public void OnCompleted_WhenCalled_ClearsTaskList()
    {
        // ARRANGE
        var sut = new CurrentTasksViewModel(ImmediateScheduler.Instance);

        sut.OnNextAll(CreateFixture().CreateMany<FileStarting>(10));
        Debug.Assert(sut.CompressionTasks.Count == 10, "Error in ARRANGE");

        // ACT
        sut.OnCompleted();

        // ASSERT
        sut.CompressionTasks.Should().BeEmpty();
    }

    [Fact]
    public void OnError_WhenCalled_ClearsTaskList()
    {
        // ARRANGE
        var sut = new CurrentTasksViewModel(ImmediateScheduler.Instance);

        sut.OnNextAll(CreateFixture().CreateMany<FileStarting>(10));
        Debug.Assert(sut.CompressionTasks.Count == 10, "Error in ARRANGE");

        // ACT
        sut.OnError(new Exception());

        // ASSERT
        sut.CompressionTasks.Should().BeEmpty();
    }

    [Fact]
    public void WhenTaskCompletes_TaskIsRemovedFromListOnGuiScheduler()
    {
        // ARRANGE
        var guiScheduler = new SchedulerMock();
        var sut = new CurrentTasksViewModel(guiScheduler);

        sut.OnNextAll(CreateFixture().CreateMany<FileStarting>(5));
        using var progress = new BehaviorSubject<int>(0);
        var completingTask = new FileStarting(
            Mock.Of<IFileInfo>(f => f.FullName == "Foo_Bar_Baz"),
            progress);
        sut.OnNext(completingTask);
        sut.OnNextAll(CreateFixture().CreateMany<FileStarting>(5));

        var taskViewModel = sut.CompressionTasks[5];
        var initialTasks = sut.CompressionTasks.ToArray();
        Debug.Assert(initialTasks.Length == 11, "Error in ARRANGE");
        Debug.Assert(taskViewModel.FileName == "Foo_Bar_Baz", "Error in ARRANGE");

        // ACT
        progress.OnNext(100);
        var beforeSchedulerRun = sut.CompressionTasks.ToArray();
        guiScheduler.RunQueuedActions();
        var afterSchedulerRun = sut.CompressionTasks.ToArray();

        // ASSERT
        beforeSchedulerRun.Should().HaveCount(11);
        beforeSchedulerRun.SequenceEqual(initialTasks).Should().BeTrue();

        afterSchedulerRun.Should().HaveCount(10);
        afterSchedulerRun.SequenceEqual(initialTasks.Where(t => !object.ReferenceEquals(t, taskViewModel))).Should().BeTrue();
    }

    private static IFixture CreateFixture()
    {
        return new Fixture().Customize(new AutoMoqCustomization());
    }
}
