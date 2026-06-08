using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
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

    public ShellViewModel(
        IDiagramDocumentViewModelFactory factory,
        IDocumentFileService files,
        IFileDialogService fileDialogs,
        IRecentFilesService recent,
        IDialogService dialogs,
        IThemeService theme,
        ToolboxViewModel toolbox,
        InspectorViewModel inspector,
        StylePaletteViewModel stylePalette,
        KeymapStatusViewModel keymapStatus)
    {
        _factory = factory;
        _files = files;
        _fileDialogs = fileDialogs;
        _recent = recent;
        _dialogs = dialogs;
        _theme = theme;
        Toolbox = toolbox;
        Inspector = inspector;
        StylePalette = stylePalette;
        KeymapStatus = keymapStatus;

        NewCommand = new RelayCommand(OnNew);
        NewErCommand = new RelayCommand(OnNewEr);
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
        ShowToolMenuCommand = new RelayCommand<ToolMenuFamily>(
            family => ToolMenuRequested?.Invoke(this, family),
            _ => HasActiveDocument);

        _recent.Changed += (_, _) => RefreshRecentFiles();
        RefreshRecentFiles();

        OnNew();
    }

    public ObservableCollection<DiagramDocumentViewModel> Documents { get; } = new();

    /// <summary>True when any open document has changes that have not been written to disk.</summary>
    public bool HasUnsavedChanges => Documents.Any(d => d.IsModified);

    public ObservableCollection<string> RecentFiles { get; } = new();

    public ToolboxViewModel Toolbox { get; }

    public InspectorViewModel Inspector { get; }

    public StylePaletteViewModel StylePalette { get; }

    /// <summary>Status-bar feedback for the keyboard-shortcut dispatcher (pending chord / messages).</summary>
    public KeymapStatusViewModel KeymapStatus { get; }

    public RelayCommand NewCommand { get; }
    public RelayCommand NewErCommand { get; }
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

    public RelayCommand<ToolMenuFamily> ShowToolMenuCommand { get; }

    public event EventHandler? ExportImageRequested;

    public event EventHandler? ExportSvgRequested;

    public event EventHandler? CopyImageRequested;

    /// <summary>Raised when a keymap action requests a category tool menu (handled by the window).</summary>
    public event EventHandler<ToolMenuFamily>? ToolMenuRequested;

    public bool HasActiveDocument => ActiveDocument is not null;

    public string Title => ActiveDocument is null ? "Draw" : $"Draw — {ActiveDocument.DisplayName}";

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
                OnPropertyChanged(nameof(HasActiveDocument));
                OnPropertyChanged(nameof(Title));
                NotifyDocumentCommands();
            }
        }
    }

    private void OnNew() => AddAndActivate(_factory.CreateNew(DiagramType.Freeform));

    private void OnNewEr() => AddAndActivate(_factory.CreateNew(DiagramType.Er));

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
