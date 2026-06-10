using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Draw.Model.Nodes;

namespace Draw.Diagramming.Er;

/// <summary>
/// Formats and parses entity columns to/from their ER text representation:
/// <c>name: type</c> followed by space-separated flag tokens. Recognized flags (case-insensitive):
/// <c>PK</c>, <c>FK</c>, <c>UNIQUE</c>/<c>UQ</c>, <c>NOT NULL</c>/<c>NN</c>, <c>NULL</c>.
/// </summary>
public static class ColumnSignature
{
    public static string Format(EntityColumn column)
    {
        string head = string.IsNullOrWhiteSpace(column.Type)
            ? column.Name
            : $"{column.Name}: {column.Type}";

        List<string> flags = new();
        if (column.IsPrimaryKey)
        {
            flags.Add("PK");
        }

        if (column.IsForeignKey)
        {
            flags.Add("FK");
        }

        if (column.IsUnique)
        {
            flags.Add("UNIQUE");
        }

        // A primary key is implicitly NOT NULL, so the marker is redundant there.
        if (!column.IsNullable && !column.IsPrimaryKey)
        {
            flags.Add("NOT NULL");
        }

        return flags.Count == 0 ? head : $"{head} {string.Join(' ', flags)}";
    }

    public static EntityColumn Parse(string text)
    {
        string s = (text ?? string.Empty).Trim();

        // The type sits between the name and the trailing flags; flags are pulled off the end first so
        // a multi-word type (e.g. "double precision") keeps any non-flag words.
        int colon = s.IndexOf(':');
        string namePart;
        string rest;
        if (colon >= 0)
        {
            namePart = s[..colon].Trim();
            rest = s[(colon + 1)..].Trim();
        }
        else
        {
            namePart = s;
            rest = string.Empty;
        }

        Flags flags = StripTrailingFlags(colon >= 0 ? rest : namePart, out string remainder);

        string name;
        string? type;
        if (colon >= 0)
        {
            name = namePart;
            type = NullIfEmpty(remainder);
        }
        else
        {
            name = remainder;
            type = null;
        }

        EntityColumn column = new()
        {
            Name = name,
            Type = type,
            IsPrimaryKey = flags.PrimaryKey,
            IsForeignKey = flags.ForeignKey,
            IsUnique = flags.Unique,
            // A primary key is always NOT NULL; otherwise honour an explicit NULL/NOT NULL, defaulting to nullable.
            IsNullable = flags.PrimaryKey ? false : flags.Nullable ?? true,
        };

        return column;
    }

    /// <summary>
    /// Validates <paramref name="text"/> against the column-signature grammar and, when it is
    /// well-formed, parses it (the <paramref name="result"/> is identical to <see cref="Parse"/>).
    /// Returns <c>false</c> with a short, human-readable <paramref name="error"/> for malformed input
    /// that <see cref="Parse"/> would otherwise accept silently-wrong — so callers can reject a bad
    /// edit instead of committing a degraded model.
    /// </summary>
    /// <remarks>
    /// Grammar, after trimming (recognized trailing flags — <c>PK</c> / <c>FK</c> /
    /// <c>UNIQUE</c>/<c>UQ</c> / <c>NOT NULL</c>/<c>NN</c> / <c>NULL</c>, case-insensitive — are pulled
    /// off the end): at most one <c>:</c>; a non-empty name; when a <c>:</c> is present a non-empty type
    /// must remain after the trailing flags are stripped (so <c>id: PK</c> or <c>flag: unique</c> —
    /// where the only post-colon word is itself a flag — are rejected rather than silently yielding a
    /// typeless column); and an explicit <c>NULL</c> may not coexist with an explicit
    /// <c>NOT NULL</c>/<c>NN</c>.
    /// </remarks>
    public static bool TryParse(string? text, [NotNullWhen(true)] out EntityColumn? result, out string? error)
    {
        result = null;
        error = null;

        string input = text ?? string.Empty;
        string s = input.Trim();

        int colon = s.IndexOf(':');
        if (colon >= 0 && s.IndexOf(':', colon + 1) >= 0)
        {
            error = "Multiple ':' separators.";
            return false;
        }

        string namePart = colon >= 0 ? s[..colon].Trim() : s;
        string flagSegment = colon >= 0 ? s[(colon + 1)..].Trim() : s;
        Flags flags = StripTrailingFlags(flagSegment, out string remainder);

        if (flags.NullConflict)
        {
            error = "Conflicting NULL and NOT NULL flags.";
            return false;
        }

        if (colon >= 0)
        {
            if (namePart.Length == 0)
            {
                error = "Name is required.";
                return false;
            }

            if (remainder.Length == 0)
            {
                error = "Type expected after ':'.";
                return false;
            }
        }
        else if (remainder.Length == 0)
        {
            error = "Name is required.";
            return false;
        }

        result = Parse(input);
        return true;
    }

    private readonly record struct Flags(bool PrimaryKey, bool ForeignKey, bool Unique, bool? Nullable, bool NullConflict);

    // Consumes recognized flag tokens off the end of the segment; the untouched leading text is returned
    // via <paramref name="remainder"/>.
    private static Flags StripTrailingFlags(string segment, out string remainder)
    {
        List<string> tokens = segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        bool pk = false;
        bool fk = false;
        bool unique = false;
        bool? nullable = null;
        bool sawNull = false;
        bool sawNotNull = false;

        int end = tokens.Count;
        while (end > 0)
        {
            string token = tokens[end - 1].ToUpperInvariant();

            if (token == "NULL" && end >= 2 && tokens[end - 2].ToUpperInvariant() == "NOT")
            {
                nullable = false;
                sawNotNull = true;
                end -= 2;
                continue;
            }

            bool matched = true;
            switch (token)
            {
                case "PK": pk = true; break;
                case "FK": fk = true; break;
                case "UNIQUE" or "UQ": unique = true; break;
                case "NN": nullable = false; sawNotNull = true; break;
                case "NULL": nullable = true; sawNull = true; break;
                default: matched = false; break;
            }

            if (!matched)
            {
                break;
            }

            end--;
        }

        remainder = string.Join(' ', tokens.Take(end));
        return new Flags(pk, fk, unique, nullable, sawNull && sawNotNull);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
