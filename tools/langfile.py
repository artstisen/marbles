"""Make a language file of Marbles, or bring one up to date with the texts of the code.

    python tools/langfile.py da

Every text given to Lang.T or Lang.F in the code (and the names listed in Lang.Names) is written to
Marbles/Lang/da.txt, in the order it appears in the code, with the translation already in the file. A new text
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
NAMES = {'da': 'Dansk', 'de': 'Deutsch', 'nb': 'Norsk bokmål', 'sv': 'Svenska', 'es': 'Español', 'fr': 'Français'}


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
    first, last = value.index('"'), value.rindex('"')
    return value[first + 1:last].replace('\\n', '\n')


def read(path, code):
    """The translations of a language file: English text -> translation"""
    pairs, english = {}, None
    if not os.path.exists(path):
        return pairs
    for line in io.open(path, encoding='utf-8').read().split('\n'):
        if line.startswith('en:'):
            english = unquote(line[3:])
        elif english is not None and line.startswith(code + ':'):
            pairs[english] = unquote(line[len(code) + 1:])
            english = None
    return pairs


def make(code):
    path = os.path.join(ROOT, 'Lang', code + '.txt')
    old = read(path, code)
    out = [f'# Marbles: {NAMES.get(code, code)} ({code})',
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
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    make(sys.argv[1])
