# Guide to the editor

The editor works in the style of Vim. It is always in one of these modes, shown at the left of the bottom line:

- **NORMAL**: keys move the cursor and give commands. This is where you start when opening an entry with text.
- **INSERT**: keys type text. An empty entry starts here. Press `Esc` to return to Normal mode.
- **VISUAL** and **V-LINE**: movements select text (or whole lines) for a command.

When a text file is edited (from the file browser), the bottom line shows its type, encoding and line endings, like `HTML UTF-8 (BOM) CRLF` or `TXT Windows-1252 LF` (a file in another encoding than UTF-8 becomes UTF-8 when it is saved; the line endings are kept: `CRLF` as used by Windows, `LF` as used by Linux and macOS). The bottom line also shows messages, the current memory string, the cursor position as `line:column` and how far into the text the cursor is. Inside Marbles, `:help` shows the list of keys.

## Save and close
Typed on the command line: press `:`, type the command and press `Enter`. `Tab` completes the command you are typing (`:spelln` and `Tab` gives `:spellnext`), `Tab` again gives the next command that starts the same and `Shift+Tab` the previous. With nothing typed, `Tab` goes through all the commands of the editor. Lines typed in front of a command are kept (`:%d` and `Tab` gives `:%delete`).

- `:w` Save
- `:wq` or `:x` or `ZZ` Save and close the editor
- `:q` or `Esc` in Normal mode: Close the editor (asks what to do with unsaved changes)
- `:q!` or `ZQ` Close the editor without saving
- `:e` (or `:reload`) Text files: read the file from disk again, when it has been changed by another program. If you have changes that are not saved it is not done; `:e!` reads the file anyway and drops your changes. What was done before cannot be undone afterwards
- `:help` Show the list of keys

`Esc` first leaves what you are in the middle of: Insert or Visual mode, a command that is half typed, or the highlighting of search matches. When there is nothing to leave, it closes the editor.

## Entering Insert mode
- `i` / `a` Insert before/after the cursor
- `I` / `A` Insert at the start/end of the line
- `o` / `O` Insert on a new line below/above

In Insert mode: `Tab` inserts 4 blank spaces, `CTRL+W` deletes the word before the cursor, `CTRL+U` deletes the line before the cursor, and `CTRL+R` pastes the current memory string. Text can be pasted from the clipboard with the paste function of your terminal.

## Moving the cursor (Normal and Visual mode)
- `h` `j` `k` `l` Left/Down/Up/Right (as the arrow keys). `j` and `k` move by rows as shown on screen
- `w` `b` `e` Next word/Previous word/End of word (`W` `B` `E`: words separated by blank space)
- `0` `_` `$` Start of line/First character/End of line (as `Home` and `End`)
- `gg` `G` Top/End of the text
- `5G` or `:5` Go to line 5
- `{` `}` Previous/Next blank line
- `f.` `t.` To/Before the next `.` on the line (`F` `T`: backwards), repeated with `;` and `,`
- `PgUp` `PgDn` One page up/down, `CTRL+U` `CTRL+D` Half a page up/down
- `zt` `zz` `zb` Scroll the row of the cursor to the top/center/bottom

A number before a movement or command repeats it: `5j` moves 5 rows down and `3w` three words forward.

## Editing (Normal mode)
- `x` `X` Delete the character under/before the cursor
- `r.` Replace the character with `.`
- `d` `c` `y` Delete/Change/Yank (copy), followed by a movement:
  - `dw` `d$` `dG` Delete a word/to the end of the line/to the end of the text
  - `cw` `ciw` `ci"` Change a word/the whole word/the text in quotes
  - `yy` `dd` `cc` Yank/Delete/Change the line (`3dd`: 3 lines)
- `D` `C` `Y` Delete/Change to the end of the line, Yank the line
- `p` `P` Paste after/before the cursor
- `J` Join the line with the next
- `>>` `<<` Indent/Unindent the line
- `~` Switch the case of the character, `gUU` `guu` Line to UPPERCASE/lowercase, `gUw` `guw` Word to UPPERCASE/lowercase
- `u` Undo (500 steps), `U` or `CTRL+R` Redo
- `.` Repeat the last change

## Visual mode
Press `v` (text), `V` (whole lines) or `Q` (a block, see below) and select with the movement keys. The commands `:vsel`, `:vline` and `:block` do the same. Then:

- `d` `y` `c` Delete/Yank/Change the selection
- `p` Replace the selection with pasted text
- `U` `u` `~` UPPERCASE/lowercase/Switch case
- `>` `<` Indent/Unindent the selected lines
- `J` `K` In V-LINE: move the selected lines down/up (`3J` moves them 3 lines). The lines stay selected
- `J` In VISUAL: join the selected lines
- `iw` `i"` `i(` Select the word/the text in quotes/the text in brackets
- `o` Move the cursor to the other end of the selection
- `v` `V` `Q` Switch to selecting text/lines/a block. The key of the mode you are in leaves it
- `gv` (in Normal mode) Select again what was selected last, in the same kind of selection, also after the selection was used by a command (like `:ol` or `y`). In Visual mode, `gv` changes places between the selection shown and the last one
- `Esc` Leave Visual mode

