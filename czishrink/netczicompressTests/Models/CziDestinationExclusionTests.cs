// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Threading.Tasks;

/// <summary>Tests exclusion of existing destinations in the CZI-only scanner.</summary>
public class CziDestinationExclusionTests
{
    [Fact]
    public async Task NestedDestinationIsExcludedEvenWhenItsFilesPredateTheRun()
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.Combine(input, "nested", "destination");
        fs.AddFile(fs.Path.Combine(input, "source.czi"), new MockFileData("source"));
        fs.AddFile(fs.Path.Combine(input, "nested", "sibling.czi"), new MockFileData("sibling"));
        fs.AddFile(fs.Path.Combine(output, ".czishrink-old", "old.czi"), new MockFileData("valuable existing output"));
        var compressor = new MultiThreadedFolderCompressor((_, _) => new NoOpFileProcessor(), new FileProcessingFailedHandler())
        {
            MatchExtensionCasing = MatchCasing.PlatformDefault,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(output),
            true,
            CompressionMode.NoOp,
            new ExecutionOptions(ThreadCount.Default),
            new ProcessingOptions(CompressionLevel.Default));
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        List<CompressorMessage.FileFinished> results = new();
        using var subscription = run.Output.OfType<CompressorMessage.FileFinished>().Subscribe(results.Add);
        Task completion = run.Output.ToTask();
        run.Start();
        await completion;

        results.Select(item => item.InputFile.Name).Should().BeEquivalentTo(
            new[] { "source.czi", "sibling.czi" }, options => options.WithoutStrictOrdering());
        results.Should().OnlyContain(item => item.ErrorMessage == null);
        fs.File.ReadAllText(fs.Path.Combine(output, ".czishrink-old", "old.czi")).Should().Be("valuable existing output");
    }
}
