// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

public static class ObservableExtensions
{
    public static Recorder<T> StartRecording<T>(this IObservable<T> source)
    {
        var result = new Recorder<T>();
        _ = source.Subscribe(result);
        return result;
    }

    public static void OnNextAll<T>(this IObserver<T> observer, IEnumerable<T> values)
    {
        foreach (var item in values)
        {
            observer.OnNext(item);
        }
    }

    public static void OnNextAllAndComplete<T>(this IObserver<T> observer, IEnumerable<T> values)
    {
        observer.OnNextAll(values);
        observer.OnCompleted();
    }
}
