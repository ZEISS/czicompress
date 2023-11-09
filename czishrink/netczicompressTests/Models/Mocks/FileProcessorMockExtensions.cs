// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

/// <summary>
/// Extension method for mocks of <see cref="IFileProcessor"/> and collections thereof.
/// </summary>
internal static class FileProcessorMockExtensions
{
    public static Mock<IFileProcessor> WithProcessFile(
        this Mock<IFileProcessor> mock,
        Action<string, string, ReportProgress, CancellationToken> processFile)
    {
        mock
            .Setup(x => x.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
            .Callback(processFile);
        return mock;
    }

    public static T CheckCount<T>(this T listOfCreatedProcessors, int maxCount)
        where T : IReadOnlyCollection<Mock<IFileProcessor>>
    {
        listOfCreatedProcessors.Count.Should().BeLessThanOrEqualTo(maxCount);
        return listOfCreatedProcessors;
    }

    public static IEnumerable<(string InputFile, string OutputFile)> GetProcessedFileArgs(this Mock<IFileProcessor> p)
    {
        var allArgses = from invocation in p.Invocations
        where invocation.Method.Name == nameof(IFileProcessor.ProcessFile)
        select invocation.Arguments;

        var filenameArgses = from args in allArgses
                             select ((string)args[0], (string)args[1]);

        return filenameArgses;
    }

    public static T CheckDisposed<T>(this T processors)
        where T : IEnumerable<Mock<IFileProcessor>>
    {
        // check all disposables have been disposed
        foreach (var d in processors)
        {
            d.Verify(x => x.Dispose(), Times.Once);
        }

        return processors;
    }
}
