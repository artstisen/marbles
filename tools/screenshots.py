"""Screenshots of Marbles for the README, as SVG images of a terminal window.

    python tools/screenshots.py [--exe PATH] [scene ...]

Marbles is run in a hidden terminal (a pseudo console) of 100x30, in a folder of its own in the temp folder with a
data directory of its own, so nothing of the user's is shown or changed. First a demo data file is made through the
pages of Marbles (a throwaway password, and made-up notes typed in the editor); then each scene starts Marbles with a
copy of it, logs in, gets the keys that set it up, and what the terminal then shows (each cell with its colors, and
the cursor) is drawn as an SVG in docs/screenshots. Run it again after a change of the screens.

Needs Windows, and the Python packages pyte and pywinpty. The notes of the demo are made up for it.
The program is the release build (publish/win-x64/Marbles.exe) unless --exe gives another.
"""
import argparse
import html
import os
import queue
import re
import shutil
import sys
import tempfile
import threading
import time

import pyte
import winpty

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'docs', 'screenshots')
COLS, ROWS = 100, 30
PASSWORD = 'demo-only-password'  # The password of the demo data file (made up, used nowhere else)

# ---------------------------------------------------------------------------------------------------------------
# The demo notes (made up for the screenshots): a title and the text, colored as Markdown

NOTES = [
    ('Trip to Lisbon', """# Trip to Lisbon

Four days in May. Fly out on Thursday morning, back on Monday night.

## Places

- Alfama: walk up to the castle early, before the crowds
- Tram 28 from Martim Moniz, sit on the right
- LX Factory on Sunday for the market
- Belem: the tower, and pastries at the old bakery

## Packing

- [x] Passport and the boarding passes
- [x] Rain jacket (it can turn in May)
- [ ] Charger and the travel adapter
- [ ] Walking shoes, the old ones

> Lisbon is a city of hills: plan the walks downhill.

## Budget

| Item      | Cost |
|-----------|-----:|
| Flights   |  310 |
| Hotel     |  420 |
| Food      |  200 |
"""),
    ('Sourdough starter', """# Sourdough starter

Feed it every day at the same time, and keep it in the warm corner.

1. Equal weights of flour and water, 50 g each
2. Stir well and leave the lid loose
3. Ready when it doubles in four to six hours

*Bake on Saturday:* 500 g flour, 350 g water, 100 g starter, 10 g salt.
"""),
    ('Reading list 2026', """# Reading list 2026

- [x] The Left Hand of Darkness
- [x] A Psalm for the Wild-Built
- [ ] The Remains of the Day
- [ ] Piranesi (again)
- [ ] The Dispossessed

**Next up:** something short for the train.
"""),
    ('Garden plan', """# Garden plan

The bed by the fence gets the most sun in the afternoon.

- Tomatoes along the fence, with stakes
- Beans on the frame, sow in May
- Herbs in the pots by the door: basil, thyme, mint (in its own pot)

Water in the evening when it is dry for more than two days.
"""),
    ('Meeting notes: kickoff', """# Kickoff, Tuesday

Present: Anna, Jonas, Priya, me

## Decisions

- First release at the end of the quarter
- Weekly check-in on Mondays at 10

## To do

- [ ] Anna: draft the plan by Friday
- [ ] Jonas: set up the test machines
- [ ] Me: write up the open questions
"""),
    ('Gift ideas', """# Gift ideas

- Mum: the cookbook from the bakery in Lisbon
- Erik: a good pocket knife
- Lea: tickets for the concert in June
"""),
    ('Running log', """# Running log

| Date  | Route        |   km | Time  |
|-------|--------------|-----:|-------|
| 02.11 | Harbour loop |  6.2 | 34:10 |
| 04.11 | Park hills   |  8.0 | 47:55 |
| 05.11 | Harbour loop |  6.2 | 33:40 |
"""),
    ('Quotes', """# Quotes

> The best time to plant a tree was twenty years ago. The second best time is now.

> Simplicity is prerequisite for reliability.
"""),
]

# The settings of every run (the rest are Marbles' own defaults)
SETTINGS = {
    'startmode': 'notes', 'autosize': 'true', 'logoanim': 'off', 'autocomplete': 'false', 'language': 'en',
    'colorscheme': 'Default', 'commandlist': 'true', 'linenumbers': 'false',
}


