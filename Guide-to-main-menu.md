# Guide to main menu
Press the ```Left/Right``` arrow keys, ```Tab``` or ```Shift+Tab``` to navigate the 4 pages of the main menu. Use ```Home/End``` to navigate to the start (page 1) and end (page 2).

## Page 1

- ```N``` **New**: Create a new entry (write a title and enter your notes).
- ```S``` **Search**: Search entries in the current data file. Search results can be sorted (Press ```Tab```) by _date changed_, _date created_ and alphabetically by _title_.
- ```R``` **Reselect**: Select the last edited entry again (use Session history ```M``` to show last viewed)
- ```F``` **Favorites**: Show all entries added to the favorites list: Collect your favorite entries in the favorites list (sorted by Date changed). Pin entries to the top of the favorites list (Press ```P```) and arrange them in the order you like (Press ```Tab```). Colorize pinned entries to highlight them (Press ```CTRL+W, 1-3```).
- ```X``` **Logout**: Return to the login menu (see the [Guide to login menu](Guide-to-login-menu.md))

## Page 2
- ```T``` **Top 20**: Shows the 20 recent edited entries sorted by date changed.
- ```M``` **Session history**: Shows the entries viewed in the current session, sorted by first to last. A session starts when logging into a data file and ends when logging out. This list is practical when switching back and fourth between several entries in a session.
- ```L``` **List all**: Lists all entries with the option to sort by Created date, Changed date and Title (Press ```Tab```).
- ```I``` **File info**: Show current working directory, file name and file size etc.
- ```D``` **Display timestamp**: Shows a dialog with the current date/time and week number.
- ```H``` **Help**: Shows the contents of the [Guide to editor](Guide-to-editor.md).
- ```A``` **About**: Show general info about Marbles.


## Page 3
- Press ```CTRL+W``` +
  - ```Z``` **Undo deletion**: Undo the last deletion of an entry (in the current session).
  - ```J``` **Transfer**: Collect entries for transfer to another data file.
  - ```I``` **Import .txt**: Import a plain text document (must be titled "import.txt").
  - ```V``` **Text vignette**: Create a static text vignette to be shown on the main page in Marbles.
  - ```M``` **Change dimensions**: Set the row and column count for the Marbles terminal output.
  - ```P``` **Color preset**: Cycle the 6 color presets (see **Page 4** for customizing color schemes).
  - ```L``` **Logo preset**: Cycle the 8 logo presets (the 8th preset can be customized by editing the settings.init file from the login menu or via the file system)
  - ```K``` **Toggle Backup**: Save a copy of the data file in a different location.
  - ```W``` **Set shortcuts key**: Change the default compound shortcuts key.
  - ```E``` **Reset shortcuts key**: Revert the compound shortcuts key to it's default value "W".
  - ```R``` **Reset password**: Reset the password to the data file (it is not possible recover your notes if you forget your password).
  - ```X``` **Default settings**: Reset all settings to default (can also be done by deleting the _settings.ini_ file and restarting Marbles).
  - ```Y``` **Restart Marbles**: This will restart the application clearing all variables in memory and load the settings again.
  - ```Q``` **Exit**: Clear all variables from memory and exit Marbles.

## Page 4
- Press ```CTRL+W``` +
  - ```G``` to **Unlock color scheme**: Unlock the controls to customize the selected color scheme
  - ```Up/Down``` to select which color group to change (indicated by the selection cursor)
  - ```Left/Right``` to cycle available colors
  - ```T``` to **Toggle Text/Background**: Switch between foreground and background colors
  - ```E``` to **Reset**: Revert to default colors of the selected color scheme (selected on **Page 3**)
  - ```Enter``` to **Save and exit**: Save the selected color combination to the _settings.ini_ file (can be edited in plain text from the login menu (see the [Guide to login menu](Guide-to-login-menu.md))). 
  - ```Esc``` to **Cancel (exit)**: Cancel your changes and lock the color editing controls.
