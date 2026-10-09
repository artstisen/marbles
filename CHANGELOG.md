# Changes

## v1.5.2
- **Line numbers** in the editor: `:number` (or `:nu`) shows or hides them, and the choice is remembered.
- **Editor, text files**: `:e` reads the file from disk again (`:e!` also when there are unsaved changes), and the bottom line shows the type, encoding and line endings of the file (`HTML UTF-8 (BOM) CRLF`).
- **Search for files** in the file browser (`S` or `:find`): by name (with `*` and `?`), by text in the files, with regular expressions, in a directory and its subfolders. The files found are shown in a list where they can be opened at the line found, shown in the browser (`B`), inspected (`I`) and added to the favorites (`P`).
- **Two panes in the file browser**: two directories are shown side by side. `Tab` goes to the other pane, and copy (`C`) and move (`M`) start in the directory of the other pane, so `S` copies or moves to it at once; the place is selected in the panes themselves, so you can go anywhere in either pane first. `T` or `:panes` switches between two panes and one. The directories of both panes are remembered (`browsedir1`, `browsedir2`, `browsepane` and `browsedual` replace `browsedir` in the settings file).
- **File browser columns**: the list has a column for the type of each item (`<DIR>`, `[Data file]`, or the extension of the file such as `TXT` and `MD`) and one for the size, so they line up.
- **Favorites of the file browser**: `-` (and `Backspace`) closes the list, lowercase `v` moves a favorite like `V`, and `u` or `:undo` puts back the favorite or folder that was removed last.
- Lowercase `v` also moves a pinned entry in the favorites list of a data file.
- The color lists show `[Default]` in the normal text color instead of "Default" in green.

## v1.5.1
- **Select mode at startup**: Marbles starts with a choice between _Note Management_ (the login page), _Text Editor_ (the file browser) and _Configuration_, where it can be set to start directly in one of the two (`startmode` in the settings file). From the login page the choices are reached with `Esc`, `T`.
- **Export**: `:export` lets you select the directory in the file browser and enter the file name (the title of the entry is suggested), asks before replacing an existing file, and shows where the file was saved.
- **New entry from the command line**: `:new My title` creates an entry with the title and opens it in the editor. `:new` alone asks for the title.
- **Esc closes the editor**: in Normal mode `Esc` works as `:q` (it asks what to do with unsaved changes). It still leaves Insert and Visual mode, cancels a half typed command and removes the search highlighting first.
- **Deleting an entry** asks for confirmation once instead of twice (a deleted entry can be brought back with `:undelete`).
- **Colors of pinned entries**: `:pin` in the favorites list shows a list of 16 text colors to pick from (`:pin 13` picks by number). Pinned entries are shown as colored text instead of with a highlighted background. The three highlight styles of earlier versions (`:pin 1-3`) are shown as cyan, yellow and red.
- **Find-and-replace box**: `:replace` in the editor shows a box with the text to find, the replacement and options (match case, regular expression, whole text/current line/selected lines), as an alternative to writing a `:s` command. A word selected in the text is filled in as the text to find.
- **Spell checking commands**: `:spellnext`, `:spellprev`, `:suggest` and `:spelladd` do the same as `]s`, `[s`, `z=` and `zg`.
- **File management in the file browser**: create directories (`A`), rename (`X`), duplicate (`Y`), copy (`C`) and move (`M`) to another directory, and delete (`D`) files and directories, also as commands (`:mkdir`, `:rename`, `:duplicate`, `:copy`, `:move`, `:delete`). Nothing is overwritten, and what Marbles is using is protected. Deleted files cannot be recovered.
- **Editor**: the question about unsaved changes when closing is a box on top of the text instead of a page of its own, and "Unsaved" is highlighted. "[Not encrypted]" is only shown for text files opened while logged in to a data file.
- The key hints at the bottom of the file browser and the favorites have highlighted keys.
- **Terminal window made smaller**: the start page, the file browser, the favorites and the help follow the size of the window when automatic size is on. When the dimensions no longer fit the window, the page is drawn again afterwards, and with a fixed size you are offered to switch to automatic size.
- **Markdown colors** in the editor: headings, bold, italic, code, links, quotes, list markers and rules are colored. Markdown files (`.md`) are colored when opened from the file browser, and `:syntax` turns the colors on and off in any text.
- **Log**: commands and the file operations of the file browser are logged, the log holds the latest 1000 lines, long lines continue on the next row, and `/text` only shows the lines containing a text. Commands in the menus and the editor are logged by name only, so no text from your notes is in the log.
- The Configuration page has _Restart Marbles_ and _View log_.
- **Help for the page you are on**: `?` (and `:help`) shows the keys of the current page: start page, login page, menus and lists, file browser, file favorites or editor. `?` in the help switches to all keys and back. The start page and the login page have their own help sections.
- **Black and white**: a setting that shows everything in black and white without changing your color scheme (`:bw` on menu page 4, or the configuration page; `monochrome` in the settings file).
- The start page has a fourth choice, _Exit_, that closes Marbles without asking.
- **Configuration page** (start page): besides what to show at startup, it now has black and white, color scheme, logo, automatic or manual size, and reset of all settings.
- The second choice on the start page is named "Text Editor/File Management", and the file info box (`I`) is also available in the list of favorites.
- **File browser**: `:new name` creates a file (as `N`), and `I` or `:info` shows the type, size, dates and path of the selected file or directory.
- **Favorites in the file browser**: `P` marks a file or directory as a favorite, and `F` shows the list of favorites, where they can be opened, shown in the browser (`B`), moved up and down (`Tab`/`V`), given a color (`C`), searched (`/`) and removed. The list is kept in `favorites.txt` in the data directory. Favorites can be grouped in folders (`A`), which open and close with `Enter`; several favorites can be marked with `Space` and moved to a folder (`M`) or removed (`D`) in one go; a favorite is put in a folder by moving it onto it, and folders can be moved, renamed (`X`), colored and removed.
- **Moving pinned entries**: `Tab` or `V` in the favorites list highlights the selected pinned entry, which then follows `j` `k` up and down among the pinned entries until `Esc` is pressed (earlier `Tab` moved it to the top).
- **Color scheme editor** (`:scheme`): colors are picked from the same list of 16 colors (`l` or `Space`, and `T` for the text color of the highlight styles) instead of stepping through them.
- The version number on the login page is placed correctly next to the logo.
- The color settings page says "Editor title" (the scroll indicator no longer exists).

