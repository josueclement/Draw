using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>
/// Backs the modal members editor — a spacious, transactional alternative to the inspector panel.
/// Seeded with cloned members so editing never touches the live document; <see cref="BuildResult"/>
/// returns the edited copies, applied as a single undo step only when the dialog is saved.
/// </summary>
public sealed class ClassMembersEditorViewModel : ViewModelBase
{
    private readonly ClassNodeKind _kind;

    public ClassMembersEditorViewModel(ClassNodeKind kind, IReadOnlyList<ClassMember> current, IReadOnlyList<string> typeSuggestions)
    {
        _kind = kind;
        TypeSuggestions = typeSuggestions;

        Fields = new ObservableCollection<ClassMemberEditRow>(
            current.Where(IsPrimary).Select(m => new ClassMemberEditRow(m.Clone())));
        Operations = new ObservableCollection<ClassMemberEditRow>(
            current.Where(m => m.Kind == MemberKind.Operation).Select(m => new ClassMemberEditRow(m.Clone())));

        AddFieldCommand = new RelayCommand(AddField);
        AddOperationCommand = new RelayCommand(AddOperation);
        RemoveCommand = new RelayCommand<ClassMemberEditRow>(Remove);
        MoveUpCommand = new RelayCommand<ClassMemberEditRow>(r => Move(r, -1));
        MoveDownCommand = new RelayCommand<ClassMemberEditRow>(r => Move(r, +1));
    }

    public ObservableCollection<ClassMemberEditRow> Fields { get; }

    public ObservableCollection<ClassMemberEditRow> Operations { get; }

    public bool IsEnum => _kind == ClassNodeKind.Enum;

    /// <summary>An enum has no operations and only literals (name-only); a class/interface has both.</summary>
    public bool ShowOperations => _kind != ClassNodeKind.Enum;

    public bool ShowMemberDetails => _kind != ClassNodeKind.Enum;

    /// <summary>Header for the primary compartment: literals for an enum, otherwise fields.</summary>
    public string PrimaryHeader => IsEnum ? "Literals" : "Fields";

    public IReadOnlyList<string> TypeSuggestions { get; }

    public IRelayCommand AddFieldCommand { get; }

    public IRelayCommand AddOperationCommand { get; }

    public IRelayCommand<ClassMemberEditRow> RemoveCommand { get; }

    public IRelayCommand<ClassMemberEditRow> MoveUpCommand { get; }

    public IRelayCommand<ClassMemberEditRow> MoveDownCommand { get; }

    /// <summary>The edited members, primary compartment first then operations (the node's storage order).</summary>
    public List<ClassMember> BuildResult()
        => Fields.Select(r => r.Model).Concat(Operations.Select(r => r.Model)).ToList();

    private void AddField()
    {
        MemberKind kind = IsEnum ? MemberKind.EnumLiteral : MemberKind.Field;
        Fields.Add(new ClassMemberEditRow(new ClassMember { Kind = kind, Visibility = MemberVisibility.Public }));
    }

    private void AddOperation()
        => Operations.Add(new ClassMemberEditRow(new ClassMember { Kind = MemberKind.Operation, Visibility = MemberVisibility.Public }));

    private void Remove(ClassMemberEditRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!Fields.Remove(row))
        {
            Operations.Remove(row);
        }
    }

    private void Move(ClassMemberEditRow? row, int delta)
    {
        if (row is null)
        {
            return;
        }

        ObservableCollection<ClassMemberEditRow> list = Fields.Contains(row) ? Fields : Operations;
        int index = list.IndexOf(row);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return;
        }

        list.Move(index, target);
    }

    private static bool IsPrimary(ClassMember m) => m.Kind is MemberKind.Field or MemberKind.EnumLiteral;
}
