using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jcl.Draw.App.Configuration;
using Jcl.Draw.App.Services;
using Jcl.Draw.App.ViewModels;
using Jcl.Draw.Diagramming.Routing;
using Jcl.Draw.Diagramming.Undo;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Nodes;
using Jcl.Draw.Model.Primitives;
using Jcl.Draw.Model.Serialization;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jcl.Draw.App.Tests;

public class ShellViewModelTests
{
    private static ShellViewModel CreateShell(
        out IDocumentFileService files,
        out IFileDialogService fileDialogs)
    {
        files = Substitute.For<IDocumentFileService>();
        fileDialogs = Substitute.For<IFileDialogService>();
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
    public void Constructor_CreatesInitialActiveDocument()
    {
        ShellViewModel shell = CreateShell(out _, out _);

        Assert.Single(shell.Documents);
        Assert.NotNull(shell.ActiveDocument);
        Assert.True(shell.HasActiveDocument);
    }

    [Fact]
    public void NewCommand_AddsAnotherDocument()
    {
        ShellViewModel shell = CreateShell(out _, out _);

        shell.NewCommand.Execute(null);

        Assert.Equal(2, shell.Documents.Count);
    }

    [Fact]
    public async Task SaveCommand_WithoutPath_PicksLocationThenSaves()
    {
        ShellViewModel shell = CreateShell(out IDocumentFileService files, out IFileDialogService fileDialogs);
        fileDialogs.PickSaveAsync(Arg.Any<string?>()).Returns("/tmp/diagram.jcld");

        await shell.SaveCommand.ExecuteAsync(null);

        await files.Received(1).SaveAsync(Arg.Any<DiagramDocument>(), "/tmp/diagram.jcld", Arg.Any<CancellationToken>());
        Assert.Equal("/tmp/diagram.jcld", shell.ActiveDocument!.FilePath);
        Assert.False(shell.ActiveDocument!.IsModified);
    }

    [Fact]
    public void DeleteCommand_CanExecute_TracksSelection()
    {
        ShellViewModel shell = CreateShell(out _, out _);

        Assert.False(shell.DeleteCommand.CanExecute(null));

        shell.ActiveDocument!.AddShape(ShapeKind.Rectangle, new Point2D(50, 50));

        Assert.True(shell.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void UndoCommand_CanExecute_TracksUndoState()
    {
        ShellViewModel shell = CreateShell(out _, out _);
        Assert.False(shell.UndoCommand.CanExecute(null));

        shell.ActiveDocument!.AddShape(ShapeKind.Rectangle, new Point2D(50, 50));

        Assert.True(shell.UndoCommand.CanExecute(null));
    }
}
