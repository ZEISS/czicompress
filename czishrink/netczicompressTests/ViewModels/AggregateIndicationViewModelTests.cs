// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="AggregateIndicationViewModel"/>.
/// </summary>
public class AggregateIndicationViewModelTests
{
    [Fact]
    public void Properties_AfterCreation_HaveExpectedValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        // ACT
        var sut = fixture.Create<AggregateIndicationViewModel>();

        // ASSERT
        sut.IndicationStatus.Should().Be(AggregateIndicationStatus.NotStarted);
    }

    [Fact]
    public void OnNext_WhenCalled_ChangesPropertyValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AggregateIndicationViewModel>();
        var newValue = fixture.Create<AggregateStatistics>();
        using var monitor = sut.Monitor();

        // ACT
        sut.OnNext(newValue);

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.IndicationStatus);
        sut.IndicationStatus.Should().Be(AggregateIndicationStatus.Started);
    }

    [Theory]
    [InlineData(0, AggregateIndicationStatus.Success)]
    [InlineData(-1, AggregateIndicationStatus.Success)]
    [InlineData(int.MinValue, AggregateIndicationStatus.Success)]
    [InlineData(1, AggregateIndicationStatus.Error)]
    [InlineData(100, AggregateIndicationStatus.Error)]
    [InlineData(int.MaxValue, AggregateIndicationStatus.Error)]
    public void OnCompleted_WhenCalled_ChangesIndicationStatus(int filesWithErrors, AggregateIndicationStatus expectation)
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AggregateIndicationViewModel>();
        var newValue = fixture.Create<AggregateStatistics>();
        newValue = newValue with { FilesWithErrors = filesWithErrors };
        sut.OnNext(newValue);
        using var monitor = sut.Monitor();

        // ACT
        sut.OnCompleted();

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.IndicationStatus);
        sut.IndicationStatus.Should().Be(expectation);
    }

    [Fact]
    public void OnError_WhenExceptionIsOperationCancelledException_ChangesStatusToCancelled()
    {
        OnError_WhenExceptionIsOfType_ChangesStatusTo<OperationCanceledException>(AggregateIndicationStatus.Cancelled);
    }

    [Fact]
    public void OnError_WhenExceptionIsNotOperationCancelledException_ChangesStatusToError()
    {
        OnError_WhenExceptionIsOfType_ChangesStatusTo<Exception>(
            AggregateIndicationStatus.Error);
    }

    private static void OnError_WhenExceptionIsOfType_ChangesStatusTo<T>(AggregateIndicationStatus expectation)
        where T : Exception
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AggregateIndicationViewModel>();
        using var monitor = sut.Monitor();

        // ACT
        sut.OnError(Mock.Of<T>());

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.IndicationStatus);
        sut.IndicationStatus.Should().Be(expectation);
    }

    private static IFixture CreateFixture()
    {
        // ARRANGE
        return new Fixture().Customize(new AutoMoqCustomization());
    }
}
