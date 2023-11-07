// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="CompositeObserver"/>.
/// </summary>
public class CompositeObserverTests
{
    [Fact]
    public void OnCompleted_IsForwardedToAllObservers()
    {
        var observerMocks = CreateObserverMocks<int>();

        var sut = CompositeObserver.Create(
            observerMocks.Select(x => x.Object).ToArray());

        sut.OnCompleted();

        foreach (var mock in observerMocks)
        {
            mock.Verify(x => x.OnCompleted(), Times.Once);
            mock.VerifyNoOtherCalls();
        }
    }

    [Fact]
    public void OnNext_IsForwardedToAllObservers()
    {
        var observerMocks = CreateObserverMocks<int>();

        var sut = CompositeObserver.Create(
            observerMocks.Select(x => x.Object).ToArray());

        sut.OnNext(17);
        sut.OnNext(17);

        foreach (var mock in observerMocks)
        {
            mock.Verify(x => x.OnNext(17), Times.Exactly(2));
            mock.VerifyNoOtherCalls();
        }
    }

    [Fact]
    public void OnError_IsForwardedToAllObservers()
    {
        var observerMocks = CreateObserverMocks<int>();

        var sut = CompositeObserver.Create(
            observerMocks.Select(x => x.Object).ToArray());

        var ex = new Exception("bla");
        sut.OnError(ex);

        foreach (var mock in observerMocks)
        {
            mock.Verify(x => x.OnError(ex), Times.Once);
            mock.VerifyNoOtherCalls();
        }
    }

    private static Mock<IObserver<T>>[] CreateObserverMocks<T>()
    {
        var observerMocks = Enumerable.Range(1, 5).Select(i => new Mock<IObserver<T>>()).ToArray();
        return observerMocks;
    }
}
