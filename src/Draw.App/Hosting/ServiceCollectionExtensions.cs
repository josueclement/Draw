using Draw.App.Configuration;
using Draw.App.Input;
using Draw.App.Services;
using Draw.App.ViewModels;
using Draw.App.Views;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Undo;
using Draw.Model.Serialization;
using IContentDialogService = Carbon.Avalonia.Desktop.Services.IContentDialogService;
using ContentDialogService = Carbon.Avalonia.Desktop.Services.ContentDialogService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Draw.App.Hosting;

/// <summary>Composition root for the application's services, options and view models.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDrawServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EditorOptions>(configuration.GetSection(EditorOptions.SectionName));
        services.Configure<RecentFilesOptions>(configuration.GetSection(RecentFilesOptions.SectionName));
        services.Configure<UndoOptions>(configuration.GetSection("Undo"));
        services.Configure<KeymapOptions>(configuration.GetSection(KeymapOptions.SectionName));

        // Stateless / shared services.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IDocumentSerializer, JsonDocumentSerializer>();

        // Connector routing strategies + dispatcher.
        services.AddSingleton<IConnectorRouteStrategy, StraightRouter>();
        services.AddSingleton<IConnectorRouteStrategy, OrthogonalRouter>();
        services.AddSingleton<IConnectorRouteStrategy, RoundedRouter>();
        services.AddSingleton<IConnectorRouter, ConnectorRouter>();
        services.AddSingleton<IDocumentFileService, DocumentFileService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IImageExportService, ImageExportService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDiagramDocumentViewModelFactory, DiagramDocumentViewModelFactory>();

        // Keyboard shortcuts (JSON-configured keymap + chord dispatcher).
        services.AddSingleton<KeymapStatusViewModel>();
        services.AddSingleton<ShortcutHintsViewModel>();
        services.AddSingleton<IKeymapService, KeymapService>();
        services.AddSingleton<KeymapActionRegistry>();
        services.AddSingleton<ChordInputDispatcher>();

        // View models.
        services.AddSingleton<ToolboxViewModel>();
        services.AddSingleton<InspectorViewModel>();
        services.AddSingleton<StylePaletteViewModel>();
        services.AddSingleton<ShellViewModel>();

        // Views.
        services.AddTransient<MainWindow>();

        return services;
    }
}
