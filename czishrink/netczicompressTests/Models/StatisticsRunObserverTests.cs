// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reactive.Subjects;

using netczicompressTests.Customizations;
using netczicompressTests.Models.Mocks;
using static netczicompress.Models.CompressorMessage;

/// <summary>
/// Tests for <see cref="StatisticsRunObserver"/>.
/// </summary>
public class StatisticsRunObserverTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    public void ObserveRun_WhenRunCompletes_CorrectFirstAndFinalResult(int scanTimeMillis)
    {
        // ARRANGE
        var f = new Fixture().Customize(new AutoMoqCustomization());
        f.Customizations.Add(new ThreadCountSpecimenBuilder());
        var parameters = f.Create<FolderCompressorParameters>();

        var recorder = new Recorder<AggregateStatistics>();
        var fileMock = new Mock<IFileInfo>(MockBehavior.Strict).Object;

        var data = new FileFinished[]
        {
            new(fileMock, 20, 10, null, null),
            new(fileMock, 20, 10, null, null),
            new(fileMock, 20, 10, null, null),
            new(fileMock, 20, 10, null, null),
            new(fileMock, 20, 10, null, null),
            new(fileMock, 500, 400, null, null),
            new(fileMock, 10, 1000, null, "Failed"),
            new(fileMock, 10, 1000, null, "Failed"),
        };

        var observable = new Subject<FileFinished>();
        var watch = Stopwatch.StartNew();

        var sut = new StatisticsRunObserver(
            recorder,
            TimeSpan.FromMilliseconds(scanTimeMillis),
            DefaultScheduler.Instance);

        // ACT
        sut.ObserveRun(parameters, observable.AsObservable());

        foreach (var item in data)
        {
            Thread.Sleep(scanTimeMillis / 2);
            observable.OnNext(item);
        }

        observable.OnCompleted();

        var durationUpperLimit = watch.Elapsed
             + TimeSpan.FromMilliseconds(2);

        SpinWait.SpinUntil(() => recorder.Completed);

        // ASSERT
        recorder.Data.Count.Should().BeInRange(2, data.Length + 1);
        AssertMonotonicity(recorder.Data);

        recorder.Data[0].Should().Be(AggregateStatistics.Empty);

        var lastStats = recorder.Data[^1];

        lastStats.FilesWithErrors.Should().Be(2);
        lastStats.FilesWithNoErrors.Should().Be(6);
        lastStats.OutputToInputRatio.Should().Be(450.0f / 600);
        lastStats.DeltaBytes.Should().Be(450 - 600);
        lastStats.InputBytes.Should().Be(600);
        lastStats.OutputBytes.Should().Be(450);
        lastStats.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        lastStats.Duration.Should().BeLessThan(durationUpperLimit);
    }

    private static void AssertMonotonicity(
        IReadOnlyList<AggregateStatistics> data)
    {
        for (int i = 1; i < data.Count; i++)
        {
            var current = data[i];
            var previous = data[i - 1];

            current.FilesWithErrors.Should().BeGreaterThanOrEqualTo(
                previous.FilesWithErrors);
            current.FilesWithNoErrors.Should().BeGreaterThanOrEqualTo(
                previous.FilesWithNoErrors);
            current.InputBytes.Should().BeGreaterThanOrEqualTo(
                previous.InputBytes);
            current.OutputBytes.Should().BeGreaterThanOrEqualTo(
                previous.OutputBytes);
            current.Duration.Should().BeGreaterThan(previous.Duration);
        }
    }
}
