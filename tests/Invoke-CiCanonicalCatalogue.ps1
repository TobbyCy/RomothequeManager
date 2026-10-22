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
# Producer scenarios: the canonical validator must accept partial/offline/stale/inaccessible
# and reject forged complete/known claims, not only malformed JSON.
. (Join-Path $PSScriptRoot 'fixtures\SyntheticLibrarySnapshots.ps1')
Import-Module (Join-Path $root 'Modules\CatalogueProjection.psm1') -Force -ErrorAction Stop
function New-ScenarioRegistry {
 $u=New-RmSyntheticUnit -Id 'scenario-unit'
 $u | Add-Member -NotePropertyName title -NotePropertyValue 'Jeu fictif'
 $a=New-RmSyntheticSnapshot -Id 'scenario-a' -Generation 'scenario-ga' -At '2026-10-20T12:00:00Z' -Units @($u)
 $b=New-RmSyntheticSnapshot -Id 'scenario-b' -Generation 'scenario-gb' -At '2026-10-21T12:00:00Z' -Units @($u)
 return (New-RmSyntheticRegistry -Snapshots @($a,$b) -LastComplete 'scenario-b')
}
function Project-Scenario($reg) {
 return (ConvertTo-RmCanonicalCatalogueDto -Catalogue (New-RmCatalogueProjection -Registry $reg))
}
$reg=New-ScenarioRegistry
$reg.libraries[0].currentAvailability='OFFLINE'
$offline=Project-Scenario $reg
if ($offline.games[0].availability -cne 'OFFLINE') { throw 'CATDTO_OFFLINE_HISTORY_LOST' }
$reg=New-ScenarioRegistry
$reg.libraries[0].freshness='STALE'
$stale=Project-Scenario $reg
if ($stale.games[0].quality -cne 'STALE') { throw 'CATDTO_STALE_LOST' }
$reg=New-ScenarioRegistry
$reg.libraries[0].snapshots[1].scopeIsExhaustive=$false
$reg.libraries[0].lastVerifiedCompleteRef='scenario-a'
$partial=Project-Scenario $reg
if ($partial.games[0].status -cne 'INCOMPLETE' -or $partial.countKnown -or $null -ne $partial.gameCount) { throw 'CATDTO_PARTIAL_FALSE_COMPLETE' }
$x=Clone-Dto $partial;$x.games[0].coverStatus='KNOWN';Assert-Rejected $x 'CATDTO_COVERAGE_FORGED'
$reg=New-ScenarioRegistry
$reg.libraries[0].indexAccessible=$false
$inaccessible=Project-Scenario $reg
if ($inaccessible.games.Count -ne 0 -or $inaccessible.countKnown -or $null -ne $inaccessible.gameCount) { throw 'CATDTO_INACCESSIBLE_COUNT' }
$reg=New-ScenarioRegistry
$second=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $reg.libraries[0] -Depth 30 -Compress)
$second.libraryId='second-library'
$second.name='Autre bibliothèque fictive'
$second.platforms=@('PS1')
foreach($snap in $second.snapshots) {
 $snap.snapshotId='ps1-'+$snap.snapshotId;$snap.generationId='ps1-'+$snap.generationId
 foreach($u in $snap.units) { $u.console='PS1' }
 foreach($proof in $snap.coverageProof) { $proof.console='PS1' }
}
$second.lastVerifiedCompleteRef='ps1-scenario-b'
$reg.libraries=@($reg.libraries[0],$second)
$multiple=Project-Scenario $reg
if ($multiple.games.Count -ne 2 -or $multiple.gameCount -ne 2 -or
 @($multiple.games | Where-Object { $_.platform -ceq 'PS1' }).Count -ne 1) { throw 'CATDTO_MULTILIB_PLATFORM_LOST' }
$x=Clone-Dto $multiple
$x.games[1].provenance[0].libraryId='ghost'
Assert-Rejected $x 'CATDTO_ORPHAN_LIBRARY'
$again=& (Join-Path $PSScriptRoot 'Export-SyntheticCanonicalCatalogue.ps1')
if ($raw -cne $again) { throw 'CATDTO_JSON_NOT_DETERMINISTIC' }
Write-Output 'CATDTO_T3_SYNTHETIC_PASS'
