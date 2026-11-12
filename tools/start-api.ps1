<#
.SYNOPSIS
    Starts the Hamzet Wasl API in the background and opens the Swagger UI.

.DESCRIPTION
    One double click (or one command) is enough to get a working backend:
      1. builds the API (unless -SkipBuild is passed),
      2. makes sure the local configuration file exists,
      3. starts the API in a detached process (the terminal returns immediately),
      4. waits until /health answers, then opens http://localhost:5153/swagger.

    The API runs on port 5153 on purpose: every document in the repository
    (README, API contract, frontend handoff) uses that URL.

.PARAMETER Stop
    Stops a running API instance and exits.

.PARAMETER NoBrowser
    Starts the API without opening the browser.

.PARAMETER SkipBuild
    Skips the build step (faster when nothing changed).

.PARAMETER Release
    Builds and runs the Release configuration instead of Debug.

.PARAMETER Port
    The port the API listens on (default 5153).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\start-api.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\start-api.ps1 -Stop
#>
[CmdletBinding()]
param(
    [switch]$Stop,
    [switch]$NoBrowser,
    [switch]$Build,
    [switch]$Release,
    [int]$Port = 5153
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root 'src\HamzetWasl.Api'
$configuration = if ($Release) { 'Release' } else { 'Debug' }
$output = Join-Path $apiProject "bin\$configuration\net10.0"
$exe = Join-Path $output 'HamzetWasl.Api.exe'
$pidFile = Join-Path $env:TEMP 'hamzet-wasl-api.pid'
$logFile = Join-Path $env:TEMP 'hamzet-wasl-api.log'
$errFile = Join-Path $env:TEMP 'hamzet-wasl-api.err.log'
$baseUrl = "http://localhost:$Port"

function Write-Ok { param([string]$Message) Write-Host "  [OK]   $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "  [i]    $Message" -ForegroundColor Cyan }
function Write-Warn { param([string]$Message) Write-Host "  [!]    $Message" -ForegroundColor Yellow }
function Write-Err { param([string]$Message) Write-Host "  [X]    $Message" -ForegroundColor Red }

function Get-RunningApi {
    $processes = Get-Process -Name 'HamzetWasl.Api' -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        if ($process.Path -and $process.Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $process
        }
    }
    return $null
}

function Stop-RunningApi {
    $running = Get-RunningApi
    if (-not $running) {
        Write-Warn 'No API instance of this project is running.'
    }
    else {
        Stop-Process -Id $running.Id -Force
        Write-Ok "Stopped the API (pid $($running.Id))."
    }
    if (Test-Path $pidFile) { Remove-Item $pidFile -Force }
}

# ── Stop mode ────────────────────────────────────────────────────────────────
if ($Stop) {
    Stop-RunningApi
    return
}

Write-Host ''
Write-Host '  Hamzet Wasl - API launcher' -ForegroundColor White
Write-Host '  ---------------------------' -ForegroundColor DarkGray
Write-Info "Project root : $root"
Write-Info "URL          : $baseUrl"
Write-Host ''

# ── 1) The local configuration file ──────────────────────────────────────────
$devSettings = Join-Path $apiProject 'appsettings.Development.json'
$example = Join-Path $apiProject 'appsettings.Development.example.json'
if (-not (Test-Path $devSettings)) {
    if (Test-Path $example) {
        Copy-Item $example $devSettings
        Write-Ok 'Created appsettings.Development.json from the example template.'
        Write-Warn 'Set your own Jwt/Storage/Otp keys and the admin password before any shared deployment.'
    }
    else {
        Write-Err 'appsettings.Development.json is missing and no example template was found.'
        exit 1
    }
}

# ── 2) Already running? then just open the browser ──────────────────────────
$running = Get-RunningApi
if ($running) {
    Write-Ok "The API is already running (pid $($running.Id))."
    if (-not $NoBrowser) {
        Start-Process "$baseUrl/swagger"
        Write-Ok 'Opened the Swagger UI.'
    }
    return
}

# ── 3) The database schema ───────────────────────────────────────────────────
# An existing database may still miss the columns/tables of the last two migrations.
# The scripts are idempotent, so they are applied automatically when something is missing.
function Get-ConnectionSettings {
    $settingsPath = Join-Path $apiProject 'appsettings.json'
    if (-not (Test-Path $settingsPath)) { return $null }
    try {
        $raw = [System.IO.File]::ReadAllText($settingsPath, [System.Text.Encoding]::UTF8)
        $json = $raw | ConvertFrom-Json
        return $json.ConnectionStrings.DefaultConnection
    }
    catch {
        return $null
    }
}

function Get-SqlCmdArguments {
    param([string]$ConnectionString)

    $server = if ($ConnectionString -match '(?i)(?:Server|Data Source)\s*=\s*([^;]+)') { $Matches[1].Trim() } else { $null }
    $database = if ($ConnectionString -match '(?i)(?:Database|Initial Catalog)\s*=\s*([^;]+)') { $Matches[1].Trim() } else { $null }
    if (-not $server -or -not $database) { return $null }

    $trusted = $ConnectionString -match '(?i)(Integrated Security|Trusted_Connection)\s*=\s*(True|Yes|1)'
    if (-not $trusted) { return $null }   # a password is never passed on the command line

    return @('-S', $server, '-d', $database)
}

$connection = Get-ConnectionSettings
$sqlArguments = Get-SqlCmdArguments -ConnectionString $connection
if ($sqlArguments -and (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    $probe = & sqlcmd @sqlArguments -h -1 -W -b -Q "SELECT CASE WHEN OBJECT_ID('dbo.GOVERNORATE','U') IS NULL OR OBJECT_ID('dbo.AREA','U') IS NULL OR COL_LENGTH('dbo.VERIFICATION_REVIEW','created_at') IS NULL THEN 'MISSING' ELSE 'OK' END" 2>$null
    if ($probe -match 'MISSING') {
        Write-Warn 'The database is missing the objects of the last two migrations - applying them now.'
        foreach ($migration in @('Hamzet_Wasl_Migration_v2_1_review_created_at.sql', 'Hamzet_Wasl_Migration_v2_2_geo_tables.sql')) {
            $script = Join-Path $root $migration
            if (-not (Test-Path $script)) { continue }
            & sqlcmd @sqlArguments -b -i $script | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Err "The migration $migration failed (exit $LASTEXITCODE)."
                Write-Host ''
                Write-Host '  Press Enter to close this window.' -ForegroundColor DarkGray
                [void](Read-Host)
                exit 1
            }
            Write-Ok "Applied $migration"
        }
    }
}
elseif ($connection -and -not $sqlArguments) {
    Write-Info 'The database uses SQL authentication: apply the migrations manually if the API complains.'
}

# ── 4) Build (only on request, or when the executable does not exist yet) ────
# The build is opt-in on purpose: a fresh build produces new unsigned assemblies and
# Windows Smart App Control refuses to load them (0x800711C7). The existing build is
# used as it is, so the API starts immediately.
$needsBuild = $Build -or -not (Test-Path $exe)
if ($needsBuild) {
    if (-not $Build) {
        Write-Info 'No build found - building it now (this may take a moment).'
    }
    else {
        Write-Info "Building ($configuration) ..."
    }
    & dotnet build (Join-Path $apiProject 'HamzetWasl.Api.csproj') -c $configuration -v quiet --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Err 'The build failed - see the output above.'
        Write-Host ''
        Write-Host '  Press Enter to close this window.' -ForegroundColor DarkGray
        [void](Read-Host)
        exit 1
    }
    Write-Ok 'Build finished (0 warnings).'
}
else {
    Write-Info "Using the existing build ($configuration)."
}

