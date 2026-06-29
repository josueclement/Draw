using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Draw.App.Services;

/// <summary>
/// Last-resort safety net for unhandled exceptions. Every crash is written to a timestamped log under
/// <c>%APPDATA%/Draw/logs/</c> (the same APPDATA/Draw root as <see cref="RecentFilesService"/>) — that
/// is the guaranteed record. A crash on the UI thread additionally shows a small dialog telling the
/// user what happened and where the log is, then exits; crashes we cannot surface a dialog for
/// (background threads, a process already tearing down) are logged only.
/// </summary>
internal static class CrashHandler
{
    // Set once we start reporting a crash; a failure while reporting must not start a second report.
    private static bool _reporting;

    /// <summary>Wires the global exception hooks. Call once, as early as possible in startup.</summary>
    public static void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string? logPath = Log(e.Exception, "Dispatcher.UnhandledException");

        // Take ownership: keep the process alive long enough to show our own dialog, then exit
        // deterministically when it closes (rather than letting Avalonia tear down underneath us).
        e.Handled = true;
        ShowDialogThenExit(e.Exception, logPath);
    }

    /// <summary>
    /// Writes <paramref name="exception"/> to a timestamped crash log. Returns the file path, or
    /// <c>null</c> if it could not be written. Never throws — logging must not mask the real crash.
    /// </summary>
    public static string? Log(Exception? exception, string source)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Draw",
                "logs");
            Directory.CreateDirectory(dir);

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string path = Path.Combine(dir, $"crash-{stamp}.log");
            string when = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
            string body = $"[{when}] {source}{Environment.NewLine}"
                + (exception?.ToString() ?? "(no exception object)") + Environment.NewLine;
            File.WriteAllText(path, body);
            return path;
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or SecurityException
                or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static void ShowDialogThenExit(Exception exception, string? logPath)
    {
        if (_reporting)
        {
            return;
        }

        _reporting = true;

        try
        {
            Window dialog = BuildDialog(exception, logPath);
            dialog.Closed += (_, _) => Environment.Exit(1);

            Window? owner = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner is { IsVisible: true })
            {
                dialog.Show(owner);
            }
            else
            {
                dialog.Show();
            }
        }
#pragma warning disable CA1031 // A crash reporter must survive any failure while reporting; the log is already written.
        catch (Exception)
        {
            Environment.Exit(1);
        }
#pragma warning restore CA1031
    }

    private static Window BuildDialog(Exception exception, string? logPath)
    {
        StackPanel panel = new() { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Draw hit an unexpected error and must close.",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = logPath is null
                ? "The error could not be written to a log file."
                : "A crash log was saved to:" + Environment.NewLine + logPath,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = exception.Message,
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });

        Button close = new() { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(close);

        Window dialog = new()
        {
            Title = "Draw — Unexpected error",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel,
        };
        close.Click += (_, _) => dialog.Close();
        return dialog;
    }
}
