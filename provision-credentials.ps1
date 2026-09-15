# provision-credentials.ps1
#
# Reads fde-credentials.env (simple KEY=VALUE lines, one per line) and
# pushes each value into GitHub Secrets or Variables via gh CLI, into
# the repo setup.bat just created.
#
# Based on the new-pack ("fde-participant-pack") version, with two
# merge-blocking gaps fixed against the source of truth (.github/workflows/cd.yml):
#   1. FDE_GATEWAY_BASE_URL was missing — cd.yml's gateway-reachability
#      step reads vars.FDE_GATEWAY_BASE_URL. Added to variableKeys.
#   2. FDE_SP_CLIENT_SECRET was missing — cd.yml's azure/login@v2 step
#      reads secrets.FDE_SP_CLIENT_SECRET. Added to secretKeys.
#
# Deliberately uses IndexOf/Substring rather than PowerShell's -split
# operator with a limit parameter — the exact split-with-limit syntax is
# the one thing here without an execution environment to confirm, so this
# uses plain .NET string methods instead, which are unambiguous.

$envFile = "fde-credentials.env"
if (-not (Test-Path $envFile)) {
    Write-Error "fde-credentials.env not found."
    exit 1
}

$values = @{}
Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#")) {
        $idx = $line.IndexOf("=")
        if ($idx -gt 0) {
            $key = $line.Substring(0, $idx).Trim()
            $val = $line.Substring($idx + 1).Trim()
            $values[$key] = $val
        }
    }
}

# Explicit lists, not auto-detected from naming convention — matches the
# explicit, readable style of the rest of this setup, and avoids
# accidentally treating a sensitive value as non-secret because of a
# naming mismatch. Must stay in sync with what .github/workflows/cd.yml
# actually references (secrets.* / vars.*).
$secretKeys = @(
    "FDE_SP_CLIENT_ID",
    "FDE_SP_CLIENT_SECRET",
    "FDE_SP_TENANT_ID",
    "FDE_SP_SUBSCRIPTION_ID",
    "FDE_AGENT_GATEWAY_ENDPOINT",
    "FDE_AGENT_GATEWAY_KEY",
    "FDE_LANGFUSE_PUBLIC_KEY",
    "FDE_LANGFUSE_SECRET_KEY",
    "FDE_PARTICIPANT_GHCR_PAT"
)

$variableKeys = @(
    "FDE_PARTICIPANT_ID",
    "FDE_POD_ID",
    "FDE_EVENT_ID",
    "FDE_NUMERIC_ID",
    "FDE_GATEWAY_BASE_URL",
    "FDE_LANGFUSE_BASE_URL",
    "FDE_RESOURCE_GROUP",
    "FDE_CONTAINERAPP_NAME"
)

foreach ($key in $secretKeys) {
    if (-not $values.ContainsKey($key)) {
        Write-Error "Missing required value in fde-credentials.env: $key"
        exit 1
    }
    Write-Host "Setting secret: $key"
    gh secret set $key --body $values[$key]
}

foreach ($key in $variableKeys) {
    if (-not $values.ContainsKey($key)) {
        Write-Error "Missing required value in fde-credentials.env: $key"
        exit 1
    }
    Write-Host "Setting variable: $key"
    gh variable set $key --body $values[$key]
}

Write-Host ""
Write-Host "All Secrets and Variables provisioned."