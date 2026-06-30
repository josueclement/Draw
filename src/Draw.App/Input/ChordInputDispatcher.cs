using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Threading;
using Draw.App.Configuration;
using Microsoft.Extensions.Options;

namespace Draw.App.Input;

/// <summary>
/// Window-level keyboard dispatcher. Accumulates key strokes into a buffer, matches them against the
/// keymap (single gestures and multi-key chords), and invokes the resolved command. A pending chord is
/// shown in the status bar and discarded after an idle timeout; Escape cancels it.
/// </summary>
public sealed class ChordInputDispatcher
{
    private readonly KeymapActionRegistry _registry;
    private readonly KeymapStatusViewModel _status;
    private readonly Dictionary<IReadOnlyList<KeyStroke>, string> _exact;
    private readonly HashSet<IReadOnlyList<KeyStroke>> _prefixes;
    private readonly List<KeyStroke> _buffer = new();
    private readonly DispatcherTimer _chordTimer;
    private readonly DispatcherTimer _messageTimer;

    // When the buffer is a complete binding that is also the prefix of a longer one, we hold this so the
    // shorter binding still fires if the chord times out or is broken by a non-matching key.
    private string? _pendingExactAction;

    public ChordInputDispatcher(
        IKeymapService keymap,
        KeymapActionRegistry registry,
        KeymapStatusViewModel status,
        IOptions<KeymapOptions> options)
    {
        _registry = registry;
        _status = status;
        _exact = new Dictionary<IReadOnlyList<KeyStroke>, string>(KeyStrokeSequenceComparer.Instance);
        _prefixes = new HashSet<IReadOnlyList<KeyStroke>>(KeyStrokeSequenceComparer.Instance);

        foreach (ParsedBinding binding in keymap.Bindings)
        {
            if (!_registry.Contains(binding.Action))
            {
                Debug.WriteLine($"[keymap] '{KeyGestureParser.Describe(binding.Strokes)}' → unknown action '{binding.Action}'; ignored.");
                continue;
            }

            _exact[binding.Strokes] = binding.Action;
            for (int length = 1; length < binding.Strokes.Count; length++)
            {
                _prefixes.Add(binding.Strokes.Take(length).ToArray());
            }
        }

        KeymapOptions opts = options.Value;
        _chordTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, opts.ChordTimeoutMs)) };
        _chordTimer.Tick += OnChordTimeout;
        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(200, opts.TransientMessageMs)) };
        _messageTimer.Tick += OnMessageTimeout;
    }

    /// <summary>Handles a key press; returns true when the key was consumed by the keymap.</summary>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (IsModifierKey(e.Key))
        {
            return false; // A bare modifier press neither buffers nor cancels a chord.
        }

        // Escape cancels a chord in progress; with an empty buffer it falls through to its binding (if any).
        if (e.Key == Key.Escape && _buffer.Count > 0)
        {
            Reset();
            return true;
        }

        KeyStroke stroke = Normalize(e.Key, e.KeyModifiers);
        List<KeyStroke> tentative = new(_buffer.Count + 1);
        tentative.AddRange(_buffer);
        tentative.Add(stroke);

        bool hasExact = _exact.TryGetValue(tentative, out string? action);
        bool hasLonger = _prefixes.Contains(tentative);

        if (hasExact && !hasLonger)
        {
            Reset();
            Invoke(action!);
            return true;
        }

        if (hasLonger)
        {
            // Either a pure prefix, or a complete binding that is also the prefix of a longer one: keep
            // waiting. Remember the exact match (if any) so it still fires on timeout / on a breaking key.
            _buffer.Add(stroke);
            _pendingExactAction = hasExact ? action : null;
            _status.Pending = KeyGestureParser.Describe(_buffer);
            RestartChordTimer();
            return true;
        }

        // The extended sequence matches nothing.
        if (_buffer.Count == 0)
        {
            return false; // A lone, unbound key that starts no chord: leave it for other handlers.
        }

        string? pendingExact = _pendingExactAction;
        Reset();
        if (pendingExact is not null)
        {
            Invoke(pendingExact);          // Commit the shorter complete binding...
            return HandleKeyDown(e);        // ...then reprocess the breaking key from an empty buffer.
        }

        Flash($"{KeyGestureParser.Describe(tentative)} — no binding");
        return true;
    }

    /// <summary>Clears any chord in progress (e.g. on focus loss or window deactivation).</summary>
    public void Reset()
    {
        _buffer.Clear();
        _pendingExactAction = null;
        _chordTimer.Stop();
        _status.Pending = null;
    }

    private void OnChordTimeout(object? sender, EventArgs e)
    {
        string? pendingExact = _pendingExactAction;
        bool hadBuffer = _buffer.Count > 0;
        Reset();

        if (pendingExact is not null)
        {
            Invoke(pendingExact);
        }
        else if (hadBuffer)
        {
            Flash("chord timed out");
        }
    }

    private void OnMessageTimeout(object? sender, EventArgs e)
    {
        _messageTimer.Stop();
        _status.TransientMessage = null;
    }

    private void Invoke(string actionId)
    {
        if (!_registry.TryGet(actionId, out KeymapAction? action))
        {
            return;
        }

        (ICommand Command, object? Parameter)? resolved = action.Resolve();
        if (resolved is null)
        {
            Flash("no active document");
            return;
        }

        ICommand command = resolved.Value.Command;
        object? parameter = resolved.Value.Parameter;
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    /// <summary>Shows a short-lived status-bar message that auto-clears after the transient timeout.</summary>
    public void Flash(string message)
    {
        _status.TransientMessage = message;
        _messageTimer.Stop();
        _messageTimer.Start();
    }

    private void RestartChordTimer()
    {
        _chordTimer.Stop();
        _chordTimer.Start();
    }

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
        or Key.LWin or Key.RWin;

    // Shift is significant, so "Shift+S" is distinct from "s" (lets shortcuts like Shift+S open a menu).
    private static KeyStroke Normalize(Key key, KeyModifiers modifiers) => new(key, modifiers);
}
