# Guide to the main menu, lists and entries

The same keys are used everywhere in Marbles:

- `h` (or `Left`) and `Tab` Previous/Next tab of the main menu
- `j` `k` (or `Down`/`Up`) Select in a list, or scroll the text of an entry
- `gg` `G` First/Last (menu page, entry in a list, or top/end of a text)
- `/` Search
- `?` Help: the list of keys. The help is shown in pages (`h` `l` previous/next page, `j` `k` one line) and can be searched: `/text` goes to the first line containing "text", and `n` `N` to the next/previous. The help shows the keys of the page you are on (start page, login page, menus and lists, file browser, favorites or editor); press `?` in the help to see all keys, and `?` again to return to the keys of the page
- `:browse` Open the [file browser](Guide-to-login-menu.md#file-browser) to edit text files. Opening a file logs out of the data file
- `Ctrl+O` or `:jump` (`:ju`, `:jmp`) Open the [Jump menu](#jump-menu), to go straight to a file, a page of Marbles or a data file
- `:` Command line: type a command and press `Enter`. The commands are shown in the menus
- `Tab` on the command line completes the command you are typing: `:und` and `Tab` gives `:undelete`. Press `Tab` again for the next command that starts the same (`Shift+Tab` for the previous); after the last one, what you typed is shown again. With nothing typed, `Tab` goes through all the commands that can be used on the page. Only the name of the command is completed, not what follows it (a title, a number), and the short forms (`:q`, `:h`) are not shown. This works on every command line: in the menus, lists, file browser and editor, each with its own commands
- `Up` and `Down` on the command line bring back the commands given before on the same page (each page has its own, the newest first, at most 20). With the start of a command typed, only the commands that start with it are shown (`:co` and `Up` finds `:copy`, not `:delete`); `Down` past the newest gives back what you typed. The commands are kept in memory only, never written to a file, and are forgotten when you log out (or close Marbles)
- `:q` Close/Return. In the main menu: Exit Marbles
- `:qa` Exit Marbles from anywhere
- `:logout` (or `X`) Close the data file and return to the [login page](Guide-to-login-menu.md)
- `Esc` Return/Cancel

The notes are saved in the data file after each change. When the same data file is open in two windows (or programs) and the other one has saved it since you logged in, you are asked before saving: **Save anyway** replaces the changes made there; **Do not save** keeps them (log out and in again to see them; the change made here is not saved).

## Jump menu
The Jump menu takes you straight to a place in Marbles. It is opened with `Ctrl+O`, or the command `:jump` (`:ju`, `:jmp`), from the main menu, the lists of entries, the file browser (with its favorites, history and search results) and the editor, and from the options of the login page (`Esc`, `O`), and it is a choice of the Mode tab of the start page (`3`, or `Ctrl+O` there too). It is a box on top of the page with five tabs, one of them with sub-tabs:

- **Bookmarks**: the places you have kept as bookmarks (see [Bookmarks](#bookmarks) below)
- **Recent**: the places you have jumped to with the menu, the latest first, with the time (today) or the date of the jump. Only jumps made in the menu are kept, not files and pages opened in other ways (the tab says so next to the filter)
- **File browser**, with the lists of the [file browser](Guide-to-login-menu.md#file-browser) as sub-tabs:
  - **History**: the files opened from the file browser, with when they were opened
  - **Favorites**: the files and directories of the favorites of the file browser (not the favorite notes of a data file). A favorite with a color of its own is shown in it, on every tab
  - **Hotlist**: the directories of the [directory hotlist](Guide-to-login-menu.md#directory-hotlist), in its order and with the names they are shown with there
- **Marbles**: the pages of Marbles: the Configuration tab (General, Layout, Editor, System), Color schemes, Logos, Clipboard, Snippet editor, Completions editor, Help and About
- **Data files**: the data files in the data directory and the directories in it

In a language where the names of the tabs do not fit in a row, _File browser_ and _Data files_ are shown shorter (as _Files_ and _Data_).

Only names are shown, not paths: a long name is shortened with `...` in the middle, so its start and its extension are seen. The columns are named above the list (below the filter): **Name**, **Type** (the kind of place: `FILE`, `DIR`, `PAGE` or `DATA`) and, on the Bookmarks tab, **Command** (the command that jumps to the bookmark), or **When** on Recent and History. A place that is gone (a file deleted or moved) is shown dimmed with _(missing)_.

- `j` `k` (or `Down`/`Up`, `gg` `G`, `PgUp` `PgDn`) Select a place
- `h` `l` (or `Left`/`Right`) The list before/after: through all the lists in a row, the sub-tabs of the file browser too
- `Tab` `Shift+Tab` The next/previous tab (on the file browser: its first list)
- `/` Filter the places of the tab: only the ones with the text in their names are shown. `Esc` shows all again
- `Enter` Jump to the selected place
- `B` Bookmark the selected place (on any list)
- `I` Information about the place: its name, kind and full path, whether it is there, and when it was jumped to or opened
- `D` Take the place out of the jump history (on the Jumps tab)
- `:` A command (see the Bookmarks tab below)
- `:path` Copy the full path of the selected file, directory or data file, and `:name` its name (the name of the file or directory itself, also when a bookmark or a directory of the hotlist is shown with a name of its own), as in the file browser: to the system clipboard and/or the paste bin, as set, and to the string memory (so `p` in the editor puts it in). On every tab; a page of Marbles and a folder of bookmarks have neither
- `Esc` (or `Q`, `Ctrl+O`) Close the menu; `?` shows its keys. The menu opens on the tab it was on the last time (while Marbles runs), so jumping between files from the Files or Favorites tab goes on there

### Bookmarks
Bookmarks are places you keep to jump to: a file, a directory, a page of Marbles or a data file. `B` on any list of the Jump menu adds the selected place, and `:bookmark` in the editor adds the file being edited (in the file browser: the selected file or directory, on `..` the directory shown). A note of a data file is not bookmarked by itself: bookmark its data file. A place is bookmarked once, and at the end of the list. On the Bookmarks tab:

- `Enter` Jump to the bookmark (on a folder: open or close it)
- `V` Move the selected bookmark or folder: it is marked `<>` and highlighted, `j` `k` move it, and `Esc` (or `Enter`, `V`) ends the moving. Not while a filter shows only some of them
- `T` `E` (or `:top`, `:bottom`) Move it to the top/end (a bookmark in a folder: of the folder)
- `X` (or `:rename name`) The name the bookmark is shown with, such as _Budget_ for `…/2026/oekonomi/budget-final-v3.md` (the place keeps its own name; an empty name shows it again). On a folder: its name
- `C` (or `:color`) Pick a text color for the bookmark or folder from the list of 16 colors. The color of a bookmark is used before the color the place has as a favorite of the file browser
- `D` (or `:remove`) Remove the bookmark (the place itself stays), and `u` (or `:undo`) puts it back
- `:clearbook` Clear the bookmarks (you are asked first)

**Folders:** Bookmarks can be grouped in folders, as the favorites of the file browser, with the same keys. A folder only exists in the list.

- `A` or `:folder name` Create a folder in front of the selected bookmark. It is shown as `[-] Name (3)` when open and `[+] Name (3)` when closed, with the number of bookmarks in it; the bookmarks of an open folder are shown below it, two spaces in
- `Enter` on a folder: Open or close it. `:expand` opens all the folders, `:collapse` closes them
- **Into and out of a folder:** move a bookmark (`V`, then `j` `k`) onto a folder to put it in, and on past the first or last bookmark of the folder to take it out. A folder moves with its bookmarks
- **Several at once:** mark bookmarks with `Space` (a marked bookmark has a `+` in front); `M` (or `:move`) moves the marked bookmarks (or the selected one) to a folder picked from a list (or a new folder, or out of the folders), `T` `E` to the top or the end, and `D` removes them. `Esc` takes the marks away
- `D` on a folder: Remove it with its bookmarks (you are asked first if there are bookmarks in it); `u` puts it back

While the list is filtered with `/text`, the bookmarks found are shown without the folders, and nothing can be moved.

**Jumping with a command:** each bookmark has a number, shown at its right on the Bookmarks tab (`:book 1`, `:book 2` ...): it is its place among the bookmarks of the list (the folders are not counted, and the bookmarks in a closed folder are), so moving a bookmark changes its number, and opening or closing a folder does not. `:book 3` jumps to bookmark 3 from anywhere the Jump menu can be opened with `:jump`: the start page, the main menu and the lists of a data file, the file browser (with its favorites, history, hotlist and search results) and the editor (which asks about changes that are not saved first, as a jump does). `:book` alone opens the Jump menu on the Bookmarks tab. A number that is not a bookmark is told about in a box, with the numbers there are.

The bookmarks are kept in `bookmarks.txt` in the data directory, one on each line: the kind (`file`, `dir`, `page` or `data`), a tab character, the place, and after other tab characters the name you gave it and its color (if any), so the file can also be edited by hand. A folder has its name in brackets (`[Work]`), its color and `collapsed` if it is closed, and the bookmarks in it are the lines after it that start with a blank space. At most 100 bookmarks (the folders are not counted). A file or directory that is renamed or moved in the file browser (or renamed with `:rename` in the editor) keeps its bookmark, and one that is gone is shown dimmed with _(missing)_.

Where a jump goes:

- **A file** is opened in the editor (where the cursor was the last time), with the file browser in its directory behind it. Closing the browser shows the Select mode page
- **A directory** is shown in the file browser
- **A page of Configuration** (on the Pages tab) opens the Select mode page on that page
- **A data file** is selected on the login page
- **Color schemes, Logos, Clipboard, Snippet editor, Completions editor, Help and About** open on top of the page you jumped from; closing them returns there

When you jump from the editor and the text has changes that are not saved, you are asked whether to save them first (`Esc` stays in the editor). A jump to a file, directory, Configuration page or data file from a data file you are logged in to logs you out of it first. With **Stay logged into data file (Session)** on (Configuration, General) you stay logged in: only a jump to another data file logs you out, and the main menu of the data file comes back when you close the file browser or the Configuration pages. With _Text Editor only_ the Data tab is not shown, and with _Note Management only_ the Files, Favorites and Hotlist tabs are not.

The jumps are kept in the file `jump.txt` in the data directory, one line for each place (at most 100): the time, a tab character, the kind (`file`, `dir`, `page` or `data`), a tab character and the place (the path of a file or directory, the name of a page such as `config/editor`, or the path of a data file in the data directory such as `notes/work.mbls`). Like the file history it is not encrypted, and it holds no titles or text of notes.

## Main menu
The main menu is a frame with four tabs: **Notes**, **Data file**, **Settings** and **Marbles**, each with its keys and commands. The tab shown is highlighted (in the colors of the tabs, set in the [color schemes](#color-schemes)). _DATA FILE MENU_ is shown at the top left, and the name of the data file at the top right, above a dividing line. The keys of the page are shown at the bottom.

- Each item can be used with its key or command, or selected and opened: `j` `k` (or `Down`/`Up`) select an item, and `Enter`, `l` (or `Right`) or `Space` opens the selected item. For the settings that are on or off (`[x]`), `Space` and `Enter` turn them on or off. The keys and commands of all the tabs can be used whichever tab is shown
- `j` on the last item of a tab goes on to the first item of the next tab (and back to Notes after Marbles). `k` on the first item goes back to the last item of the previous tab
- `gg` (or `Home`) selects the first item of Notes, and `G` (or `End`) the last item of Marbles
- `Tab` goes to the next tab, and `Shift+Tab` the other way. After a command, the tab it is on is shown
- `Esc` shows the **Options** of the data file, a small box with five choices: **1 `O` Jump menu** (the [Jump menu](#jump-menu)), **2 `F` File browser** (as `:browse`), **3 `S` Configuration** (the Configuration tab of the start page, as `:config`: it logs out, unless you stay logged into the data file), **4 `X` Logout** and **5 `Q` Exit** (close Marbles, without asking again). The key of a choice opens it at once (the same keys as on the start page and in the options of the login page, in all languages). Or select with `j` `k` (`gg` `G`: the first/last) or the number and press `Enter`. `Esc` closes the box
- `h` (or `Left`) shows the previous tab, and `1` to `4` select a tab
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
- `:transfer` **Transfer**: Collect entries for transfer to another data file, protected by a key. The questions, the key and the messages are shown in boxes on top of the page. The collection stays after it is completed, so it can be completed in more data files; an entry completed into a data file where an entry with its ID is already (the file it came from) gets an ID of its own.
- `:backup` **Backup**: Turn backup on or off. When on, a copy of the data file is saved in another directory every time it is saved. The directory is selected in the file browser: go to it and press `S`. It must be outside the data directory, so a backup is never written on top of a data file. A data file in a subfolder of the data directory is backed up in the same subfolder of the backup directory, so data files with the same name in different subfolders have backups of their own. When the backup cannot be written (a drive that is not plugged in), you are told so once, and the backup stays on: it is written again at the first save after the directory is back.
- `:password` **Reset password**: Change the password of the data file. It is not possible to recover your notes if you forget your password.
- `:` **Command line**: Type a command. "COMMAND:" is shown on the bottom line while typing.
- `?` **Help**
- `X` **Logout**

### Page 2: Settings and Marbles
**Settings**
- `:scheme` **Color schemes**: Select a color scheme, edit the colors and save them as a scheme of your own, on [a page of their own](#color-schemes). The item shows the scheme in use. `:scheme Name` uses the scheme with the name at once
- `:logo` **Logos**: Select the logo of the start and login pages, or draw one of your own, on [a page of their own](#logos). The item shows the logo in use. `:logo Name` uses the logo with the name at once
- `:bw` **Black and white**: Show everything in black and white, whatever the color scheme is. The color scheme is kept and is used again when black and white is turned off with the same command. While it is on, the terminal's own background is used
- `:language` **Language**: Pick the language in a box on top of the menu, or select one by its code: `:language en` (English), `da` (Danish), `de` (German), `nb` (Norwegian), `sv` (Swedish), `es` (Spanish), `fr` (French), `it` (Italian), `pl` (Polish), `nl` (Dutch), `fi` (Finnish), or the code of a language file you added (see [Language files](Guide-to-installation.md#language-files)). The language is saved as `language` in the settings file, and can also be changed on the Configuration page
- `:number` **Line numbers**: Show or hide line numbers in the editor (the same as `:number` in the editor itself).
- `:size` **Size**: Set the number of rows and columns used by Marbles.
- `:size auto` **Follow the window**: Turn automatic dimensions on or off. When on, Marbles uses the size of the terminal window (leaving one row and one column unused), checked when the login page, a menu, a list or an entry is shown. A window resized while the editor is open is picked up when the editor is closed. Setting the dimensions yourself with `:size` turns automatic dimensions off. Turning automatic dimensions off goes back to the default size (80 x 23).
- `:clipboard` **Clipboard**: The clipboard settings: yanked text sent to the system clipboard and/or the paste bin file (see [the guide to the editor](Guide-to-editor.md#system-clipboard-and-paste-bin))

**Marbles**
- `:browse` **Browse files**: Open the file browser.
- `D` **Display timestamp**: The current date and time (also in compact form) and the week number, in a box on top of the page. Any key closes it.
- `:guide` **Quick guide**: A short introduction to Marbles. It is also found on the start page and in the options of the login page.
- `:defaults` **Default settings**: Reset all settings.
- `:jump` **Jump menu**: Go straight to a file, a page of Marbles or a data file (see [Jump menu](#jump-menu)). It is also opened with `Ctrl+O`.
- `:config` **More configurations**: Go to the Configuration tab of the [Select mode](Guide-to-login-menu.md#select-mode) page, with the rest of the settings, the settings file, _Restart Marbles_ and the log. You are logged out of the data file, as with _Logout_. The Help tab of that page has the help and _About_
- `:logout` **Logout**
- `:q` **Exit**

`:about` (general information about Marbles) and `:restart` are not in the menu, but still work as commands.

### Color schemes
The colors of Marbles are chosen and edited on a page of their own, _COLOR SCHEMES_, opened with `:scheme` in the main menu (or its item on the Settings tab) or from the Configuration page of the start page (select _Color scheme_ and press `Enter`). It is a frame with three tabs, as the start page: **Schemes**, **Colors** (with the sub-tabs _Page 1_, _Page 2_ and _Page 3_) and **Preview**. `Tab` goes from tab to tab, and `Shift+Tab` the other way. `h` and `l` (or `Left`/`Right`) go through the pages of Colors.

**Schemes** lists the color schemes: the eight built-in ones (Default, Deep Blue, Marbles Quest, Tasty Bytes, Yellow Monochrome, Green Monochrome, Ice Warp and Gray Box), and below them the ones you have saved yourself. The one in use is marked `[x]`. A scheme can have a logo that goes with it: _Default_, _Deep Blue_ and _Gray Box_ have the logo _Marbles_, and _Marbles Quest_ and _Tasty Bytes_ the logo _Reflections_, which is used too when the scheme is selected (in a scheme file this is the line `uselogo=Reflections`, with the name of a logo; the logo can be changed afterwards as usual).

- `j` `k` (or `Up`/`Down`, `gg` `G`) Select a scheme
- `Enter` (or `Space`, `l`) Use the selected scheme: it is used at once, and its name is saved (`colorscheme` in the settings file). Its colors are then the ones edited on the Colors tab
- `Y` Duplicate the selected scheme (a built-in one too) as a scheme of your own: type a name for the copy and press `Enter` (the name with ` 2` is suggested). The copy is selected in the list, and the scheme in use stays as it is. A name that is taken, or that of a built-in scheme, cannot be used
- `R` Rename a scheme of your own: type the new name and press `Enter`. Its file is renamed, and if it is the scheme in use, the setting follows. The name of a built-in scheme or of another scheme of yours cannot be used, and the built-in schemes cannot be renamed
- `D` Delete a scheme of your own (you are asked first). The built-in schemes cannot be deleted
- `:scheme Name` in the main menu uses a scheme by its name at once

**Colors** shows the colors of the scheme being edited, each group with a sample and the names of its colors. _Page 1_ has Text, Logo and labels, Dividers, Favorites, Accent, Editor title, Line numbers, Current line number, Key hints, Unsaved label and Background; _Page 2_ has Highlight, Attention, Warning, Selector, Tab text, Tab frames, Tab corner, Box borders, Tab highlight, Tab focus and Other tabs, and _Highlight focused tabs_; _Page 3_ has the colors of colored text in the editor, Syntax 1 to 12.

- `j` `k` Select a group
- `Enter` (or `Space`) Pick its color from a list of the 16 colors: select with `j` `k` and press `Enter` (or `l`), or `Esc` to cancel. For Highlight, Attention, Warning and Tabs (shown inverted) this is the background color, and `T` picks the text color
- `[Default]` (for the colors that have it) is not a color of its own: the color follows the one it names, as _[Default]: the dividers_ for the tab frames, so changing the dividers changes them too. Pick a color to keep it apart. The built-in schemes have all such colors set, so changing one color changes only that one
- **Accent**: the keys in the Quick guide (it was called _Preview title_; a scheme file with `previewtitle` is still read). It is also the color Syntax 2 has from the start
- **Line numbers** and **Current line number**: the line numbers of the editor, and the number of the line the cursor is on. `[Default]` is the color of the dividers, and of the text
- **Selector**: the arrow `->` that shows the selection in lists, menus, settings and boxes. `Enter` picks its background and `T` the arrow itself. It has colors of its own (in the built-in schemes the same as Highlight), so it stays as it is when the colors of Highlight are changed. A scheme file without `selector` and `selectortext` gets the colors of its highlight
- **Syntax 1** to **Syntax 12** (Page 3): the colors of colored text in the editor (`:syntax`). They are shared by all the kinds of text that are colored (Markdown, HTML, CSS, JavaScript, JSON, XML, C#), and each is shown with what it is used for: 1 keywords, tags and selectors (and bold and list marks in Markdown), 2 names of attributes and properties (italic), 3 text in quotes and values (code), 4 numbers, 5 links, 6 comments (quotes, web addresses and rules), and 7 to 12 the headings 1 to 6 of Markdown. `[Default]` is the color each had before it could be set, shown after it: the colors of the logo, the accent, the favorites, the editor title and the dividers for 1, 2, 3, 5 and 6, and fixed colors for the numbers and the headings
- **Unsaved label**: the label _Unsaved_ shown at the top right of the editor when the text has changes that are not saved. `Enter` picks its background and `T` its text; `[Default]` is the text inverted
- **Key hints**: the keys on the bottom line of the pages (the key shown inverted, and what it does). `[Default]` is the color of the text
- **Tab text**: the text inside the frames with tabs (the start page and its Configuration, the main menu, the color schemes, the logos) and in the boxes shown on top of a page (messages, questions, lists to pick from). The names of the tabs and sub-tabs, the titles of the boxes and the highlighted text of a message or question keep their own colors. `[Default]` is the color of the text. In a scheme file it is `frametext` (`tabtext` is the text of _Tab highlight_)
- **Tab frames**: the frames with tabs (the start page, the main menu, this page). **Tab corner**: the left side, the corner and the top of the tab shown in such a frame (not the corner at its right end). In a lighter color than the frame it makes the tab stand out, as if light fell on it from the top left; `[Default]` is the color of the tab frames. **Box borders**: the borders and title tabs of the boxes shown on top of a page. `[Default]` (the default) is the color of the dividers
- **Tab highlight**: the tab and sub-tab shown (the focused tab), and the titles of the boxes, when they are highlighted. `[Default]` is the color of the tab frames as the background, with the text in the color of **Tab focus**, so the focused tab keeps its color also when it is highlighted (`T` gives the text a color of its own). In black and white the highlight is shown inverted. **Highlight focused tabs** (`Enter` or `Space` on it): off, the focused tab is not highlighted: its name has the color of **Tab focus**, and it is open into the frame
- **Tab focus**: the name of the focused tab and sub-tab (and the titles of the boxes) when the highlight is off. **Other tabs**: the names of the tabs and sub-tabs not shown. `[Default]` is the color of the text. With a color of their own, the focused tab can be told apart with the highlight off. The default scheme does so: the focused tab is cyan, the others dark gray, and the borders of the boxes gray
- **Background**: `[Default]` shows the text on the terminal's own background, so a transparent or blurred terminal window stays that way. Pick a color to give Marbles a background of its own: the whole window is filled with it, and the terminal's background is used again when Marbles exits. The background cannot have the color of the text on it
- A color shown in brackets in the list cannot be picked, as the text would not be visible on its background
- `E` Back to the colors of the scheme in use
- `S` Save the colors as a scheme of your own: type a name for it and press `Enter` (a scheme of your own is suggested by its name; a built-in name cannot be used, and another scheme of yours with the name is replaced if you want that). The scheme is then the one in use. Nothing you change is used until you save

**Preview** shows the colors being edited on a small screen on their background, with a frame with tabs, a box, and two lines of the editor with line numbers and colored text.

- `Esc` (or `Q`) Close. If something was changed and not saved, you are asked in a box whether to save it (`Y`, `N`, or `Esc` to stay)
- `?` Show the help of the page

**Schemes of your own** are kept as text files in the directory `colorschemes` in the data directory (the name of the scheme and `.txt`), with a line for each color (`text=Gray`, `tabframes=default`, `tabhighlight=true`). They can be copied to another computer, or edited by hand: the colors are Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow, Gray, DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow and White, and `default` for the colors that can have it. If the scheme in use is missing when Marbles starts, the default scheme is used. `:defaults` goes back to the default scheme.

With _Black and white_ on, the colors can still be edited: they are used when black and white is turned off.

### Logos
The logo of the start and login pages can be one of your own. The logos are chosen and drawn on a page of their own, _LOGOS_, opened with `:logo` in the main menu (or its item on the Settings tab) or from the Configuration page of the start page (select _Logo_ and press `Enter`). It is a frame with three tabs: **Logos**, **Editor** and **Colors**. `Tab` goes from one to the next, and `h` `l` between the two pages of Colors.

**Logos** shows the selected logo at the top and the logos below it: the five built-in logos, and the ones you have saved yourself. The built-in logos are _Marbles_ (the one used from the start), _Reflections_ (the Marbles logo with a color for each row, from blue to dark green) _Adventure_ (a hut in the forest with smoke from the chimney, a mountain with snow on its top and a stream, with colors for its cells) and _Islands_ (two islands with palms, the sun over the water and stars in the sky, with colors for its cells). They are also there to work on: `E` edits one, and `S` saves it as a logo of your own with another name. The one in use is marked `[x]`.

- `j` `k` (or `Up`/`Down`, `gg` `G`) Select a logo: it is shown above the list
- `Enter` (or `Space`, `l`) Use the selected logo: it is used at once, and its name is saved (`logo` in the settings file)
- `E` Edit the selected logo (on the Editor tab)
- `Y` Duplicate the selected logo (a built-in one too) as a logo of your own: type a name for the copy and press `Enter` (the name with ` 2` is suggested). The copy is selected in the list, and the logo in use stays as it is. A name that is taken, or that of a built-in logo, cannot be used
- `R` Rename a logo of your own: type the new name and press `Enter`. Its file is renamed, and if it is the logo in use, the setting follows. The name of a built-in logo or of another logo of yours cannot be used, and the built-in logos cannot be renamed
- `D` Delete a logo of your own (you are asked first). The built-in logos cannot be deleted
- `:logo Name` in the main menu uses a logo by its name at once

**Editor** shows the logo on a grid of 4 rows of 63 characters, the size of the Marbles logo, in a box with a ruler above it (a dot for each column, `:` for every 5th, and the tens as digits). The cursor cannot leave the grid. The editor has the modes of the [text editor](Guide-to-editor.md), shown below the grid with the row and column of the cursor:

- **NORMAL**: `h` `j` `k` `l` (or the arrow keys) move, `0` `$` go to the start/end of the row, `gg` `G` to the first/last row. `x` clears the character under the cursor, `r` and a character replaces it, `dd` clears the row, and `u` undoes (the last 100 changes)
- `i` or `R` go to **REPLACE** mode, `a` after the cursor: what you type is drawn over the grid and the cursor moves on. `Backspace` clears the character before the cursor, `Enter` goes to the start of the next row, the arrow keys move, and `Esc` goes back to NORMAL
- `S` Save the logo as one of your own: type a name for it and press `Enter` (a logo of your own is suggested by its name; _Marbles_ cannot be used, and another logo of yours with the name is replaced if you want that). The logo is then the one in use
- `Esc` (or `Q`) in NORMAL mode closes the page. If something was changed and not saved, you are asked in a box whether to save it (`Y`, `N`, or `Esc` to stay)

**Colors** has the sub-tabs _Rows_ and _Cells_. **Rows** shows the logo at the top and the color of each of its rows below it. `j` `k` select a row and `Enter` (or `Space`) picks its color from the list of colors; `[Default]` is the color of _Logo and labels_ of the color scheme. **All rows** gives all four rows the color picked, or with `[Default]` sets them all back to the color of the logo. The colors are saved with the logo (`S`), as a line `colors=` after its rows in the file (`colors=Cyan,Cyan,Blue,default`). In black and white the logo is white.

**Cells** shows the logo on the grid of the Editor tab, where each cell can be painted in a color of its own. A cell with a color of its own is drawn in it; the others have the color of their row. Below the grid are the color you paint with and the color of the cell under the cursor.

- `h` `j` `k` `l` (or the arrow keys) move, `0` `$` go to the start/end of the row, `gg` `G` to the first/last row. As `h` and `l` move in the grid here, `H` and `L` go to the other page of Colors (the hint at the right end of the sub-tabs says `H/L`)
- `c` Pick the color to paint with from the list of colors
- `e` Paint with the color of the cell under the cursor, when it has a color of its own (the line _Color of the cell_ shows the tip `(e: pick this color)`). It is a quick way to go on with a color that is already in the logo
- `Space` Paint the cell under the cursor and move to the next one (the first time, the color is asked for)
- `x` Clear the color of the cell: it has the color of its row again
- `v` Select a rectangle (**VISUAL**): move to the opposite corner, then `Space` paints all its cells and `x` clears them. `Esc` (or `v`) gives up the selection
- `X` Clear the colors of all the cells
- `u` Undo, `S` Save, `Esc` (or `Q`) Close

A new color for a row (or for all rows) does not change the cells that have a color of their own, and neither does drawing another character in a cell on the Editor tab: the color belongs to the place, not to the character. Blank space has no color to see, so paint the characters. The colors of the cells are saved with the logo as four lines `cells=` after the others, one for each row, with a character for each cell: the number of the color as a hex digit (`0` Black, `1` DarkBlue, `2` DarkGreen, `3` DarkCyan, `4` DarkRed, `5` DarkMagenta, `6` DarkYellow, `7` Gray, `8` DarkGray, `9` Blue, `A` Green, `B` Cyan, `C` Red, `D` Magenta, `E` Yellow, `F` White) and `.` for a cell without a color of its own (`cells=CCC...EE`).

Only the printable ASCII characters can be typed (letters, digits and signs such as `_ - " % # /`), so the logo looks the same in every terminal. **Logos of your own** are kept as text files in the directory `logos` in the data directory (the name of the logo and `.txt`), with the 4 rows of the logo. They can be copied to another computer or edited by hand; a file can have other characters that take one column (as the block characters `█ ▀ ▄`), but not all terminal fonts have them (the console of a Raspberry Pi does not), so ASCII is the safe choice. Rows longer than 63 characters are cut, and shorter ones filled with blank space. If the logo in use is missing when Marbles starts, _Marbles_ is used. `:defaults` goes back to _Marbles_. Rows without a color of their own are drawn in the color of _Logo and labels_ of the color scheme.

## Lists of entries
Search results, Top 20, Favorites, Session history and List all are shown as lists. The kind of list (_ALL_, _TOP 20_, _FAVORITES_, _SESSION HISTORY_, _SEARCH_) is shown at the top left, the most used keys at the bottom, and `?` shows all the keys of the lists.

- `j` `k` Select an entry, `PgUp` `PgDn` Move 3 entries
- `gg` `G` Select the first/last entry of the list (on the first/last page)
- `h` `l` Previous/Next page of the list, `Home` `End` the first/last page
- `Enter` or `Space` Open the selected entry in the [editor](Guide-to-editor.md). Closing the editor (`:q`) returns to the list, with the entry selected (to the main menu when the entry was opened from there, as with `R`)
- `1`-`9` and `0` Open entry 1-10 directly. `:12` opens entry 12
- `P` Add the selected entry to (or remove it from) the favorites, or pin it to the top of the favorites list. On a pinned entry, `P` asks whether to remove it from the favorites, or only unpin it (`U`): it then stays a favorite, below the pinned entries
- `Tab` Change the sorting
- `V` In the favorites list: move the selected pinned entry. The entry is marked `<>` and highlighted, and follows `j` `k` up and down among the pinned entries (also to the next page) until you press `Esc` (or `Enter`, `V`). The order is saved as you go
- `T` or `:title` Change the title of the selected entry, in a box on top of the list. The list is shown again afterwards
- `:delete` Delete the selected entry. You are asked once; a deleted entry can be brought back with `:undelete` in the main menu
- `I` or `:info` Info about the selected entry: when it was created and last changed, the number of words, characters and lines, and its syntax colors (set with `:syntax` in the editor), in a box on top of the list. Any key closes it
- `:duplicate` Make a copy of the selected entry (named "(copy)"), which is selected in the list
- `:readonly` (or `:ro`) Make the selected entry read only, or not any more. A read-only entry has `RO` at the end of its row in the lists, and is protected against changes by accident: it opens in the editor as usual, where it can be read, searched and yanked from, but not changed (see [the guide to the editor](Guide-to-editor.md)), and its title cannot be changed (`T`, `:title`), and it cannot be reverted (`:revert`) or deleted (`:delete`); you are told so in a box. `:duplicate` makes a copy that can be edited, and `I` (`:info`) shows whether it is read only. It is not a protection against other people: anyone who can log in to the data file can turn it off. It is kept in the data file, also when the entry is exported, merged or transferred
- `:revert` Revert the selected entry to the text before its last edit (since you logged in)
- `:export` Export the text of the selected entry as a text file. Select the directory in the file browser (go to it and press `S`), then enter the file name (the title of the entry is suggested). The exported file is not encrypted
- `:import` Import a text file as a new entry (selected in the file browser). It is opened in the editor
- `C` or `:color` Set the text color of a pinned entry (in the favorites list), as `C` in the favorites of the file browser. The 16 colors are shown in a list, as they look with your color scheme: select with `j` `k` and press `Enter` (or `l`), or `Esc` to cancel. _Default_ removes the color. `:color 13` sets color 13 of the list directly
- `:transfer` Add the selected entry to the transfer collection
- `:clipboard` The clipboard settings (as in the main menu and the editor); `Esc` returns to the list
- `/` or `S` Search, `A` List all, `F` Favorites, `M` Session history, `N` New entry (Top 20 is opened from the main menu)
- `Esc`, `-` or `:q` Return to the main menu
