namespace Marbles;

// Macros: recording what is done in the editor, and playing a macro (see MacroLanguage for the language, and
// MacroFiles for the files). Nothing here touches the console
internal sealed partial class EditorEngine
{
    // The keys of each command recorded so far, and of the command being typed (and if it is left out)
    private readonly List<ConsoleKeyInfo[]> _macroCommands = new();
    private readonly List<ConsoleKeyInfo> _macroCommand = new();
    private bool _macroCommandLeftOut;

    // While a macro plays: how deep macros play macros, how many keys and steps are left before it is stopped, and
    // the macros read during the run (each is read once)
    private int _macroDepth;
    private int _macroKeysLeft;
    private readonly Dictionary<string, List<MacroLanguage.Step>> _macroCache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxMacroDepth = 8;
    private const int MaxMacroKeys = 200_000;

    /// <summary>
    /// A macro is being recorded (":record"): the Esc that would close the editor asks what to do instead
    /// </summary>
    internal bool IsRecording { get; private set; }

    /// <summary>
    /// A macro is being played
    /// </summary>
    internal bool IsPlayingMacro => _macroDepth > 0;

    /// <summary>
    /// The text of a macro by its name, or null if there is none (set by the host: MacroFiles.Load)
    /// </summary>
    internal Func<string, string?>? MacroLoader { get; set; }

    // What the host is asked to do that changes nothing in the text or only shows something: it is recorded, and
    // passed over when a macro plays (the clipboard after a yank, a setting of the view, the help)
    private static bool IsSideEffect(EditorRequest request) => request is EditorRequest.Clipboard
        or EditorRequest.WrapChanged or EditorRequest.LineNumbersChanged or EditorRequest.ScrollOffChanged
        or EditorRequest.CompleteChanged or EditorRequest.SyntaxChanged or EditorRequest.Help;

    internal void StartRecording()
    {
        _macroCommands.Clear();
        _macroCommand.Clear();
        _macroCommandLeftOut = false;
        IsRecording = true;
        // The completions are not shown while recording: what Tab or Enter would complete to depends on the lists
        ClearCompletions();
    }

    /// <summary>
    /// Stop recording
    /// </summary>
    /// <returns>The lines of the macro (for saving it)</returns>
    internal List<string> StopRecording()
    {
        IsRecording = false;
        var lines = MacroLanguage.ToLines(_macroCommands);
        _macroCommands.Clear();
        _macroCommand.Clear();
        return lines;
    }

    /// <summary>
    /// A macro played by the host while recording (picked in the list of ":macro"): recorded as "play name"
    /// </summary>
    internal void RecordPlay(string name)
    {
        if (!IsRecording || IsPlayingMacro) return;
        _macroCommands.Add(MacroLanguage.ParseKeys(":play " + name.Replace("<", "<lt>") + "<CR>").ToArray());
    }

    // A command brought back with Up on the command line: it is recorded as typed (":" and its text), as the
    // commands given before are not the same when the macro is played
    private void RecordCommandAsTyped(string command)
    {
        if (!IsRecording || _replaying || IsPlayingMacro) return;
        _macroCommand.Clear();
        _macroCommand.AddRange(MacroLanguage.ParseKeys(":" + command.Replace("<", "<lt>") + "<CR>"));
    }

    // Called by HandleKey before a key is handled: a command that starts is recorded from its first key
    private void RecordMacroKey(ConsoleKeyInfo key, bool startsCommand)
    {
        if (!IsRecording || _replaying || IsPlayingMacro) return;
        if (startsCommand)
        {
            _macroCommand.Clear();
            _macroCommandLeftOut = false;
        }

        _macroCommand.Add(key);
    }

