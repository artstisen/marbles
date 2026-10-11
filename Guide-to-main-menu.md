# Guide to the main menu, lists and entries

The same keys are used everywhere in Marbles:

- `h` `l` (or `Left`/`Right`, `Tab`) Previous/Next menu page
- `j` `k` (or `Down`/`Up`) Select in a list, or scroll the text of an entry
- `gg` `G` First/Last (menu page, entry in a list, or top/end of a text)
- `/` Search
- `?` Help: the list of keys. The help is shown in pages (`h` `l` previous/next page, `j` `k` one line) and can be searched: `/text` goes to the first line containing "text", and `n` `N` to the next/previous. The help shows the keys of the page you are on (start page, login page, menus and lists, file browser, favorites or editor); press `?` in the help to see all keys, and `?` again to return to the keys of the page
- `:browse` Open the [file browser](Guide-to-login-menu.md#file-browser) to edit text files. Opening a file logs out of the data file
- `:` Command line: type a command and press `Enter`. The commands are shown in the menus
- `Tab` on the command line completes the command you are typing: `:und` and `Tab` gives `:undelete`. Press `Tab` again for the next command that starts the same (`Shift+Tab` for the previous); after the last one, what you typed is shown again. With nothing typed, `Tab` goes through all the commands that can be used on the page. Only the name of the command is completed, not what follows it (a title, a number), and the short forms (`:q`, `:h`) are not shown. This works on every command line: in the menus, lists, file browser and editor, each with its own commands
- `:q` Close/Return. In the main menu: Exit Marbles
- `:qa` Exit Marbles from anywhere
- `:logout` (or `X`) Close the data file and return to the [login page](Guide-to-login-menu.md)
- `Esc` Return/Cancel

## Main menu
The main menu has 2 pages, each with two boxes of keys and commands: four boxes in all, numbered 1 to 4 on the line below them, where the number of the box with the selection is highlighted. _MENU_ is shown at the top left, and the name of the data file at the top right, above a dividing line. The keys of the page are shown at the bottom.

- Each item can be used with its key or command, or selected and opened: `j` `k` (or `Down`/`Up`) select an item, and `Enter`, `l` (or `Right`) or `Space` opens the selected item. For the settings that are on or off (`[x]`), `Space` and `Enter` turn them on or off. The title of the box with the selection is highlighted
- `j` on the last item of a box goes on to the first item of the next box (on the next page after box 2, and back to box 1 after box 4). `k` on the first item goes back to the last item of the previous box
- `gg` (or `Home`) selects the first item of box 1, and `G` (or `End`) the last item of box 4
- `Tab` goes to the next box: from the two boxes of the first page to the two of the second, and back to the first. `Shift+Tab` goes the other way
- `Esc` shows a small box with three choices: **1 Logout**, **2 Select mode** (log out and go to the [Select mode](Guide-to-login-menu.md#select-mode) page) and **3 Exit** (close Marbles, without asking again). Select with `j` `k` (`gg` `G`: the first/last) or the number and press `Enter`, or `Esc` to close the box
- `h` (or `Left`) shows the previous page, and `1` and `2` select a page
- `?` shows the keys of the main menu (a section of the help of its own)

### Page 1: Notes and Data file
**Notes**
- `N` **New**: Create a new entry (write a title and enter your notes). With the command `:new My title` the entry is created with that title and opened in the editor; `:new` alone asks for the title. The command also works in the lists.
- `S` or `/` **Search**: Search the entries of the data file (the search text is typed in a box on top of the page, starting with the last search). All the words you type must be found in the title or text of an entry. The result can be sorted (press `Tab`) by _date changed_, _date created_ and alphabetically by _title_.
- `R` **Reselect**: Select the last edited entry again.
- `F` **Favorites**: Show the entries added to the favorites list.
- `T` **Top 20**: The 20 most recently edited entries.
- `M` **Session history**: The entries viewed since you logged in.
- `A` **List all**: All entries, sorted by date created, date changed or title (press `Tab`).
- `:undelete` **Undo deletion**: Undo the last deletion of an entry (since you logged in).

**Data file**
- `I` **File info**: Information about the data file in a box on top of the menu (the same box as `I` in the file browser): name, type, size, the number of entries, when it was created and last changed, the full path, and the backup directory. Any key closes it.
- `:import` **Import text file**: Import a text file as a new entry. The file is selected in the file browser, and the entry gets the name of the file as its title.
- `:transfer` **Transfer**: Collect entries for transfer to another data file, protected by a key. The questions, the key and the messages are shown in boxes on top of the page.
- `:backup` **Backup**: Turn backup on or off. When on, a copy of the data file is saved in another directory every time it is saved. The directory is selected in the file browser: go to it and press `S`.
- `:password` **Reset password**: Change the password of the data file. It is not possible to recover your notes if you forget your password.
- `:` **Command line**: Type a command. "COMMAND:" is shown on the bottom line while typing.
- `?` **Help**
- `X` **Logout**

### Page 2: Settings and Marbles
**Settings**
- `:scheme` **Edit color scheme**: Open the [color scheme editor](#color-scheme-editor)
- `:color` **Color preset**: Pick one of the color presets in a box on top of the menu (`j` `k`, `gg` `G` or the number, and `Enter`; `Esc` cancels). The last choice, _Edit the color scheme..._ (or `E`), opens the [color scheme editor](#color-scheme-editor). `:color 5` selects preset 5 at once
- `:bw` **Black and white**: Show everything in black and white, whatever the color scheme is. The color scheme is kept and is used again when black and white is turned off with the same command. While it is on, the terminal's own background is used
- `:language` **Language**: Pick the language in a box on top of the menu, or select one by its code: `:language en` (English), `da` (Danish), `de` (German), `nb` (Norwegian), `sv` (Swedish), `es` (Spanish), `fr` (French). The language is saved as `language` in the settings file, and can also be changed on the Configuration page
- `:number` **Line numbers**: Show or hide line numbers in the editor (the same as `:number` in the editor itself).
- `:size` **Size**: Set the number of rows and columns used by Marbles.
- `:size auto` **Follow the window**: Turn automatic dimensions on or off. When on, Marbles uses the size of the terminal window (leaving one row and one column unused), checked when the login page, a menu, a list or an entry is shown. A window resized while the editor is open is picked up when the editor is closed. Setting the dimensions yourself with `:size` turns automatic dimensions off. Turning automatic dimensions off goes back to the default size (80 x 23).

**Marbles**
- `:browse` **Browse files**: Open the file browser.
- `D` **Display timestamp**: The current date and time (also in compact form) and the week number, in a box on top of the page. Any key closes it.
- `:guide` **Quick guide**: A short introduction to Marbles. It is also found on the start page and in the options of the login page.
- `:about` **About**: General information about Marbles.
- `:defaults` **Default settings**: Reset all settings.
- `:restart` **Restart Marbles**
- `:logout` **Logout**
- `:q` **Exit**

### Color scheme editor
The colors of Marbles are edited on a page of their own, opened with `:scheme` in the main menu or from the Configuration page of the start page (select _Color scheme_ and press `Enter`). It shows the colors being edited: the preset at the top, the ten groups of colors below it, each with a sample and the names of its colors, and a small screen at the bottom that shows them all on the background. Nothing is used until you save.

- `j` `k` (or `Up`/`Down`, `gg` `G`) Select the preset or a group
- On the preset: `l` and `h` (or `Enter`) Start from the next/previous preset. The groups get the colors of the preset
- On a group: `l` (or `Enter`, `Space`, `Right`) Pick its color from a list of the 16 colors: select with `j` `k` and press `Enter` (or `l`), or `Esc` to cancel. For Highlight, Attention and Warning (shown inverted) this is the background color
- `T` Pick the text color of Highlight, Attention or Warning
- **Background**: the last group. `[Default]` (the default) shows the text on the terminal's own background, so a transparent or blurred terminal window stays that way. Pick a color to give Marbles a background of its own: the whole window is filled with it, and the terminal's background is used again when Marbles exits. The background cannot have the color of the text, the logo, dividers, favorites or titles. The color presets (`:color`) and `:defaults` use `[Default]`. The setting is `background` in the settings file (a color number, or `default`)
- A color shown in brackets in the list cannot be picked, as the text would not be visible on its background
- `E` Back to the colors of the preset
- `S` Save the colors and close the editor
- `Esc` (or `Q`) Close. If something was changed, you are asked in a box whether to save it (`Y`/`N`, or `j`/`k` and `Enter`)
- `?` Show the help of the editor

With _Black and white_ on, the colors can still be edited: they are used when black and white is turned off.

## Lists of entries
Search results, Top 20, Favorites, Session history and List all are shown as lists. The kind of list (_ALL_, _TOP 20_, _FAVORITES_, _SESSION HISTORY_, _SEARCH_) is shown at the top left, the most used keys at the bottom, and `?` shows all the keys of the lists.

- `j` `k` Select an entry, `PgUp` `PgDn` Move 3 entries
- `gg` `G` Select the first/last entry of the list (on the first/last page)
- `h` `l` Previous/Next page of the list, `Home` `End` the first/last page
- `Enter` or `Space` Open the selected entry in the [editor](Guide-to-editor.md). Closing the editor (`:q`) returns to the list, with the entry selected (to the main menu when the entry was opened from there, as with `R`)
- `1`-`9` and `0` Open entry 1-10 directly. `:12` opens entry 12
- `P` Add the selected entry to (or remove it from) the favorites, or pin it to the top of the favorites list
- `Tab` Change the sorting
- `Tab` or `V` In the favorites list: move the selected pinned entry. The entry is highlighted and follows `j` `k` up and down among the pinned entries (also to the next page) until you press `Esc` or `Enter`. The order is saved as you go
- `:title` Change the title of the selected entry, in a box on top of the list. The list is shown again afterwards
- `:delete` Delete the selected entry. You are asked once; a deleted entry can be brought back with `:undelete` in the main menu
- `I` or `:info` Info about the selected entry: when it was created and last changed, the number of words, characters and lines, and its syntax colors (set with `:syntax` in the editor), in a box on top of the list. Any key closes it
- `:duplicate` Make a copy of the selected entry (named "(copy)"), which is selected in the list
- `:revert` Revert the selected entry to the text before its last edit (since you logged in)
- `:export` Export the text of the selected entry as a text file. Select the directory in the file browser (go to it and press `S`), then enter the file name (the title of the entry is suggested). The exported file is not encrypted
- `:import` Import a text file as a new entry (selected in the file browser). It is opened in the editor
- `C` or `:color` Set the text color of a pinned entry (in the favorites list), as `C` in the favorites of the file browser. The 16 colors are shown in a list, as they look with your color scheme: select with `j` `k` and press `Enter` (or `l`), or `Esc` to cancel. _Default_ removes the color. `:color 13` sets color 13 of the list directly
- `:transfer` Add the selected entry to the transfer collection
- `/` or `S` Search, `A` List all, `T` Top 20, `F` Favorites, `M` Session history, `N` New entry
- `Esc`, `-` or `:q` Return to the main menu
