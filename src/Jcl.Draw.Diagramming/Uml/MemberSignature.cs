using System;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.Diagramming.Uml;

/// <summary>Formats and parses class members to/from their UML text representation.</summary>
public static class MemberSignature
{
    public static string Format(ClassMember member)
    {
        if (member.Kind == MemberKind.EnumLiteral)
        {
            return member.Name;
        }

        string marker = Marker(member.Visibility);
        if (member.Kind == MemberKind.Operation)
        {
            string parameters = member.Parameters ?? string.Empty;
            string signature = $"{marker} {member.Name}({parameters})";
            return string.IsNullOrWhiteSpace(member.Type) ? signature : $"{signature}: {member.Type}";
        }

        return string.IsNullOrWhiteSpace(member.Type)
            ? $"{marker} {member.Name}"
            : $"{marker} {member.Name}: {member.Type}";
    }

    internal static string Marker(MemberVisibility visibility) => visibility switch
    {
        MemberVisibility.Private => "-",
        MemberVisibility.Protected => "#",
        MemberVisibility.Package => "~",
        _ => "+",
    };

    internal static MemberVisibility ParseMarker(char c) => c switch
    {
        '-' => MemberVisibility.Private,
        '#' => MemberVisibility.Protected,
        '~' => MemberVisibility.Package,
        _ => MemberVisibility.Public,
    };
}
