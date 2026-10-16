# Builds the release versions of Marbles in the directory "publish":
#   portable    The lightweight version for all platforms. It needs the .NET 10 runtime to be installed,
#               and is started with:  dotnet Marbles.dll
#   win-x64 ... A self-contained single file for each platform. The .NET runtime is included in the file,
#               so nothing has to be installed to run it.
# Usage:  ./publish.ps1                     (all versions)
#         ./publish.ps1 portable,win-x64    (one or more versions)
param(
    [string[]]$Platforms = @("portable", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-arm", "osx-x64", "osx-arm64")
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "Marbles/Marbles.csproj"
$output = Join-Path $PSScriptRoot "publish"
$known = @("portable", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-arm", "osx-x64", "osx-arm64")

# "portable,win-x64" as one argument (as when started with pwsh -File) is a list too
$Platforms = @($Platforms | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($platform in $Platforms) {
    if ($known -notcontains $platform) { throw "Unknown version: '$platform' (known: $($known -join ', '))" }
}

# Each folder is emptied first, so files left from earlier versions are not released. A data folder (made when a
# version was tried there) holds data files and settings: it is never deleted, nothing is published. All folders
# are checked before any is emptied
foreach ($platform in $Platforms) {
    if (Test-Path (Join-Path $output $platform "data")) {
        throw "$(Join-Path $output $platform) contains a data folder: move it away first (nothing was deleted)"
    }
}

foreach ($platform in $Platforms) {
    Write-Host "Publishing $platform..."
    $folder = Join-Path $output $platform
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    if ($platform -eq "portable") {
        dotnet publish $project -c Release `
            -p:UseAppHost=false `
            -p:DebugType=none `
            -o (Join-Path $output $platform) --nologo -v quiet
    }
    else {
        dotnet publish $project -c Release -r $platform --self-contained true `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=none `
            -o (Join-Path $output $platform) --nologo -v quiet
    }
    if ($LASTEXITCODE -ne 0) { throw "Publishing $platform failed" }
}

Get-ChildItem $output -Recurse -File |
    Select-Object @{ n = "File"; e = { $_.FullName.Substring($output.Length + 1) } },
                  @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
