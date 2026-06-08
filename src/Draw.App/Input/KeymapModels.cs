using System.Collections.Generic;

namespace Draw.App.Input;

/// <summary>
/// One on-disk keymap entry: a key sequence and the action id it triggers. A null or empty
/// <see cref="Action"/> in a user file <em>unbinds</em> any default binding with the same keys.
/// </summary>
public sealed record KeymapBinding(string? Keys, string? Action);

/// <summary>The on-disk keymap file shape (the baked-in default and user override share it).</summary>
public sealed record KeymapFile(IReadOnlyList<KeymapBinding>? Bindings);
