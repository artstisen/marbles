Marbles language file pack
==========================

English is built into Marbles. The other languages are the text files in the folder "lang":

  da.txt  Dansk (Danish)            fr.txt  Français (French)
  de.txt  Deutsch (German)          it.txt  Italiano (Italian)
  nb.txt  Norsk bokmål (Norwegian)  pl.txt  Polski (Polish)
  sv.txt  Svenska (Swedish)         nl.txt  Nederlands (Dutch)
  es.txt  Español (Spanish)         fi.txt  Suomi (Finnish)

They come with Marbles, in the folder "lang" next to the program. This pack is for when that folder is missing, or
when you want to change a translation or make a new one.

To use them
-----------
Put the folder "lang" next to the Marbles program (or in the data directory of Marbles, which is used first), and
choose the language on the start page (Configuration > General > Language) or with ":language da" in the main menu.

To change a translation
-----------------------
Edit the file. Each text is written in English, with the translation below it:

  en: "File deleted"
  pl: "Plik usunięty"

A translation left empty is shown in English. Keep the placeholders ({0}), the keys and the commands as they are.
The list of languages is read again each time it is opened, so a change can be tried without restarting Marbles.

To add a language
-----------------
Copy one of the files, name it by the code of the language (pt.txt, cs.txt, pt-br.txt), change its first line to
name it ("# Marbles: Português (pt)") and the code in front of each translation ("pt:"), and translate the texts.

The files are UTF-8 text. See Guide-to-installation.md (Language files) for more.
