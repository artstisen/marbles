# Marbles
A free, open-source, cross-platform Vim-style modal text editor for taking encrypted notes in any terminal program on Windows, Linux, and macOS.

Marbles includes an orthodox file manager and a capable Unicode text editor with syntax highlighting, code completion, spell checking, macro recorder/editor, code snippet support, search/replace, file diff, jump menu, bookmarks, hotlist and favorites, and can be used as a feature-rich text editing and file management tool. 

## Pure 7-bit ASCII TUI
The aim is to make an unbloated set of core tools. I've included functionality that I would normally have added via lightweight, functional plugins, and init modifications in Neovim, without disrupting the classic terminal aesthetic. The interface of Marbles is drawn with standard 128-character ASCII only (no extended ASCII or ANSI character sets). This makes Marbles compatible with all standard monospaced fonts, regardless of the platform.  Encrypted notes, text/code files can contain any Unicode text.


![Marbles animation](marbles.gif)


## About Marbles
Marbles provides a simple interface for creating, editing, searching, copying, arranging, importing, exporting and deleting notes. Notes are stored as a searchable collection of entries contained in a single encrypted file for easy portability. Multiple files can be created and managed from within the program itself. Marbles will handle large text documents and data files with thousands of notes.

Marbles also works as a text editor: when it starts you select between your encrypted notes and a file browser for editing ordinary text/code files (.txt, .md, .html, .json, .cs...) with the same editor. It can be set to start directly in either. The file browser can also manage files and directories (create, rename, copy, move, delete) and keeps a list of your favorite files, file history and directory hotlist width file diff.

Marbles is operated in the style of Vim: the editor has Normal, Insert and Visual modes, and the menus and lists use the same keys for moving around (`h` `j` `k` `l`), searching (`/`), help (`?`) and commands (`:`).


## Specifications
- Supported platforms: Windows, Linux and macOS
- Requirements: .NET 10 runtime (available as self-contained single file executables, with the .NET runtime included)
- Application type: Text mode
- Core code base: Agnostic
- Box drawing: ASCII
- Console I/O encoding: UTF-8
- Languages: English and German built in; Danish, Norwegian (bokmål), Swedish, Spanish, French, Italian, Polish, Dutch and Finnish as language files that can be changed, and more can be added (the commands and keys are the same in all languages)
- Date/time format: ISO 8601
- File encryption: AES-256-GCM, with the key derived from the password using PBKDF2-HMAC-SHA256
- Software license: [MIT](LICENSE)

## Tools used
- Code editor: Marbles
- Programming language: C# 14
- Framework: .NET 10
- Claude Opus 5.5: Scripting/testing/code review (as of Marbles v1.5.0)

Marbles relies on the .NET framework to handle the differences between the platforms and has no OS-specific logic built into the source.

## Guides/getting started
- [Download and installation](Guide-to-installation.md)
- [Login page](Guide-to-login-menu.md)
- [Main menu, lists and entries](Guide-to-main-menu.md)
- [Editor](Guide-to-editor.md)
- [Changes and upgrading from earlier versions](CHANGELOG.md)
- [PowerShell scripting tips](Guide-to-windows-terminal-crt.md)

Inside Marbles, press `?` for the list of keys.

## Building from source
Marbles has no dependencies besides .NET itself.

```
dotnet build Marbles.sln
dotnet test Marbles.sln
dotnet run --project Marbles
```

`AGENTS.md` has the rules for working on the code (for people and for coding agents).

`publish.ps1` builds self-contained single file executables (with the .NET runtime included) for Windows, Linux and macOS, and the lightweight version for all platforms that needs the .NET runtime.

## Gallery

Marbles in Windows Terminal on Windows 11:
![Marbles in WT on Windows 11](marbles-wt-win11.jpg)

Marbles in QTerminal on Lubuntu (lightweight Linux distribution based on Ubuntu):
![Marbles in QTerminal on Lubuntu](marbles-qterminal.jpg)

Marbles in LXTerminal on Raspberry Pi (Debian-based Linux distribution):
![Marbles in LXTerminal on Raspberry Pi](marbles-lxterminal.jpg)

Marbles in Windows Terminal with CRT mod:
![Marbles Windows Terminal with CRT mod](marbles-crt.gif)

Multiple instances of Marbles in split Windows Terminal with CRT mod:
![Marbles Windows Terminal split with CRT mod](marbles-wt-crt-mod.jpg)

### Windows Terminal CRT mod:

- [Guide to installing the Windows Terminal CRT mod](Guide-to-windows-terminal-crt.md)
