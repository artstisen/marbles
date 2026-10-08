# Guide to the start and login pages

## Select mode
Marbles can be used for encrypted notes and as an editor for ordinary text files. When it starts you select what to use it for:

- `1` **Note Management (encrypted notes)**: Continue to the login page
- `2` **Text Editor**: Open the [file browser](#file-browser) to edit plain text files. Closing the file browser returns here
- `3` **Configuration**: Select what Marbles does when it starts

Move the selection with `j`/`k` (or the arrow keys) or the number, and press `Enter`. `?` shows the help and `Q` exits Marbles.

### Configuration
- **Show choices on startup** (the default)
- **Start in Note Management**: Go directly to the login page
- **Start in Text Editor (file browser)**: Go directly to the file browser

Select with `j`/`k` or `1`-`3` and press `Enter` to set it. `Esc` returns. The choice is saved as `startmode` (`select`, `notes` or `editor`) in the settings file, and `:defaults` in the main menu sets it back to showing the choices.

When Marbles starts directly in one of the two, the choices can still be reached: from the login page with `Esc`, `T`, and from the file browser by closing it.

## Login page
Data files (your encrypted collections of notes) are managed from the login page.
A default data file "data.mbls" is created when first starting Marbles, where you are asked to create its password (8 to 128 characters). It is not possible to recover your notes if you forget your password.

Log in to the selected data file by typing its password and pressing `Enter`.

- `Left`/`Right` (or `Tab`) Select a data file in the current directory
- `Up`/`Down` Select a directory
- `Space` Move the selected data file to another directory: select the directory and press `Enter`
- `Esc` Clear the password field. When the field is empty: open the options

## Options
Press `Esc` and then one of the following keys:

- `I` Show information about the data file
- `N` Create a new data file
- `X` Rename the selected data file
- `C` Create a copy of the selected data file
- `D` Delete the selected data file
- `M` Merge two data files into a new, or export a data file as XML (the exported file is not encrypted)
- `F` Browse files: open the [file browser](#file-browser) to edit plain text files
- `T` Go to the [Select mode](#select-mode) page
- `h` `l` Select the previous/next data file, `j` `k` Select a directory. The options stay open
- `R` Refresh the login page
- `Y` Restart Marbles
- `G` Show the log
- `S` Edit the settings file _settings.ini_ (it can also be edited from the file system). Marbles restarts and loads the settings when the file is saved
- `?` Show the help (the list of keys)
- `Q` Exit Marbles
- `Esc` Close the options

## File browser
The file browser (`Esc`, `F`) is for editing single text files (.txt, .md, .html ...) with the Marbles editor, without logging in to a data file. **Text files are not encrypted.**

The browser starts in the Marbles data directory and remembers the directory you leave it in.

The file browser can also be opened with the command `:browse` when you are logged in to a data file. You can look around and close the browser again without anything else happening, but opening a file logs you out of the data file, and the login page is shown when you close the browser.

- `j` `k` Select, `gg` `G` First/Last, `PgUp` `PgDn` One page up/down
- `l` or `Enter` Open the selected directory or file
- `h`, `-` or `Backspace` Go to the directory above (above the root of a drive on Windows: the list of drives)
- `/text` Only show the names containing "text". `Esc` shows all again
- `N` or `:new name` Create a new file in the directory and open it. Without a name (`N` or `:new`) you are asked for it
- `I` or `:info` Show information about the selected file or directory: type, size (for a directory: the number of files and directories in it), when it was created and last changed, and the full path. Any key closes the box
- See [Managing files](#managing-files) and [Favorites](#favorites) below for the other keys
- `R` Refresh
- `:cd path` Go to a directory. `:home` Go to the Marbles data directory
- `?` Help
- `Esc`, `Q` or `:q` Close the file browser

A text file is opened in the [editor](Guide-to-editor.md), where `:w` saves it and `:q` returns to the browser. Files are saved as UTF-8 with the line endings they had. Files larger than 10 MB and files that are not text are not opened.

**Files that are not UTF-8:** When opening a file in another encoding (UTF-16, or the Windows-1252/ISO-8859-1 used by older Western European text files), Marbles asks whether to convert it to UTF-8 and replace the file, or to save the converted text as a copy named with "-utf8" and leave the file as it is. Nothing is written before you save in the editor. Marbles cannot tell which 8-bit encoding a file uses: if the text looks wrong, close the editor without saving.

**Data files:** Opening a Marbles data file (.mbls) closes the browser and selects the file on the login page. A data file outside the data directory can be copied to it first.

### Managing files
These work on the selected file or directory (one at a time):

- `A` or `:mkdir name` Create a new directory
- `X` or `:rename name` Rename. Without a name you are asked for it, starting from the current name
- `Y` or `:duplicate` Make a copy in the same directory (named `name-copy`)
- `C` or `:copy` Copy to another directory
- `M` or `:move` Move to another directory
- `D` or `:delete` Delete. You are asked first, and for a directory you are told how many files and directories it contains

For copy and move the browser lets you select the target: go to the directory and press `S` (`Esc` cancels). While a directory is being copied the name of the current file is shown, and `Esc` cancels the copy (what was copied so far is removed).

Things to know:

- **Deleted files cannot be recovered.** They are not moved to a recycle bin.
- Nothing is overwritten. An operation is refused if something with the same name is already in the target directory.
- A directory cannot be copied or moved into itself.
- What Marbles is using cannot be renamed, moved or deleted here: the data directory (and the directories it is in), `settings.ini`, the favorites list, and the data file you are logged in to.
- Hidden files are not shown in the browser, but they are included when a directory is copied, moved or deleted.

### Favorites
Files and directories you use often can be marked as favorites:

- `P` or `:fav` Add the selected file or directory to the favorites, or remove it. Favorites are marked with a `*`
- `F` or `:favorites` Show the list of favorites

In the list of favorites:

- `j` `k` Select, `gg` `G` First/Last
- `l` or `Enter` Open the file in the editor (closing the editor returns to the list). A directory: go to it in the browser
- `B` or `:goto` Show the file in the browser: its directory is opened with the file selected, where it can be renamed, copied, moved or deleted
- `Tab` or `V` Move the selected favorite: it is highlighted and follows `j` `k` up and down the list until you press `Esc`
- `C` or `:color` Pick a text color for the favorite from the list of 16 colors
- `P`, `D` or `:remove` Remove it from the favorites (the file itself stays where it is)
- `/text` Only show the paths containing "text". `Esc` shows all again
- `?` Help
- `Esc`, `Q`, `F` or `h` Return to the browser

The list is kept in the file `favorites.txt` in the data directory (one line per favorite: the color, a tab character and the path). Favorites follow when you rename or move a file or directory in the browser, and are removed when you delete it. A favorite whose file is gone is shown as _(missing)_.

## Logo animation
The logo is drawn with a sweep when arriving at the login page. Any key ends the sweep. To turn it off, set `logoanim=false` in the settings file (`Esc`, `S`).

## Directories
Marbles creates the directory "data" in the directory it is started from, and keeps the data files and the settings file there.
Directories within the data directory must be created from the file system. Refresh the login page (`Esc`, `R`) to see new directories. Directories inside these directories are not shown.

## Data files from earlier versions
The first time you log in to a data file from Marbles v1.0.x it is converted to the current file format. A copy of the original file is kept in the same directory, named as the data file with ".legacy" added. See the [list of changes](CHANGELOG.md).
