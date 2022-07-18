# Installing and running Marbles
Marbles is distributed as a framework-dependent application and does not include the .NET runtime and libraries. This means that the .NET framework (a free open source cross-platform framework) must be downloaded and installed separately on your system before you can run Marbles.

## Install .NET
Ensure that you have .NET 6/core installed on your system. If not you can get it here:

- [Install .NET on Windows](https://docs.microsoft.com/en-us/dotnet/core/install/windows?tabs=net60)
- [Install .NET on Linux](https://docs.microsoft.com/en-us/dotnet/core/install/linux)
- [Install .NET on Raspberry Pi](https://docs.microsoft.com/en-us/dotnet/iot/deployment)

### Download .NET for Windows and Linux (offline install)
- [Download the .NET Runtime 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)


## Download and run Marbles (Windows/Linux)

   1. Download: [Marbles1011.zip](https://github.com/artstisen/marbles/releases/download/v1.0.1.1/Marbles1011.zip) (73Kb)
   2. Unzip the _Marbles.dll_ and _Marbles.runtimeconfig.json_ into a directory of your choosing.
   3. Open your favorite terminal, navigate to the Marbles directory and type ```dotnet Marbles.dll``` (Linux is case sensitive)

You can always simplify the process and create an alias like "_mbls_" etc. to run Marbles with less typing.


### Download with Conhost executable (Windows)

The preferred terminal on Windows is the _Windows Terminal_. But if you prefer a Windows download that contains an executable that you can run directly, use the following option. The supplied Conhost executable is the same application used for the classic Windows console or cmd.exe. The Zip contains _Marbles.exe_, _Marbles.dll_ and _Marbles.runtimeconfig.json_ (The _Marbles.dll_ and _Marbles.runtimeconfig.json_ in this download are the same builds as the first download for Windows and Linux).

- Download: [Marbles1011-WinExe.zip](https://github.com/artstisen/marbles/releases/download/v1.0.1.1/Marbles1011-WinExe.zip) (145Kb)



## Migrating your notes from Ahoy to Marbles

If you are using the Ahoy note-taking application you can migrate your data to Marbles using the AhoyToMarbles migration tool.

### Specifications:

- Supported platforms: Windows and Linux
- Requirements: .NET 6/Core
- Code signing: SHA-256
- Software license: MIT
- Download: [AhoyToMarbles1012.zip](https://github.com/artstisen/marbles/releases/download/v1.0.1.1/AhoyToMarbles1012.zip) (18Kb)
- Download w. Conhost executable (Windows): [AhoyToMarbles1012-WinExe.zip](https://github.com/artstisen/marbles/releases/download/v1.0.1.1/AhoyToMarbles1012-WinExe.zip) (90Kb)
