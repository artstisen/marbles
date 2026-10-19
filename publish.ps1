# Builds the release versions of Marbles in the directory "publish":
#   portable    The lightweight version for all platforms. It needs the .NET 10 runtime to be installed,
#               and is started with:  dotnet Marbles.dll
#   win-x64 ... A self-contained single file for each platform. The .NET runtime is included in the file,
#               so nothing has to be installed to run it.
#   languages   The language file pack: a zip with the language files (lang), the German file built into
#               Marbles (built-in) and a read-me, to be downloaded on its own
# Each version is also packed for a release in "publish/release", named by the version of Marbles: a zip for
# Windows and the portable version (Marbles-1.5.3-win-x64.zip), and a tar.gz for Linux and macOS
# (Marbles-1.5.3-linux-x64.tar.gz), where the program is marked as one that can be run. Each has a folder "Marbles"
# with the files in it. The language pack is Marbles-languages-1.5.3.zip
# Usage:  ./publish.ps1                     (all versions)
#         ./publish.ps1 portable,win-x64    (one or more versions)
param(
    [string[]]$Platforms = @("portable", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-arm", "osx-x64", "osx-arm64", "languages")
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "Marbles/Marbles.csproj"
$output = Join-Path $PSScriptRoot "publish"
$release = Join-Path $output "release"
$known = @("portable", "win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-arm", "osx-x64", "osx-arm64", "languages")

# "portable,win-x64" as one argument (as when started with pwsh -File) is a list too
$Platforms = @($Platforms | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($platform in $Platforms) {
    if ($known -notcontains $platform) { throw "Unknown version: '$platform' (known: $($known -join ', '))" }
}

# The version of Marbles (1.5.3), for the names of the archives
[xml]$csproj = Get-Content $project
$version = ($csproj.Project.PropertyGroup.FileVersion | Where-Object { $_ } | Select-Object -First 1) -replace '\.0$', ''

# Each folder is emptied first, so files left from earlier versions are not released. A data folder (made when a
# version was tried there) holds data files and settings: it is never deleted, nothing is published. All folders
# are checked before any is emptied
foreach ($platform in $Platforms) {
    if (Test-Path (Join-Path $output $platform "data")) {
        throw "$(Join-Path $output $platform) contains a data folder: move it away first (nothing was deleted)"
    }
}

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# Pack the files of a version for a release, in a folder "Marbles": a tar.gz for Linux and macOS (the program
# marked as one that can be run, which a zip made on Windows cannot tell), else a zip
function New-ReleaseArchive([string]$platform, [string]$folder) {
    New-Item -ItemType Directory -Force $release | Out-Null
    $files = Get-ChildItem $folder -Recurse -File
    if ($platform -like "linux-*" -or $platform -like "osx-*") {
        $archive = Join-Path $release "Marbles-$version-$platform.tar.gz"
        if (Test-Path $archive) { Remove-Item $archive }
        $stream = [System.IO.File]::Create($archive)
        try {
            $gzip = [System.IO.Compression.GZipStream]::new($stream, [System.IO.Compression.CompressionLevel]::Optimal)
            $tar = [System.Formats.Tar.TarWriter]::new($gzip, [System.Formats.Tar.TarEntryFormat]::Pax, $false)
            foreach ($file in $files) {
                $name = [System.IO.Path]::GetRelativePath($folder, $file.FullName).Replace('\', '/')
                $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, "Marbles/$name")
                # rwxr-xr-x for the program, rw-r--r-- for the others
                $entry.Mode = [System.IO.UnixFileMode]$(if ($name -eq "Marbles") { 493 } else { 420 })
                $entry.ModificationTime = [DateTimeOffset]$file.LastWriteTimeUtc
                $data = $file.OpenRead()
                try {
                    $entry.DataStream = $data
                    $tar.WriteEntry($entry)
                }
                finally { $data.Dispose() }
            }
            $tar.Dispose()
            $gzip.Dispose()
        }
        finally { $stream.Dispose() }
    }
    else {
        $archive = Join-Path $release "Marbles-$version-$platform.zip"
        if (Test-Path $archive) { Remove-Item $archive }
        $zip = [System.IO.Compression.ZipFile]::Open($archive, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in $files) {
                $name = [System.IO.Path]::GetRelativePath($folder, $file.FullName).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, "Marbles/$name",
                    [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
        finally { $zip.Dispose() }
    }
}

foreach ($platform in $Platforms) {
    Write-Host "Publishing $platform..."
    $folder = Join-Path $output $platform
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    if ($platform -eq "languages") {
        # The zip is named by the version of Marbles (Marbles-languages-1.5.3.zip)
        $lang = Join-Path $PSScriptRoot "Marbles/Lang"
        $staging = Join-Path ([System.IO.Path]::GetTempPath()) ("marbles-languages-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Force (Join-Path $staging "lang"), (Join-Path $staging "built-in") | Out-Null
        Copy-Item (Join-Path $lang "External/*.txt") (Join-Path $staging "lang")
        Copy-Item (Join-Path $lang "*.txt") (Join-Path $staging "built-in")
        Copy-Item (Join-Path $lang "Pack/README.txt") $staging
        New-Item -ItemType Directory -Force $folder | Out-Null
        $pack = Join-Path $folder "Marbles-languages-$version.zip"
        Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $pack
        Remove-Item -Recurse -Force $staging
        New-Item -ItemType Directory -Force $release | Out-Null
        Copy-Item $pack $release -Force
        continue
    }
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
    New-ReleaseArchive $platform $folder
}

Get-ChildItem $output -Recurse -File |
    Select-Object @{ n = "File"; e = { $_.FullName.Substring($output.Length + 1) } },
                  @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