if (-not (Test-Path $exe)) {
    Write-Err "The API executable was not found at $exe."
    exit 1
}

# ── 4) Start the API in the background ───────────────────────────────────────
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $baseUrl

Write-Info 'Starting the API ...'
try {
    $process = Start-Process -FilePath $exe `
        -WorkingDirectory $output `
        -WindowStyle Minimized `
        -RedirectStandardOutput $logFile `
        -RedirectStandardError $errFile `
        -PassThru
}
catch {
    Write-Err "The API could not be started: $($_.Exception.Message)"
    if ($_.Exception.Message -match '0x800711C7' -or $_.Exception.Message -match 'Application Control') {
        Write-Warn 'Windows Smart App Control blocked the freshly built assembly.'
        Write-Warn 'Turn it off: Windows Security -> Device performance & security -> Smart App Control -> Off -> Restart.'
        Write-Warn 'Then run this launcher again (it will rebuild and start the API).'
    }
    Write-Host ''
    Write-Host '  Press Enter to close this window.' -ForegroundColor DarkGray
    [void](Read-Host)
    exit 1
}
$process.Id | Out-File -FilePath $pidFile -Encoding ascii

# ── 5) Wait for the health endpoint ──────────────────────────────────────────
$ready = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    Start-Sleep -Milliseconds 500
    if ($process.HasExited) { break }
    try {
        $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) { $ready = $true; break }
    }
    catch {
        # not up yet
    }
}

Write-Host ''
if (-not $ready) {
    Write-Err 'The API did not answer on /health.'
    if (Test-Path $errFile) {
        Write-Host ''
        Write-Host '  Last lines of the error log:' -ForegroundColor DarkGray
        Get-Content $errFile -Tail 12 | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
        if ((Get-Content $errFile -Raw) -match '0x800711C7') {
            Write-Warn 'Windows Smart App Control blocked the freshly built assembly.'
            Write-Warn 'Turn it off: Windows Security -> Device performance & security -> Smart App Control -> Off -> Restart.'
        }
    }
    Write-Host ''
    Write-Host '  Press Enter to close this window.' -ForegroundColor DarkGray
    [void](Read-Host)
    exit 1
}

Write-Ok "The API is up (pid $($process.Id))."
Write-Info "Log file : $logFile"
Write-Host ''
Write-Host '  Swagger UI      : ' -NoNewline; Write-Host "$baseUrl/swagger" -ForegroundColor Yellow
Write-Host '  OpenAPI JSON    : ' -NoNewline; Write-Host "$baseUrl/swagger/v1/swagger.json" -ForegroundColor Yellow
Write-Host '  Health          : ' -NoNewline; Write-Host "$baseUrl/health" -ForegroundColor Yellow
Write-Host ''

if (-not $NoBrowser) {
    Start-Process "$baseUrl/swagger"
    Write-Ok 'Opened the Swagger UI in your browser.'
}

Write-Host '  To stop the API : powershell -ExecutionPolicy Bypass -File .\tools\start-api.ps1 -Stop' -ForegroundColor DarkGray
Write-Host ''
