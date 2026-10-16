"""Check a language file of Marbles: what LangTests checks, and texts that may be too long for the screen.

    python tools/checklang.py de
    python tools/checklang.py pl path/to/pl.txt     (a file somewhere else, such as the folder lang)

Errors (the tests fail on these): an empty translation, placeholders ({0}) that differ, blank space at the start
or end that differs, key hints whose keys differ, keys in brackets ([Y], [Esc]) that differ, and lines broken by
hand (\\n) longer than 79 characters.
Warnings: translations much longer than the English text, which may not fit where they are shown (menus, boxes,
the help and the key hints have room for about the length of the English text).
"""
import io
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Marbles')


def language_path(code, given=None):
    """The file of a language: the path given, built in (Marbles/Lang), or of the folder lang (Marbles/Lang/External)"""
    if given:
        return given
    built_in = os.path.join(ROOT, 'Lang', code + '.txt')
    return built_in if os.path.exists(built_in) else os.path.join(ROOT, 'Lang', 'External', code + '.txt')


def unquote(value):
    """The text between the first and last quote, or None if there are not two quotes"""
    first, last = value.find('"'), value.rfind('"')
    if first < 0 or last <= first:
        return None
    return value[first + 1:last].replace('\\n', '\n')


# Lines that cannot be read (no text in quotes): counted as errors by check()
BAD_LINES = []


def pairs(code, given=None):
    result, english = [], None
    # utf-8-sig: a file saved with a BOM (as by Notepad) is read as one without
    for number, line in enumerate(io.open(language_path(code, given), encoding='utf-8-sig').read().split('\n'), 1):
        if line.startswith('en:'):
            english = unquote(line[3:])
            if english is None:
                BAD_LINES.append(f'line {number}: no text in quotes')
        elif english is not None and line.startswith(code + ':'):
            translation = unquote(line[len(code) + 1:])
            if translation is None:
                BAD_LINES.append(f'line {number}: no translation in quotes')
            else:
                result.append((english, translation))
            english = None
    return result


def key(part):
    return part.split(' ')[0]


def check(code, given=None):
    errors, warnings = [], []
    for english, text in pairs(code, given):
        if text == '':
            errors.append('not translated: ' + english)
            continue
        if sorted(re.findall(r'\{\d+\}', english)) != sorted(re.findall(r'\{\d+\}', text)):
            errors.append('placeholders: ' + english + '  ->  ' + text)
        if (len(english) - len(english.lstrip()), len(english) - len(english.rstrip())) != \
                (len(text) - len(text.lstrip()), len(text) - len(text.rstrip())):
            errors.append('blank space at start/end: "' + english + '"  ->  "' + text + '"')
        if ' | ' in english:
            e, t = english.split(' | '), text.split(' | ')
            if len(e) != len(t) or any(' ' in a and not key(a).startswith('/') and key(a) != key(b) for a, b in zip(e, t)):
                errors.append('key hints (each part starts with the same key): ' + english + '  ->  ' + text)
        bracket = r'\[(?:[A-Z]|Esc|Enter|Tab)\]'
        if re.findall(bracket, english) != re.findall(bracket, text):
            errors.append('keys in brackets: ' + english + '  ->  ' + text)
        if '\n' in english and any(len(line) > 79 for line in text.split('\n')):
            errors.append('a line longer than 79: ' + english[:40])
        if '\n' not in english and len(text) > max(len(english) * 1.35, len(english) + 8):
            warnings.append(f'long ({len(english)} -> {len(text)}): {english}  ->  {text}')
    errors = BAD_LINES + errors
    for e in errors:
        print('ERROR', e)
    for w in warnings:
        print('WARNING', w)
    print(f'{code}: {len(errors)} errors, {len(warnings)} warnings')


if __name__ == '__main__':
    # The texts are printed in UTF-8 (a Windows console uses an older code page by default)
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    if len(sys.argv) not in (2, 3):
        sys.exit(__doc__)
    check(sys.argv[1], sys.argv[2] if len(sys.argv) == 3 else None)
