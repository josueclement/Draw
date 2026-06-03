using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Draw.App.Configuration;
using Draw.App.Services;
using Draw.App.ViewModels;
using Draw.App.Views;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Undo;
using Draw.Model.Serialization;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Draw.App.Tests;

/// <summary>
/// End-to-end view test: arming a shape tool and clicking the canvas must add a node.
/// Runs on Avalonia's headless backend (no GPU/fonts needed) using the real App resources.
/// </summary>
public class CanvasPlacementHeadlessTests
{
    // Entry point discovered by HeadlessUnitTestSession; uses the real App so App.axaml
    // resources (FluentTheme + editor brushes) are loaded exactly as in production.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<global::Draw.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    private static ShellViewModel CreateShell()
    {
        IDocumentFileService files = Substitute.For<IDocumentFileService>();
        IFileDialogService fileDialogs = Substitute.For<IFileDialogService>();
        IRecentFilesService recent = Substitute.For<IRecentFilesService>();
        recent.Files.Returns(new List<string>());
        IDialogService dialogs = Substitute.For<IDialogService>();
        IThemeService theme = Substitute.For<IThemeService>();

        DiagramDocumentViewModelFactory factory = new(
            new JsonDocumentSerializer(),
            new ConnectorRouter(new IConnectorRouteStrategy[] { new StraightRouter() }),
            Options.Create(new EditorOptions()),
            Options.Create(new UndoOptions()));

        return new ShellViewModel(factory, files, fileDialogs, recent, dialogs, theme, new ToolboxViewModel(), new InspectorViewModel());
    }

    [Fact]
    public async Task ClickingCanvas_WithShapeArmed_AddsNode()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(CanvasPlacementHeadlessTests));

        await session.Dispatch(() =>
        {
            ShellViewModel shell = CreateShell();
            DiagramDocumentViewModel doc = shell.ActiveDocument!;

            DiagramView view = new() { DataContext = doc };
            Window window = new()
            {
                DataContext = shell,
                Content = view,
                Width = 800,
                Height = 600,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            // Arm a shape tool, exactly as clicking the palette does.
            shell.Toolbox.SelectedShape = shell.Toolbox.Shapes.First();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(doc.Nodes);

            // Click in the middle of the canvas.
            window.MouseDown(new Point(400, 300), MouseButton.Left);
            window.MouseUp(new Point(400, 300), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(doc.Nodes);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task PlacedShape_RenderProbe()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(CanvasPlacementHeadlessTests));

        await session.Dispatch(() =>
        {
            ShellViewModel shell = CreateShell();
            DiagramDocumentViewModel doc = shell.ActiveDocument!;

            DiagramView view = new() { DataContext = doc };
            Window window = new()
            {
                DataContext = shell,
                Content = view,
                Width = 800,
                Height = 600,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            shell.Toolbox.SelectedShape = shell.Toolbox.Shapes.First();
            Dispatcher.UIThread.RunJobs();

            window.MouseDown(new Point(400, 300), MouseButton.Left);
            window.MouseUp(new Point(400, 300), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            // Real Skia render (UseHeadlessDrawing=false) → capture the frame to a PNG we can inspect.
            string status;
            try
            {
                using var frame = window.CaptureRenderedFrame();
                if (frame is null)
                {
                    status = "frame=NULL";
                }
                else
                {
                    frame.Save("/tmp/render_probe.png");
                    status = $"frame={frame.PixelSize.Width}x{frame.PixelSize.Height} saved";
                }
            }
            catch (System.Exception ex)
            {
                status = "capture threw: " + ex.GetType().Name + ": " + ex.Message;
            }

            System.IO.File.WriteAllText("/tmp/render_probe_status.txt",
                $"nodes={doc.Nodes.Count}; {status}");

            window.Close();
        }, CancellationToken.None);
    }
}
