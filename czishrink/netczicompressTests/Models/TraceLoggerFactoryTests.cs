// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Class to define test collection for <see cref="TraceLoggerFactoryTests"/>
/// </summary>
[CollectionDefinition(nameof(TraceLoggerFactoryTests), DisableParallelization = true)]
public sealed class TraceLoggerFactoryTestsCollection {}

/// <summary>
/// Tests for <see cref="TraceLoggerFactory"/>.
/// </summary>
[Collection(nameof(TraceLoggerFactoryTests)]
public sealed class TraceLoggerFactoryTests : IDisposable
{
    private readonly MemoryTraceListener listener = new();

    public TraceLoggerFactoryTests()
    {
        Trace.Listeners.Add(this.listener);
        this.listener.Reset();
    }

    [Fact]
    public void AddProvider_WhenCalled_Throws()
    {
        // ARRANGE
        using ILoggerFactory sut = new TraceLoggerFactory();

        // ACT
        Action act = () => sut.AddProvider(Mock.Of<ILoggerProvider>());

        // ASSERT
        act.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(true, LogLevel.Information, "Information", "INFO")]
    [InlineData(false, LogLevel.Information, "Information", "INFO")]
    [InlineData(true, LogLevel.Warning, "Warning", "WARN")]
    [InlineData(false, LogLevel.Warning, "Warning", "WARN")]
    [InlineData(true, LogLevel.Error, "Error", "ERROR")]
    [InlineData(false, LogLevel.Error, "Error", "ERROR")]
    [InlineData(true, LogLevel.Critical, "Error", "FATAL")]
    [InlineData(false, LogLevel.Critical, "Error", "FATAL")]
    public void LogHighLevel_WhenCalled_TracesExpected(
        bool useLogLevels,
        LogLevel level,
        string expectedTraceLevel,
        string expectedLogLevel)
    {
        // ARRANGE
        using ILoggerFactory sut = new TraceLoggerFactory(useLogLevels);
        var logger = sut.CreateLogger<TraceLoggerFactoryTests>();

        // ACT
        DateTimeOffset before = DateTimeOffset.Now;
        logger.Log(level, "Bla {eins} {zwei}", 1, 2);

        // ASSERT
        var trace = this.listener.ToString();
        if (useLogLevels)
        {
            trace = CheckAndSkipPrefix(trace, expectedTraceLevel);
        }

        CheckLogMessage(trace, before, expectedLogLevel, "Bla 1 2");
    }

    [Theory]
    [InlineAutoData(LogLevel.None)]
    [InlineAutoData(LogLevel.Debug)]
    [InlineAutoData(LogLevel.Trace)]
    [InlineAutoData((LogLevel)int.MaxValue)]
    public void LogLowLevel_WhenCalled_TracesNothing(LogLevel level, bool useLogLevels)
    {
        // ARRANGE
        using ILoggerFactory sut = new TraceLoggerFactory(useLogLevels);
        var logger = sut.CreateLogger<TraceLoggerFactoryTests>();

        // ACT
        logger.Log(level, "Bla {eins} {zwei}", 1, 2);

        // ASSERT
        var trace = this.listener.ToString();
        trace.Should().BeEmpty();
    }

    [Theory]
    [InlineAutoData(LogLevel.None, false)]
    [InlineAutoData(LogLevel.Debug, false)]
    [InlineAutoData(LogLevel.Trace, false)]
    [InlineAutoData((LogLevel)int.MaxValue, false)]
    [InlineAutoData(LogLevel.Information, true)]
    [InlineAutoData(LogLevel.Warning, true)]
    [InlineAutoData(LogLevel.Error, true)]
    [InlineAutoData(LogLevel.Critical, true)]
    public void LoggerIsEnabled_WhenCalled_ReturnsExpected(LogLevel level, bool expected, bool useLogLevels)
    {
        // ARRANGE
        using ILoggerFactory sut = new TraceLoggerFactory(useLogLevels);
        var logger = sut.CreateLogger<TraceLoggerFactoryTests>();

        // ACT
        var actual = logger.IsEnabled(level);

        // ASSERT
        actual.Should().Be(expected);
    }

    [Theory]
    [AutoData]
    public void LoggerBeginScope_WhenCalled_ReturnsNull(bool useLogLevels)
    {
        // ARRANGE
        using ILoggerFactory sut = new TraceLoggerFactory(useLogLevels);
        var logger = sut.CreateLogger<TraceLoggerFactoryTests>();

        // ACT
        var actual = logger.BeginScope("bla");

        // ASSERT
        actual.Should().BeNull();
        this.listener.ToString().Should().BeEmpty();
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(this.listener);
    }

    private static string CheckAndSkipPrefix(string trace, string level)
    {
        // we expect that the trace-text starts with something like "<loggername> {level}: 0 : ",
        //  and we want to remove this part from the string
        var levelText = $"{level}: 0 : ";
        int index = trace.IndexOf(levelText, StringComparison.Ordinal);
        index.Should().BePositive();
        trace = trace[(index + levelText.Length)..];
        return trace;
    }

    private static void CheckLogMessage(string logLine, DateTimeOffset before, string expectedLevel, string expectedMessage)
    {
        var traceTokens = logLine.Split('|');
        traceTokens.Length.Should().Be(5);
        var loggedDate = DateTimeOffset.Parse(traceTokens[0]);
        loggedDate.Should().BeBefore(DateTimeOffset.Now);
        loggedDate.Should().BeAfter(before);
        traceTokens[1].Should().Be(typeof(TraceLoggerFactoryTests).FullName);
        traceTokens[2].Should().Be(expectedLevel);
        traceTokens[3].Should().Be("0");
        traceTokens[4].Should().Be(expectedMessage + "\n");
    }

    private class MemoryTraceListener : TraceListener
    {
        private readonly StringBuilder result = new();

        public void Reset()
        {
            Trace.Flush();
            this.result.Clear();
        }

        public override void Write(string? message)
        {
            this.result.Append(message);
        }

        public override void WriteLine(string? message)
        {
            this.result.Append(message).Append('\n');
        }

        public override string ToString()
        {
            Trace.Flush();
            return this.result.ToString();
        }
    }
}
