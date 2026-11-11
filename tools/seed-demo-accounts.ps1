#Requires -Version 5.1
<#
.SYNOPSIS
    Creates the four non-admin demo accounts used by the frontend handoff.

.DESCRIPTION
    The application seeds the admin account only. This script registers the volunteer,
    association, company and disabled-user demo accounts through the public API, marks
    their email/phone as verified directly in the development database (the local email/SMS
    transports deliberately do not print OTP codes), and fills a small useful profile so the
    UI has data to render.

    Run it once after the API is started:
        powershell -ExecutionPolicy Bypass -File ./tools/seed-demo-accounts.ps1

    The default password is Demo@12345. Pass -Password to change it; the summary printed at
    the end always shows the password that was actually used.

.NOTES
    - Requires PowerShell 5.1+, the API running, and sqlcmd for the verification flags.
    - If sqlcmd is missing, the accounts are still created; only the verified flags are skipped.
    - The organizations intentionally stay in pending_approval so the onboarding/review
      screens can be demonstrated. Approve them through the admin flow when needed.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5153',
    [string]$SqlServer = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'HamzetWasl',
    [string]$Password = 'Demo@12345'
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

function Wait-Api {
    $deadline = (Get-Date).AddSeconds(60)
    do {
        try {
            $health = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 3
            if ($health.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    } while ((Get-Date) -lt $deadline)

    throw "The API is not reachable at $BaseUrl. Start it with: dotnet run --project src/HamzetWasl.Api"
}

function Invoke-Api {
    param(
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$Path,
        [object]$Body,
        [string]$Token
    )

    $headers = @{}
    if ($Token) {
        $headers['Authorization'] = "Bearer $Token"
    }

    $arguments = @{
        Method  = $Method
        Uri     = "$BaseUrl$Path"
        Headers = $headers
    }

    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 12 -Compress
        $arguments['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
        $arguments['ContentType'] = 'application/json; charset=utf-8'
    }

    try {
        $response = Invoke-RestMethod @arguments
        if ($null -ne $response -and $response.success -eq $false) {
            $code = $response.error.code
            $message = $response.error.message
            throw "API error ${code}: $message"
        }

        return $response.data
    }
    catch {
        $status = $null
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $status = [int]$_.Exception.Response.StatusCode
        }

        $detail = $_.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($detail)) {
            $detail = $_.Exception.Message
        }

        if ($status) {
            throw "HTTP $status calling $Method $Path`: $detail"
        }

        throw
    }
}

function Register-Or-Login {
    param(
        [Parameter(Mandatory)] [string]$Role,
        [Parameter(Mandatory)] [string]$Email,
        [Parameter(Mandatory)] [string]$Phone,
        [Parameter(Mandatory)] [string]$Name,
        [bool]$IsOrganization = $false
    )

    $registerBody = @{
        role            = $Role
        email           = $Email
        phone           = $Phone
        password        = $Password
        confirmPassword = $Password
        termsAccepted   = $true
        policyVersion   = '1.0'
    }

    if ($IsOrganization) {
        $registerBody['organizationName'] = $Name
    }
    else {
        $registerBody['name'] = $Name
    }

    try {
        return Invoke-Api -Method POST -Path '/api/v1/auth/register' -Body $registerBody
    }
    catch {
        $registerError = $_
        try {
            return Invoke-Api -Method POST -Path '/api/v1/auth/login' -Body @{
                emailOrPhone = $Email
                password     = $Password
            }
        }
        catch {
            throw $registerError
        }
    }
}

function Set-AccountVerified {
    param([Parameter(Mandatory)] [string]$Email)

    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Warning 'sqlcmd was not found; skipping the email/phone verified flags.'
        return $false
    }

    $sql = @"
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

UPDATE dbo.USER_ACCOUNT
SET email_verified = 1, phone_verified = 1
WHERE email = N'$Email';

UPDATE dbo.AspNetUsers
SET EmailConfirmed = 1, PhoneNumberConfirmed = 1
WHERE Email = N'$Email';
"@

    & sqlcmd -S $SqlServer -d $Database -b -Q $sql | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed while verifying $Email. Check -SqlServer and -Database."
    }

    return $true
}

