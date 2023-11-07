// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Desktop;

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.ReactiveUI;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // NOTE: when in use, a GUID will be prepended to the filename.
        using TextWriterTraceListener listener = CreateTraceListener();
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Trace.Flush();
            Trace.Listeners.Remove(listener);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(level: Avalonia.Logging.LogEventLevel.Error)
            .UseReactiveUI();
    }

    private static TextWriterTraceListener CreateTraceListener()
    {
        var progName = typeof(Program).Assembly.GetName().Name ?? "CziShrink";
        var traceLogFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            progName);
        if (!Directory.Exists(traceLogFolder))
        {
            try
            {
                Directory.CreateDirectory(traceLogFolder);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        var traceLogFile = System.IO.Path.Combine(
            traceLogFolder,
            $"{progName}.log");
        var listener = new TextWriterTraceListener(traceLogFile, "logFileListener");
        return listener;
    }
}
