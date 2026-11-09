// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.Diagnostics;

/// <summary>Records secondary failures without replacing the operation's error.</summary>
internal static class FailureDiagnostics
{
    public static bool Try(Action action, Exception? primary = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception secondary)
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
            catch
            {
                // Diagnostic storage must not replace the exception being reported.
            }
        }

        try
        {
            Trace.TraceWarning("Secondary processing/cleanup failure: {0}", secondary);
        }
        catch
        {
            // A failing trace sink must not interfere with cleanup or error delivery.
        }
    }
}
