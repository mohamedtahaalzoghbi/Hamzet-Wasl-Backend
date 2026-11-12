<#
.SYNOPSIS
    Creates the desktop shortcuts that start (and stop) the Hamzet Wasl API.

.DESCRIPTION
    Two shortcuts are written to the current user's desktop:
      - "Hamzet Wasl API"        : starts the API and opens the Swagger UI
      - "Hamzet Wasl API (stop)" : stops the running API

    The shortcuts call tools\start-api.ps1, which builds the project, starts the API
    on http://localhost:5153 in the background and opens http://localhost:5153/swagger.

.PARAMETER ProjectRoot
    The repository folder (defaults to the parent of this script's folder).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\create-shortcuts.ps1
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $ProjectRoot 'tools\start-api.ps1'
if (-not (Test-Path $launcher)) {
    throw "The launcher script was not found at $launcher."
}

$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell

$icon = Join-Path $ProjectRoot 'src\HamzetWasl.Api\bin\Debug\net10.0\HamzetWasl.Api.exe'

function New-ApiShortcut {
    param(
        [string]$Name,
        [string]$Arguments,
        [string]$Description
    )

    $path = Join-Path $desktop "$Name.lnk"
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = 'powershell.exe'
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $ProjectRoot
    $shortcut.Description = $Description
    $shortcut.WindowStyle = 1
    if (Test-Path $icon) {
        $shortcut.IconLocation = "$icon,0"
    }
    $shortcut.Save()
    Write-Host "created: $path" -ForegroundColor Green
}

New-ApiShortcut -Name 'Hamzet Wasl API' `
    -Arguments "-NoProfile -ExecutionPolicy Bypass -File `"$launcher`"" `
    -Description 'Start the Hamzet Wasl backend and open the Swagger UI'

New-ApiShortcut -Name 'Hamzet Wasl API (stop)' `
    -Arguments "-NoProfile -ExecutionPolicy Bypass -File `"$launcher`" -Stop" `
    -Description 'Stop the running Hamzet Wasl backend'

Write-Host ''
Write-Host 'Done. Double click "Hamzet Wasl API" to start the backend and open Swagger.' -ForegroundColor Cyan
