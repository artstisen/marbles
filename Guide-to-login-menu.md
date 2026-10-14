# Guide to the start and login pages

## Select mode
Marbles can be used for encrypted notes and as an editor for ordinary text files. When it starts, the start page is shown in a frame with three tabs at its top: **Mode**, where you select what to use Marbles for, **Configuration**, with the settings, and **Help**, with the help and the about page. The settings are on four pages, shown as the sub-tabs **General**, **Layout**, **Editor** and **System** in a row at the top of the frame. The tab and sub-tab shown are highlighted in the color of the frame.

`Tab` goes to the next tab (Mode, Configuration, Help, and round to Mode again), and `Shift+Tab` the other way. `h` and `l` (or `Left`/`Right`) go to the previous and the next sub-tab of the tab shown, going round, so in Configuration they go through General, Layout, Editor and System. The keys are shown at the right ends of the two rows: `Tab` after the tabs and `h/l` after the sub-tabs, in the color of the frame. A tab opens with the sub-tab it showed last. This is the same in all the frames with tabs (the color schemes, the logos).


### Mode
- `1` **Note Management (encrypted notes)**: Continue to the login page
- `2` **Text Editor/File Management**: Open the [file browser](#file-browser) to edit plain text files and manage files. Closing the file browser returns here
- `3` **Jump menu**: Open the [Jump menu](Guide-to-main-menu.md#jump-menu), to go straight to a file, a page of Marbles or a data file (also with `Ctrl+O`). Pages such as Color schemes and Help open on top of the start page
- `4` **Quick guide**: A short introduction to Marbles: what the two parts are for, the keys for moving around, and the modes of the editor. `Esc` closes it, and `?` shows the help with all the keys
- `5` **Exit**: Close Marbles at once

Move the selection with `j`/`k` (or the arrow keys) or the number, and press `Enter`. `?` shows the help, and `Q` (or `Esc`) exits Marbles after asking in a box on top of the page (`Y`/`N`, or `j`/`k` and `Enter`; `Esc` cancels).

### Configuration
The settings are on four pages, so they can be changed in one place without logging in to a data file. `j` `k` (and `gg` `G`) select a setting on the page, and the number of a row selects it. `Enter` or `Space` changes it. `Esc` or `Q` goes back to the Mode tab. Changes are saved at once.

**General**

- **Show choices on startup** (the default)
- **Start in Note Management**: Go directly to the login page
- **Start in Text Editor (file browser)**: Go directly to the file browser
  - **Open the favorites list first** (shown when starting in the Text Editor): Start in the [favorites](#favorites) of the file browser. Closing the list shows the file browser. Saved as `startfavorites` in the settings file
  - The choice is saved as `startmode` (`select`, `notes` or `editor`) in the settings file, and `:defaults` in the main menu sets it back to showing the choices
- **Text Editor only** and **Note Management only**: Use this Marbles for one of the two modes only. They are for running two copies of Marbles in separate directories, one as a text editor and one for notes. Only one of them can be set (setting one clears the other, and setting it again turns it off)
  - _Text Editor only_: _Note Management_ is not shown in the Mode tab, and the file browser does not open data files (`.mbls`): it says so on its bottom line
  - _Note Management only_: _Text Editor/File Management_ is not shown in the Mode tab
  - The choices of the Mode tab are numbered as they are shown (`1` to `4`), and starting in the mode that is not used cannot be chosen (if it was chosen, the choices are shown at startup)
  - Saved as `onlymode` (`none`, `editor` or `notes`) in the settings file. _Reset all settings_ and `:defaults` turn both off
- **Language**: The language of the menus, help and messages: English, Dansk (Danish), Deutsch (German), Norsk bokmål (Norwegian), Svenska (Swedish), Español (Spanish) or Français (French). `Enter` shows the languages in a box to pick from. Commands, keys and the names of the editor modes (NORMAL, INSERT, VISUAL ...) are English in all languages, so they can be talked about the same way everywhere. The guides, the settings file and the log are in English

**Layout**

- **Automatic size**: Follow the size of the terminal window. Turned off, the default size (80 x 23) is used until you set another
- **Size**: Set the number of columns and rows by hand (shown when _Automatic size_ is off)
- **Black and white (no colors)**: Show everything in black and white. Your color scheme is kept and is used again when this is turned off
- **Color scheme**: The color scheme in use. `Enter` opens the [color schemes](Guide-to-main-menu.md#color-schemes), where a scheme is selected, and the colors are edited and saved as a scheme of your own (not shown when _Black and white_ is on)
- **Logo**: The logo in use. `Enter` opens the [logos](Guide-to-main-menu.md#logos), where a logo is selected, or drawn and saved as one of your own (4 rows of 63 characters)
- **Logo animation**: How fast the logo is swept in from the left when you arrive at the login page: _Slow_, _Fast_ (the default), _Very fast_ or _Off_. `Enter` shows them in a box to pick from. Saved as `logoanim` (`slow`, `fast`, `veryfast` or `off`) in the settings file

**Editor**

- **Line numbers**: Show line numbers to the left of the text (off from the start)
- **Wrap long lines**: Long lines go on to the next rows (on from the start). When off, the view scrolls sideways (see [the guide to the editor](Guide-to-editor.md#long-lines))
- **Keep 12 lines around the cursor**: When the cursor comes within 12 lines of the top or the bottom of the editor, the text scrolls, so you see what comes next (on from the start). The number is set with `:scrolloff 8` in the editor (see [the guide to the editor](Guide-to-editor.md#scrolling))
- **Clipboard and paste bin**: `Enter` opens the clipboard settings: where yanked text goes out of Marbles (see [the guide to the editor](Guide-to-editor.md#system-clipboard-and-paste-bin))
- **Syntax highlighting**: Color the text of the kinds of files the editor knows (Markdown, HTML, CSS, JavaScript, JSON, XML), on from the start. It is the setting `syntaxcolors`, which `:syntax off` and `:syntax on` in the editor also change
- **Spell checker**: The languages used when spell checking is turned on in the editor with `:spell` (it is not turned on here). `Enter` shows the languages there is a spell file for in a box, where `Enter` turns a language on or off (several can be on, and the last one stays on) and `Esc` closes. Saved as `spelllang` (`en` or `da,en`). The spell files are in the directory `spell`; without any, the row says so
- **Snippet editor**: Open the file of [snippets](Guide-to-editor.md#snippets), `snippets.txt`, in the editor. If there is no file yet, you are asked whether to make one, with two examples. Closing the editor returns to this page
- **Complete words while typing**: In Insert mode, the word typed is looked up in the lists of completions, and `Tab` completes it (see [Completing words](Guide-to-editor.md#completing-words)). Off from the start; `:complete` in the editor changes it too. It is the setting `autocomplete`
- **Completions editor**: Open the file of completions, `completions.txt`, in the editor. If there is no file yet, it is made with lists to start with. Closing the editor returns to this page

**System**

- **Reset all settings**: Set all settings back to their defaults (you are asked first, in a box where _No_ is selected first)
- **Edit settings.ini**: Open the settings file in the editor (it can also be edited from the file system). When the file is saved, Marbles starts over and reads the settings again; closing it without saving returns to the page
- **Restart Marbles**: Start Marbles over: the settings are read again and the start page is shown as at startup (you are asked first, in a box)
- **Info**: Show the version of Marbles, the directory of the program, the data directory and the settings file in a box. Any key closes it
- **View log**: Show the [log](#the-log)

### Commands
On every tab of the start page (Mode, the pages of Configuration, Help), `:` opens the command line on the bottom line. `Tab` completes the command you are typing, from the commands of this page:

- `:note` (or `:notes`) The login page, as _Note Management_ on the Mode tab; `:file` (or `:browse`) The file browser, as _Text Editor/File Management_. With _Text Editor only_ or _Note Management only_, the mode that is not used is refused
- `:mode` The Mode tab; `:config` (or `:general`), `:layout`, `:editor` and `:system` The pages of the Configuration tab (`:editor` is the Editor page of Configuration, not the text editor)
- `:jump` (or `:ju`) The [Jump menu](Guide-to-main-menu.md#jump-menu)
- `:help` (or `:h`) The help of the start page, as `?`; `:about` The about page; `:quick` (or `:guide`) The quick guide
- `:scheme` The color schemes; `:logo` The logos; `:snippet` (or `:snip`) The snippet editor; `:completions` (or `:comp`) The completions editor; `:clipboard` The clipboard settings; `:log` The log
- `:restart` Restart Marbles (asks first); `:reset` Reset all settings (asks first, as on the System page); `:quit`, `:exit` (or `:q`) Exit Marbles (asks first)

The command names are the same in all languages, and the names of the main menu work here too (`:browse`, `:guide`, `:scheme`, `:logo`).

### Help
- `1` **Help**: All the keys and commands of Marbles, as `:help` (`?` on a page shows the keys of that page)
- `2` **About**: General information about Marbles, and its license on the next page. `Esc` closes it

Select with `j`/`k` or the number and press `Enter`. `Esc` goes back to the Mode tab.

When Marbles starts directly in one of the two, the choices can still be reached: from the login page with `Esc`, `T`, and from the file browser by closing it.

## Login page
Data files (your encrypted collections of notes) are managed from the login page.
A default data file "data.mbls" is created when first starting Marbles, where you are asked to create its password (8 to 128 characters). It is not possible to recover your notes if you forget your password.

Log in to the selected data file by typing its password and pressing `Enter`.

- `Left`/`Right` (or `Tab`) Select a data file in the current directory
- `Up`/`Down` Select a directory: `data` (the data directory itself) or one of the directories in it, shown as `data/notes`
- `Space` Move the selected data file to another directory: select the directory and press `Enter`
- `Esc` Clear the password field. When the field is empty: open the options

## Options
Press `Esc` to show the options in a box. Select one with `j` `k` (or the arrow keys, `gg` `G`) and press `Enter` (or `l`), or press the key shown in front of it:

- `F` Browse files: open the [file browser](#file-browser) to edit plain text files
- `T` Go to the [Select mode](#select-mode) page
- `S` Go to the [Configuration](#configuration) tab of the Select mode page, with the settings
- `O` Open the [Jump menu](Guide-to-main-menu.md#jump-menu), to go straight to a file, a page of Marbles or a data file
- `N` Create a new data file
- `X` Rename the selected data file
- `C` Create a copy of the selected data file
- `D` Delete the selected data file
- `M` Merge two data files into a new, or export a data file as XML (the exported file is not encrypted)
- `I` Show information about the data file in a box (name, type, size, dates, path and backup directory). Any key closes it
- `R` Refresh the login page
- `U` Show the quick guide
- `?` Show the help (the list of keys)
- `Q` Exit Marbles
- `Esc` (or `h`) Close the options

Restarting Marbles, the log and the settings file are on the System page of the Configuration tab of the [Select mode](#select-mode) page (`S`).

The data file and the directory are selected on the login page itself, with the arrow keys (see above).

## File browser
The file browser (`Esc`, `F`) is for editing single text files (.txt, .md, .html ...) with the Marbles editor, without logging in to a data file. **Text files are not encrypted.**

The browser shows two panes side by side, each with its own directory. The pane with the focus has a highlighted label (`PANE 1` or `PANE 2`) in the line above its list, and all keys work on that pane.

- `Tab` Go to the other pane. The selection of the pane without the focus is still shown, but not highlighted
- `T` or `:panes` Show one pane (the one with the focus, in the full width of the window) or two

In a pane that is narrow, as when the window is 80 columns wide, the type column is left out and long names are shortened. With one pane, or a wide window, all columns are shown.

The browser starts in the Marbles data directory and remembers the directories of both panes, the file or directory selected in each, the pane that had the focus and whether one or two panes were shown (`browsedir1`, `browsedir2`, `browsesel1`, `browsesel2`, `browsepane` and `browsedual` in the settings file). They are saved however the browser is left (closed, a jump to another page, or exiting Marbles), and also when a file is opened in the editor. The selection is remembered by name: if the file is gone, the first row is selected.

The file browser can also be opened with the command `:browse` when you are logged in to a data file. You can look around and close the browser again without anything else happening, but opening a file logs you out of the data file, and the login page is shown when you close the browser.

- `j` `k` Select, `gg` `G` First/Last, `PgUp` `PgDn` One page up/down
- `l` or `Enter` Open the selected directory or file. A file is opened in the editor where the cursor was when you closed it the last time (see [Where the cursor was](#where-the-cursor-was))
- `h`, `-` or `Backspace` Go to the directory above (above the root of a drive on Windows: the list of drives)
- `/text` Only show the names containing "text". `Esc` shows all again
- `N` or `:new name` Create a new file in the directory and open it. Without a name (`N` or `:new`) you are asked for it
- `I` or `:info` (also in the list of favorites) Show information about the selected file or directory: type, size (for a directory: the number of files and directories in it), when it was created and last changed, and the full path. Any key closes the box
- `V` or `:peek` Peek at the selected file: it is shown read-only in the other pane (with one pane shown, the second pane is shown while you peek). Move and search in it with the keys of the editor (`j` `k`, `gg` `G`, `Ctrl+D`, `/text`, `y` to yank to the clipboard ...); what would change the text is refused. `Enter` opens the file in the editor at the line of the cursor, and `Esc` (or `:q`) goes back to the browser. A large file is shown up to about 2000 lines (100 KB), with a last line telling that there is more. Directories, data files and files that are not text are not peeked at
- See [Managing files](#managing-files), [Favorites](#favorites) and [File history](#file-history) below for the other keys
- `R` Refresh
- `:path` Copy the full path of the selected file or directory (on `..`: the directory shown) to the system clipboard and/or the paste bin, as set in the [clipboard settings](Guide-to-editor.md#system-clipboard-and-paste-bin)
- `:match` With two panes: the other pane shows the directory of the pane with the focus (with the same file or directory selected). The focus stays where it is
- `:cd path` Go to a directory. `:home` Go to the folder of the data files in use (the data directory, or the subfolder of it chosen for the data files)
- `:data` Go to the data directory itself, with the files Marbles keeps there: `settings.ini`, `snippets.txt` and `completions.txt` (the snippets and the completions of the editor), `favorites.txt` and `history.txt` (of the file browser), and `jump.txt` (of the Jump menu)
- `Tab` on the command line completes the command you are typing, and goes through the commands of the file browser when pressed again (see [the command line](Guide-to-main-menu.md))
- `?` Help
- `Esc`, `Q` or `:q` Close the file browser
- `:exit` Exit Marbles (asks first)

A text file is opened in the [editor](Guide-to-editor.md), where `:w` saves it and `:q` returns to the browser. Files are saved as UTF-8 with the line endings they had. Files larger than 10 MB and files that are not text are not opened.

**Files that are not UTF-8:** When opening a file in another encoding (UTF-16, or the Windows-1252/ISO-8859-1 used by older Western European text files), Marbles asks whether to convert it to UTF-8 and replace the file, or to save the converted text as a copy named with "-utf8" and leave the file as it is. Nothing is written before you save in the editor. Marbles cannot tell which 8-bit encoding a file uses: if the text looks wrong, close the editor without saving.

**Data files:** Opening a Marbles data file (.mbls) closes the browser and selects the file on the login page. A data file outside the data directory can be copied to it first.

### Managing files
These work on the selected file or directory. Duplicate, copy, move, favorite and delete also work on several at once (see marking below):

- `A` or `:mkdir name` Create a new directory
- `X` or `:rename name` Rename. Without a name you are asked for it, starting from the current name
- `Y` or `:duplicate` Make a copy in the same directory (named `name-copy`)
- `C` or `:copy` Copy to another directory
- `M` or `:move` Move to another directory
- `D` or `:delete` Delete. You are asked first, and for a directory you are told how many files and directories it contains
- `Space` Mark (or unmark) the selected file or directory and go to the next. A marked one has a `+` in front of its name, and the number marked is shown at the bottom right of the pane. While some are marked, `Y` `C` `M` `P` and `D` work on all the marked ones instead of the selected one: they are duplicated, copied or moved to the directory you select, made favorites (or, if they all are favorites already, removed from the favorites), or deleted after one question that tells how many files and directories are deleted in all. The marks stay while the pane shows the same directory (also with a filter), and `Esc` clears them. If one of them fails, the operation stops there and the message tells which one; those not done stay marked. `X` (rename) and `I` (info) always work on the selected one

Names (of a new file or directory, a new name for one, and the name of a favorites folder) are typed in a box on top of the list: `Enter` accepts, `Esc` cancels. Questions (delete, converting a file that is not UTF-8, copying a data file to the data directory, and removing favorites) are asked in a box too: `Y` or `N` answers at once, or select with `j` `k` and press `Enter`. `Esc` cancels. When something cannot be undone, the question is highlighted as a warning and _No_ is selected first.

A new name (of a file, a directory or a data file) follows the same rules on all platforms, so files can be copied between Windows, macOS and Linux: it cannot contain `\ / : * ? " < > |` or control characters, cannot end with a dot or a space, can be at most 255 characters long, and cannot be a name Windows keeps for devices (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`, also with an extension, as `con.txt`). If a name cannot be used, the message tells why, or that a file or directory with that name already exists.

For copy and move you select where to: after `C` or `M` the browser stays as it is, with the name of the file (or the number marked) at the top left, in the attention color (as the name of the pane with the focus). Go to the directory, in either pane, and press `S` to copy or move the file to the directory shown in the pane with the focus. `Esc` cancels. With two panes the focus goes to the other pane, so pressing `S` at once copies or moves to it, and `Tab` takes you back if the place is in the first pane. With one pane you go to the directory in that pane. While you are selecting, the other file keys and commands are not in use (`:cd`, `:home` and `:data` are). Both panes are updated afterwards, and the focus stays in the pane copied or moved to, with the (last) file copied or moved selected. While a directory is being copied the name of the current file is shown, and `Esc` cancels the copy (what was copied so far is removed).

Things to know:

- **Deleted files cannot be recovered.** They are not moved to a recycle bin.
- Nothing is overwritten. An operation is refused if something with the same name is already in the target directory.
- A directory cannot be copied or moved into itself.
- What Marbles is using cannot be renamed, moved or deleted here: the data directory (and the directories it is in), `settings.ini`, the favorites list, the file history, and the data file you are logged in to.
- Hidden files are not shown in the browser, but they are included when a directory is copied, moved or deleted.

### Searching for files
`/text` only narrows the list of the directory shown. To find files in the directories below it as well, or by the text in them, use the search:

- `S` or `:find` Open the search box. It searches the directory of the pane with the focus

In the box:

- **Name**: Part of the name of the file. With `*` (any characters) and `?` (one character) the whole name must fit, as in `*.md`. Leave it empty to search all files for a text
- **Text in files**: Find the text files that contain a text. Files that are not text, files larger than 10 MB and Marbles data files (they are encrypted) are not searched
- **Regular expression**: The name and the text are regular expressions (`^(notes|plan)\.`, `\d{4}-\d\d`)
- **Match case**: Off, so `cat` also finds `Cat`
- **Where**: The directory and its subfolders, or the directory only
- **Search**: Press `Enter` here to search

The box opens in the _Name_ field: type and press `Enter`. `j` `k` select a row, `Enter` or `Space` edit a field or change an option, and `Esc` closes the box. What you entered is remembered.

While it searches, the bottom line shows the directory being read and the number of files found. `Esc` stops the search and shows what was found so far. The search also stops at 5000 files. Hidden files and directories are not searched, and links to directories are not followed.

The files found are shown in a list, with the directory they are in. When you searched for a text, the first line containing it is shown with its line number.

- `j` `k` Select, `gg` `G` First/Last
- `l` or `Enter` Open the file in the editor, at the line that was found. Closing the editor returns to the list
- `B` or `:goto` Show the file in the browser: its directory is opened in the pane with the focus, with the file selected
- `I` or `:info` Show information about the file
- `P` or `:fav` Add the file to the favorites, or remove it
- `/text` Only show the files with "text" in the path or the line found. `Esc` shows all again
- `S` or `:find` Search again
- `Esc`, `Q`, `h` or `-` Return to the browser

### Favorites
Files and directories you use often can be marked as favorites:

- `P` or `:fav` Add the selected file or directory to the favorites, or remove it. Favorites are marked with a `*`
- `F` or `:favorites` Show the list of favorites

In the list of favorites:

- `j` `k` Select, `gg` `G` First/Last
- `l` or `Enter` Open the file in the editor (closing the editor returns to the list). A directory: go to it in the browser
- `B` or `:goto` Show the file in the browser: its directory is opened with the file selected, where it can be renamed, copied, moved or deleted
- `Tab` or `v` Move the selected favorite or folder: it is highlighted and follows `j` `k` up and down the list until you press `Esc`
- `C` or `:color` Pick a text color for the favorite from the list of 16 colors
- `P`, `D` or `:remove` Remove it from the favorites (the file itself stays where it is)
- `u` or `:undo` Put back the favorite, or the folder with its favorites, that was removed last
- `A` or `:folder name` Create a folder (see below)
- `/text` Only show the paths containing "text". `Esc` shows all again
- `?` Help
- `Esc`, `Q`, `F`, `h` or `-` Return to the browser

**Folders:** Favorites can be grouped in folders. A folder only exists in the list of favorites; nothing is created on disk and no files are moved.

- `A` or `:folder name` Create a folder in front of the selected item. It is shown as `[-] Name` when open and `[+] Name` when closed, with the number of favorites in it
- `l` or `Enter` on a folder: Open or close it. The favorites in an open folder are shown below it, two spaces in
- `:expand` Open all the folders, `:collapse` Close all the folders
- **Several at once:** Mark the favorites with `Space` (it marks or unmarks the selected favorite and goes to the next; a marked favorite has a `+` in front, and the number of marked is shown at the bottom right). Then press `M` and pick where they go from the list: one of your folders, _New folder..._ (you are asked for its name, and it is placed where the first of the favorites is) or _(No folder)_. Without marks, `M` moves the selected favorite. With marks, `D` removes all the marked favorites (you are asked first), and `u` puts them back. `Esc` clears the marks
- **To the top or the end:** `T` (or `:top`) moves the marked favorites to the top of the list, and `E` (or `:bottom`) to the end, in the order they have. Without marks it moves the selected favorite, or the selected folder with its favorites. A favorite in a folder goes to the top or the end of its folder (`M` moves it out of the folder)
- **Into a folder:** Move a favorite (`Tab` or `V`, then `j` `k`) onto the folder. Moving down onto a folder puts it first in the folder, moving up from below puts it last. A closed folder opens
- **Out of a folder:** Move the favorite on, past the first or the last favorite in the folder
- A folder is moved like a favorite. It is closed first, and moves with its favorites past the favorite or folder next to it
- `X` or `:rename name` Rename the folder
- `C` Pick a color for the folder
- `P`, `D` or `:remove` Remove the folder. If there are favorites in it you are asked first, as they are removed from the list with it (the files stay)

While the list is filtered with `/text` the favorites found are shown without the folders, and nothing can be moved.

The list is kept in the file `favorites.txt` in the data directory (one line per favorite: the color, a tab character and the path; a folder has its name in brackets instead of a path, and the favorites in it start with a blank space). A file that has been closed in the editor has the line and column of the cursor before its path (`3:12` and a tab character). Favorites follow when you rename or move a file or directory in the browser, and are removed when you delete it. A favorite whose file is gone is shown as _(missing)_.

### File history
The file history lists the files you have opened in the editor from the file browser (also from the favorites, the search and the history itself), the last opened first, with the time each was opened. A file opened again moves to the top. It is practical when you work on many files over several days.

- `O` or `:history` (in the file browser) Show the file history
- `j` `k` Select, `gg` `G` First/Last, `PgUp` `PgDn` One page up/down
- `l` or `Enter` Open the file in the editor (`:q` returns to the history)
- `B` or `:goto` Show the file in the file browser: its directory, with the file selected
- `P` or `:fav` Add the file to the favorites, or remove it. A favorite has a `*` after its name
- `I` or `:info` Show information about the file
- `D` or `:remove` Take the file out of the history (the file itself stays)
- `X` or `:clear` Clear the file history. You are asked first, in a box on top of the list
- `/text` Only show the files whose path contains "text". `Esc` shows all again
- `?` Help, `:` the command line
- `Esc`, `Q`, `O`, `h` or `-` Back to the file browser

The history holds the last 100 files: when it is full, the file opened longest ago is dropped. A file that is gone is shown as _(missing)_. The list is kept in the file `history.txt` in the data directory (one line per file: the time it was opened, a tab character and the path, with the line and column of the cursor and a tab character before the path once the file has been closed in the editor). Like the favorites, it is not encrypted.

### Where the cursor was
When a file is closed in the editor, the line and column of the cursor are kept: in the history, and in the favorites if the file is one. The next time the file is opened, the cursor is placed there, so you go on where you left off; entries of a data file work the same way. The favorites are looked in first, as the history holds only the last 100 files. If the file has changed since and the line or column is no longer there, the cursor goes to the end of the line or of the text. A file opened from the results of a [search](#searching-for-files) is opened at the line found instead.

## The log
The log lists what has happened since Marbles was started: the startup, logins, commands, and the file operations of the file browser (new, open, rename, copy, move and delete, with the reason if one failed). It is shown from the Configuration tab of the Select mode page (_View log_).

- The log opens at the end, with the latest lines
- `h` `l` Previous/Next page, `j` `k` One line, `gg` `G` First/Last
- `/text` Only show the lines containing "text", for example `/failed` or `/file:`. `Esc` shows all again
- `Esc` or `Q` Close the log

The log is kept in memory only: nothing is written to disk, and it is empty when Marbles is started. It holds the latest 1000 lines.

As the log can be read without a password, commands given in the menus and in the editor are logged by name only (`:new ...`, `:s ...`): titles, search texts and other text typed after a command are not in the log. Something typed on a command line that is not a command is logged as "(not a command)". File names and paths in the file browser are logged in full.

## Logo animation
The logo is drawn with a sweep when arriving at the login page. Any key ends the sweep. Its speed is set with _Logo animation_ on the Layout page of the Configuration tab, where it can also be turned off.

## Directories
Marbles creates the directory "data" in the directory it is started from, and keeps the data files and the settings file there.
Directories within the data directory must be created from the file system. Refresh the login page (`Esc`, `R`) to see new directories. Directories inside these directories are not shown.

## Data files from earlier versions
The first time you log in to a data file from Marbles v1.0.x it is converted to the current file format. Dates of entries that an early version wrote with the day first (`17-11-2021 12:21:46`) are turned around in the converted file (`2021-11-17 12:21:46`), so the entries sort by their dates. A copy of the original file is kept in the same directory, named as the data file with ".legacy" added. See the [list of changes](CHANGELOG.md).
