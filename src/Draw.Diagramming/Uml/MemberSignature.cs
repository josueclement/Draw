using System;
using Draw.Model.Nodes;

namespace Draw.Diagramming.Uml;

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

    public static ClassMember Parse(string text, MemberKind context)
    {
        string s = (text ?? string.Empty).Trim();

        MemberVisibility visibility = MemberVisibility.Public;
        if (s.Length > 0 && (s[0] is '+' or '-' or '#' or '~'))
        {
            visibility = ParseMarker(s[0]);
            s = s[1..].Trim();
        }

        if (context == MemberKind.EnumLiteral)
        {
            return new ClassMember { Kind = MemberKind.EnumLiteral, Visibility = visibility, Name = s };
        }

        int open = s.IndexOf('(');
        if (open >= 0)
        {
            int close = s.IndexOf(')', open + 1);
            string name = s[..open].Trim();
            string parameters = close > open ? s[(open + 1)..close].Trim() : string.Empty;
            string? type = null;
            if (close >= 0)
            {
                int colon = s.IndexOf(':', close);
                if (colon >= 0)
                {
                    type = NullIfEmpty(s[(colon + 1)..].Trim());
                }
            }

            return new ClassMember
            {
                Kind = MemberKind.Operation,
                Visibility = visibility,
                Name = name,
                Parameters = parameters,
                Type = type,
            };
        }

        int fieldColon = s.IndexOf(':');
        if (fieldColon >= 0)
        {
            return new ClassMember
            {
                Kind = MemberKind.Field,
                Visibility = visibility,
                Name = s[..fieldColon].Trim(),
                Type = NullIfEmpty(s[(fieldColon + 1)..].Trim()),
            };
        }

        return new ClassMember { Kind = MemberKind.Field, Visibility = visibility, Name = s };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

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
