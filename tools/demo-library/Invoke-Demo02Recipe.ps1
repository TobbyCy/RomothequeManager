#requires -Version 5.1
<#
.SYNOPSIS
  DEMO-02 : générer et valider les trois bibliothèques fictives sur Windows.
.DESCRIPTION
  Le dossier OutputRoot doit être choisi explicitement, absolu, local et INEXISTANT.
  Le générateur DEMO-01 valide lui-même la destination et les contrats.
  Aucun scan de ROMothèque réelle ; seuls les nouveaux dossiers synthétiques sont utilisés.
.EXAMPLE
  .\tools\demo-library\Invoke-Demo02Recipe.ps1 -OutputRoot 'C:\Temp\Romotheque-Demo02-Nouveau'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputRoot,
    [switch]$SkipDotNetTests
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$generator = Join-Path $PSScriptRoot 'generate.cjs'
$testProject = Join-Path $repo 'tests\dotnet\RomothequeManager.V2.Tests\RomothequeManager.V2.Tests.csproj'

if (-not [System.IO.Path]::IsPathRooted($OutputRoot) -or
    $OutputRoot.StartsWith('\\') -or $OutputRoot.StartsWith('//') -or
    $OutputRoot.Contains('://')) {
    throw 'OutputRoot doit être un chemin local absolu explicitement choisi.'
}
if (Test-Path -LiteralPath $OutputRoot) {
    throw 'OutputRoot existe déjà : aucun écrasement ni nettoyage automatique.'
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js 20+ est requis.'
}
Write-Host '[DEMO02] Génération IDEAL / CHAOS / MYSTERY dans une nouvelle sortie synthétique...'
& node $generator --output $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'DEMO02_GENERATION_FAILED' }
& node $generator --verify $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'DEMO02_INTEGRITY_FAILED' }

$expected = @{
    IDEAL   = @(32, 30, 180)
    CHAOS   = @(33, 30, 166)
    MYSTERY = @(32,  0,   0)
}
foreach ($variant in @('IDEAL', 'CHAOS', 'MYSTERY')) {
    $manifestPath = Join-Path (Join-Path $OutputRoot $variant) 'manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $counts = @([int]$manifest.expected.romsPhysicalFiles,
                [int]$manifest.expected.esdeGameEntries,
                [int]$manifest.expected.mediaFiles)
    $wanted = $expected[$variant]
    for ($i=0; $i -lt 3; $i++) {
        if ($counts[$i] -ne $wanted[$i]) {
            throw ('DEMO02_ORACLE_MISMATCH: ' + $variant)
        }
    }
    if (@($manifest.games).Count -ne 30) {
        throw ('DEMO02_GAME_COUNT_MISMATCH: ' + $variant)
    }
    Write-Host ('[DEMO02] {0}: {1} ROMS / {2} XML / {3} PNG ; 30 jeux de référence' -f
        $variant, $counts[0], $counts[1], $counts[2])
}

if (-not $SkipDotNetTests) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET SDK 10 est requis pour les tests NEXT-01 / NEXT-02.'
    }
    Write-Host '[DEMO02] Tests MSTest sur fixtures temporaires indépendantes de OutputRoot...'
    & dotnet test $testProject --configuration Release --property:RestoreLockedMode=true --filter 'FullyQualifiedName~DemoLibraryGeneratorTests'
    if ($LASTEXITCODE -ne 0) { throw 'DEMO02_NEXT01_NEXT02_TESTS_FAILED' }
}
Write-Host '[DEMO02] PASS — bibliothèques synthétiques vérifiées. Aucun scan réel, aucune Release.'
Write-Host ('[DEMO02] Sortie conservée pour recette manuelle : ' + $OutputRoot)
