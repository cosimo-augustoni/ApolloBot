#Requires -Version 7.0
<#
.SYNOPSIS
    Renders the Aspire-generated compose file into a Swarm stack file with every value inlined.

.DESCRIPTION
    `docker stack deploy` does not read .env files, so this script replaces every ${NAME} and ${NAME:-default}
    placeholder in docker-compose.yaml with the value from the env file Aspire writes next to it
    (.env.<environment>, falling back to .env) and writes apollo.stack.yaml.

    If any placeholder has no value (absent or empty, without a default), the script lists the variable names,
    writes nothing and exits with 1, so a half-rendered stack can never be deployed.
    Values are never printed: they include passwords.

    Values are escaped for a double-quoted YAML scalar (Aspire quotes every value) and for the $ interpolation
    that `docker stack deploy` applies again. `$${NAME}` stays an escaped literal.

    See docs/deployment.md.

.PARAMETER OutputPath
    The Aspire output directory. Defaults to src/Apollo.AppHost/aspire-output, where aspire do writes without -o.

.PARAMETER Environment
    The Aspire environment whose env file is used. Defaults to Production, like aspire do.

.EXAMPLE
    ./deploy/Render-Stack.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputPath = (Join-Path $PSScriptRoot '..' 'src' 'Apollo.AppHost' 'aspire-output'),

    [string] $Environment = 'Production'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Fail([string] $message) {
    [Console]::Error.WriteLine("Render-Stack: $message")
    exit 1
}

$composePath = Join-Path $OutputPath 'docker-compose.yaml'
if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    Fail "docker-compose.yaml not found in '$OutputPath'. Run aspire do prepare-stack first."
}

# prepare-docker-compose writes the values to .env.<environment>; plain publish only writes .env as an empty template.
$envPath = Join-Path $OutputPath ".env.$Environment"
if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    $envPath = Join-Path $OutputPath '.env'
}
if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    Fail "Neither .env.$Environment nor .env found in '$OutputPath'. Run aspire do prepare-stack first."
}

$values = @{}
foreach ($line in Get-Content -LiteralPath $envPath) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
        $value = $Matches[2].Trim()
        if ($value.Length -ge 2 -and $value[0] -eq $value[-1] -and $value[0] -in '"', "'") {
            $value = $value.Substring(1, $value.Length - 2)
        }
        $values[$Matches[1]] = $value
    }
}

$missing = [System.Collections.Generic.SortedSet[string]]::new()
$placeholder = [regex] '(?<!\$)\$\{([A-Za-z_][A-Za-z0-9_]*)(?::-([^}]*))?\}'

$rendered = $placeholder.Replace((Get-Content -LiteralPath $composePath -Raw), {
    param($match)
    $name = $match.Groups[1].Value
    $value = $values[$name]
    if ([string]::IsNullOrEmpty($value)) {
        if ($match.Groups[2].Success) {
            $value = $match.Groups[2].Value
        }
        else {
            [void] $missing.Add($name)
            return $match.Value
        }
    }
    # YAML double-quoted scalar escapes, then $ -> $$ so docker stack deploy keeps it literal.
    return $value.Replace('\', '\\').Replace('"', '\"').Replace('$', '$$')
})

if ($missing.Count -gt 0) {
    Fail "No value for $($missing.Count) placeholder(s), nothing written: $($missing -join ', '). Set them as AppHost parameters (docs/deployment.md) and run aspire do prepare-stack again."
}

$stackPath = Join-Path $OutputPath 'apollo.stack.yaml'
Set-Content -LiteralPath $stackPath -Value $rendered -NoNewline -Encoding utf8NoBOM
Write-Host "Rendered $stackPath from $(Split-Path $envPath -Leaf)."
