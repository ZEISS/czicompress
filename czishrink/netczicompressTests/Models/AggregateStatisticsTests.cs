// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="AggregateStatistics"/>.
/// </summary>
public class AggregateStatisticsTests
{
    [Fact]
    public void Empty_EqualsDefault()
    {
        AggregateStatistics.Empty.Should().Be(default(AggregateStatistics));
    }

    [Fact]
    public void Empty_HasExpectedPropertyValues()
    {
        AggregateStatistics sut = AggregateStatistics.Empty;
        sut.Should().BeEquivalentTo(
        new AggregateStatistics
        {
            FilesWithErrors = 0,
            FilesWithNoErrors = 0,
            Duration = TimeSpan.Zero,
            InputBytes = 0,
            OutputBytes = 0,
        },
        cfg => cfg.ComparingByMembers<AggregateStatistics>());

        sut.DeltaBytes.Should().Be(0);
        float.IsNaN(sut.OutputToInputRatio).Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void NonEmpty_HasExpectedCalculatedPropertyValues(AggregateStatistics sut)
    {
        sut.DeltaBytes.Should().Be(sut.OutputBytes - sut.InputBytes);
        sut.OutputToInputRatio.Should().Be(1.0f * sut.OutputBytes / sut.InputBytes);
    }

    [Fact]
    public void ConcreteExample_HasExpectedCalculatedPropertyValues()
    {
        var sut = new AggregateStatistics
        {
            OutputBytes = 1500,
            InputBytes = 2000,
        };
        sut.DeltaBytes.Should().Be(-500);
        sut.OutputToInputRatio.Should().Be(0.75f);
    }
}
