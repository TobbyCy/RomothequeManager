# T3 contract harness: mutations are passed to the independent validator.
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Import-Module (Join-Path $root 'Modules\CatalogueDtoContract.psm1') -Force -ErrorAction Stop
function Assert-Rejected {
 param($Payload,[string]$Code)
 $actual=$null
 try { [void](Assert-RmCanonicalCatalogueDto -Dto $Payload) }
 catch { $actual=$_.Exception.Message }
 if ($null -eq $actual -or -not $actual.Contains($Code)) { throw ('CATDTO_CANARY_NOT_REJECTED:'+$Code+':'+$actual) }
}
function Clone-Dto($dto) { return (ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $dto -Depth 40 -Compress)) }
$raw=& (Join-Path $PSScriptRoot 'Export-SyntheticCanonicalCatalogue.ps1')
$dto=ConvertFrom-Json -InputObject $raw
if (-not (Assert-RmCanonicalCatalogueDto -Dto $dto)) { throw 'CATDTO_VALID_NOT_ACCEPTED' }
$x=Clone-Dto $dto;$x.schemaVersion='999';Assert-Rejected $x 'CATDTO_SCHEMA_UNSUPPORTED'
$x=Clone-Dto $dto;$x.games[0].provenance[0].snapshotId='ghost';Assert-Rejected $x 'CATDTO_ORPHAN_SNAPSHOT'
$x=Clone-Dto $dto;$x.games[0].provenance[0].generationId='ghost';Assert-Rejected $x 'CATDTO_GENERATION_PROVENANCE_MISMATCH'
$x=Clone-Dto $dto;$x.games[0].provenance[0].observedAtUtc='2026-01-01T00:00:00Z';Assert-Rejected $x 'CATDTO_GENERATION_PROVENANCE_MISMATCH'
$x=Clone-Dto $dto;$x.games[0].observations[0].snapshotId='ghost';Assert-Rejected $x 'CATDTO_OBSERVATION_PROVENANCE'
$x=Clone-Dto $dto;$x.games[0].observations[0].variantId='ghost';Assert-Rejected $x 'CATDTO_ORPHAN_VARIANT'
$x=Clone-Dto $dto;$x.games[0].observations[1].romUnitId=$x.games[0].observations[0].romUnitId;Assert-Rejected $x 'CATDTO_DUPLICATE_OBSERVATION'
$x=Clone-Dto $dto;$x.games+=@($x.games[0]);Assert-Rejected $x 'CATDTO_DUPLICATE_GAME'
$x=Clone-Dto $dto;$x.games[0].coverStatus='UNKNOWN';Assert-Rejected $x 'CATDTO_COVERAGE_FORGED'
$x=Clone-Dto $dto;$x.registry.libraries[0].generations[1].coverageProof=@();Assert-Rejected $x 'CATDTO_COMPLETE_UNPROVEN'
$x=Clone-Dto $dto;$x.registry.libraries[0].generations[1].scopeIsExhaustive=$false;Assert-Rejected $x 'CATDTO_COMPLETE_UNPROVEN'
$x=Clone-Dto $dto;$x.registry.libraries[0].generations[1].scanStatus='failed';Assert-Rejected $x 'CATDTO_COMPLETE_UNPROVEN'
$x=Clone-Dto $dto;$x.gameCount=999;Assert-Rejected $x 'CATDTO_COUNT_MISMATCH'
$x=Clone-Dto $dto;$x.games[0].PSObject.Properties.Remove('provenance');Assert-Rejected $x 'CATDTO_FIELD_MISSING:provenance'
$x=Clone-Dto $dto;$x.games[0].observations=$null;Assert-Rejected $x 'CATDTO_GAME_ARRAYS'
$x=Clone-Dto $dto;$x.registry.libraries[0].generations[1].generationId='gen-a';Assert-Rejected $x 'CATDTO_DUPLICATE_SNAPSHOT_GENERATION'
$again=& (Join-Path $PSScriptRoot 'Export-SyntheticCanonicalCatalogue.ps1')
if ($raw -cne $again) { throw 'CATDTO_JSON_NOT_DETERMINISTIC' }
Write-Output 'CATDTO_T3_SYNTHETIC_PASS'
