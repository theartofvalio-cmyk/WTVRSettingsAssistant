param(
    [string]$OutputPath = 'D:\_Software\WTVRAssistant\Builds\WTAssistant'
)

$ErrorActionPreference = 'Stop'

$workspace = $PSScriptRoot
$project = Join-Path $workspace 'WTAssistant\WTVRSettingsAssistant.csproj'
$stage = Join-Path $workspace ('artifacts\manual-clean-publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$preserve = @('Settings', 'GraphicSettings', 'ControlSettings', 'Diagnostics')

New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$outputResolved = (Resolve-Path -LiteralPath $OutputPath).Path

dotnet publish $project `
    -c Release `
    -p:Platform=x86 `
    -r win-x86 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $stage

$allowed = @{}
Get-ChildItem -LiteralPath $stage -Force | ForEach-Object { $allowed[$_.Name] = $true }
foreach ($name in $preserve) { $allowed[$name] = $true }

$archive = Join-Path $outputResolved 'System\PreviousRuntimeFiles'
New-Item -ItemType Directory -Path $archive -Force | Out-Null
$allowed['System'] = $true

Get-ChildItem -LiteralPath $outputResolved -Force | ForEach-Object {
    if ($allowed.ContainsKey($_.Name)) { return }
    $source = (Resolve-Path -LiteralPath $_.FullName).Path
    if (-not $source.StartsWith($outputResolved + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to move outside output folder: $source"
    }
    $destination = Join-Path $archive $_.Name
    if (Test-Path -LiteralPath $destination) {
        $destination = Join-Path $archive ($_.BaseName + '-' + (Get-Date -Format 'yyyyMMddHHmmss') + $_.Extension)
    }
    Move-Item -LiteralPath $source -Destination $destination -Force
}

Get-ChildItem -LiteralPath $stage -Force | ForEach-Object {
    if ($preserve -contains $_.Name) { return }
    $destination = Join-Path $outputResolved $_.Name
    if ($_.PSIsContainer) {
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Get-ChildItem -LiteralPath $_.FullName -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
        }
    } else {
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
}

Write-Host "Clean publish complete:"
Write-Host $outputResolved
Get-ChildItem -LiteralPath $outputResolved -Force | Sort-Object Name | Select-Object Mode, Name, Length | Format-Table -AutoSize