    // Called by HandleKey after a key is handled. What the host must do (a box, saving, leaving) cannot be played
    // again: in INSERT mode (and Ctrl+O) only that key is left out, so the text typed is kept; another command is
    // left out whole. A command that is done is kept
    private void RecordMacroCommandDone(bool insertKey)
    {
        if (!IsRecording || _replaying || IsPlayingMacro || _macroCommand.Count == 0) return;
        if (_request != EditorRequest.None && !IsSideEffect(_request))
        {
            if (insertKey)
            {
                _macroCommand.RemoveAt(_macroCommand.Count - 1);
                // INSERT mode was left by it (Ctrl+O): as with Esc
                if (Mode == EditorMode.Normal) _macroCommand.Add(MacroLanguage.ParseKeys("<Esc>")[0]);
            }
            else _macroCommandLeftOut = true;
            if (_request != EditorRequest.MacroStop) SetMessage(Lang.T("Not recorded in the macro"), false);
        }

        if (Mode != EditorMode.Normal || HasPending) return;
        if (_macroCommand.Count > 0 && !_macroCommandLeftOut) _macroCommands.Add(_macroCommand.ToArray());
        _macroCommand.Clear();
        _macroCommandLeftOut = false;
    }

    /// <summary>
    /// ":play name 3": play a macro (a number of times). It is undone as one step. It stops at a step that cannot
    /// be done in a macro (saving, closing or leaving the text), with a message
    /// </summary>
    internal void PlayMacro(string name, int times)
    {
        // (While recording, the command is recorded as "play name", not the steps of the macro one by one)
        var error = PlayMacroSteps(name, times);
        SetMessage(error.Length > 0 ? error : Lang.F("Played the macro {0}", name), error.Length > 0);
    }

    // The steps of a macro (read once in a run), or null with the message why there are none
    private List<MacroLanguage.Step>? MacroSteps(string name, out string error)
    {
        error = string.Empty;
        if (_macroCache.TryGetValue(name, out var cached)) return cached;
        var text = MacroLoader?.Invoke(name);
        if (text == null)
        {
            error = Lang.F("There is no macro named {0}", name);
            return null;
        }

        var steps = MacroLanguage.Compile(text, out error);
        if (error.Length > 0) return null;
        _macroCache[name] = steps;
        return steps;
    }

    // Play a macro; the message about why it stopped, or empty
    private string PlayMacroSteps(string name, int times)
    {
        if (_macroDepth >= MaxMacroDepth) return Lang.T("Macros play other macros too deep");
        var first = _macroDepth == 0;
        if (first) _macroCache.Clear();
        var steps = MacroSteps(name, out var error);
        if (steps == null) return error;

        if (first)
        {
            ResetPending();
            if (Mode != EditorMode.Normal) Mode = EditorMode.Normal;
            Buffer.EndStep(Cursor);
            _macroKeysLeft = MaxMacroKeys;
            // All of it is undone as one step
            Buffer.BeginGroup(Cursor);
        }

        _macroDepth++;
        try
        {
            for (var time = 0; time < times; time++)
            {
                // Each time a macro is played counts too (also one without steps)
                if (--_macroKeysLeft < 0) return Lang.T("The macro is stopped: it has too many steps");
                foreach (var step in steps)
                {
                    // Each step counts (also one that plays another macro, or one without keys)
                    if (--_macroKeysLeft < 0) return Lang.T("The macro is stopped: it has too many steps");
                    if (step.Play != null)
                    {
                        var inner = PlayMacroSteps(step.Play, step.Times);
                        if (inner.Length > 0) return inner;
                        continue;
                    }

                    foreach (var key in step.Keys)
                    {
                        if (--_macroKeysLeft < 0) return Lang.T("The macro is stopped: it has too many steps");
                        var request = HandleKey(key);
                        if (request != EditorRequest.None && !IsSideEffect(request))
                        {
                            // Saving, closing, leaving the text or opening a box is not done in a macro
                            return Lang.F("Line {0} of the macro is not done in a macro: {1}", step.Line,
                                MacroLanguage.FormatKey(key) ?? string.Empty);
                        }

                        if (MessageIsError)
                        {
                            return Lang.F("The macro stopped at line {0}: {1}", step.Line, Message);
                        }
                    }
                }
            }

            return string.Empty;
        }
        finally
        {
            _macroDepth--;
            if (first)
            {
                // Back in NORMAL mode, with nothing pending
                if (Mode == EditorMode.Insert)
                {
                    HandleInsert(new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false));
                }

                ResetPending();
                if (Mode != EditorMode.Normal) Mode = EditorMode.Normal;
                Buffer.EndGroup(Cursor);
                _request = EditorRequest.None;
                _macroCache.Clear();
                ClampCursor();
                EnsureCursorVisible();
            }
        }
    }
}
