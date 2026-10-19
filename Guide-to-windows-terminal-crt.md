# Creating an alias for quick starting Marbles

In this example I have installed Marbles (by unzipping the build for Windows, _Marbles.exe_ and the folder _lang_) to a "_Marbles_" folder inside the "**Documents**" directory. I use the cross-platform edition of [PowerShell](https://aka.ms/powershell) as my default shell in Windows Terminal and will have to interact with PowerShell to start Marbles.

Normally you would navigate to the directory where _Marbles.exe_ is located and start it in PowerShell (with the portable version: `dotnet Marbles.dll`). To make it easier we can just create an alias that will enable us to start Marbles regardless of our current path in PowerShell, using a single word like "_mbls_" or whatever you prefer to name it.


## Create a custom profile for PowerShell

To persist aliases in PowerShell between restarts we need to save them to a custom PowerShell profile. Start by creating a "_PowerShell_" folder in your "**Documents**" directory to hold the profile "_Microsoft.PowerShell_profile.ps1_". Just create a new .txt file and rename it to "_Microsoft.PowerShell_profile.ps1_". PowerShell will automatically detect the file and read it upon startup.


### Create an alias for Marbles

Now open the .ps1 file in notepad etc. and add the following script:

```
# -=-=-=-=-=-=-=- Environment -=-=-=-=-=-=-=-
# Create a variable to hold the path to the user root
$psUser = Resolve-Path ~

# -=-=-=-=-=-=-=- Marbles -=-=-=-=-=-=-=-
# Create a variable to hold the path to the working directory
$marblesDir = "\Documents\Marbles"

# Create a function that starts Marbles, with what is typed after the alias ("mbls ." shows the
# current directory in the file browser, "mbls notes.md" opens the file in the editor)
Function fnc-marbles {& "$psUser$marblesDir\Marbles.exe" @args}
# (with the portable version: {dotnet "$psUser$marblesDir\Marbles.dll" @args})
# Marbles keeps its notes and settings in the folder "data" next to the program, wherever it is started from.
# If the directory you start it in has a "data" folder of its own, that one is used instead
# (this can be used intentionally to create multiple configurations)

# Create an alias to call the function
New-Alias -Name mbls -Value fnc-marbles

# Create an alias to open the marbles directory (optional)
New-Alias -Name marbles -Value fnc-marblesop
# The ii is short for Invoke-Item, and the dot is the current directory.
Function fnc-marblesop {ii $psUser$marblesDir}
```

Now you can start Marbles by typing "**mbls**" and open the marbles directory in Windows Explorer by typing "**marbles**".
You can always modify the PowerShell script to encompass multiple copies of Marbles etc.

### Create a profile alias (optional)

If you want to be able to quickly edit your profile by typing "**ps1**" copy the following lines to the .ps1 file:

```
# -=-=-=-=-=-=-=- PowerShell -=-=-=-=-=-=-=-
# Create variable to hold the path to the Powershell profile folder
$psDir = "\Documents\PowerShell"
# Create a function to open the file (this file)
Function fnc-ps1 {ii $psUser$psDir\Microsoft.PowerShell_profile.ps1}
# Create an alias to call the function
New-Alias -Name ps1 -Value fnc-ps1
```
Remember to initialize the ```$psUser``` variable as described in the top of the article.
TIP: You can display your absolute profile path (including your user name) by typing ```$PROFILE``` in PowerShell.

## Creating a custom help text with your changes (optional)

Finally you can create a simple help text to display your new commands by creating a "_myhelp.txt_" text file within your "PowerShell" directory with the following content:

```
*** Custom help ***

ps1 = Open PowerShell settings file
wtedit = Open Windows Terminal settings file
mbls = Run Marbles
marbles = Open Marbles directory
```

-And inserting the following script into your PowerShell profile:

```
# -=-=-=-=-=-=-=- HELP -=-=-=-=-=-=-=-
# Read content of the myhelp.txt file inside the psDir
Function fnc-myHelp {Get-Content $psUser$psDir\myhelp.txt}
New-Alias -Name myhelp -Value fnc-myHelp
```
Now use the alias "**myhelp**" to display the help text when you need to refresh your memory (PowerShell also has dedicated tags for adding help content directly to the PS help system).




# Adding the CRT mod to Windows Terminal

To install the mod we need to add a few settings to the Windows Terminal settings file. This guide will take you through the steps.

## Setting startup behaviour for Windows Terminal

Open the WT (Windows Terminal) settings file found in:

```C:\Users\User\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json```

(remember to replace "_User_" with your own user)

You can also open the file directly from WT by pressing ```CTRL+,``` and clicking the "Open JSON file" found in the bottom left corner of the settings window.

**TIP:** Create an alias for opening the WT settings file through PowerShell by adding the following script to the .ps1 file (as described in detail in the start of this article):
```
# -=-=-=-=-=-=-=- Windows Terminal  -=-=-=-=-=-=-=-
# Create variable to hold the path of the WT settings file
$wtDir = "\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\"
# Create a function to open the settings file
Function fnc-wt1 {ii $psUser$wtDir\settings.json}
# Create an alias to call the function
New-Alias -Name wtedit -Value fnc-wt1
```
Remember to initialize the ```$psUser``` variable as described in the top of the article.

Typing "**wtedit"** will now open the settings.json in your preferred editor.


Insert the following lines in below the ```"defaultProfile": "{GUID}",``` in the WT settings file (see full example further down):

```
"centerOnLaunch": true,
"initialCols": 88,
"initialRows": 55,
```

## Setting the cursor in Windows Terminal

Create a "_WT_" folder inside your "**Documents**" directory (for the shader effect below), and insert the following lines into the WT settings file, inside the ```profiles```, ```list``` array:

```
"cursorColor": "#61D6D6",
"cursorShape": "filledBox",
```

## Referencing the CRT shader effect and font

Download the [oldschool font pack](https://int10h.org/oldschool-pc-fonts/) and extract the _AcPlus_IBM_EGA_9x8.ttf_ font file and drag it to your Fonts folder within your control panel to install it on your system. The font is named "AcPlus IBM EGA 9x8" on your system.
Download the [shader effect crt.hlsl](https://github.com/Hammster/windows-terminal-shaders) and place the _crt.hlsl_ file within the "_WT_" folder created earlier.

Insert the following lines inside the ```profiles```, ```list``` array:

```
"experimental.pixelShaderPath": "C:\\Users\\User\\Documents\\WT\\crt.hlsl",
"experimental.retroTerminalEffect": true,
"font": 
{
    "face": "AcPlus IBM EGA 9x8",
    "size": 18
},
```

## Add command to toggle shader effect on/off

In the ```Actions``` section add:

```
{
    "command": "toggleShaderEffects",
    "keys": "shift+f10"
},
```

Your WT settings.json file should look something like this with the new lines added:
```
 {
    "$help": "https://aka.ms/terminal-documentation",
    "$schema": "https://aka.ms/terminal-profiles-schema",
    "actions": 
    [
        {
            "command": "toggleShaderEffects",
            "keys": "shift+f10"
        },
        {
            "command": 
            {
                "action": "copy",
                "singleLine": false
            },
            "keys": "ctrl+c"
        },
        {
            "command": "paste",
            "keys": "ctrl+v"
        },
        {
            "command": "toggleFocusMode",
            "keys": "shift+f11"
        },
        {
            "command": "find",
            "keys": "ctrl+shift+f"
        },
        {
            "command": 
            {
                "action": "splitPane",
                "split": "auto",
                "splitMode": "duplicate"
            },
            "keys": "alt+shift+d"
        }
    ],
 	"centerOnLaunch": true,
    "copyFormatting": "none",
    "copyOnSelect": false,
    "defaultProfile": "{GUID}",
    "initialCols": 88,
    "initialRows": 55,
    "language": "en-US",
    "launchMode": "default",
    "profiles": 
    {
        "defaults": 
        {
            "startingDirectory": "%USERPROFILE%/Documents/"
        },
        "list": 
        [
            {
                "cursorColor": "#61D6D6",
                "cursorShape": "filledBox",
                "experimental.pixelShaderPath": "C:\\Users\\User\\Documents\\WT\\crt.hlsl",
                "experimental.retroTerminalEffect": true,
                "font": 
                {
                    "face": "AcPlus IBM EGA 9x8",
                    "size": 18
                },
                "guid": "{GUID}",
                "hidden": false,
                "name": "PowerShell",
                "padding": "5, 5, 5, 5",
                "source": "Windows.Terminal.PowershellCore",
                "tabTitle": "Command Prompt"
            },
            ...
```
(the last lines where removed to save you from unnecessary scrolling)

## Adjust the Marbles settings

Start Windows Terminal and type "**mbls**" to start Marbles.
On the start page, open the **Configuration** tab and go to the **Layout** page: turn off _Automatic size_, set _Size_ to 88 x 54, and turn on _Classic ASCII corners_. The changes are saved at once.

You can also edit the settings file instead (**Configuration**, **System**, _settings.ini_), with these lines:

```
linewidth=88
linecount=54
autosize=false
asciistyle=1
```

When the file is saved, Marbles restarts with the new settings.
This completes the configuration.


**PS:** Try using ```SHIFT+F11``` to toggle focus mode on/off, this will give you a borderless terminal window which compliments the CRT mod. 
