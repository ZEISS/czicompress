// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>Checks logging failure isolation and reliable writer closure.</summary>
public class CsvLogFailureTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CompletionAttemptsCloseAndRecordsFailuresWithoutThrowing(bool failFlush, bool failClose)
    {
        using var writer = new FaultingWriter { FailFlush = failFlush, FailClose = failClose };
        var logger = new CsvLogFileWriter(writer);
        Action complete = logger.OnCompleted;

        complete.Should().NotThrow();
        writer.CloseCalls.Should().Be(1);
        logger.LastError.Should().NotBeNull();
        logger.OnCompleted();
        writer.CloseCalls.Should().Be(1);
    }

    [Fact]
    public void OnErrorPreservesTheRunErrorWhileRecordingFlushFailure()
    {
        using var writer = new FaultingWriter { FailFlush = true };
        var logger = new CsvLogFileWriter(writer);
        var primary = new InvalidOperationException("Original run failure");
        Action error = () => logger.OnError(primary);

        error.Should().NotThrow();
        writer.CloseCalls.Should().Be(1);
        logger.LastError.Should().BeSameAs(writer.FlushError);
        primary.Data.Values.Cast<object>().OfType<List<Exception>>().SelectMany(value => value).Should().Contain(writer.FlushError);
    }

    [Fact]
    public void HeaderWriteFailureIsRecordedAndClosesTheWriter()
    {
        using var writer = new FaultingWriter { FailWrite = true };
        var logger = new CsvLogFileWriter(writer);

        logger.LastError.Should().BeSameAs(writer.WriteError);
        writer.CloseCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("write", false)]
    [InlineData("flush", false)]
    [InlineData("close", false)]
    [InlineData("write", true)]
    [InlineData("flush", true)]
    [InlineData("close", true)]
    public void FatalRuntimeFailuresPropagateInsteadOfBeingRecordedAsLoggingFailures(string stage, bool wrapped)
    {
        // Construct an exception object; do not exhaust memory in this test.
        var fatal = new OutOfMemoryException("Synthetic fatal runtime failure");
        Exception failure = wrapped ? new AggregateException(new IOException("Ordinary failure"), fatal) : fatal;
        using var writer = new FaultingWriter
        {
            FailWrite = stage == "write",
            FailFlush = stage == "flush",
            FailClose = stage == "close",
            FailureOverride = failure,
        };
        var logger = stage == "write" ? null : new CsvLogFileWriter(writer);
        Action operation = () =>
        {
            if (logger == null)
            {
                _ = new CsvLogFileWriter(writer);
            }
            else
            {
                logger.OnCompleted();
            }
        };

        Record.Exception(operation).Should().BeSameAs(failure);
        logger?.LastError.Should().BeNull();
        if (stage == "flush")
        {
            writer.CloseCalls.Should().Be(1);
        }
    }

    private sealed class FaultingWriter : StringWriter
    {
        public bool FailFlush { get; init; }

        public bool FailClose { get; init; }

        public bool FailWrite { get; init; }

        public Exception? FailureOverride { get; init; }

        public int CloseCalls { get; private set; }

        public IOException FlushError { get; } = new("Flush failed");

        public IOException WriteError { get; } = new("Write failed");

        public override void Write(string? value)
        {
            if (this.FailWrite)
            {
                throw this.FailureOverride ?? this.WriteError;
            }

            base.Write(value);
        }

        public override void Flush()
        {
            if (this.FailFlush)
            {
                throw this.FailureOverride ?? this.FlushError;
            }

            base.Flush();
        }

        public override void Close()
        {
            this.CloseCalls++;
            base.Close();
            if (this.FailClose)
            {
                throw this.FailureOverride ?? new IOException("Close failed");
            }
        }
    }
}
