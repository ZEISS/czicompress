// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using netczicompressTests.Customizations;

using IFolderCompressorRun = netczicompress.Models.IObservableAction<netczicompress.Models.CompressorMessage>;

/// <summary>
/// Tests for <see cref="FolderCompressorDecorator"/>.
/// </summary>
public class FolderCompressorDecoratorTests
{
    [Fact]
    public void PrepareNewRun_WhenCalled_CallsObserveRunWithExpectedArgs()
    {
        // ARRANGE
        var strictMocks = new MockRepository(MockBehavior.Strict);
        var coreMock = strictMocks.Create<IFolderCompressor>();
        var observeRun = new Mock<Action<FolderCompressorParameters, IObservable<CompressorMessage>>>();
        var coreOutput = strictMocks.OneOf<IObservable<CompressorMessage>>();
        var observableAction = strictMocks.OneOf<IFolderCompressorRun>(
            x => x.Output == coreOutput);

        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        fixture.Customizations.Add(new CompressionLevelSpecimenBuilder());
        fixture.Customizations.Add(new ThreadCountSpecimenBuilder());
        var p = fixture.Create<FolderCompressorParameters>();
        var t = fixture.Create<CancellationToken>();
        coreMock
            .Setup(x => x.PrepareNewRun(p, t))
            .Returns(observableAction)
            .Verifiable(Times.Once);

        var sut = new FolderCompressorDecorator
        {
            Core = coreMock.Object,
            ObserveRun = observeRun.Object,
        };

        // ACT
        var actual = sut.PrepareNewRun(p, t);

        // ASSERT
        actual.Should().BeSameAs(observableAction);
        coreMock.Verify();
        observeRun.Verify(d => d.Invoke(p, coreOutput), Times.Once);
        observeRun.VerifyNoOtherCalls();
    }
}
