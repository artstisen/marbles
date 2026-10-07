# Guide to the editor

The editor works in the style of Vim. It is always in one of these modes, shown at the left of the bottom line:

- **NORMAL**: keys move the cursor and give commands. This is where you start when opening an entry with text.
- **INSERT**: keys type text. An empty entry starts here. Press `Esc` to return to Normal mode.
- **VISUAL** and **V-LINE**: movements select text (or whole lines) for a command.

The bottom line also shows messages, the current memory string, the cursor position as `line:column` and how far into the text the cursor is. Inside Marbles, `:help` shows the list of keys.

## Save and close
Typed on the command line: press `:`, type the command and press `Enter`.

- `:w` Save
- `:wq` or `:x` or `ZZ` Save and close the editor
- `:q` Close the editor (asks what to do with unsaved changes)
- `:q!` or `ZQ` Close the editor without saving
- `:help` Show the list of keys

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
Press `v` (text) or `V` (whole lines) and select with the movement keys. Then:

- `d` `y` `c` Delete/Yank/Change the selection
- `p` Replace the selection with pasted text
- `U` `u` `~` UPPERCASE/lowercase/Switch case
- `>` `<` `J` Indent/Unindent/Join the selected lines
- `iw` `i"` `i(` Select the word/the text in quotes/the text in brackets
- `o` Move the cursor to the other end of the selection
- `Esc` Leave Visual mode

## Search
- `/text` `?text` Search forwards/backwards, `n` `N` Next/Previous match
- `*` Search for the word under the cursor
- `Esc` or `:noh` Remove the highlighting of the matches

The text you search for is a regular expression: `/\d+` finds numbers and `/cat|dog` finds either word. A search in lowercase ignores case; with an uppercase letter in it the search matches case.

## Replace
- `:s/old/new/` Replace the first `old` on the line with `new`
- `:s/old/new/g` Replace all on the line
- `:%s/old/new/g` Replace all in the text
- `:2,9s/old/new/g` Replace all on line 2 to 9
- `V` (select lines) then `:s/old/new/g` Replace all on the selected lines

`old` is a regular expression as when searching. In `new`, `&` is the matched text, `\1` the first group and `\n` a line break. Add `i` to ignore case or `I` to match case: `:%s/old/new/gi`.

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
- `:%y` `:%d` Yank all/Delete all, `:2,9d` Delete line 2 to 9
- `:clear` Clear all text

## Title and other single line fields
Titles, file names and search queries are typed in a simple field without modes: type the text and press `Enter` to accept or `Esc` to cancel. `Home`/`End` and `CTRL+Left`/`CTRL+Right` move the cursor, and `CTRL+W`, `CTRL+U` and `CTRL+R` work as in Insert mode.
