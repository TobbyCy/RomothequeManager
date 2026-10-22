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
$shared=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'fixtures\CatalogueT3CanonicalSample.json')))
if (-not (Assert-RmCanonicalCatalogueDto -Dto $shared)) { throw 'CATDTO_SHARED_SAMPLE_INVALID' }
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
$reg.libraries[0].freshness='UNKNOWN'
$unknownFreshness=Project-Scenario $reg
if ($unknownFreshness.games[0].quality -cne 'UNKNOWN') { throw 'CATDTO_UNKNOWN_FRESHNESS_FORGED' }
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
$x=Clone-Dto $dto
foreach($observation in $x.games[0].observations) { $observation.observedTitle='Titre falsifié' }
Assert-Rejected $x 'CATDTO_TITLE_PROVENANCE_MISMATCH'
$x=Clone-Dto $dto
$x.games[0].quality='CONTRADICTORY'
Assert-Rejected $x 'CATDTO_TITLE_CONFLICT_FORGED'
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].snapshotId='snap-a'
Assert-Rejected $x 'CATDTO_DUPLICATE_SNAPSHOT_GENERATION'
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].coverage[0].status='partial'
Assert-Rejected $x 'CATDTO_COMPLETE_COVERAGE_MISMATCH'
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].coverageProof[0].rootKey='forged-root'
Assert-Rejected $x 'CATDTO_PROOF_PARTITION_INVALID'
$x=Clone-Dto $dto
$x.games[0].observations[0].PSObject.Properties.Remove('observedTitle')
Assert-Rejected $x 'CATDTO_FIELD_MISSING:observedTitle'
# T4-A — adversarial mutations must reach the independent DTO validator.
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].observedUnitCount=1
Assert-Rejected $x 'CATDTO_OBSERVATION_COUNT_EXCEEDS_SNAPSHOT'
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].observedUnitCount=99
Assert-Rejected $x 'CATDTO_SNAPSHOT_OBSERVATION_COUNT_MISMATCH'
# A complete GBA proof is legitimate even when PS1 is unobserved;
# global COMPLETE remains forbidden and countKnown stays false.
$reg=New-ScenarioRegistry
$reg.libraries[0].platforms=@('GBA','PS1')
$reg.libraries[0].lastVerifiedCompleteRef=$null
$mixedCoverage=Project-Scenario $reg
if ($mixedCoverage.games[0].coverStatus -cne 'UNKNOWN' -or
 $mixedCoverage.countKnown -or
 @($mixedCoverage.registry.libraries[0].generations[1].coverage | Where-Object { $_.console -ceq 'GBA' -and $_.status -ceq 'complete' }).Count -ne 1) {
 throw 'CATDTO_MIXED_PLATFORM_COVERAGE_LOST'
}
$x=Clone-Dto $dto
$x.games[0].availability='OFFLINE'
Assert-Rejected $x 'CATDTO_LIBRARY_STATE_FORGED'
$x=Clone-Dto $dto
$x.games[0].freshness='STALE'
Assert-Rejected $x 'CATDTO_LIBRARY_STATE_FORGED'
$x=Clone-Dto $dto
$x.games[0].observations[0].discNumber=0
Assert-Rejected $x 'CATDTO_DISC_INVALID'
$x=Clone-Dto $dto
$x.games[0].observations[0].discNumber=2.5
Assert-Rejected $x 'CATDTO_DISC_INVALID'
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].capturedAtUtc='2026-10-21T12:00:00+01:00'
Assert-Rejected $x 'CATDTO_UTC_REQUIRED'
$x=Clone-Dto $dto
$x.registry.libraries[0].generations[1].coverageProof[0].status='invalid'
Assert-Rejected $x 'CATDTO_PROOF_STATUS_INVALID'
$x=Clone-Dto $dto
$x.registry.libraries[0].sources+=@([pscustomobject]@{sourceKey='source-other';rootKey='root-other';domains=@('ROMS')})
Assert-Rejected $x 'CATDTO_COMPLETE_UNPROVEN'
$x=Clone-Dto $dto
$x.registry.libraries[0].platforms+=@('GBA')
Assert-Rejected $x 'CATDTO_DUPLICATE_PLATFORM'
$x=Clone-Dto $dto
$x.games[0].quality='KNOWN'
$x.games[0].provenance[0].snapshotId='snap-a'
$x.games[0].provenance[0].generationId='gen-a'
$x.games[0].provenance[0].observedAtUtc='2026-10-20T12:00:00Z'
foreach($o in $x.games[0].observations) {
 $o.snapshotId='snap-a';$o.generationId='gen-a'
}
Assert-Rejected $x 'CATDTO_HISTORICAL_SNAPSHOT_AS_LATEST'
$x=Clone-Dto $dto
$other=Clone-Dto $x.games[0]
$other.gameId='game-forged-second'
$x.games+=@($other)
$x.gameCount=2
Assert-Rejected $x 'CATDTO_CROSS_GAME_DUPLICATE_ROM'
$x=Clone-Dto $dto
# Simultaneous manipulation: remove proof, change scan, pretend complete everywhere.
$x.registry.libraries[0].generations[1].coverageProof=@()
$x.registry.libraries[0].generations[1].scanStatus='failed'
$x.registry.libraries[0].generations[1].scopeIsExhaustive=$false
$x.registry.libraries[0].generations[1].coverage[0].status='complete'
$x.games[0].coverStatus='KNOWN';$x.games[0].status='KNOWN'
Assert-Rejected $x 'CATDTO_COMPLETE_UNPROVEN'
$malformedWasRejected=$false
try { $null=ConvertFrom-Json -InputObject '{"schemaVersion":' -ErrorAction Stop } catch { $malformedWasRejected=$true }
if (-not $malformedWasRejected) { throw 'CATDTO_MALFORMED_JSON_ACCEPTED' }

# Two complete roots: changing enumeration order must not change canonical bytes.
$reg=New-ScenarioRegistry
$src2=[pscustomobject]@{sourceKey='source-2';rootKey='root-2';domains=@('ROMS')}
$reg.libraries[0].sources+=@($src2)
foreach($snap in $reg.libraries[0].snapshots) {
 $snap.coverageProof+=@([pscustomobject]@{
  console='GBA';sourceKey='source-2';rootKey='root-2';domain='ROMS';status='complete'
 })
}
$original=ConvertTo-Json -InputObject (Project-Scenario $reg) -Depth 40 -Compress
$reg.libraries[0].sources=@($reg.libraries[0].sources[1],$reg.libraries[0].sources[0])
$reg.libraries[0].snapshots=@($reg.libraries[0].snapshots[1],$reg.libraries[0].snapshots[0])
foreach($snap in $reg.libraries[0].snapshots) {
 $snap.coverageProof=@($snap.coverageProof[1],$snap.coverageProof[0])
}
$reordered=ConvertTo-Json -InputObject (Project-Scenario $reg) -Depth 40 -Compress
if ($original -cne $reordered) { throw 'CATDTO_INPUT_ORDER_CHANGED_JSON' }
$again=& (Join-Path $PSScriptRoot 'Export-SyntheticCanonicalCatalogue.ps1')
if ($raw -cne $again) { throw 'CATDTO_JSON_NOT_DETERMINISTIC' }
Write-Output 'CATDTO_T3_SYNTHETIC_PASS'
