// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using netczicompressTests.Models.Mocks;

/// <summary>
/// Tests for <see cref="AsyncEnumerableToObservableActionAdapter{T}"/>.
/// </summary>
public class AsyncEnumerableToObservableActionAdapterTests
{
    [Theory]
    [AutoData]
    public async Task Start_WhenCalled_ReturnsItemsInSequenceAndCompletes(int[] data)
    {
        // ARRANGE
        var sut = CreateSut(data);

        var recorder = new Recorder<int>();
        sut.Output.Subscribe(recorder);

        // ACT
        var task = sut.Output.ToTask();
        sut.Start();
        await task;

        // ASSERT
        recorder.Data.Should().BeEquivalentTo(data);
    }

    [Theory]
    [AutoData]
    public async Task Start_WhenCalledTwice_ThrowsInvalidOperationException(int[] data)
    {
        // ARRANGE
        var sut = CreateSut(data);
        var task = sut.Output.ToTask();
        sut.Start();

        // ACT
        Action act = () => sut.Start();

        // ASSERT
        act.Should().Throw<InvalidOperationException>().WithMessage("Start can only be called once.");

        // CLEANUP
        await task;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_WhenCanceled_StopsAndReportsErrorInOutput(bool enumerableThrowsWhenCanceled)
    {
        // ARRANGE
        using var cts = new CancellationTokenSource();
        var sut = CreateSut(Enumerable.Range(1, 10), enumerableThrowsWhenCanceled, cts.Token);

        Recorder<int> recorder = new();
        sut.Output.Subscribe(recorder);
        sut.Output.Subscribe(
            onNext: x =>
            {
                if (x == 5)
                {
                    cts.Cancel();
                }
            },
            onError: ex => { });

        // ACT
        var task = sut.Output.ToTask();
        sut.Start();

        // ASSERT
        if (enumerableThrowsWhenCanceled)
        {
            Func<Task> checkError = () => task;
            await checkError.Should().ThrowAsync<OperationCanceledException>();
            recorder.Error.Should().BeOfType<OperationCanceledException>();
        }
        else
        {
            await task;
            recorder.Error.Should().BeNull();
        }

        recorder.Data.Should().BeEquivalentTo(
            Enumerable.Range(1, 5));
    }

    private static AsyncEnumerableToObservableActionAdapter<T> CreateSut<T>(
        IEnumerable<T> data,
        bool throwWhenCanceled = false,
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? CancellationToken.None;
        var sut = new AsyncEnumerableToObservableActionAdapter<T>(
            AsAsync(data, throwWhenCanceled, token),
            token);
        return sut;
    }

    private static async IAsyncEnumerable<T> AsAsync<T>(
        IEnumerable<T> data,
        bool throwWhenCanceled,
        [EnumeratorCancellation] CancellationToken token)
    {
        await Task.Yield();
        foreach (var item in data)
        {
            if (throwWhenCanceled)
            {
                token.ThrowIfCancellationRequested();
            }
            else if (token.IsCancellationRequested)
            {
                yield break;
            }

            yield return item;
            await Task.Yield();
        }
    }
}
