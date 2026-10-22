# T3: canonical candidate catalogue producer. Synthetic fixtures only.
[CmdletBinding()]
param([string]$OutputJson)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'fixtures\SyntheticLibrarySnapshots.ps1')
Import-Module (Join-Path $root 'Modules\LibrarySnapshotProjection.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $root 'Modules\CatalogueProjection.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $root 'Modules\CatalogueDtoContract.psm1') -Force -ErrorAction Stop
function New-T3Registry {
 $u1=New-RmSyntheticUnit -Id 'unit-disc-1'
 $u1 | Add-Member -NotePropertyName title -NotePropertyValue 'Étoile des disques'
 $u1 | Add-Member -NotePropertyName discNumber -NotePropertyValue 1
 $u2=New-RmSyntheticUnit -Id 'unit-disc-2'
 $u2 | Add-Member -NotePropertyName title -NotePropertyValue 'Étoile des disques'
 $u2 | Add-Member -NotePropertyName discNumber -NotePropertyValue 2
 $u3=New-RmSyntheticUnit -Id 'unit-variant'
 $u3.variantId='variant-b'
 $u3 | Add-Member -NotePropertyName title -NotePropertyValue 'Étoile des disques'
 $a=New-RmSyntheticSnapshot -Id 'snap-a' -Generation 'gen-a' -At '2026-10-20T12:00:00Z' -Units @($u1)
 $b=New-RmSyntheticSnapshot -Id 'snap-b' -Generation 'gen-b' -At '2026-10-21T12:00:00Z' -Units @($u1,$u2,$u3)
 $reg=New-RmSyntheticRegistry -Snapshots @($a,$b) -LastComplete 'snap-b'
 return $reg
}
$registry=New-T3Registry
$catalogue=New-RmCatalogueProjection -Registry $registry
$dto=ConvertTo-RmCanonicalCatalogueDto -Catalogue $catalogue
[void](Assert-RmCanonicalCatalogueDto -Dto $dto)
if ($dto.games.Count -ne 1 -or $dto.games[0].observations.Count -ne 3 -or
 $dto.games[0].variants.Count -ne 2) { throw 'CATDTO_FIXTURE_MISMATCH' }
# Verify the original UTF-8 BOM source was parsed as real U+00C9, not mojibake.
$expectedTitle=([char]0x00C9)+'toile des disques'
if ($dto.games[0].title -cne $expectedTitle -or
 [int][char]$dto.games[0].title[0] -ne 0x00C9) { throw 'CATDTO_UNICODE_SOURCE_CORRUPTED' }
$json=ConvertTo-Json -InputObject $dto -Depth 40 -Compress
$roundtrip=ConvertFrom-Json -InputObject $json
[void](Assert-RmCanonicalCatalogueDto -Dto $roundtrip)
if ($roundtrip.games[0].title -cne $expectedTitle -or
 [int][char]$roundtrip.games[0].title[0] -ne 0x00C9) { throw 'CATDTO_UNICODE_JSON_ROUNDTRIP' }
if ($OutputJson) { [IO.File]::WriteAllText($OutputJson,$json,(New-Object Text.UTF8Encoding($false))) }
Write-Output $json
