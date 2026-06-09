using System;
using System.Collections.ObjectModel;
using System.Linq;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="ClassNode"/>: name, stereotype and member compartments.</summary>
public sealed class ClassNodeViewModel : NodeViewModelBase
{
    private const double NameCompartmentHeight = 28d;
    private const double MemberRowHeight = 18d;
    private const double CompartmentPadding = 8d;

    private readonly ClassNode _model;
    private readonly INodeEditContext _context;

    public ClassNodeViewModel(ClassNode model, INodeEditContext context, IThemeService theme)
        : base(model, theme)
    {
        _model = model;
        _context = context ?? throw new ArgumentNullException(nameof(context));

        PrimaryMembers = new ObservableCollection<ClassMemberViewModel>(
            _model.Members.Where(IsPrimary).Select(Wrap));
        Operations = new ObservableCollection<ClassMemberViewModel>(
            _model.Members.Where(m => m.Kind == MemberKind.Operation).Select(Wrap));
    }

    public new ClassNode Model => _model;

    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    public ClassNodeKind Kind => _model.Kind;

    public bool IsEnum => _model.Kind == ClassNodeKind.Enum;

    public bool HasOperations => _model.Kind != ClassNodeKind.Enum;

    public string? Stereotype => _model.Kind switch
    {
        ClassNodeKind.Interface => "«interface»",
        ClassNodeKind.Enum => "«enumeration»",
        _ => null,
    };

    public bool HasStereotype => Stereotype is not null;

    public ObservableCollection<ClassMemberViewModel> PrimaryMembers { get; }

    public ObservableCollection<ClassMemberViewModel> Operations { get; }

    public string Name
    {
        get => _model.Name;
        set
        {
            if (!string.Equals(_model.Name, value, StringComparison.Ordinal))
            {
                _context.BeginMemberEdit();
                _model.Name = value ?? string.Empty;
                _context.EndMemberEdit();
                OnPropertyChanged();
            }
        }
    }

    public bool IsAbstract
    {
        get => _model.IsAbstract;
        set
        {
            if (_model.IsAbstract != value)
            {
                _context.BeginMemberEdit();
                _model.IsAbstract = value;
                _context.EndMemberEdit();
                OnPropertyChanged();
            }
        }
    }

    public override double MinHeight
    {
        get
        {
            int rows = PrimaryMembers.Count + Operations.Count;
            return NameCompartmentHeight + (rows * MemberRowHeight) + CompartmentPadding;
        }
    }

    public override double MinWidth => 80d;

    public void AddPrimaryMember()
    {
        MemberKind kind = IsEnum ? MemberKind.EnumLiteral : MemberKind.Field;
        string name = IsEnum ? "LITERAL" : "field";
        AddMember(new ClassMember { Kind = kind, Name = name, Visibility = MemberVisibility.Public }, PrimaryMembers);
    }

    public void AddOperation()
        => AddMember(new ClassMember { Kind = MemberKind.Operation, Name = "operation", Visibility = MemberVisibility.Public }, Operations);

    /// <summary>
    /// Inserts a blank member of <paramref name="kind"/> at <paramref name="index"/> in its
    /// compartment (clamped; negative ⇒ append), enters inline editing on it and returns its
    /// view model so the caller can focus the editor. Used by the canvas add/rapid-entry flow.
    /// </summary>
    public ClassMemberViewModel InsertNewMember(MemberKind kind, int index)
    {
        _context.BeginMemberEdit();

        ClassMember member = new() { Kind = kind, Visibility = MemberVisibility.Public };
        ClassMemberViewModel vm = Wrap(member);
        vm.IsNewlyAdded = true;

        ObservableCollection<ClassMemberViewModel> list = kind == MemberKind.Operation ? Operations : PrimaryMembers;
        int target = index < 0 || index > list.Count ? list.Count : index;
        list.Insert(target, vm);
        ReorderModelFromCollections();

        vm.BeginEdit();
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
        return vm;
    }

