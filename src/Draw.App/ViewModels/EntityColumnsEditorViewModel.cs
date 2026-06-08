using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>
/// Backs the modal table-columns editor — a spacious, transactional alternative to the inspector panel.
/// Seeded with cloned columns so editing never touches the live document; <see cref="BuildResult"/>
/// returns the edited copies, applied as a single undo step only when the dialog is saved.
/// </summary>
public sealed class EntityColumnsEditorViewModel : ViewModelBase
{
    public EntityColumnsEditorViewModel(IReadOnlyList<EntityColumn> current, IReadOnlyList<string> typeSuggestions)
    {
        TypeSuggestions = typeSuggestions;
        Columns = new ObservableCollection<EntityColumnEditRow>(current.Select(c => new EntityColumnEditRow(c.Clone())));

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand<EntityColumnEditRow>(Remove);
        MoveUpCommand = new RelayCommand<EntityColumnEditRow>(r => Move(r, -1));
        MoveDownCommand = new RelayCommand<EntityColumnEditRow>(r => Move(r, +1));
    }

    public ObservableCollection<EntityColumnEditRow> Columns { get; }

    public IReadOnlyList<string> TypeSuggestions { get; }

    public IRelayCommand AddCommand { get; }

    public IRelayCommand<EntityColumnEditRow> RemoveCommand { get; }

    public IRelayCommand<EntityColumnEditRow> MoveUpCommand { get; }

    public IRelayCommand<EntityColumnEditRow> MoveDownCommand { get; }

    public List<EntityColumn> BuildResult() => Columns.Select(r => r.Model).ToList();

    private void Add() => Columns.Add(new EntityColumnEditRow(new EntityColumn()));

    private void Remove(EntityColumnEditRow? row)
    {
        if (row is not null)
        {
            Columns.Remove(row);
        }
    }

    private void Move(EntityColumnEditRow? row, int delta)
    {
        if (row is null)
        {
            return;
        }

        int index = Columns.IndexOf(row);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= Columns.Count)
        {
            return;
        }

        Columns.Move(index, target);
    }
}
