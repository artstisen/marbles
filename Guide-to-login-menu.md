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
- `h` `l` Select the previous/next data file, `j` `k` Select a directory. The options stay open
- `R` Refresh the login page
- `Y` Restart Marbles
- `G` Show the log
- `S` Edit the settings file _settings.ini_ (it can also be edited from the file system). Marbles restarts and loads the settings when the file is saved
- `Q` Exit Marbles
- `Esc` Close the options

## Directories
Marbles creates the directory "data" in the directory it is started from, and keeps the data files and the settings file there.
Directories within the data directory must be created from the file system. Refresh the login page (`Esc`, `R`) to see new directories. Directories inside these directories are not shown.

## Data files from earlier versions
The first time you log in to a data file from Marbles v1.0.x it is converted to the current file format. A copy of the original file is kept in the same directory, named as the data file with ".legacy" added. See the [list of changes](CHANGELOG.md).
