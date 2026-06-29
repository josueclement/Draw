using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Draw.App.ViewModels;
using Draw.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Draw.App;

public partial class App : Application
{
    private readonly IServiceProvider? _services;

    // Parameterless constructor for the XAML previewer/designer.
    public App()
    {
    }

    public App(IServiceProvider? services)
    {
        _services = services;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (_services is not null
            && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();

            // Windows/Linux deliver a double-clicked file as a command-line argument.
            OpenStartupFiles(desktop.Args);

            // macOS never passes the file via argv — it routes opens through the activation feature.
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            {
                activatable.Activated += OnActivated;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OpenStartupFiles(IReadOnlyList<string>? args)
    {
        if (args is null)
        {
            return;
        }

        // Keep only real files so stray host-configuration switches never raise an error dialog.
        OpenFiles(args.Where(File.Exists).ToList());
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is not FileActivatedEventArgs fileArgs)
        {
            return;
        }

        OpenFiles(fileArgs.Files
            .Select(file => file.TryGetLocalPath())
            .Where(path => path is not null)
            .Select(path => path!)
            .ToList());
    }

    private void OpenFiles(IReadOnlyList<string> files)
    {
        if (_services is null || files.Count == 0)
        {
            return;
        }

        ShellViewModel shell = _services.GetRequiredService<ShellViewModel>();

        // Defer to the UI loop: OpenFilesAsync mutates the document collection the window binds to.
        Dispatcher.UIThread.Post(() => _ = shell.OpenFilesAsync(files));
    }
}
