// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

using AutoFixture;

using IFolderCompressorRun = netczicompress.Models.IObservableAction<netczicompress.Models.CompressorMessage>;

public static class FolderCompressorMockExtensions
{
    public static Mock<IFolderCompressor> WithRun(this Mock<IFolderCompressor> self, IFolderCompressorRun run)
    {
        self
            .Setup(x => x.PrepareNewRun(It.IsAny<FolderCompressorParameters>(), It.IsAny<CancellationToken>()))
            .Returns(run);
        return self;
    }

    public static Mock<IFolderCompressor> WithData(this Mock<IFolderCompressor> self, IEnumerable<CompressorMessage> messages)
    {
        var runMock = new Mock<IFolderCompressorRun>().WithData(messages);
        return self.WithRun(runMock.Object);
    }

    public static Mock<IFolderCompressor> WithAutoData(this Mock<IFolderCompressor> self, IFixture fixture)
    {
        IEnumerable<CompressorMessage> messages =
            fixture.CreateMany<CompressorMessage.FileStarting>();
        messages = messages.Concat(
            fixture.CreateMany<CompressorMessage.FileFinished>());

        var runMock = new Mock<IFolderCompressorRun>().WithData(messages);
        return self.WithRun(runMock.Object);
    }

    public static Mock<IFolderCompressor> WithException(this Mock<IFolderCompressor> self, Exception ex)
    {
        var runMock = new Mock<IFolderCompressorRun>().WithException(ex);
        return self.WithRun(runMock.Object);
    }

    public static Mock<IFolderCompressor> WithWaitForCancellation(this Mock<IFolderCompressor> self)
    {
        self
            .Setup(
                x => x.PrepareNewRun(It.IsAny<FolderCompressorParameters>(), It.IsAny<CancellationToken>()))
            .Returns<FolderCompressorParameters, CancellationToken>(
                (_, token) => new Mock<IFolderCompressorRun>().WithWaitForCancellation(token).Object);
        return self;
    }
}
