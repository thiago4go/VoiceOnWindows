$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$localDotnet = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"

if (Test-Path $localDotnet) {
    & $localDotnet publish $PSScriptRoot -c Release
    exit $LASTEXITCODE
}

dotnet publish $PSScriptRoot -c Release
