// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.Diagnostics;

/// <summary>Records secondary failures without replacing the operation's error.</summary>
internal static class FailureDiagnostics
{
    /// <summary>Cleanup may handle ordinary failures, never fatal runtime errors.</summary>
    /// <param name="error">The failure to classify, including any wrapped failures.</param>
    /// <returns>Whether cleanup may report the failure and continue.</returns>
    public static bool IsRecoverable(Exception error) => error switch
    {
        OutOfMemoryException or StackOverflowException or AccessViolationException => false,
        AggregateException aggregate => aggregate.InnerExceptions.All(IsRecoverable),
        _ => error.InnerException == null || IsRecoverable(error.InnerException),
    };

    public static bool Try(Action action, Exception? primary = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception secondary) when (IsRecoverable(secondary))
        {
            Record(secondary, primary);
            return false;
        }
    }

    public static void Record(Exception secondary, Exception? primary = null)
    {
        if (primary != null)
        {
            try
            {
                lock (primary)
                {
                    const string key = "CziShrink.SecondaryErrors";
                    if (primary.Data[key] is not List<Exception> errors)
                    {
                        errors = new List<Exception>();
                        primary.Data[key] = errors;
                    }

                    errors.Add(secondary);
                }
            }
            catch (Exception storageError) when (IsRecoverable(storageError))
            {
                // Diagnostic storage must not replace the exception being reported.
            }
        }

        try
        {
            Trace.TraceWarning("Secondary processing/cleanup failure: {0}", secondary);
        }
        catch (Exception traceError) when (IsRecoverable(traceError))
        {
            // A failing trace sink must not interfere with cleanup or error delivery.
        }
    }
}
