namespace Draw.App.Input;

/// <summary>The vim <c>:</c> commands the editor understands.</summary>
public enum VimExKind
{
    /// <summary><c>:w</c> — save the active document.</summary>
    Write,

    /// <summary><c>:q</c> — close the active tab (prompting if modified; <c>:q!</c> discards).</summary>
    Quit,

    /// <summary><c>:wq</c> — save, then close the active tab.</summary>
    WriteQuit,

    /// <summary><c>:qa</c> — quit the whole app (prompting per modified tab; <c>:qa!</c> discards all).</summary>
    QuitAll,
}

/// <summary>A parsed vim <c>:</c> command and whether it carried a <c>!</c> (force) suffix.</summary>
public readonly record struct VimExCommand(VimExKind Kind, bool Bang)
{
    /// <summary>
    /// Parses raw command-line text (with or without the leading <c>:</c>) into a command, or <c>null</c>
    /// when it is empty / unrecognised. A trailing <c>!</c> sets <see cref="Bang"/> (force / discard).
    /// </summary>
    public static VimExCommand? Parse(string? input)
    {
        string text = (input ?? string.Empty).Trim();
        if (text.StartsWith(':'))
        {
            text = text[1..];
        }

        text = text.Trim();
        bool bang = text.EndsWith('!');
        if (bang)
        {
            text = text[..^1].Trim();
        }

        return text switch
        {
            "w" => new VimExCommand(VimExKind.Write, bang),
            "q" => new VimExCommand(VimExKind.Quit, bang),
            "wq" => new VimExCommand(VimExKind.WriteQuit, bang),
            "qa" => new VimExCommand(VimExKind.QuitAll, bang),
            _ => null,
        };
    }
}