function Set-DemoProfile {
    param(
        [Parameter(Mandatory)] [string]$Role,
        [Parameter(Mandatory)] [string]$Token,
        [Parameter(Mandatory)] [string]$Name
    )

    switch ($Role) {
        'volunteer' {
            Invoke-Api -Method PUT -Path '/api/v1/me/volunteer-profile' -Token $Token -Body @{
                introduction    = 'Demo volunteer ready for the frontend handoff.'
                helpMode        = 'online_and_field'
                skillLevel      = 'beginner'
                weeklyHours     = 5
                experienceNotes = 'Demo data created by tools/seed-demo-accounts.ps1.'
                skillIds        = @()
            } | Out-Null

            Invoke-Api -Method PUT -Path '/api/v1/me/volunteer-location' -Token $Token -Body @{
                governorate      = 'cairo'
                serviceArea      = 'nasr_city'
                latitude         = 30.05
                longitude        = 31.35
                serviceRadiusKm  = 10
            } | Out-Null
        }

        'disabled_user' {
            Invoke-Api -Method PUT -Path '/api/v1/me/accessibility' -Token $Token -Body @{
                needType         = 'visual'
                needTypes        = @('visual', 'hearing')
                theme            = 'system'
                fontScale        = 1.0
                reducedMotion    = $false
                interactionMode  = 'touch'
                notificationMode = 'in_app'
            } | Out-Null
        }

        { $_ -in @('association', 'company') } {
            $field = if ($Role -eq 'association') { 'education' } else { 'technology' }
            Invoke-Api -Method PUT -Path '/api/v1/me/organization' -Token $Token -Body @{
                name                = $Name
                field               = $field
                description         = 'Demo organization ready for the frontend handoff.'
                governorate         = 'cairo'
                city                = 'nasr_city'
                website             = 'https://example.org'
                employeeCountRange  = '11-50'
                contactInformation  = 'demo@example.org'
            } | Out-Null
        }
    }
}

$accounts = @(
    [pscustomobject]@{
        Role           = 'volunteer'
        Email          = 'demo.volunteer@hamzetwasl.local'
        Phone          = '+201000000001'
        Name           = 'Demo Volunteer'
        IsOrganization = $false
    },
    [pscustomobject]@{
        Role           = 'association'
        Email          = 'demo.association@hamzetwasl.local'
        Phone          = '+201000000002'
        Name           = 'Demo Association'
        IsOrganization = $true
    },
    [pscustomobject]@{
        Role           = 'company'
        Email          = 'demo.company@hamzetwasl.local'
        Phone          = '+201000000003'
        Name           = 'Demo Company'
        IsOrganization = $true
    },
    [pscustomobject]@{
        Role           = 'disabled_user'
        Email          = 'demo.disabled@hamzetwasl.local'
        Phone          = '+201000000004'
        Name           = 'Demo Disabled User'
        IsOrganization = $false
    }
)

Write-Host "Waiting for $BaseUrl ..." -ForegroundColor Cyan
Wait-Api
Write-Host 'API is ready.' -ForegroundColor Green

$results = @()
foreach ($account in $accounts) {
    Write-Host "Preparing $($account.Role) ..." -ForegroundColor Cyan

    $auth = Register-Or-Login `
        -Role $account.Role `
        -Email $account.Email `
        -Phone $account.Phone `
        -Name $account.Name `
        -IsOrganization $account.IsOrganization

    $verified = Set-AccountVerified -Email $account.Email
    Set-DemoProfile -Role $account.Role -Token $auth.accessToken -Name $account.Name

    $results += [pscustomobject]@{
        Role         = $account.Role
        Email        = $account.Email
        Password     = $Password
        AccountState = $auth.accountStatus
        Verified     = if ($verified) { 'yes' } else { 'not changed' }
    }
}

$admin = [pscustomobject]@{
    Role         = 'admin'
    Email        = 'admin@hamzetwasl.local'
    Password     = 'Admin@12345'
    AccountState = 'active (seeded)'
    Verified     = 'email verified by seed'
}

$all = @($admin) + $results
Write-Host ''
Write-Host 'Demo accounts ready:' -ForegroundColor Green
$all | Format-Table -AutoSize

Write-Host 'Organizations stay in pending_approval until the admin approves their submitted review.' -ForegroundColor Yellow
Write-Host 'The volunteer can book an interview only after email/phone verification and three evidence uploads.' -ForegroundColor Yellow
