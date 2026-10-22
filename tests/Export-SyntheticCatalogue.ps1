# T1 synthetic-only catalogue exporter; output path optional, no ROM access.
[CmdletBinding()]
param([string]$OutputJson)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'fixtures\SyntheticLibrarySnapshots.ps1')
Import-Module (Join-Path $root 'Modules\LibrarySnapshotProjection.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $root 'Modules\CatalogueProjection.psm1') -Force -ErrorAction Stop
function New-T1Fixture {
 $u1=New-RmSyntheticUnit -Id 'rom-disc-1'
 $u1 | Add-Member -NotePropertyName title -NotePropertyValue 'Jeu synthétique'
 $u1 | Add-Member -NotePropertyName discNumber -NotePropertyValue 1
 $u2=New-RmSyntheticUnit -Id 'rom-disc-2'
 $u2 | Add-Member -NotePropertyName title -NotePropertyValue 'Jeu synthétique'
 $u2 | Add-Member -NotePropertyName discNumber -NotePropertyValue 2
 $u3=New-RmSyntheticUnit -Id 'rom-variant-2'
 $u3.variantId='variant-2'
 $u3 | Add-Member -NotePropertyName title -NotePropertyValue 'Jeu synthétique'
 $a=New-RmSyntheticSnapshot -Id 't1-snap-a' -Generation 't1-gen-a' -At '2026-10-20T12:00:00Z' -Units @($u1)
 $b=New-RmSyntheticSnapshot -Id 't1-snap-b' -Generation 't1-gen-b' -At '2026-10-21T12:00:00Z' -Units @($u1,$u2,$u3)
 $reg=New-RmSyntheticRegistry -Snapshots @($a,$b) -LastComplete 't1-snap-b'
 return $reg
}
$fixture=New-T1Fixture
$catalog=New-RmCatalogueProjection -Registry $fixture
if ($catalog.gameCount -ne 1 -or @($catalog.games[0].units).Count -ne 3 -or
    @($catalog.games[0].variants).Count -ne 2) { throw 'CATALOG_EXPORT_SYNTHETIC_EXPECTATION_FAILED' }
$expectedTitle='Jeu synth'+([char]0x00E9)+'tique'
if ($catalog.games[0].title -cne $expectedTitle) { throw 'CATALOG_UNICODE_SOURCE_CORRUPTED' }
$json=ConvertTo-Json -InputObject $catalog -Depth 30 -Compress
$roundtrip=ConvertFrom-Json -InputObject $json
if ($roundtrip.schemaVersion -cne '0.1.0-ui-catalogue-t1-candidate' -or
    $roundtrip.gameCount -ne 1 -or $roundtrip.games[0].title -cne $expectedTitle) { throw 'CATALOG_EXPORT_ROUNDTRIP_FAILED' }
if ($OutputJson) { [IO.File]::WriteAllText($OutputJson,$json,(New-Object Text.UTF8Encoding($false))) }
Write-Output $json
