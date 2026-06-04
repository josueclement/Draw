namespace Draw.Model.Nodes;

/// <summary>
/// One column of an <see cref="EntityNode"/>. <see cref="Type"/> is free text (the SQL data type,
/// e.g. <c>int</c> or <c>varchar(255)</c>); the flags carry the column's ER/relational semantics.
/// </summary>
public sealed class EntityColumn
{
    public string Name { get; set; } = string.Empty;

    public string? Type { get; set; }

    public bool IsPrimaryKey { get; set; }

    public bool IsForeignKey { get; set; }

    /// <summary>True when the column accepts NULL. Defaults to true; a primary key is implicitly not nullable.</summary>
    public bool IsNullable { get; set; } = true;

    public bool IsUnique { get; set; }

    public EntityColumn Clone() => new()
    {
        Name = Name,
        Type = Type,
        IsPrimaryKey = IsPrimaryKey,
        IsForeignKey = IsForeignKey,
        IsNullable = IsNullable,
        IsUnique = IsUnique,
    };
}
