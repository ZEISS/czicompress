// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="NoOpFileProcessor"/>.
/// </summary>
public class NoOpFileProcessorTests
{
    [Fact]
    public void NeedsExistingOutputDirectory_ShouldBeFalse()
    {
        using var target = new NoOpFileProcessor();
        target.NeedsExistingOutputDirectory.Should().BeFalse();
    }

    [Fact]
    public void ProcessFile_WhenCalled_ReportsProgress100()
    {
        // ARRANGE
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        var mock = fixture.Freeze<Mock<ReportProgress>>();

        var sut = new NoOpFileProcessor();

        // ACT
        sut.ProcessFile(
            fixture.Create<string>(),
            fixture.Create<string>(),
            mock.Object,
            fixture.Create<CancellationToken>());

        // ASSERT
        mock.Verify(x => x.Invoke(100), Times.Once);
        mock.VerifyNoOtherCalls();
    }
}