    /// <summary>Finds the compartment and index of <paramref name="member"/> (index -1 if absent).</summary>
    public (ObservableCollection<ClassMemberViewModel> List, int Index) Locate(ClassMemberViewModel member)
    {
        int primary = PrimaryMembers.IndexOf(member);
        return primary >= 0 ? (PrimaryMembers, primary) : (Operations, Operations.IndexOf(member));
    }

    /// <summary>
    /// Removes any still-unnamed members created via the add flow (e.g. a trailing blank left by
    /// Enter-then-Escape). Silent: the original insert already captured undo and marked dirty.
    /// </summary>
    public void DiscardEmptyNewMembers()
    {
        System.Collections.Generic.List<ClassMemberViewModel> blanks = PrimaryMembers.Concat(Operations)
            .Where(m => m.IsNewlyAdded && string.IsNullOrWhiteSpace(m.Name))
            .ToList();
        if (blanks.Count == 0)
        {
            return;
        }

        foreach (ClassMemberViewModel m in blanks)
        {
            _model.Members.Remove(m.Model);
            PrimaryMembers.Remove(m);
            Operations.Remove(m);
        }

        OnPropertyChanged(nameof(MinHeight));
    }

    public void RemoveMember(ClassMemberViewModel member)
    {
        if (member is null)
        {
            return;
        }

        _context.BeginMemberEdit();
        _model.Members.Remove(member.Model);
        PrimaryMembers.Remove(member);
        Operations.Remove(member);
        _context.EndMemberEdit();
        OnPropertyChanged(nameof(MinHeight));
    }

    /// <summary>
    /// Replaces the whole member set in one gesture (used by the modal editor's Save). The supplied
    /// members are cloned so the editor's working copies stay detached from the document; the swap is
    /// captured as a single undo step.
    /// </summary>
    public void ReplaceMembers(System.Collections.Generic.IReadOnlyList<ClassMember> members)
    {
        _context.BeginMemberEdit();

        PrimaryMembers.Clear();
        Operations.Clear();
        foreach (ClassMember member in members)
        {
            ClassMember clone = member.Clone();
            ClassMemberViewModel vm = Wrap(clone);
            (IsPrimary(clone) ? PrimaryMembers : Operations).Add(vm);
        }

        ReorderModelFromCollections();
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
    }

    public void MoveMember(ClassMemberViewModel member, int delta)
    {
        ObservableCollection<ClassMemberViewModel> list = PrimaryMembers.Contains(member) ? PrimaryMembers : Operations;
        int index = list.IndexOf(member);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= list.Count)
        {
            return;
        }

        _context.BeginMemberEdit();
        list.Move(index, target);
        ReorderModelFromCollections();
        _context.EndMemberEdit();
    }

    /// <summary>Commits any member row currently being edited (used by the Escape/blur path).</summary>
    public bool CommitPendingEdits()
    {
        bool any = false;
        foreach (ClassMemberViewModel m in PrimaryMembers.Concat(Operations))
        {
            if (m.IsEditing)
            {
                m.CommitEdit();
                any = true;
            }
        }

        return any;
    }

    private void AddMember(ClassMember member, ObservableCollection<ClassMemberViewModel> list)
    {
        _context.BeginMemberEdit();
        _model.Members.Add(member);
        list.Add(Wrap(member));
        _context.EndMemberEdit();
        GrowToFitContent();
        OnPropertyChanged(nameof(MinHeight));
    }

    private void GrowToFitContent()
    {
        if (Height < MinHeight)
        {
            Height = MinHeight; // base setter writes through to the model bounds
        }
    }

    private void ReorderModelFromCollections()
    {
        _model.Members.Clear();
        foreach (ClassMemberViewModel m in PrimaryMembers)
        {
            _model.Members.Add(m.Model);
        }

        foreach (ClassMemberViewModel m in Operations)
        {
            _model.Members.Add(m.Model);
        }
    }

    private static bool IsPrimary(ClassMember m) => m.Kind is MemberKind.Field or MemberKind.EnumLiteral;

    private ClassMemberViewModel Wrap(ClassMember m) => new(m, _context);
}