def write_settings(data, settings):
    values = dict(SETTINGS, **settings)
    lines = ['; Marbles screenshots', '[SETTINGS]'] + ['%s=%s' % item for item in values.items()]
    with open(os.path.join(data, 'settings.ini'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')


# ---------------------------------------------------------------------------------------------------------------
# Running Marbles in a pseudo console

KEYS = {'<Esc>': '\x1b', '<Enter>': '\r', '<Tab>': '\t', '<Down>': '\x1b[B', '<Up>': '\x1b[A',
        '<Left>': '\x1b[D', '<Right>': '\x1b[C', '<C-o>': '\x0f', '<C-k>': '\x0b', '<BS>': '\x7f'}


def run(exe, work, steps, start_wait=4):
    """Start Marbles, send the steps of keys, and return the screen when all is drawn"""
    proc = winpty.PtyProcess.spawn([exe], cwd=work, dimensions=(ROWS, COLS))
    screen = pyte.Screen(COLS, ROWS)
    stream = pyte.Stream(screen)
    chunks = queue.Queue()

    def reader():
        while True:
            try:
                data = proc.read(65536)
            except Exception:
                return
            if data:
                chunks.put(data)

    threading.Thread(target=reader, daemon=True).start()

    def pump(seconds):
        end = time.time() + seconds
        while time.time() < end:
            try:
                stream.feed(chunks.get(timeout=0.05))
            except queue.Empty:
                pass

    pump(start_wait)
    for step in steps:
        for part in re.split(r'(<[^>]+>)', step):
            if part == '<wait>':
                pump(1)
            elif part:
                proc.write(KEYS.get(part, part))
                pump(0.3)
        pump(1.2)
    pump(1.5)
    proc.terminate(force=True)
    return screen


def make_data_file(exe, work):
    """The demo data file, made through the pages of Marbles: the password, then each note typed in the editor and
    colored as Markdown. Returns the path of the data file"""
    data = os.path.join(work, 'data')
    if os.path.exists(data):
        shutil.rmtree(data)
    os.makedirs(data)
    write_settings(data, {})
    steps = ['<Enter>', PASSWORD + '<Enter>', PASSWORD + '<Enter>', '<Enter>', PASSWORD + '<Enter>']
    for title, text in NOTES:
        # A new note opens in INSERT mode; the text is typed line by line
        steps += ['N', title + '<Enter>']
        steps += [line + '<Enter>' for line in text.rstrip('\n').split('\n')]
        steps += ['<Esc>', ':syntax md<Enter>', ':wq<Enter>', '<Esc>']
    run(exe, work, steps)
    path = os.path.join(data, 'data.mbls')
    if not os.path.exists(path):
        sys.exit('The demo data file was not made: ' + path)
    return path


# ---------------------------------------------------------------------------------------------------------------
# The screen as an SVG of a terminal window

# The 16 colors of the console (the palette of Windows Terminal, Campbell)
PALETTE = {
    'black': '#0C0C0C', 'red': '#C50F1F', 'green': '#13A10E', 'brown': '#C19C00', 'blue': '#0037DA',
    'magenta': '#881798', 'cyan': '#3A96DD', 'white': '#CCCCCC', 'brightblack': '#767676', 'brightred': '#E74856',
    'brightgreen': '#16C60C', 'brightbrown': '#F9F1A5', 'brightblue': '#3B78FF', 'brightmagenta': '#B4009E',
    'brightcyan': '#61D6D6', 'brightwhite': '#F2F2F2',
}
DEFAULT_FG, DEFAULT_BG = '#CCCCCC', '#0C0C0C'
CELL_W, CELL_H, FONT_SIZE = 8.4, 17, 14
PAD, BAR = 14, 34


def color(name, default):
    if name == 'default':
        return default
    if name in PALETTE:
        return PALETTE[name]
    if re.fullmatch(r'[0-9a-fA-F]{6}', name):
        return '#' + name
    return default


def svg(screen, title, show_cursor):
    width = PAD * 2 + COLS * CELL_W
    height = BAR + PAD * 2 + ROWS * CELL_H
    out = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">'
           % (width, height, width, height),
           '<title>%s</title>' % html.escape(title),
           '<style>text{font-family:"Cascadia Mono","Cascadia Code",Consolas,"DejaVu Sans Mono",Menlo,'
           'monospace;font-size:%dpx;white-space:pre}</style>' % FONT_SIZE,
           '<rect width="100%%" height="100%%" rx="10" fill="%s"/>' % DEFAULT_BG,
           '<rect width="100%%" height="%d" rx="10" fill="#2B2B2B"/>' % BAR,
           '<rect y="%d" width="100%%" height="10" fill="#2B2B2B"/>' % (BAR - 10)]
    for i, dot in enumerate(['#FF5F57', '#FEBC2E', '#28C840']):
        out.append('<circle cx="%d" cy="%d" r="6" fill="%s"/>' % (20 + i * 20, BAR // 2, dot))
    out.append('<text x="%.1f" y="%d" fill="#A0A0A0" text-anchor="middle">%s</text>'
               % (width / 2, BAR // 2 + 5, html.escape(title)))
    top = BAR + PAD
    cursor = None if screen.cursor.hidden or not show_cursor else (screen.cursor.y, screen.cursor.x)
    for y in range(ROWS):
        row = screen.buffer[y]
        cells = []
        for x in range(COLS):
            char = row[x]
            fg, bg = color(char.fg, DEFAULT_FG), color(char.bg, DEFAULT_BG)
            if char.reverse:
                fg, bg = bg, fg
            if cursor == (y, x):
                fg, bg = DEFAULT_BG, DEFAULT_FG
            cells.append((char.data or ' ', fg, bg, char.bold))
        # The backgrounds, in runs of the same color
        x = 0
        while x < COLS:
            bg = cells[x][2]
            end = x
            while end < COLS and cells[end][2] == bg:
                end += 1
            if bg != DEFAULT_BG:
                out.append('<rect x="%.1f" y="%d" width="%.1f" height="%d" fill="%s"/>'
                           % (PAD + x * CELL_W, top + y * CELL_H, (end - x) * CELL_W, CELL_H, bg))
            x = end
        # The text, in runs of the same color (each run placed at its column, so any font lines up)
        x = 0
        while x < COLS:
            fg, bold = cells[x][1], cells[x][3]
            end = x
            while end < COLS and cells[end][1] == fg and cells[end][3] == bold:
                end += 1
            text = ''.join(cell[0] for cell in cells[x:end])
            if text.strip():
                stripped = text.rstrip()
                out.append('<text x="%.1f" y="%.1f" fill="%s"%s textLength="%.1f" lengthAdjust="spacingAndGlyphs"'
                           ' xml:space="preserve">%s</text>'
                           % (PAD + x * CELL_W, top + y * CELL_H + CELL_H * 0.75, fg,
                              ' font-weight="bold"' if bold else '', len(stripped) * CELL_W,
                              html.escape(stripped)))
            x = end
    out.append('</svg>')
    return '\n'.join(out) + '\n'


# ---------------------------------------------------------------------------------------------------------------
# The scenes: a name, a title, the settings, the keys after the login (None: not logged in), and if the cursor is
# drawn

SCENES = [
    ('main-menu', 'Marbles: the main menu of a data file', {}, [], False),
    ('notes-list', 'Marbles: all the notes of a data file', {'colorscheme': 'Deep Blue'}, ['A'], False),
    ('editor', 'Marbles: editing a note as Markdown, a search with its count', {},
     ['A', '8', '<wait>', 'gg', '/walk<Enter>'], True),
    ('color-schemes', 'Marbles: the color schemes', {'colorscheme': 'Tasty Bytes'},
     [':jump colors<Enter>', '<wait>', '<Tab>', '<Tab>'], False),
]


def main():
    parser = argparse.ArgumentParser(description='Screenshots of Marbles as SVG images')
    parser.add_argument('--exe', default=os.path.join(ROOT, 'publish', 'win-x64', 'Marbles.exe'))
    parser.add_argument('scenes', nargs='*', help='the scenes to make (all when none is given)')
    options = parser.parse_args()
    if not os.path.exists(options.exe):
        sys.exit('Marbles is not there: ' + options.exe + ' (run publish.ps1 win-x64, or give --exe)')
    # Marbles runs from a copy in a folder of its own, so its data directory (next to the program) is the demo's
    work = os.path.join(tempfile.gettempdir(), 'marbles-screenshots')
    if os.path.exists(work):
        shutil.rmtree(work)
    program = os.path.join(work, 'program')
    shutil.copytree(os.path.dirname(os.path.abspath(options.exe)), program,
                    ignore=shutil.ignore_patterns('data', '*.pdb'))
    exe = os.path.join(program, os.path.basename(options.exe))
    demo = os.path.join(work, 'demo.mbls')
    shutil.copyfile(make_data_file(exe, program), demo)
    os.makedirs(OUT, exist_ok=True)
    for name, title, settings, keys, show_cursor in SCENES:
        if options.scenes and name not in options.scenes:
            continue
        data = os.path.join(program, 'data')
        shutil.rmtree(data)
        os.makedirs(data)
        write_settings(data, settings)
        shutil.copyfile(demo, os.path.join(data, 'data.mbls'))
        steps = [] if keys is None else [PASSWORD + '<Enter>', '<wait>'] + keys
        screen = run(exe, program, steps)
        with open(os.path.join(OUT, name + '.svg'), 'w', encoding='utf-8', newline='\n') as f:
            f.write(svg(screen, title, show_cursor))
        print(name)
    shutil.rmtree(work, ignore_errors=True)


if __name__ == '__main__':
    main()