## v1.5.0

### Upgrading from v1.0.x: read this first
- **Data files are converted, and the conversion is one-way.** The first time you log in to a data file from an earlier version, Marbles saves it in the new file format. Earlier versions of Marbles cannot open the converted file.
- **A copy of the original is kept.** Before converting, Marbles copies the original file to `<name>.mbls.legacy` in the same directory. The copy is still encrypted the old, weaker way. Delete it when you have checked that your notes are in place.
- **The keys have changed.** The `CTRL+W` shortcuts are gone. Marbles is now operated in the style of Vim (see below). Press `?` anywhere for the list of keys.
- **.NET 10** is required instead of .NET 6.
- Your `settings.ini` is read as it is. The key mapping settings (`kbtrigger`, `kbsave` ...) no longer exist and are removed the next time Marbles saves the settings.

### Encryption
- The encryption key is derived from the password with PBKDF2-HMAC-SHA256 (600,000 iterations and a random salt). Earlier versions used the password itself as the key.
- Data files are encrypted with AES-256-GCM, using a new random nonce for every save. A wrong password, a damaged file and a file that has been tampered with are all detected.
- Passwords can be up to 128 characters, and all characters count (earlier versions treated all non-ASCII characters as the same character).
- Generated passwords (`:pw` in the editor) come from a cryptographic random number generator.

### Safer saving
- Data files, backup files and the settings file are written to a temporary file that then replaces the original, so an interrupted save cannot leave a half written file.
- A data file whose content cannot be read is no longer opened as an empty file (saving it would have overwritten the content). Characters that cannot be stored are removed when saving, and files from earlier versions containing such characters are recovered when loading.

### New editor
- Normal, Insert and Visual modes, with the mode shown on the bottom line together with the cursor position.
- Vim movements, counts, operators and text objects (`dw`, `ciw`, `3dd`, `yy`, `p` ...), undo/redo in steps, and repeat (`.`).
- Search with regular expressions (`/` `?` `n` `N` `*`) and find-and-replace (`:s/old/new/g`, `:%s/old/new/g`).
- A command line (`:w`, `:q`, `:wq`, `:5`, `:help` ...).
- In V-LINE mode `J` and `K` move the selected lines down and up.
- Lines are wrapped at blank spaces, and wide characters and emoji are handled.
- Pasting text is fast, and text copied from the terminal window no longer includes padding or the scroll marker.
- Spell checking with the spell files of Vim/Neovim (`.spl`): `:spell da,en` marks the misspelled words, `]s` `[s` go to the next/previous, `z=` shows suggestions to pick from and `zg` adds the word to your word list. The spell files are placed in a directory named `spell`.
- See the [guide to the editor](Guide-to-editor.md).

### Menus and lists
- `h`/`l` change menu page, `j`/`k` select in lists and scroll the text of an entry, `gg`/`G` go to the first/last, `/` searches and `?` shows help.
- A command line (`:`) replaces the `CTRL+W` shortcuts: `:delete`, `:transfer`, `:size`, `:color`, `:q` ... The commands are shown in the menus.
- On the login page, `Esc` opens the options menu.
- `:color 5` and `:logo 3` select a color or logo preset by number.
- `:size auto` makes Marbles follow the size of the terminal window.
- `P` on the first page of the main menu shows a preview of the 5 last edited entries, where an entry can be selected and opened.
- `:import` and `:backup` use the file browser to select the file to import and the backup directory. Any text file can be imported (earlier only "import.txt" in the data directory).
- The command line is highlighted while typing.
- The help (`?`) is shown in pages that can be browsed both ways and searched (`/text`, `n`, `N`). It also covers the file browser.
- See the guides to the [main menu](Guide-to-main-menu.md) and the [login page](Guide-to-login-menu.md).

### File browser
- From the options of the login page (`Esc`, `F`): browse directories and edit plain text files in the editor, without logging in to a data file. New files can be created.
- Text files are saved as UTF-8 with their line endings kept. Files in other encodings can be converted, replacing the file or as a copy.
- Opening a data file from the browser selects it on the login page.
- `:browse` opens the browser when logged in to a data file. Opening a file from it logs out.
- See the [guide to the login page](Guide-to-login-menu.md#file-browser).

### Other
- The logo is drawn with a sweep on the login page (`logoanim=false` in the settings file turns it off).
- The settings file is read by setting name, so the order of the settings does not matter.
- Sorting entries by title is by character order on all platforms.
- Fixed: the application could crash after a very long session of key presses.
- Fixed: changing menu page while viewing an entry could erase part of the "Created" date.

## v1.0.1.1
- The last release of the first generation of Marbles (2022).
