using System;
using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Validates <paramref name="text"/> against the member-signature grammar and, when it is
    /// well-formed, parses it (the <paramref name="result"/> is identical to <see cref="Parse"/>).
    /// Returns <c>false</c> with a short, human-readable <paramref name="error"/> for malformed input
    /// that <see cref="Parse"/> would otherwise accept silently-wrong — so callers can reject a bad
    /// edit instead of committing a degraded model.
    /// </summary>
    /// <remarks>
    /// Grammar, after trimming and an optional leading visibility marker (<c>+ - # ~</c>):
    /// <list type="bullet">
    /// <item>Enum literal: a non-empty name.</item>
    /// <item>Operation (input contains <c>(</c>): a single balanced <c>(</c>…<c>)</c> pair — parameter
    /// types may not themselves contain parentheses — a non-empty name before <c>(</c>, and after
    /// <c>)</c> either nothing or one <c>: type</c> clause with a non-empty type and no further
    /// <c>:</c>. Parameter text is otherwise free-form.</item>
    /// <item>Field (no <c>(</c>): a non-empty name, no stray <c>)</c>, at most one <c>:</c>, and a
    /// non-empty type when a <c>:</c> is present.</item>
    /// </list>
    /// </remarks>
    public static bool TryParse(string? text, MemberKind context, [NotNullWhen(true)] out ClassMember? result, out string? error)
    {
        result = null;
        error = null;

        string input = text ?? string.Empty;
        string s = input.Trim();
        if (s.Length > 0 && (s[0] is '+' or '-' or '#' or '~'))
        {
            s = s[1..].Trim();
        }

        if (context == MemberKind.EnumLiteral)
        {
            if (s.Length == 0)
            {
                error = "Name is required.";
                return false;
            }

            result = Parse(input, context);
            return true;
        }

        int open = s.IndexOf('(');
        if (open >= 0)
        {
            if (s.IndexOf('(', open + 1) >= 0)
            {
                error = "Parameter list may not contain a nested '('.";
                return false;
            }

            int close = s.IndexOf(')', open + 1);
            if (close < 0 || s.IndexOf(')', close + 1) >= 0)
            {
                error = "Unbalanced parentheses.";
                return false;
            }

            if (s[..open].Trim().Length == 0)
            {
                error = "Operation name is required.";
                return false;
            }

            string tail = s[(close + 1)..].Trim();
            if (tail.Length > 0)
            {
                if (tail[0] != ':')
                {
                    error = "Unexpected text after ')'.";
                    return false;
                }

                string returnType = tail[1..].Trim();
                if (returnType.Length == 0)
                {
                    error = "Type expected after ':'.";
                    return false;
                }

                if (returnType.IndexOf(':') >= 0)
                {
                    error = "Multiple ':' separators.";
                    return false;
                }
            }

            result = Parse(input, context);
            return true;
        }

        if (s.IndexOf(')') >= 0)
        {
            error = "Unbalanced parentheses.";
            return false;
        }

        int colon = s.IndexOf(':');
        if (colon >= 0)
        {
            if (s.IndexOf(':', colon + 1) >= 0)
            {
                error = "Multiple ':' separators.";
                return false;
            }

            if (s[..colon].Trim().Length == 0)
            {
                error = "Name is required.";
                return false;
            }

            if (s[(colon + 1)..].Trim().Length == 0)
            {
                error = "Type expected after ':'.";
                return false;
            }
        }
        else if (s.Length == 0)
        {
            error = "Name is required.";
            return false;
        }

        result = Parse(input, context);
        return true;
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
