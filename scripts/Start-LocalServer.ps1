[CmdletBinding()]
param(
    [int] $Port = 5282
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/PbHtmlBuilder.Host/PbHtmlBuilder.Host.csproj'
$url = "http://localhost:$Port"

Write-Host "Starting PbHtmlBuilder at $url"
dotnet run --project $project --urls $url
