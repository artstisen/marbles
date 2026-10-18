"""Make a language file of Marbles, or bring one up to date with the texts of the code.

    python tools/langfile.py da
    python tools/langfile.py pl path/to/pl.txt     (a file somewhere else, such as the folder lang)

Every text given to Lang.T or Lang.F in the code (and the names listed in Lang.Names) is written to the file of
the language, in the order it appears in the code, with the translation already in the file. German is
built into the program (Marbles/Lang/de.txt); the other languages are files of the folder lang next to the program
(Marbles/Lang/External/pl.txt in the source). A new language gets a new file. A new text
gets an empty translation (it is shown in English until it is translated), and a text that is no longer in the
code is left out. Run it after changing texts, then fill in the empty translations.
Needs Python 3 only. The tests (LangTests) check the files against the code in the same way.
"""
import io
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Marbles')
CALL = re.compile(r'\bLang\.[TF]\(\s*((?:"(?:[^"\\]|\\.)*"\s*(?:\+\s*)?)+)')
LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')
NAMES = {'da': 'Dansk', 'de': 'Deutsch', 'nb': 'Norsk bokmål', 'sv': 'Svenska', 'es': 'Español', 'fr': 'Français',
         'it': 'Italiano', 'pl': 'Polski', 'nl': 'Nederlands', 'fi': 'Suomi'}

BUILT_IN = ('de',)  # Built into the program (Marbles/Lang); the others are files of the folder lang


def language_path(code, given=None):
    """Where the file of a language is: the path given, Marbles/Lang/xx.txt for a built-in language, else
    Marbles/Lang/External/xx.txt (published in the folder lang next to the program)"""
    if given:
        return given
    if code in BUILT_IN:
        return os.path.join(ROOT, 'Lang', code + '.txt')
    return os.path.join(ROOT, 'Lang', 'External', code + '.txt')



def unescape(text):
    escapes = {'n': '\n', 't': '\t', 'r': '\r', '"': '"', '\\': '\\'}
    return re.sub(r'\\(.)', lambda m: escapes.get(m.group(1), m.group(1)), text)


def code_texts():
    """The English texts of the code: (file, text), each text once"""
    found, seen = [], set()
    for directory, subdirectories, files in os.walk(ROOT):
        subdirectories[:] = sorted(d for d in subdirectories if d not in ('bin', 'obj'))
        for name in sorted(files):
            if not name.endswith('.cs') or name == 'Lang.cs':
                continue
            path = os.path.join(directory, name)
            source = io.open(path, encoding='utf-8-sig').read()
            for call in CALL.finditer(source):
                text = ''.join(unescape(l.group(1)) for l in LITERAL.finditer(call.group(1)))
                if text not in seen:
                    seen.add(text)
                    found.append((os.path.relpath(path, ROOT).replace('\\', '/'), text))
    # The names made while the program runs (colors, months, days)
    source = io.open(os.path.join(ROOT, 'Lang.cs'), encoding='utf-8-sig').read()
    block = source[source.index('internal static readonly string[] Names ='):]
    for m in LITERAL.finditer(block[:block.index('};')]):
        if m.group(1) not in seen:
            seen.add(m.group(1))
            found.append(('Lang.cs (names made while running)', m.group(1)))
    return found


def quote(text):
    return '"' + text.replace('\n', '\\n') + '"'


def unquote(value):
    """The text between the first and last quote, or None if there are not two quotes"""
    first, last = value.find('"'), value.rfind('"')
    if first < 0 or last <= first:
        return None
    return value[first + 1:last].replace('\\n', '\n')


def read(path, code):
    """The translations of a language file: English text -> translation"""
    pairs, english = {}, None
    if not os.path.exists(path):
        return pairs
    # utf-8-sig: a file saved with a BOM (as by Notepad) is read as one without
    for number, line in enumerate(io.open(path, encoding='utf-8-sig').read().split('\n'), 1):
        if line.startswith('en:'):
            english = unquote(line[3:])
            if english is None:
                print(f'{path}:{number}: no text in quotes (the line is not used)')
        elif english is not None and line.startswith(code + ':'):
            translation = unquote(line[len(code) + 1:])
            if translation is None:
                print(f'{path}:{number}: no translation in quotes (the line is not used)')
            else:
                pairs[english] = translation
            english = None
    return pairs


def make(code, given=None):
    path = language_path(code, given)
    old = read(path, code)
    # The name of the language: the one the file has, a known one, or the code
    name = NAMES.get(code, code)
    if os.path.exists(path):
        first = io.open(path, encoding='utf-8-sig').readline()
        m = re.match(r'^# Marbles: (.+) \(' + re.escape(code) + r'\)\s*$', first)
        if m:
            name = m.group(1)
    out = [f'# Marbles: {name} ({code})',
           '# Each text is given in English ("en:") and translated below it ("' + code + ':"), in quotes. "\\n" is a line break.',
           '# Keep as they are: the placeholders ({0}, {1}), the keys (j/k, Enter, Esc, Tab ...), the commands (:help ...),',
           '# the names of the editor modes (NORMAL, INSERT, VISUAL ...) and blank space at the start or end of a text.',
           '# In key hints ("j/k Select | Esc Close") each part starts with the key. An empty translation is shown in English.',
           '# Made with tools/langfile.py, which keeps the translations when the texts of the code change.',
           '']
    current, missing = None, 0
    for source, text in code_texts():
        if source != current:
            out += ['', '# ' + source]
            current = source
        translation = old.get(text, '')
        missing += translation == ''
        out += ['en: ' + quote(text), code + ': ' + quote(translation)]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    io.open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
    print(f'{path}: {missing} not translated')


if __name__ == '__main__':
    # The texts are printed in UTF-8 (a Windows console uses an older code page by default)
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    if len(sys.argv) not in (2, 3):
        sys.exit(__doc__)
    make(sys.argv[1], sys.argv[2] if len(sys.argv) == 3 else None)
