<#
.SYNOPSIS
    Creates the desktop (and project folder) shortcuts that start and stop the API.

.DESCRIPTION
    Two shortcuts are written in two places - the current user's desktop and the
    project folder itself - plus two plain .cmd launchers next to the solution:

      - "Hamzet Wasl API"        : starts the API and opens the Swagger UI
      - "Hamzet Wasl API (stop)" : stops the running API

    The shortcuts call tools\start-api.ps1, which builds the project, starts the API
    on http://localhost:5153 in the background and opens http://localhost:5153/swagger.

    The shortcuts are created through the Unicode IShellLink interface: the WScript
    object converts the paths to the system ANSI code page, which destroys a project
    folder whose name is written in Arabic.

.PARAMETER ProjectRoot
    The repository folder (defaults to the parent of this script's folder).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\create-shortcuts.ps1
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    # The script lives in <project>\tools, so the project root is its parent folder.
    $scriptFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = Split-Path -Parent $scriptFolder
}

$launcher = Join-Path $ProjectRoot 'tools\start-api.ps1'
if (-not (Test-Path $launcher)) {
    throw "The launcher script was not found at $launcher."
}

# ── Unicode-safe shortcut writer ─────────────────────────────────────────────
if (-not ('HamzetShellLink' -as [type])) {
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLinkCoClass { }

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010B-0000-0000-C000-000000000046")]
internal interface IPersistFile
{
    void GetClassID(out Guid pClassID);
    [PreserveSig] int IsDirty();
    void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
    void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
    void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
}

public static class HamzetShellLink
{
    /// <summary>Creates a .lnk file. Every string is handled as Unicode.</summary>
    public static void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory, string description, string iconPath)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        link.SetPath(targetPath);
        link.SetArguments(arguments);
        link.SetWorkingDirectory(workingDirectory);
        link.SetDescription(description);
        if (!string.IsNullOrEmpty(iconPath))
        {
            link.SetIconLocation(iconPath, 0);
        }

        ((IPersistFile)link).Save(shortcutPath, true);
    }

    /// <summary>Reads the arguments of a .lnk file (used by the verification step).</summary>
    public static string ReadArguments(string shortcutPath)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        ((IPersistFile)link).Load(shortcutPath, 0);
        var buffer = new StringBuilder(1024);
        link.GetArguments(buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
'@
}

$icon = Join-Path $ProjectRoot 'src\HamzetWasl.Api\bin\Debug\net10.0\HamzetWasl.Api.exe'
$targets = @(
    [Environment]::GetFolderPath('Desktop'),
    $ProjectRoot
)

function New-ApiShortcut {
    param(
        [string]$Folder,
        [string]$Name,
        [string]$Arguments,
        [string]$Description
    )

    $destination = Join-Path $Folder "$Name.lnk"
    [HamzetShellLink]::Create($destination, 'powershell.exe', $Arguments, $ProjectRoot, $Description, $icon)
    Write-Host "created: $destination" -ForegroundColor Green
}

foreach ($folder in $targets) {
    if (-not (Test-Path $folder)) { continue }
    Write-Host "`nIn: $folder" -ForegroundColor Cyan

    New-ApiShortcut -Folder $folder -Name 'Hamzet Wasl API' `
        -Arguments "-NoProfile -ExecutionPolicy Bypass -File `"$launcher`"" `
        -Description 'Start the Hamzet Wasl backend and open the Swagger UI'

    New-ApiShortcut -Folder $folder -Name 'Hamzet Wasl API (stop)' `
        -Arguments "-NoProfile -ExecutionPolicy Bypass -File `"$launcher`" -Stop" `
        -Description 'Stop the running Hamzet Wasl backend'
}

# A plain .cmd launcher next to the solution: it works everywhere (the path of the script
# is resolved by the command interpreter itself, so non-ASCII folders are safe).
$cmdStart = @"
@echo off
REM Starts the Hamzet Wasl API and opens the Swagger UI.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\start-api.ps1"
"@
$cmdStop = @"
@echo off
REM Stops the running Hamzet Wasl API.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\start-api.ps1" -Stop
"@
[System.IO.File]::WriteAllText((Join-Path $ProjectRoot 'Start Hamzet API.cmd'), $cmdStart, (New-Object System.Text.ASCIIEncoding))
[System.IO.File]::WriteAllText((Join-Path $ProjectRoot 'Stop Hamzet API.cmd'), $cmdStop, (New-Object System.Text.ASCIIEncoding))
Write-Host "`ncreated: $(Join-Path $ProjectRoot 'Start Hamzet API.cmd')" -ForegroundColor Green
Write-Host "created: $(Join-Path $ProjectRoot 'Stop Hamzet API.cmd')" -ForegroundColor Green

# ── Verification ─────────────────────────────────────────────────────────────
Write-Host ''
Write-Host 'Verification:' -ForegroundColor Cyan
foreach ($folder in $targets) {
    $shortcut = Join-Path $folder 'Hamzet Wasl API.lnk'
    if (-not (Test-Path $shortcut)) { continue }
    $arguments = [HamzetShellLink]::ReadArguments($shortcut)
    $scriptPath = [regex]::Match($arguments, '-File\s+"(?<p>[^"]+)"').Groups['p'].Value
    Write-Host ("  {0} -> script exists: {1}" -f $shortcut, (Test-Path -LiteralPath $scriptPath))
}

Write-Host ''
Write-Host 'Done. Double click "Hamzet Wasl API" (or "Start Hamzet API.cmd") to start the backend.' -ForegroundColor Cyan
