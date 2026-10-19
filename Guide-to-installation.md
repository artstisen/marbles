# Installing and running Marbles
Marbles comes in two kinds of builds, so pick the one that suits you:

- **A single file for your platform** (recommended): the .NET runtime is built into the file, so nothing else has to be installed. It is about 35-40 MB
- **The portable version**: a small version (about 1 MB) that runs on every platform, but needs the .NET 10 runtime to be installed (see [Install .NET](#install-net) below)

## Download and run Marbles
Download the latest Marbles archive for your platform from the [releases page](https://github.com/artstisen/marbles/releases), and unpack it in a directory of your choosing: it holds a folder _Marbles_ with the program in it. Keep the folder _lang_ next to the program: it holds the languages other than English and German.

| Platform | Download |
|---|---|
| Windows (most PCs) | `Marbles-1.5.3-win-x64.zip` |
| Windows on ARM | `Marbles-1.5.3-win-arm64.zip` |
| Linux (most PCs) | `Marbles-1.5.3-linux-x64.tar.gz` |
| Linux on 64-bit ARM (Raspberry Pi with a 64-bit system) | `Marbles-1.5.3-linux-arm64.tar.gz` |
| Linux on 32-bit ARM (Raspberry Pi with a 32-bit system) | `Marbles-1.5.3-linux-arm.tar.gz` |
| macOS on Apple silicon (M1 and later) | `Marbles-1.5.3-osx-arm64.tar.gz` |
| macOS on Intel | `Marbles-1.5.3-osx-x64.tar.gz` |
| Any platform, with .NET 10 installed | `Marbles-1.5.3-portable.zip` |

On Linux and macOS, unpack it with ```tar -xzf Marbles-1.5.3-linux-x64.tar.gz``` (with the name of the file you downloaded): the program can then be run as it is.

### The single file
Open your favorite terminal, go to the Marbles directory and start the program:

- **Windows**: type ```Marbles.exe``` (or ```.\Marbles.exe``` in PowerShell)
- **Linux and macOS**: type ```./Marbles``` (Linux and macOS are case sensitive). If it was not unpacked with `tar` (some programs leave out that it can be run), make it executable once with ```chmod +x Marbles```. On macOS the program is not signed: if macOS refuses to open it, allow it once with ```xattr -d com.apple.quarantine Marbles```

### The portable version
Unzip `Marbles-1.5.3-portable.zip`: its folder _Marbles_ has _Marbles.dll_, _Marbles.deps.json_, _Marbles.runtimeconfig.json_ and the folder _lang_. Open your favorite terminal, go to the Marbles directory and type ```dotnet Marbles.dll``` (Linux and macOS are case sensitive).

## Install .NET
Only the portable version needs this. Ensure that you have the .NET 10 runtime installed on your system. If not you can get it here:

- [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
- [Install .NET on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [Download the .NET Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Good to know
You can create an alias like "_mbls_" to run Marbles with less typing. See [this guide](Guide-to-windows-terminal-crt.md) for more information.

Marbles keeps your notes and settings in a directory named "data", next to the Marbles program, so the same notes and settings are used wherever Marbles is started from. If the directory you start Marbles in has a directory "data" of its own, that one is used instead (as earlier versions did, which used the directory they were started in). If the program directory cannot be written to (a program installed for all users), the data directory is "Marbles/data" in your application data (`~/.local/share` on Linux, `AppData\Local` on Windows). The languages other than English and German are files in the folder "lang" next to the program (see [Language files](#language-files)). To use spell checking in the editor, place spell files in a directory named "spell" next to it (see the [guide to the editor](Guide-to-editor.md#spell-checking)). Marbles needs a terminal window of at least 80 columns and 23 rows.

## Starting Marbles with a file
Give a file or a directory when you start Marbles, and it is opened at once, without the start page:

```
marbles notes.md                Open notes.md in the editor (the file browser is in its directory behind it)
marbles ~/projects/site         Show the directory in the file browser
marbles .                       Show the directory you are in, in the file browser
marbles work.mbls               Select the data file on the login page (in the data directory)
```

A path that is not a full path is taken from the directory you are in. Closing the editor (`:q`) shows the file browser in the directory of the file, and closing the file browser the start page, as after a jump from the [Jump menu](Guide-to-main-menu.md#jump-menu); `:exit` leaves Marbles. A file that is not there yet (in a directory that is) is made if you want that: Marbles asks first. A data file outside the data directory is shown in the file browser, which offers to copy it to the data directory. With _Text Editor only_ a data file is not opened, and with _Note Management only_ a text file or directory is not.

(On Windows the program is `Marbles.exe`, and the portable version is started with `dotnet Marbles.dll notes.md`.)

## Language files
English and German are built into Marbles. The other languages are text files in a folder named `lang` next to the Marbles program: `da.txt` (Danish), `nb.txt` (Norwegian), `sv.txt` (Swedish), `es.txt` (Spanish), `fr.txt` (French), `it.txt` (Italian), `pl.txt` (Polish), `nl.txt` (Dutch) and `fi.txt` (Finnish). They come with Marbles; keep the folder next to the program when you copy or move it. If it is missing, those languages are not offered (and Marbles starts in English if one of them was chosen, keeping the choice for when the file is back).

The language files can also be downloaded on their own, as the **language file pack** on the [releases page](https://github.com/artstisen/marbles/releases) (`Marbles-languages-1.5.3.zip`): the folder `lang` with the language files, the German file that is built into Marbles (in the folder `built-in`, to change the German translation), and a read-me. Unzip it next to the Marbles program to put the folder `lang` in place. In the source, the files are `Marbles/Lang/External` (and `Marbles/Lang/de.txt`), and `./publish.ps1 languages` makes the pack in _publish/languages_.

Marbles also looks for a folder `lang` in the data directory, which is used first. A language file there (or next to the program):

- **Changes a translation**: copy the file (or `de.txt` from the source, `Marbles/Lang`) into the folder, and edit the translations. A file for German is used instead of the one built in, and the list of languages shows `(lang)` after it
- **Adds a language**: a file named by the code of the language (`pt.txt`, `cs.txt`, `pt-br.txt`, in any letter case: `PT-BR.txt`) is found by itself and offered in the list of languages, with how much of it is translated. Its first line names it: `# Marbles: Português (pt)`

The files are UTF-8 text, with each text in English and translated below it:

```
en: "File deleted"
pl: "Plik usunięty"
```

A translation left empty, or missing, is shown in English. Keep the placeholders (`{0}`), the keys and the commands as they are. In the source, `python tools/langfile.py pl path/to/pl.txt` makes a file with all the texts of Marbles (or brings one up to date, keeping the translations), and `python tools/checklang.py pl path/to/pl.txt` checks it. The list of languages is read again each time it is opened, so a file can be changed and tried without restarting Marbles.

## Building from source
With the .NET 10 SDK installed:

```
dotnet build Marbles.sln -c Release
```

The files to run are then found in _Marbles/bin/Release/net10.0_.

To build executables that include the .NET runtime (so nothing has to be installed to run them), run ```./publish.ps1``` with PowerShell. It creates a single file for each platform in the directory _publish_, and the lightweight version for all platforms (which needs the .NET runtime, and is started with ```dotnet Marbles.dll```) in _publish/portable_. It also packs each of them for a release in _publish/release_: a zip for Windows and the portable version, and a tar.gz for Linux and macOS (where the program is marked as one that can be run), each with a folder _Marbles_, and the language file pack (`Marbles-languages-1.5.3.zip`). `./publish.ps1 win-x64,languages` builds only the ones named.

## Upgrading from Marbles v1.0.x
Copy your "data" directory to the directory of the new version, or unzip the new version over the old.
The first time you log in to a data file it is converted to the new file format, which earlier versions cannot open. Please read the [list of changes](CHANGELOG.md) before upgrading.

# Migrating your notes from AHOY! to Marbles

If you are using the [Ahoy note-taking application](https://github.com/artstisen/AHOY) you can migrate your data to Marbles using the AhoyToMarbles migration tool. The tool creates a data file in the format of Marbles v1.0.x, which Marbles converts the first time you log in to it.

![AhoyToMarbles](AhoyToMarbles-migration-tool.png)

### Specifications:

- Supported platforms: Windows and Linux
- Requirements: .NET 6
- Software license: MIT
- Download: [AhoyToMarbles1012.zip](https://github.com/artstisen/marbles/releases/download/v1.0.1.1/AhoyToMarbles1012.zip) (18Kb)
- Download w. Conhost executable (Windows): [AhoyToMarbles1012-WinExe.zip](https://github.com/artstisen/marbles/releases/download/v1.0.1.1/AhoyToMarbles1012-WinExe.zip) (90Kb)
