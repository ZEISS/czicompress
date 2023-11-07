// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

/// <summary>
/// A logger provider and factory that uses <see cref="Trace"/> to write log messages.
/// </summary>
/// <remarks>
/// Logs levels Information, Warning, Error, and Critical.
/// <para/>
/// <see cref="ILoggerFactory.AddProvider"/> is not supported.
/// </remarks>
public sealed class TraceLoggerFactory : ILoggerFactory, ILoggerProvider
{
    private readonly bool useLogLevelsAsTraceLevels;

    public TraceLoggerFactory(bool useLogLevelsAsTraceLevels = false)
    {
        this.useLogLevelsAsTraceLevels = useLogLevelsAsTraceLevels;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TraceLogger(categoryName, this.useLogLevelsAsTraceLevels);
    }

    void ILoggerFactory.AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException();
    }

    void IDisposable.Dispose()
    {
        Trace.Flush();
    }

    private sealed class TraceLogger : ILogger
    {
        private readonly string categoryName;
        private readonly bool useLogLevelsAsTraceLevels;

        public TraceLogger(string categoryName, bool useLogLevelsAsTraceLevels)
        {
            this.categoryName = categoryName;
            this.useLogLevelsAsTraceLevels = useLogLevelsAsTraceLevels;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (this.IsEnabled(logLevel))
            {
                var msg = $"{DateTimeOffset.Now:O}|{this.categoryName}|{ToString(logLevel)}|{eventId}|{formatter(state, exception)}";
                if (this.useLogLevelsAsTraceLevels)
                {
                    switch (logLevel)
                    {
                        case LogLevel.Error:
                        case LogLevel.Critical:
                            Trace.TraceError(msg);
                            break;
                        case LogLevel.Information:
                            Trace.TraceInformation(msg);
                            break;
                        case LogLevel.Warning:
                            Trace.TraceWarning(msg);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    Trace.WriteLine(msg);
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => false,
                LogLevel.Debug => false,
                LogLevel.Information => true,
                LogLevel.Warning => true,
                LogLevel.Error => true,
                LogLevel.Critical => true,
                LogLevel.None => false,
                _ => false,
            };
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        private static string ToString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => string.Empty,
            };
        }
    }
}
