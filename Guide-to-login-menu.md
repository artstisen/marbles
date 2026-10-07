# Guide to the login page
Data files (your encrypted collections of notes) are managed from the login page shown when starting Marbles.
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
- `h` `l` Select the previous/next data file, `j` `k` Select a directory. The options stay open
- `R` Refresh the login page
- `Y` Restart Marbles
- `G` Show the log
- `S` Edit the settings file _settings.ini_ (it can also be edited from the file system). Marbles restarts and loads the settings when the file is saved
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
- `N` Create a new file in the directory and open it
- `R` Refresh
- `:cd path` Go to a directory. `:home` Go to the Marbles data directory
- `?` Help
- `Esc`, `Q` or `:q` Close the file browser

A text file is opened in the [editor](Guide-to-editor.md), where `:w` saves it and `:q` returns to the browser. Files are saved as UTF-8 with the line endings they had. Files larger than 10 MB and files that are not text are not opened.

**Files that are not UTF-8:** When opening a file in another encoding (UTF-16, or the Windows-1252/ISO-8859-1 used by older Western European text files), Marbles asks whether to convert it to UTF-8 and replace the file, or to save the converted text as a copy named with "-utf8" and leave the file as it is. Nothing is written before you save in the editor. Marbles cannot tell which 8-bit encoding a file uses: if the text looks wrong, close the editor without saving.

**Data files:** Opening a Marbles data file (.mbls) closes the browser and selects the file on the login page. A data file outside the data directory can be copied to it first.

## Logo animation
The logo is drawn with a sweep when arriving at the login page. Any key ends the sweep. To turn it off, set `logoanim=false` in the settings file (`Esc`, `S`).

## Directories
Marbles creates the directory "data" in the directory it is started from, and keeps the data files and the settings file there.
Directories within the data directory must be created from the file system. Refresh the login page (`Esc`, `R`) to see new directories. Directories inside these directories are not shown.

## Data files from earlier versions
The first time you log in to a data file from Marbles v1.0.x it is converted to the current file format. A copy of the original file is kept in the same directory, named as the data file with ".legacy" added. See the [list of changes](CHANGELOG.md).
