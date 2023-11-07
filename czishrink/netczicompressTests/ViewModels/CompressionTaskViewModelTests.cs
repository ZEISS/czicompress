// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.Reactive.Subjects;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="CompressionTaskViewModel"/>.
/// </summary>
/// <remarks>
/// Note that <see cref="CompressionTaskViewModel.AddWhileNotCompleted"/>
/// is tested via <see cref="CurrentTasksViewModelTests"/>.
/// </remarks>
public class CompressionTaskViewModelTests
{
    [Fact]
    public void PublicProperties_WhenGot_HaveExpectedValues()
    {
        // ARRANGE
        var fileName = CreateFixture().Create<string>();
        var progress = new BehaviorSubject<int>(0);
        var sut = new CompressionTaskViewModel(
            fileName,
            progress);

        List<(int Progress, (string FileName, int ProgressPercent, bool IsIndeterminateProgress, bool IsCompleted, string ChangedProps))> data = new();
        var changedProperties = new List<string?>();
        sut.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName);

        void Collect(int p)
        {
            changedProperties.Clear();
            progress.OnNext(p);
            changedProperties.Sort();
            data.Add(
                (p,
                   (sut.FileName,
                    sut.ProgressPercent,
                    sut.IsIndeterminateProgress,
                    sut.IsCompleted,
                    string.Join(",", changedProperties))));
        }

        // ACT
        Collect(0);
        Collect(1);
        Collect(2);
        Collect(2);
        Collect(10);
        Collect(50);
        Collect(99);
        Collect(100);

        // ASSERT
        const string ProgressPercent = nameof(sut.ProgressPercent);
        const string IsIndeterminateProgress = nameof(sut.IsIndeterminateProgress);
        const string IsCompleted = nameof(sut.IsCompleted);
        static string Cat(params string[] args) => string.Join(",", args);
        data.Should().BeEquivalentTo(
            new[]
            {
                (0,   (fileName, 0,   true,  false, string.Empty)),
                (1,   (fileName, 1,   true,  false, ProgressPercent)),
                (2,   (fileName, 2,   false, false, Cat(IsIndeterminateProgress, ProgressPercent))),
                (2,   (fileName, 2,   false, false, string.Empty)),
                (10,  (fileName, 10,  false, false, ProgressPercent)),
                (50,  (fileName, 50,  false, false, ProgressPercent)),
                (99,  (fileName, 99,  false, false, ProgressPercent)),
                (100, (fileName, 100, false, true,  Cat(IsCompleted, ProgressPercent))),
            });
    }

    private static IFixture CreateFixture()
    {
        return new Fixture().Customize(new AutoMoqCustomization());
    }
}