### Visual block
`Q` selects a block: the same columns on each of the lines, like a rectangle (V-BLOCK is shown at the bottom left). Move the cursor to the opposite corner with the movement keys. The columns are counted as the text is shown, so a character that takes two columns (like 中) is taken whole when the block cuts through it.

- `$` The block goes to the end of each line, however long the lines are (until you move the cursor sideways)
- `I` Type text in front of the block, and `A` after it. You type on the first line, and when you press `Esc` the text is put on all the lines of the block. With `I` a line that ends before the block is left as it is; with `A` it gets blank spaces up to the block. After `$`, `A` adds the text at the end of each line
- `d` (or `x`) Delete the block: the text to the right of it moves left. `D` (or `X`) deletes to the end of each line
- `y` Yank (copy) the block. `Y` yanks to the end of each line
- `c` Change the block: it is deleted and you type the new text, which is put on all the lines when you press `Esc`. `C` changes to the end of each line
- `p` Replace the block with the current memory string
- `r` and a character: Replace each character of the block with it
- `U` `u` `~` UPPERCASE/lowercase/Switch case
- `>` `<` Indent/Unindent the lines of the block
- `O` Move the cursor to the other corner on the same line, `o` to the opposite corner

A yanked or deleted block is pasted as a block with `p` (after the cursor) or `P` (before it): its lines go in at the same column on the lines from the cursor down, the text to the right of it stays in line, and lines are added at the end of the text if there are not enough. `2p` pastes it twice, side by side. A change made on a block is undone with one `u`, and `.` makes it again at the cursor.

## Search
- `/text` `?text` Search forwards/backwards, `n` `N` Next/Previous match
- `*` Search for the word under the cursor
- `Esc` or `:noh` Remove the highlighting of the matches (press `Esc` again to close the editor)

The text you search for is a regular expression: `/\d+` finds numbers and `/cat|dog` finds either word. A search in lowercase ignores case; with an uppercase letter in it the search matches case.

## Replace
The easy way is `:replace`, which shows a box to fill in:

- **Find** and **Replace with**: the box opens in the _Find_ field. Type the text and press `Enter` to continue to the next field
- If a word (or other text within one line) is selected in VISUAL mode when you type `:replace`, it is filled in as the text to find, and the box opens in the _Replace with_ field
- **Match case**: off by default, so `cat` also finds `Cat`
- **Regular expression**: off by default, so the text is found exactly as written. When on, _Find_ is a regular expression and `&`, `\1` and `\n` can be used in _Replace with_ (see below)
- **Where**: the whole text or the current line. With lines selected in V-LINE mode before typing `:replace`, also the selected lines
- **Replace all**: press `Enter` here to replace

`j` `k` select a row, `Enter` or `Space` edit a field or change an option, and `Esc` closes the box (in a field: leaves the text as it was). The box remembers what you entered. The replacement is one change: `u` undoes it.

The same can be written as a command:

- `:s/old/new/` Replace the first `old` on the line with `new`
- `:s/old/new/g` Replace all on the line
- `:%s/old/new/g` Replace all in the text
- `:2,9s/old/new/g` Replace all on line 2 to 9
- `V` (select lines) then `:s/old/new/g` Replace all on the selected lines

`old` is a regular expression as when searching. In `new`, `&` is the matched text, `\1` the first group and `\n` a line break. Add `i` to ignore case or `I` to match case: `:%s/old/new/gi`.

## Spell checking
Spell checking is off until you ask for it. It uses the spell files of Vim and Neovim (see below).

- `:spell` Check the text and mark the misspelled words. The bottom line tells how many were found
- `:spell da,en` As above, with Danish and English. The languages are remembered (`spelllang` in the settings file)
- `:nospell` Remove the marks
- `]s` `[s` (or `:spellnext` `:spellprev`) Go to the next/previous misspelled word
- `z=` (or `:suggest`) Show suggestions for the word under the cursor. Press the number of a suggestion (or select it with `j` `k` and press `Enter`) to replace the word, `a` to add the word to your word list, or `Esc` to close
- `zg` (or `:spelladd`) Add the word under the cursor to your word list

The marks follow the text while you edit, until you use `:nospell` or close the editor. The word you are writing is not marked until you leave it. Single letters, words with digits, and web and e-mail addresses are not checked.

