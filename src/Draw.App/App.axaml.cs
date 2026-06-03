using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        }

        base.OnFrameworkInitializationCompleted();
    }
}
