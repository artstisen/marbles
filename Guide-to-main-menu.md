# Guide to the main menu, lists and entries

The same keys are used everywhere in Marbles:

- `h` `l` (or `Left`/`Right`, `Tab`) Previous/Next menu page
- `j` `k` (or `Down`/`Up`) Select in a list, or scroll the text of an entry
- `gg` `G` First/Last (menu page, entry in a list, or top/end of a text)
- `/` Search
- `?` Help: the list of keys. The help is shown in pages (`h` `l` previous/next page, `j` `k` one line) and can be searched: `/text` goes to the first line containing "text", and `n` `N` to the next/previous. The help shows the keys of the page you are on (start page, login page, menus and lists, file browser, favorites or editor); press `?` in the help to see all keys, and `?` again to return to the keys of the page
- `:browse` Open the [file browser](Guide-to-login-menu.md#file-browser) to edit text files. Opening a file logs out of the data file
- `:` Command line: type a command and press `Enter`. The commands are shown in the menus
- `:q` Close/Return. In the main menu: Exit Marbles
- `:qa` Exit Marbles from anywhere
- `:logout` (or `X`) Close the data file and return to the [login page](Guide-to-login-menu.md)
- `Esc` Return/Cancel

## Main menu
The main menu has 4 pages. Besides `h`/`l`, the pages can be selected with `1`-`4`, and `Home`/`End` go to the first/last page.

### Page 1
- `:guide` **Quick guide**: A short introduction to Marbles. It is also found on the start page and in the options of the login page.
- `N` **New**: Create a new entry (write a title and enter your notes). With the command `:new My title` the entry is created with that title and opened in the editor; `:new` alone asks for the title. The command also works in lists and when viewing an entry.
- `S` or `/` **Search**: Search the entries of the data file. All the words you type must be found in the title or text of an entry. The result can be sorted (press `Tab`) by _date changed_, _date created_ and alphabetically by _title_.
- `R` **Reselect**: Select the last edited entry again.
- `F` **Favorites**: Show the entries added to the favorites list.
- `P` **Preview**: Turn the preview on or off. The preview lists the 5 last edited entries below the menu: select one with `j` `k` and open it with `Enter`. The preview is off by default.
- `:` **Command line**: Type a command. "COMMAND:" is shown on the bottom line while typing.
- `X` **Logout**

### Page 2
- `T` **Top 20**: The 20 most recently edited entries.
- `M` **Session history**: The entries viewed since you logged in.
- `A` **List all**: All entries, sorted by date created, date changed or title (press `Tab`).
- `I` **File info**: The directory, name and size of the data file.
- `:browse` **Browse files**: Open the file browser.
- `:number` **Line numbers in the editor**: Show or hide line numbers in the editor (the same as `:number` in the editor itself).
- `D` **Display timestamp**: The current date, time and week number.
- `?` **Help**
- `:about` **About**: General information about Marbles.

### Page 3 (commands)
- `:undelete` **Undo deletion**: Undo the last deletion of an entry (since you logged in).
- `:transfer` **Transfer**: Collect entries for transfer to another data file, protected by a key.
- `:import` **Import text file**: Import a text file as a new entry. The file is selected in the file browser, and the entry gets the name of the file as its title.
- `:vignette` **Text vignette**: Create a text to be shown on the main menu.
- `:size` **Change dimensions**: Set the number of rows and columns used by Marbles.
- `:size auto` **Follow the size of the window**: Turn automatic dimensions on or off. When on, Marbles uses the size of the terminal window (leaving one row and one column unused), checked when the login page, a menu, a list or an entry is shown. A window resized while the editor is open is picked up when the editor is closed. Setting the dimensions yourself with `:size` turns automatic dimensions off.
- `:color` **Color preset**: Use the next of the color presets, or select one by number: `:color 5` (see page 4 for making your own).
- `:logo` **Logo preset**: Use the next of the logo presets, or select one by number: `:logo 3` (the last preset shows a text of your own, set as `logotxt` in the settings file).
- `:backup` **Toggle backup**: Save a copy of the data file in another directory every time it is saved. The directory is selected in the file browser: go to it and press `S`.
- `:password` **Reset password**: Change the password of the data file. It is not possible to recover your notes if you forget your password.
- `:defaults` **Default settings**: Reset all settings.
- `:restart` **Restart Marbles**
- `:logout` **Logout**
- `:q` **Exit**

### Page 4 (color scheme)
- `:scheme` **Unlock color scheme**: Edit the colors of the selected color preset
  - `j` `k` (or `Up`/`Down`) Select the color group to change
  - `l` (or `Right`, `Space`) Pick the color from a list of the 16 colors: select with `j` `k` and press `Enter` (or `l`), or `Esc` to cancel. For the last three groups (the highlight styles) this is the background color
  - `T` Pick the text color of a highlight style (the last three groups)
  - A color shown in brackets in the list cannot be picked, as the text would not be visible on its background
  - `E` Reset to the default colors of the preset
- `:bw` **Black and white**: Show everything in black and white, whatever the color scheme is. The color scheme is kept and is used again when black and white is turned off with the same command. While it is on, the color scheme cannot be edited
  - `Enter` Save and exit
  - `Esc` Cancel

## Lists of entries
Search results, Top 20, Favorites, Session history and List all are shown as lists.

- `j` `k` Select an entry, `gg` `G` Select the first/last entry on the page, `PgUp` `PgDn` Move 3 entries
- `h` `l` Previous/Next page of the list
- `Enter` or `Space` Open the selected entry
- `1`-`9` and `0` Open entry 1-10 directly. `:12` opens entry 12
- `P` Add the selected entry to (or remove it from) the favorites, or pin it to the top of the favorites list
- `Tab` Change the sorting
- `Tab` or `V` In the favorites list: move the selected pinned entry. The entry is highlighted and follows `j` `k` up and down among the pinned entries (also to the next page) until you press `Esc` or `Enter`. The order is saved as you go
- `:title` Change the title of the selected entry
- `:delete` Delete the selected entry
- `:pin` Set the text color of a pinned entry (in the favorites list). The 16 colors are shown in a list, as they look with your color scheme: select with `j` `k` and press `Enter` (or `l`), or `Esc` to cancel. _Default_ removes the color. `:pin 13` sets color 13 of the list directly
- `:transfer` Add the selected entry to the transfer collection
- `/` or `S` Search, `A` List all, `T` Top 20, `F` Favorites, `M` Session history, `N` New entry
- `Esc` or `:q` Return to the main menu

## Viewing an entry
Opening an entry shows its text with a menu of 3 pages.

- `Enter` or `I` Edit the text (see the [guide to the editor](Guide-to-editor.md))
- `j` `k` Scroll the text one row, `Space` and `b` (or `PgDn`/`PgUp`) one page, `gg` `G` to the top/end
- `T` Change the title
- `F` Add to/remove from the favorites
- `D` Duplicate the entry
- `S` or `/` Open the editor and search the text
- `N` or `:new` New entry (`:new My title` to give the title at once)
- `:delete` Delete the entry. You are asked once; a deleted entry can be brought back with `:undelete` in the main menu
- `:revert` Revert to the text before the last edit (since you logged in)
- `:transfer` Add the entry to the transfer collection
- `:import` Import a text file as a new entry (selected in the file browser)
- `:export` Export the text of the entry as a text file. Select the directory in the file browser (go to it and press `S`), then enter the file name (the title of the entry is suggested). The exported file is not encrypted
- `Esc` or `:q` Return to the list
