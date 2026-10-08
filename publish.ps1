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

foreach ($platform in $Platforms) {
    Write-Host "Publishing $platform..."
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
