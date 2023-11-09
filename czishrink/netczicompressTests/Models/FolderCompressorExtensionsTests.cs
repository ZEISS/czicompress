// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using netczicompressTests.Customizations;
using netczicompressTests.Models.Mocks;
using static netczicompress.Models.CompressorMessage;
using IFolderCompressorRun = netczicompress.Models.IObservableAction<netczicompress.Models.CompressorMessage>;

/// <summary>
/// Tests for <see cref="FolderCompressorExtensions"/>.
/// </summary>
public class FolderCompressorExtensionsTests
{
    [Fact]
    public void DecorateWithRunObserver_WhenCalled_ReportsMessagesFromRunToObserver()
    {
        // ARRANGE
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        fixture.Customizations.Add(new ThreadCountSpecimenBuilder());
        var p = fixture.Create<FolderCompressorParameters>();
        var t = fixture.Create<CancellationToken>();

        IEnumerable<CompressorMessage> runData = CreateMixedMessages(fixture);

        var runMock = new Mock<IFolderCompressorRun>().WithData(runData);

        var coreMock = new Mock<IFolderCompressor>();
        coreMock
            .Setup(x => x.PrepareNewRun(p, t))
            .Returns(runMock.Object)
            .Verifiable(Times.Once);

        var recorder = new Recorder<FileFinished>();
        var runObserverMock = new Mock<IFolderCompressorRunObserver<FileFinished>>();
        runObserverMock
            .Setup(
                x => x.ObserveRun(p, It.IsAny<IObservable<FileFinished>>()))
            .Callback<FolderCompressorParameters, IObservable<FileFinished>>(
                (pp, obs) => obs.Subscribe(recorder))
            .Verifiable(Times.Once);

        var sut = coreMock.Object.DecorateWithRunObserver(runObserverMock.Object);

        // ACT
        sut.PrepareNewRun(p, t).Start();

        // ASSERT
        coreMock.Verify();
        runObserverMock.Verify();
        recorder.Data.Should().BeEquivalentTo(
            runData.OfType<FileFinished>(),
            cfg => cfg.WithoutStrictOrdering());
    }

    [Fact]
    public void DecorateWithObserver_WhenCalled_ReportsMessagesFromRunToObserver()
    {
        // ARRANGE
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        fixture.Customizations.Add(new ThreadCountSpecimenBuilder());
        var p = fixture.Create<FolderCompressorParameters>();
        var t = fixture.Create<CancellationToken>();

        var runData = CreateMixedMessages(fixture);

        var runMock = new Mock<IFolderCompressorRun>().WithData(runData);

        var coreMock = new Mock<IFolderCompressor>();
        coreMock
            .Setup(x => x.PrepareNewRun(p, t))
            .Returns(runMock.Object)
            .Verifiable(Times.Once);

        var recorder = new Recorder<FileFinished>();

        var sut = coreMock.Object.DecorateWithObserver(recorder, Scheduler.Immediate);

        // ACT
        sut.PrepareNewRun(p, t).Start();

        // ASSERT
        coreMock.Verify();
        recorder.Data.Should().BeEquivalentTo(
            runData.OfType<FileFinished>(),
            cfg => cfg.WithoutStrictOrdering());
    }

    private static IEnumerable<CompressorMessage> CreateMixedMessages(IFixture fixture)
    {
        return fixture.CreateMany<FileStarting>(5).Cast<CompressorMessage>()
            .Concat(fixture.CreateMany<FileFinished>(3))
            .Concat(fixture.CreateMany<FileStarting>(5))
            .Concat(fixture.CreateMany<FileFinished>(8)).ToArray();
    }
}
