"""Check a language file of Marbles: what LangTests checks, and texts that may be too long for the screen.

    python tools/checklang.py de

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


def unquote(value):
    first, last = value.index('"'), value.rindex('"')
    return value[first + 1:last].replace('\\n', '\n')


def pairs(code):
    result, english = [], None
    for line in io.open(os.path.join(ROOT, 'Lang', code + '.txt'), encoding='utf-8').read().split('\n'):
        if line.startswith('en:'):
            english = unquote(line[3:])
        elif english is not None and line.startswith(code + ':'):
            result.append((english, unquote(line[len(code) + 1:])))
            english = None
    return result


def key(part):
    return part.split(' ')[0]


def check(code):
    errors, warnings = [], []
    for english, text in pairs(code):
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
    for e in errors:
        print('ERROR', e)
    for w in warnings:
        print('WARNING', w)
    print(f'{code}: {len(errors)} errors, {len(warnings)} warnings')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    check(sys.argv[1])
