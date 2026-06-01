using System;
using Avalonia;
using Jcl.Draw.App.Hosting;
using Microsoft.Extensions.Hosting;

namespace Jcl.Draw.App;

internal static class Program
{
    // The Avalonia desktop lifetime owns the foreground loop, so we start the host
    // (for hosted services, logging, options), run Avalonia to completion, then stop it.
    [STAThread]
    public static void Main(string[] args)
    {
        using IHost host = CreateHost(args);
        host.Start();
        try
        {
            BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static IHost CreateHost(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddDrawServices(builder.Configuration);
        return builder.Build();
    }

    // Parameterless overload required by the Avalonia XAML previewer/designer.
    public static AppBuilder BuildAvaloniaApp() => Configure(null);

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) => Configure(services);

    private static AppBuilder Configure(IServiceProvider? services)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
