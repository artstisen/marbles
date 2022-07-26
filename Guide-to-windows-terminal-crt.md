


# Creating an alias for quick starting Marbles

In this example I have installed Marbles (by copy/pasting _Marbles.dll_ and _Marbles.runtimeconfig.json_) to a "_Marbles_" folder inside the "**Documents**" directory. I use the cross-platform edition of [PowerShell](https://aka.ms/powershell) as my default shell in Windows Terminal and will have to interact with PowerShell to start Marbles.

Normally you would navigate to the directory where the _Marbles.dll_ is located and call the the "_dotnet run_" command in PowerShell. To make it easier we can just create an alias that will enable us to start Marbles regardless of our current path in PowerShell, using a single word like "_mbls_" or whatever you prefer to name it.


## Create a custom profile for PowerShell

To persist aliases in PowerShell between restarts we need to save them to a custom PowerShell profile. Start by creating a "_PowerShell_" folder in your "**Documents**" directory to hold the profile "_Microsoft.PowerShell_profile.ps1_". Just create a new .txt file and rename it to "_Microsoft.PowerShell_profile.ps1_". PowerShell will automatically detect the file and read it upon startup.


### Create an alias for Marbles

Now open the .ps1 file in notepad etc. and add the following script:

```
# -=-=-=-=-=-=-=- Marbles -=-=-=-=-=-=-=-
# Create a variable to hold the path to the working directory
$marblesDir = "C:\Users\User\Documents\Marbles"

# Create a function that will change current directory using $marblesDir and start Marbles
Function fnc-marbles {cd $marblesDir; dotnet Marbles.dll}
# If we don't change directory the application will be started in the current directory which may not
# be the Marbles directory. Marbles always checks for the "data" folder inside its executing directory.
# If no data folder is found in the executing directory Marbles will create it and place its settings.ini inside it.
# (this can be used intentionally to create multiple configurations)

# Create an alias to call the function
New-Alias -Name mbls -Value fnc-marbles

# Create an alias to open the marbles directory (optional)
New-Alias -Name marbles -Value fnc-marblesop
# The ii is short for Invoke-Item, and the dot is the current directory.
Function fnc-marblesop {ii $marblesDir}
```
(remember to replace "_User_" with your own user)

Now you can start Marbles by typing "**mbls**" and open the marbles directory in Windows Explorer by typing "**marbles**".
You can always modify the PowerShell script to encompass multiple copies of Marbles etc.

### Create a profile alias (optional)

If you want to be able to quickly edit your profile by typing "**ps1**" copy the following lines to the .ps1 file:

```
# -=-=-=-=-=-=-=- PowerShell -=-=-=-=-=-=-=-
# Create variable to hold the path to the Powershell profile folder
$psDir = "C:\Users\User\Documents\PowerShell"
# Create a function to open the file (this file)
Function fnc-ps1 {ii $psDir\Microsoft.PowerShell_profile.ps1}
# Create an alias to call the function
New-Alias -Name ps1 -Value fnc-ps1
```
(remember to replace "_User_" with your own user. You can display your absolute profile path (including your user name) by typing ```$PROFILE``` in PowerShell)

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
Function fnc-myHelp {Get-Content $psDir\myhelp.txt}
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
$wtDir = "C:\Users\User\AppData\Local\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\"
# Create a function to open the settings file
Function fnc-wt1 {ii $wtDir\settings.json}
# Create an alias to call the function
New-Alias -Name wtedit -Value fnc-wt1
```
(remember to replace "_User_" with your own user)
Typing "**wtedit"** will now open the settings.json in your preferred editor.


Insert the following lines in below the ```"defaultProfile": "{GUID}",``` in the WT settings file (see full example further down):

```
"centerOnLaunch": true,
"initialCols": 88,
"initialRows": 55,
```

## Setting a background image and cursor in Windows Terminal

Download the [CRT background image](wt-crt-bg1.jpg) and create a "_WT_" folder inside your "**Documents**" directory.

Insert the following lines into the WT settings file, inside the ```profiles```, ```list``` array:

```
"backgroundImage": "C:\\Users\\User\\Documents\\WindowsTerminal\\wt-crt-bg1.jpg",
"backgroundImageAlignment": "topLeft",
"backgroundImageOpacity": 0.6,
"backgroundImageStretchMode": "fill",
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
                "backgroundImage": "C:\\Users\\User\\Documents\\WT\\wt-crt-bg1.jpg",
                "backgroundImageAlignment": "topLeft",
                "backgroundImageOpacity": 0.6,
                "backgroundImageStretchMode": "fill",
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

Start Windows Terminal at type "**mbls**" to start Marbles.
Press ```CTRL+W, S``` from the login menu and update the following settings (same as editing the _settings.ini_ file within the Marbles directory) to accommodate the settings in WT:

```
linewidth=88
linecount=54
asciistyle=1
```

Press ```CTRL+Q``` to save and exit to the login menu. This will restart Marbles with the updated settings.
This completes the configuration.


**PS:** Try using ```SHIFT+F11``` to toggle focus mode on/off, this will give you a borderless terminal window which compliments the CRT mod. 





