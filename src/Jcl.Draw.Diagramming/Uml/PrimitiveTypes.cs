using System.Collections.Generic;

namespace Jcl.Draw.Diagramming.Uml;

/// <summary>Common primitive/type names offered as autocomplete suggestions for member types.</summary>
public static class PrimitiveTypes
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "void", "bool", "byte", "char", "short", "int", "long",
        "float", "double", "decimal", "string", "object",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
    };
}
