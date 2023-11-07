// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

using System.Reactive.Subjects;

using IFolderCompressorRun = netczicompress.Models.IObservableAction<netczicompress.Models.CompressorMessage>;

public static class FolderCompressorRunMockExtensions
{
    public static Mock<IObservableAction<T>> WithData<T>(this Mock<IObservableAction<T>> runMock, IEnumerable<T> messages)
    {
        var subject = new Subject<T>();
        runMock.SetupGet(x => x.Output).Returns(subject.AsObservable());
        runMock
            .Setup(x => x.Start())
            .Callback(() => subject.OnNextAllAndComplete(messages));
        return runMock;
    }

    public static Mock<IFolderCompressorRun> WithException(this Mock<IFolderCompressorRun> runMock, Exception ex)
    {
        var subject = new Subject<CompressorMessage>();
        runMock.SetupGet(x => x.Output).Returns(subject.AsObservable());
        runMock
            .Setup(x => x.Start())
            .Callback(() => subject.OnError(ex));
        return runMock;
    }

    public static Mock<IFolderCompressorRun> WithWaitForCancellation(
        this Mock<IFolderCompressorRun> runMock,
        CancellationToken token)
    {
        var subject = new Subject<CompressorMessage>();
        runMock.SetupGet(x => x.Output).Returns(subject.AsObservable());
        runMock
            .Setup(x => x.Start())
            .Callback(() =>
            {
                token.Register(() =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException ex)
                    {
                        subject.OnError(ex);
                    }
                });
            });
        return runMock;
    }

    public static IObservableAction<T> ToObservableAction<T>(this IEnumerable<T> messages)
    {
        return new Mock<IObservableAction<T>>().WithData(messages).Object;
    }
}
