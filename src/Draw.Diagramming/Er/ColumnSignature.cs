using System;
using System.Collections.Generic;
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

    private readonly record struct Flags(bool PrimaryKey, bool ForeignKey, bool Unique, bool? Nullable);

    // Consumes recognized flag tokens off the end of the segment; the untouched leading text is returned
    // via <paramref name="remainder"/>.
    private static Flags StripTrailingFlags(string segment, out string remainder)
    {
        List<string> tokens = segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        bool pk = false;
        bool fk = false;
        bool unique = false;
        bool? nullable = null;

        int end = tokens.Count;
        while (end > 0)
        {
            string token = tokens[end - 1].ToUpperInvariant();

            if (token == "NULL" && end >= 2 && tokens[end - 2].ToUpperInvariant() == "NOT")
            {
                nullable = false;
                end -= 2;
                continue;
            }

            bool matched = true;
            switch (token)
            {
                case "PK": pk = true; break;
                case "FK": fk = true; break;
                case "UNIQUE" or "UQ": unique = true; break;
                case "NN": nullable = false; break;
                case "NULL": nullable = true; break;
                default: matched = false; break;
            }

            if (!matched)
            {
                break;
            }

            end--;
        }

        remainder = string.Join(' ', tokens.Take(end));
        return new Flags(pk, fk, unique, nullable);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
