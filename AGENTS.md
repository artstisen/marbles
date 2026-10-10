# Rules for working on Marbles

For anyone changing the code, people and coding agents alike.

## Commands
Every place with a command line (main menu, entry view, lists of entries, file browser, favorites, search results, editor) has a table of its commands in `Marbles/Commands.cs`. `Tab` completes from these tables, and the log uses them to tell a command from other text.

**When a command is added, renamed or removed, change the table of that place in `Commands.cs` in the same change.**

- A command goes in the first list of its table (the names `Tab` completes to). A short form of it (`q` for `quit`) goes in the second list, which is accepted but not offered
- A command used in several places goes in the table of each of them
- A new place with a command line gets a value in `CommandContext`, a table, and passes its context to `CommandLine.Read`
- `CommandTests` compares the tables with the code that runs the commands (it reads the source files), and fails with the names that differ. If the code that runs the commands of a place is moved or restructured, update where that test looks
- Also add the command to the help (`Tools.cs`, the section of the place) and to the guide that covers it

## Texts and languages
Text shown to the user is written in English in the code and given to `Lang.T("...")`, or `Lang.F("... {0} ...", value)` when it has values in it. The other languages are files in `Marbles/Lang/` (`da.txt` ...), built into the program.

- Write the whole text in the call, as one text: `Lang.F("{0} files found", count)`, not `count + " files found"` (word order differs between languages), and not `Lang.T(condition ? "a" : "b")` (write two calls). A name made while running (a color, a month) is listed in `Lang.Names`
- What stays English in all languages: commands, keys, the names of the editor modes (NORMAL, INSERT, VISUAL ...), the settings file, the log, and the guides. Keys are not translated in key hints either (`j/k Select`: only `Select` is)
- After adding or changing texts, run `python tools/langfile.py xx` for each language file (da, de, nb, sv, es, fr): new texts get an empty translation, which is shown in English until it is filled in. Then translate them, and run `python tools/checklang.py xx` (0 errors; its warnings point at translations that may be too long for the screen)
- The language files are built into the program: build again to see a change in a file
- The screen is 80 columns: keep translations about as long as the English. Key hints, help rows and page footers that are too long are cut at the edge (`Tools.FitLine`), labels in boxes take the width of their text, and nothing may be placed at a fixed column after a text that can be translated
- `LangTests` checks the language files against the code (missing or unknown texts, placeholders, keys, blank space) and fails on text written to the screen without `Lang.T`. A line that is meant to stay English gets a comment containing "English"
- Text that is kept (a field, a list made at startup) is not translated when the language changes: look it up when it is shown

## Checks before a change is done
- `dotnet test Marbles.sln` passes
- The change is tried in the running program, not only in tests
- Help (`?` / `:help`), the guides (`Guide-to-*.md`) and `CHANGELOG.md` describe the change
- A new setting is read and written by name in `FileIo` and counted in `SettingsTests`

## Code
- Source files are UTF-8 with BOM and CRLF line endings. Markdown files have LF
- No dependencies besides .NET itself
- `EditorEngine`, `TextBuffer`, the highlighters and `CommandCompletion` do not touch the console, so they can be tested. Keep it that way: drawing belongs in `TextArea` and the pages
- Text shown to the user is kept in `DialogText` (or written in a `Lang.T` call where it is used)
- The log (`EventLog`) can be read without a password: never log titles, the text of notes, search texts or passwords. Commands are logged by name only
- The encryption (AES-256-GCM, PBKDF2-HMAC-SHA256) is not to be changed without it being asked for

## Releases
- The version is set in `DialogText.ApplicationVersion` and in `Marbles.csproj` (`AssemblyVersion`, `FileVersion`)
- `publish.ps1` builds the `publish` folder
