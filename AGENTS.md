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

## Checks before a change is done
- `dotnet test Marbles.sln` passes
- The change is tried in the running program, not only in tests
- Help (`?` / `:help`), the guides (`Guide-to-*.md`) and `CHANGELOG.md` describe the change
- A new setting is read and written by name in `FileIo` and counted in `SettingsTests`

## Code
- Source files are UTF-8 with BOM and CRLF line endings. Markdown files have LF
- No dependencies besides .NET itself
- `EditorEngine`, `TextBuffer`, the highlighters and `CommandCompletion` do not touch the console, so they can be tested. Keep it that way: drawing belongs in `TextArea` and the pages
- Text shown to the user is kept in `DialogText`
- The log (`EventLog`) can be read without a password: never log titles, the text of notes, search texts or passwords. Commands are logged by name only
- The encryption (AES-256-GCM, PBKDF2-HMAC-SHA256) is not to be changed without it being asked for

## Releases
- The version is set in `DialogText.ApplicationVersion` and in `Marbles.csproj` (`AssemblyVersion`, `FileVersion`)
- `publish.ps1` builds the `publish` folder
