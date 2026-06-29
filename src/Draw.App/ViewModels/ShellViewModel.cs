using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Draw.App.Configuration;
using Draw.App.Input;
using Draw.App.Services;
using Draw.Model.Documents;
using Draw.Model.Serialization;

namespace Draw.App.ViewModels;

/// <summary>Root view model: open documents (tabs), file/edit commands, toolbox and inspector.</summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly IDiagramDocumentViewModelFactory _factory;
    private readonly IDocumentFileService _files;
    private readonly IFileDialogService _fileDialogs;
    private readonly IRecentFilesService _recent;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private readonly EditorOptions _editorOptions;

    /// <summary>Every keyboard overlay, so opening one can close the rest (at most one open at a time).</summary>
    private readonly IOverlayPalette[] _overlays;

    public ShellViewModel(
        IDiagramDocumentViewModelFactory factory,
        IDocumentFileService files,
        IFileDialogService fileDialogs,
        IRecentFilesService recent,
        IDialogService dialogs,
        IThemeService theme,
        IOptions<EditorOptions> editorOptions,
        ToolboxViewModel toolbox,
        ToolPaletteViewModel toolPalette,
        InspectorViewModel inspector,
        StylePaletteViewModel stylePalette,
        KeymapStatusViewModel keymapStatus,
        ShortcutHintsViewModel shortcutHints,
        IconPaletteViewModel iconPalette,
        StylePickerViewModel stylePicker,
        ShortcutHelpViewModel shortcutHelp)
    {
        _factory = factory;
        _files = files;
        _fileDialogs = fileDialogs;
        _recent = recent;
        _dialogs = dialogs;
        _theme = theme;
        _editorOptions = editorOptions.Value;
        Toolbox = toolbox;
        ToolPalette = toolPalette;
        Inspector = inspector;
        StylePalette = stylePalette;
        KeymapStatus = keymapStatus;
        ShortcutHints = shortcutHints;
        IconPalette = iconPalette;
        StylePicker = stylePicker;
        ShortcutHelp = shortcutHelp;
        _overlays = [ToolPalette, IconPalette, StylePicker, ShortcutHelp];

        // The armed-tool state lives on the toolbox; refresh the context hints when it changes.
        Toolbox.PropertyChanged += OnToolboxPropertyChanged;

        NewCommand = new RelayCommand(OnNew);
        NewMindMapCommand = new RelayCommand(OnNewMindMap);
        OpenCommand = new AsyncRelayCommand(OnOpenAsync);
        OpenRecentCommand = new AsyncRelayCommand<string>(OnOpenRecentAsync);
        SaveCommand = new AsyncRelayCommand(OnSaveAsync, () => HasActiveDocument);
        SaveAsCommand = new AsyncRelayCommand(OnSaveAsAsync, () => HasActiveDocument);
        CloseDocumentCommand = new AsyncRelayCommand<DiagramDocumentViewModel>(OnCloseDocumentAsync);
        UndoCommand = new RelayCommand(OnUndo, () => ActiveDocument?.CanUndo ?? false);
        RedoCommand = new RelayCommand(OnRedo, () => ActiveDocument?.CanRedo ?? false);
        DeleteCommand = new RelayCommand(OnDelete, () => ActiveDocument?.HasSelection ?? false);
        CopyCommand = new AsyncRelayCommand(OnCopyAsync, () => ActiveDocument?.HasNodeSelection ?? false);
        CutCommand = new AsyncRelayCommand(OnCutAsync, () => ActiveDocument?.HasNodeSelection ?? false);
        PasteCommand = new AsyncRelayCommand(OnPasteAsync, () => HasActiveDocument);
        DuplicateCommand = new RelayCommand(OnDuplicate, () => ActiveDocument?.HasNodeSelection ?? false);
        InsertImageCommand = new AsyncRelayCommand(OnInsertImageAsync, () => HasActiveDocument);
        ExportImageCommand = new RelayCommand(OnExportImage, () => HasActiveDocument);
        ExportSvgCommand = new RelayCommand(OnExportSvg, () => HasActiveDocument);
        CopyImageCommand = new RelayCommand(OnCopyImage, () => HasActiveDocument);
        ToggleThemeCommand = new RelayCommand(OnToggleTheme);
        ToggleInspectorCommand = new RelayCommand(() => IsInspectorOpen = !IsInspectorOpen);
        ShowToolMenuCommand = new RelayCommand<ToolMenuFamily>(
            family => OpenExclusive(ToolPalette, () => ToolPalette.Open(family)),
            _ => HasActiveDocument);
        ShowIconPaletteCommand = new RelayCommand(
            () => OpenExclusive(IconPalette, IconPalette.Open),
            () => ActiveDocument?.HasNodeSelection ?? false);
        ShowStylePickerCommand = new RelayCommand(
            () => OpenExclusive(StylePicker, StylePicker.Open),
            () => ActiveDocument?.HasSelection ?? false);
        ShowHelpCommand = new RelayCommand(
            () => OpenExclusive(ShortcutHelp, ShortcutHelp.Open),
            () => HasActiveDocument);

        _recent.Changed += (_, _) => RefreshRecentFiles();
        RefreshRecentFiles();

        OnNew();
    }

    public ObservableCollection<DiagramDocumentViewModel> Documents { get; } = new();

    /// <summary>True when any open document has changes that have not been written to disk.</summary>
    public bool HasUnsavedChanges => Documents.Any(d => d.IsModified);

    public ObservableCollection<string> RecentFiles { get; } = new();

    public ToolboxViewModel Toolbox { get; }

    /// <summary>The keyboard tool palette (Shift+S / Shift+C); opened via <see cref="ShowToolMenuCommand"/>.</summary>
    public ToolPaletteViewModel ToolPalette { get; }

    public InspectorViewModel Inspector { get; }

    public StylePaletteViewModel StylePalette { get; }

    /// <summary>The Shift+I icon (status-marker) palette overlay.</summary>
    public IconPaletteViewModel IconPalette { get; }

    /// <summary>The Shift+Y style picker overlay.</summary>
    public StylePickerViewModel StylePicker { get; }

    /// <summary>The Shift+H keyboard-shortcut help overlay.</summary>
    public ShortcutHelpViewModel ShortcutHelp { get; }

    /// <summary>Status-bar feedback for the keyboard-shortcut dispatcher (pending chord / messages).</summary>
    public KeymapStatusViewModel KeymapStatus { get; }

    /// <summary>Status-bar list of shortcuts relevant to the current selection / armed tool.</summary>
    public ShortcutHintsViewModel ShortcutHints { get; }

    public RelayCommand NewCommand { get; }
    public RelayCommand NewMindMapCommand { get; }
    public AsyncRelayCommand OpenCommand { get; }
    public AsyncRelayCommand<string> OpenRecentCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand SaveAsCommand { get; }
    public AsyncRelayCommand<DiagramDocumentViewModel> CloseDocumentCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }
    public AsyncRelayCommand CutCommand { get; }
    public AsyncRelayCommand PasteCommand { get; }
    public RelayCommand DuplicateCommand { get; }
    public AsyncRelayCommand InsertImageCommand { get; }
    public RelayCommand ExportImageCommand { get; }
    public RelayCommand ExportSvgCommand { get; }
    public RelayCommand CopyImageCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    /// <summary>Toggles the right-side inspector panel between open and collapsed (strip) states.</summary>
    public RelayCommand ToggleInspectorCommand { get; }

    public RelayCommand<ToolMenuFamily> ShowToolMenuCommand { get; }

    /// <summary>Opens the Shift+I icon palette for the node selection.</summary>
    public RelayCommand ShowIconPaletteCommand { get; }

    /// <summary>Opens the Shift+Y style picker for the selection.</summary>
    public RelayCommand ShowStylePickerCommand { get; }

    /// <summary>Opens the Shift+H keyboard-shortcut help overlay.</summary>
    public RelayCommand ShowHelpCommand { get; }

    /// <summary>The overlay currently open (at most one), or null — the window routes Esc/letters to it.</summary>
    public IOverlayPalette? ActiveOverlay =>
        ToolPalette.IsOpen ? ToolPalette
        : IconPalette.IsOpen ? IconPalette
        : StylePicker.IsOpen ? StylePicker
        : ShortcutHelp.IsOpen ? ShortcutHelp
        : null;

    public event EventHandler? ExportImageRequested;

    public event EventHandler? ExportSvgRequested;

    public event EventHandler? CopyImageRequested;

    public bool HasActiveDocument => ActiveDocument is not null;

    public string Title => ActiveDocument is null ? "Draw" : $"Draw — {ActiveDocument.DisplayName}";

    /// <summary>Whether the right-side inspector panel is expanded. When false it collapses to a thin strip.</summary>
    public bool IsInspectorOpen
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsInspectorCollapsed));
            }
        }
    } = true;

    public bool IsInspectorCollapsed => !IsInspectorOpen;

    /// <summary>App-wide snap-to-grid toggle, backed by the shared <see cref="EditorOptions"/> singleton so
    /// flipping it affects every open document at once. Session-only — not persisted across restarts.</summary>
    public bool SnapToGrid
    {
        get => _editorOptions.SnapToGrid;
        set
        {
            if (_editorOptions.SnapToGrid != value)
            {
                _editorOptions.SnapToGrid = value;
                OnPropertyChanged();
            }
        }
    }

    public DiagramDocumentViewModel? ActiveDocument
    {
        get;
        set
        {
            DiagramDocumentViewModel? previous = field;
            if (SetProperty(ref field, value))
            {
                if (previous is not null)
                {
                    previous.UndoStateChanged -= OnActiveUndoStateChanged;
                    previous.SelectionChanged -= OnActiveSelectionChanged;
                    previous.PropertyChanged -= OnActiveDocumentPropertyChanged;
                }

                if (field is not null)
                {
                    field.UndoStateChanged += OnActiveUndoStateChanged;
                    field.SelectionChanged += OnActiveSelectionChanged;
                    field.PropertyChanged += OnActiveDocumentPropertyChanged;
                }

                Inspector.SetTarget(field);
                StylePalette.SetActiveDocument(field);
                IconPalette.SetActiveDocument(field);
                StylePicker.SetActiveDocument(field);
                ShortcutHints.Refresh(field, Toolbox);
                OnPropertyChanged(nameof(HasActiveDocument));
                OnPropertyChanged(nameof(Title));
                NotifyDocumentCommands();
            }
        }
    }

    private void OnNew() => AddAndActivate(_factory.CreateNew(DiagramType.Freeform));

    private void OnNewMindMap() => AddAndActivate(_factory.CreateNew(DiagramType.MindMap));

    private async Task OnOpenAsync()
    {
        string? path = await _fileDialogs.PickOpenAsync();
        if (path is not null)
        {
            await OpenPathAsync(path);
        }
    }

    private async Task OnOpenRecentAsync(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            await OpenPathAsync(path);
        }
    }

    private async Task OpenPathAsync(string path)
    {
        DiagramDocumentViewModel? existing = Documents.FirstOrDefault(
            d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveDocument = existing;
            return;
        }

        try
        {
            DiagramDocument document = await _files.LoadAsync(path);
            AddAndActivate(_factory.Create(document, path));
            _recent.Add(path);
        }
        catch (Exception ex) when (ex is IOException or DocumentSerializationException or UnauthorizedAccessException)
        {
            await _dialogs.ShowErrorAsync("Could not open file", ex.Message);
            _recent.Remove(path);
        }
    }

    private Task OnSaveAsync() => ActiveDocument is null ? Task.CompletedTask : SaveAsync(ActiveDocument, ActiveDocument.FilePath);

    private Task OnSaveAsAsync() => ActiveDocument is null ? Task.CompletedTask : SaveAsync(ActiveDocument, null);

    private async Task<bool> SaveAsync(DiagramDocumentViewModel document, string? path)
    {
        if (path is null)
        {
            string suggested = document.FilePath is null ? "diagram" : Path.GetFileNameWithoutExtension(document.FilePath);
            path = await _fileDialogs.PickSaveAsync(suggested);
            if (path is null)
            {
                return false;
            }
        }

        try
        {
            await _files.SaveAsync(document.Document, path);
            document.MarkSaved(path);
            _recent.Add(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowErrorAsync("Could not save file", ex.Message);
            return false;
        }
    }

    private async Task OnCloseDocumentAsync(DiagramDocumentViewModel? document)
    {
        document ??= ActiveDocument;
        if (document is null)
        {
            return;
        }

        if (!await EnsureSavedBeforeDiscardAsync(document))
        {
            return;
        }

        int index = Documents.IndexOf(document);
        Documents.Remove(document);
        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = Documents.Count > 0 ? Documents[Math.Min(index, Documents.Count - 1)] : null;
        }

        document.Dispose();
    }

    /// <summary>
    /// Prompts to save when <paramref name="document"/> has unsaved changes. Returns false when the
    /// caller must abort the close — the user chose Cancel, or chose Save but the save failed / was
    /// itself cancelled (e.g. the Save As picker was dismissed).
    /// </summary>
    private async Task<bool> EnsureSavedBeforeDiscardAsync(DiagramDocumentViewModel document)
    {
        if (!document.IsModified)
        {
            return true;
        }

        ActiveDocument = document;
        switch (await _dialogs.ConfirmUnsavedAsync(document.DisplayName))
        {
            case UnsavedChangesChoice.Save:
                return await SaveAsync(document, document.FilePath);
            case UnsavedChangesChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Walks every open document, prompting to save each modified one in turn. Returns false as soon
    /// as the user cancels (or a save fails), signalling that the window must stay open.
    /// </summary>
    public async Task<bool> TryCloseAllAsync()
    {
        foreach (DiagramDocumentViewModel document in Documents.ToList())
        {
            if (!await EnsureSavedBeforeDiscardAsync(document))
            {
                return false;
            }
        }

        return true;
    }

    private void OnUndo() => ActiveDocument?.Undo();

    private void OnRedo() => ActiveDocument?.Redo();

    private void OnDelete() => ActiveDocument?.DeleteSelected();

    private Task OnCopyAsync() => ActiveDocument?.CopySelectionAsync() ?? Task.CompletedTask;

    private Task OnCutAsync() => ActiveDocument?.CutSelectionAsync() ?? Task.CompletedTask;

    private Task OnPasteAsync() => ActiveDocument?.PasteAsync() ?? Task.CompletedTask;

    private void OnDuplicate() => ActiveDocument?.DuplicateSelection();

    private async Task OnInsertImageAsync()
    {
        if (ActiveDocument is null)
        {
            return;
        }

        string? path = await _fileDialogs.PickOpenImageAsync();
        if (path is null)
        {
            return;
        }

        try
        {
            byte[] data = await File.ReadAllBytesAsync(path);
            ActiveDocument.AddImageAtViewportCenter(data, ImageFormatFromPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowErrorAsync("Could not insert image", ex.Message);
        }
    }

    private static string ImageFormatFromPath(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext.Length == 0 ? "png" : ext;
    }

    private void OnExportImage() => ExportImageRequested?.Invoke(this, EventArgs.Empty);

    private void OnExportSvg() => ExportSvgRequested?.Invoke(this, EventArgs.Empty);

    private void OnCopyImage() => CopyImageRequested?.Invoke(this, EventArgs.Empty);

    private void OnToggleTheme() => _theme.Toggle();

    private void AddAndActivate(DiagramDocumentViewModel document)
    {
        Documents.Add(document);
        ActiveDocument = document;
    }

    private void OnActiveUndoStateChanged(object? sender, EventArgs e)
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnActiveSelectionChanged(object? sender, EventArgs e)
    {
        DeleteCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        ShowIconPaletteCommand.NotifyCanExecuteChanged();
        ShowStylePickerCommand.NotifyCanExecuteChanged();
        ShortcutHints.Refresh(ActiveDocument, Toolbox);
    }

    /// <summary>Opens one overlay and closes the others, so at most one is ever open.</summary>
    private void OpenExclusive(IOverlayPalette target, Action open)
    {
        foreach (IOverlayPalette overlay in _overlays)
        {
            if (!ReferenceEquals(overlay, target))
            {
                overlay.Close();
            }
        }

        open();
    }

    private void OnToolboxPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // RaiseModes() raises ActiveToolHint whenever a tool is armed or disarmed.
        if (e.PropertyName == nameof(ToolboxViewModel.ActiveToolHint))
        {
            ShortcutHints.Refresh(ActiveDocument, Toolbox);
        }
    }

    private void OnActiveDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiagramDocumentViewModel.DisplayName))
        {
            OnPropertyChanged(nameof(Title));
        }
    }

    private void NotifyDocumentCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        InsertImageCommand.NotifyCanExecuteChanged();
        ExportImageCommand.NotifyCanExecuteChanged();
        ExportSvgCommand.NotifyCanExecuteChanged();
        CopyImageCommand.NotifyCanExecuteChanged();
        ShowToolMenuCommand.NotifyCanExecuteChanged();
        ShowIconPaletteCommand.NotifyCanExecuteChanged();
        ShowStylePickerCommand.NotifyCanExecuteChanged();
        ShowHelpCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (string path in _recent.Files)
        {
            RecentFiles.Add(path);
        }
    }
}
