#!/usr/bin/env pwsh
# TicketSpan Event Backend — local startup.
# Loads .env into the process environment, then runs the API with
# --no-launch-profile so Properties/launchSettings.json is ignored. This mirrors
# production, where the host (Render) injects the same keys as real env vars.
#
# Usage:
#   ./startup.ps1                # loads ./.env, runs the API
#   ./startup.ps1 -EnvFile .env.staging
#   ./startup.ps1 -Configuration Release

[CmdletBinding()]
param(
    [string]$EnvFile = ".env",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$envPath = Join-Path $root $EnvFile
$project = Join-Path $root "src/Api/Api.csproj"

if (-not (Test-Path $envPath)) {
    throw "Env file not found: $envPath (copy .env.example to .env)"
}

Write-Host "Loading environment from $envPath" -ForegroundColor Cyan
$loaded = 0
foreach ($line in Get-Content -Path $envPath) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#")) { continue }

    $idx = $trimmed.IndexOf("=")
    if ($idx -lt 1) { continue }

    $key = $trimmed.Substring(0, $idx).Trim()
    $value = $trimmed.Substring($idx + 1).Trim()
    if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'"))) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    Set-Item -Path "Env:$key" -Value $value
    $loaded++
}
Write-Host "Loaded $loaded variable(s)" -ForegroundColor Green

Write-Host "Starting API ($Configuration, --no-launch-profile)" -ForegroundColor Cyan
& dotnet run --project $project --configuration $Configuration --no-launch-profile
exit $LASTEXITCODE
