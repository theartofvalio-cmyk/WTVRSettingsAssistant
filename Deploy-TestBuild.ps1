param(
    [string]$Destination = 'D:\WTVRSettingsAssistant'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputRoot = Join-Path $projectRoot 'bin\Release\net10.0-windows'
$preservedFolders = @('Settings', 'GraphicSettings', 'ControlSettings')

dotnet build (Join-Path $projectRoot 'WTVRSettingsAssistant.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-Process -Name WTVRSettingsAssistant -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

Get-ChildItem -LiteralPath $outputRoot -Recurse -File -Force | ForEach-Object {
    $sourceFile = $_.FullName
    $relativePath = $sourceFile.Substring($outputRoot.Length).TrimStart('\', '/')
    $topFolder = ($relativePath -split '[\\/]', 2)[0]
    if ($preservedFolders -contains $topFolder) { return }

    $target = Join-Path $Destination $relativePath
    $targetFolder = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null
    try {
        Copy-Item -LiteralPath $sourceFile -Destination $target -Force
    }
    catch [System.IO.IOException] {
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw }
        $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
        $targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        if ($sourceHash -ne $targetHash) { throw }
        Write-Host "Kept identical locked file: $relativePath"
    }
}

$executable = Join-Path $Destination 'WTVRSettingsAssistant.exe'
Start-Process -FilePath $executable -WorkingDirectory $Destination
