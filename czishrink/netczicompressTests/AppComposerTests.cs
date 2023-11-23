// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests;

using System.Diagnostics;
using System.Reactive.Disposables;
using System.Text.RegularExpressions;

using netczicompress;
using netczicompress.ViewModels;

/// <summary>
/// Class to define test collection for <see cref="AppComposerTests"/>
/// </summary>
[CollectionDefinition(nameof(AppComposerTests), DisableParallelization = true)]
public class AppComposerTestsCollection { }

/// <summary>
/// Tests for <see cref="AppComposer"/>.
/// </summary>
[Collection(nameof(AppComposerTests))]
public partial class AppComposerTests
{
    [Fact]
    public void ComposeMainViewModel_WhenCalledDoesNotThrow_AndLogsMessage()
    {
        var listenerMock = new Mock<TraceListener>();
        var listener = listenerMock.Object;
        using var cleanup = Disposable.Create(() => Trace.Listeners.Remove(listener));
        Trace.Listeners.Add(listener);

        // ACT
        var actual = AppComposer.ComposeMainViewModel(new ProgramNameAndVersion());

        // ASSERT
        actual.Should().NotBeNull();
        var re = ExpectedLogMessagePattern();
        listenerMock.Verify(x => x.WriteLine(It.Is<string>(msg => re.IsMatch(msg))), Times.Once);

        // check composition
        actual.About.Should().BeOfType<AboutViewModel>();
        actual.ErrorList.Should().BeOfType<ErrorListViewModel>();
        actual.CurrentTasks.Should().BeOfType<CurrentTasksViewModel>();

        // check initial state
        actual.InputDirectory.Should().BeNull();
        actual.OutputDirectory.Should().BeNull();
        actual.Recursive.Should().BeTrue();
        actual.SelectedMode.Value.Should().Be(CompressionMode.CompressUncompressed);
        actual.OverallStatus.Should().BeEmpty();
    }

    [GeneratedRegex(@"^\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d\.\d+?[+-]\d\d:\d\d\|netczicompress.App\|INFO\|0\|Starting CZI Shrink \d+\.\d+\.\d+.*? using libczicompressc \d+\.\d+\.\d+.*")]
    private static partial Regex ExpectedLogMessagePattern();
}