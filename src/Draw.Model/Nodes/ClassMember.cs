namespace Draw.Model.Nodes;

/// <summary>
/// One member of a <see cref="ClassNode"/>. <see cref="Type"/> is free text (the return type
/// for operations); <see cref="Parameters"/> is free text and used only by operations.
/// </summary>
public sealed class ClassMember
{
    public MemberVisibility Visibility { get; set; } = MemberVisibility.Public;

    public string Name { get; set; } = string.Empty;

    public string? Type { get; set; }

    public string? Parameters { get; set; }

    public MemberKind Kind { get; set; } = MemberKind.Field;

    public bool IsStatic { get; set; }

    public bool IsAbstract { get; set; }

    public ClassMember Clone() => new()
    {
        Visibility = Visibility,
        Name = Name,
        Type = Type,
        Parameters = Parameters,
        Kind = Kind,
        IsStatic = IsStatic,
        IsAbstract = IsAbstract,
    };
}