### Spell files
Marbles reads spell files from a directory named `spell`, next to the `data` directory. A language needs one file, named like `en.utf-8.spl` or `da.utf-8.spl`. These are the files Vim and Neovim use, so you can copy them from there (the directory `spell` of the Vim runtime or of your Neovim configuration), or download them from the [Vim spell file archive](https://ftp.nluug.nl/pub/vim/runtime/spell/). Only UTF-8 files can be used.

The words you add are saved as plain text, one word per line, in `spell/<language>.utf-8.add` (for the first of the languages). This is the format of Neovim's word list, so an existing list can be copied to the directory as well.

## Line numbers
- `:number` (or `:nu`) Show or hide line numbers. `:set number` and `:set nonumber` do one of the two

The numbers are shown to the left of the text, with a small gap. A long line that continues on the next row has its number on the first row only, so the numbers are the ones used by `:5` and shown as `line:column` on the bottom line. The number of the line the cursor is on stands out. Line numbers are on from the start. The choice is remembered (`linenumbers` in the settings file) and is used for notes and text files alike. It can also be set on the Configuration page of the start page, and with `:number` on page 2 of the main menu.

When text is copied from the terminal window with the mouse, the line numbers are copied too: turn them off first (`:nu`). Text yanked in the editor never has them.

## Colors
The editor can color the parts of a text by what they are, for Markdown, HTML, CSS, JavaScript, JSON and XML. The text itself is shown as it is written: it is coloring, not a preview, and nothing is checked for errors.

- Files named `.md`, `.html`/`.htm`, `.css`, `.js`/`.mjs`, `.json` and `.xml` (also `.svg`, `.xsd`, `.xsl`, `.xaml`, `.rss`, `.csproj` and other XML files) are colored when they are opened from the file browser
- `:syntax` Turn the colors on or off (`:syntax on` and `:syntax off` do one of the two). When you turn them off in one of these files, that is remembered (`syntaxcolors` in the settings file), so files are opened without colors until you turn them on again
- `:syntax md`, `:syntax html`, `:syntax css`, `:syntax js`, `:syntax json`, `:syntax xml` Color the text as that kind, whatever the name of the file is. This is how the colors are used in a note of a data file, where they are off until you ask for them, and it lasts while the editor is open. In a note, `:syntax` alone colors it as Markdown

Selected text, search matches and misspelled words are shown on top of the colors. With _Black and white_ on, the colored parts are shown brighter than the text.

### Markdown

| | |
|---|---|
| `# Heading 1` to `###### Heading 6` | Red, magenta, yellow, cyan, green and blue |
| `**bold**` `__bold__` and list markers (`-` `*` `+` `1.` `[x]`) | The color of the logo and labels in your color scheme |
| `*italic*` `_italic_` | The color of the preview title |
| `` `code` `` and blocks of code between lines starting with ```` ``` ```` | The color of favorites |
| `[link text]` | The color of the editor title |
| `(address)` of a link, web addresses, `> quotes` and rules (`---`) | The color of dividers |

The reading of Markdown is simple: emphasis inside emphasis is shown as the outer one, emphasis that continues on the next line is not found, and tables are not colored.

### HTML, CSS, JavaScript, JSON and XML

| | |
|---|---|
| Comments | The color of dividers |
| Strings (text in quotes), and the values of attributes in HTML | The color of favorites |
| The words of JavaScript (`const`, `function`, `return` ...), HTML tags, CSS selectors and words like `@media` and `!important` | The color of the logo and labels |
| The names of attributes in HTML, of properties in CSS and of values in JSON | The color of the preview title |
| Numbers (also with units, like `12px`), CSS colors like `#fff`, and character entities like `&amp;` | Magenta |

In an HTML file, what is between `<style>` and `</style>` is colored as CSS, and what is between `<script>` and `</script>` as JavaScript. XML is colored like HTML (tags, attributes, comments and entities), without the CSS and JavaScript, and the text of a `<![CDATA[ ... ]]>` section is left uncolored.

This too is a simple reading. Regular expressions written between slashes in JavaScript are not recognized (and can be colored wrongly if they contain quotes), the code inside `${...}` in a template string is shown as part of the string, and CSS or JavaScript written in the attributes of an HTML tag is shown as an ordinary value.

## String memory
Text that is yanked, and lines that are deleted, are kept in the string memory (up to 64 strings). The memory is kept while you are logged in, so text can be copied from one entry to another.

- `p` `P` Paste the current memory string
- `CTRL+P` `CTRL+N` Select the previous/next memory string
- `:reg` Show the current memory string
- `:putall` Insert all memory strings
- `:regclear` Clear the string memory

## Other commands
- `:ts` Insert an ISO timestamp (YYYY-MM-DD HH:MM:SS)
- `:cal` Insert a calendar (select start month and year, and the length)
- `:pw` Insert a generated strong password
- `:trim` Trim blank space at the start and end of all lines
- `:ol` Make the selected lines a numbered list (`1. `, `2. ` ...), and `:ul` a bulleted list (`- `). Select the lines with `V` (or `v`, `Q`) and type the command: the command line then starts with `'<,'>`, which stands for the selected lines. Without a selection the command works on the line of the cursor, and `:2,5ol` on lines 2 to 5. Lines that are a list of the other kind are changed to this kind, and when all the lines already are a list of the kind asked for, the list marks are removed again. Blank lines are left as they are, the indentation is kept, and indented lines are numbered as a list of their own (1, 2 ... again under each item). One `u` undoes it
- `:%y` `:%d` Yank all/Delete all, `:2,9d` Delete line 2 to 9
- `:clear` Clear all text

## Title and other single line fields
Titles, file names and search queries are typed in a simple field without modes: type the text and press `Enter` to accept or `Esc` to cancel. `Home`/`End` and `CTRL+Left`/`CTRL+Right` move the cursor, and `CTRL+W`, `CTRL+U` and `CTRL+R` work as in Insert mode.
