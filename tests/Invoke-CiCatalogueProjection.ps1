# T1 synthetic-only positive and negative harness. PS5.1 execution pending.
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'fixtures\SyntheticLibrarySnapshots.ps1')
Import-Module (Join-Path $root 'Modules\LibrarySnapshotProjection.psm1') -Force -ErrorAction Stop
Import-Module (Join-Path $root 'Modules\CatalogueProjection.psm1') -Force -ErrorAction Stop
function Require([bool]$Condition,[string]$Code) { if (-not $Condition) { throw ('CATALOG_TEST_FAIL:'+$Code) } }
function MustReject([scriptblock]$Action,[string]$Code) {
 $caught=$null
 try { & $Action | Out-Null } catch { $caught=$_.Exception.Message }
 if ($null -eq $caught -or -not $caught.Contains($Code)) { throw ('CATALOG_NEGATIVE_NOT_REJECTED:'+$Code+':'+$caught) }
}
function New-T1Registry {
 $u=New-RmSyntheticUnit -Id 'unit-1'
 $u | Add-Member -NotePropertyName title -NotePropertyValue 'Étoile'
 $a=New-RmSyntheticSnapshot -Id 'a' -Generation 'ga' -At '2026-10-20T12:00:00Z' -Units @($u)
 $b=New-RmSyntheticSnapshot -Id 'b' -Generation 'gb' -At '2026-10-21T12:00:00Z' -Units @($u)
 return (New-RmSyntheticRegistry -Snapshots @($a,$b) -LastComplete 'b')
}
$registry=New-T1Registry
$cat=New-RmCatalogueProjection -Registry $registry
Require ($cat.gameCount -eq 1) 'ONE_GAME'
Require ($cat.games[0].title -ceq 'Étoile') 'UNICODE_TITLE'
Require ($cat.games[0].title -ceq (([char]0x00C9)+'toile')) 'UNICODE_CODEPOINT_U00C9'
Require ($cat.games[0].provenance.snapshotId -ceq 'b') 'LATEST_SNAPSHOT'
Require ((Search-RmCatalogue -Catalogue $cat -Text 'éTOILE').total -eq 1) 'UNICODE_CASE_SEARCH'
Require ((Search-RmCatalogue -Catalogue $cat -LibraryId 'LIB-FIXTURE').total -eq 0) 'ORDINAL_LIBRARY_FILTER'
Require ((Search-RmCatalogue -Catalogue $cat -Limit 1).items.Count -eq 1) 'PAGINATION'
MustReject { Search-RmCatalogue -Catalogue $cat -Limit 0 } 'CATALOG_PAGE_INVALID'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].units=@($bad.libraries[0].snapshots[1].units[0],$bad.libraries[0].snapshots[1].units[0])
MustReject { New-RmCatalogueProjection -Registry $bad } 'CATALOG_DUPLICATE_ROM_ID'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].units[0].gameId=$null
$amb=New-RmCatalogueProjection -Registry $bad
Require ($null -eq $amb.gameCount -and -not $amb.countKnown -and @($amb.games).Count -eq 0 -and @($amb.diagnostics | Where-Object { $_.code -ceq 'CATALOG_IDENTITY_AMBIGUOUS' }).Count -eq 1) 'AMBIGUITY_NOT_GUESSED'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].scopeIsExhaustive=$false
$bad.libraries[0].lastVerifiedCompleteRef='a'
$partial=New-RmCatalogueProjection -Registry $bad
Require ($partial.games[0].coverageStatus -ceq 'INCOMPLETE') 'PARTIAL_NOT_COMPLETE'
$bad=New-T1Registry
$bad.libraries[0].indexAccessible=$false
$inaccessible=New-RmCatalogueProjection -Registry $bad
Require ($null -eq $inaccessible.gameCount -and -not $inaccessible.countKnown -and @($inaccessible.diagnostics | Where-Object { $_.code -ceq 'CATALOG_INDEX_INACCESSIBLE' }).Count -eq 1) 'INDEX_NOT_ZERO_PROOF'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].schemaVersion='9.9'
MustReject { New-RmCatalogueProjection -Registry $bad } 'LIBRARY_SCHEMA_UNSUPPORTED'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].coverageProof=@()
$bad.libraries[0].lastVerifiedCompleteRef='a'
$missing=New-RmCatalogueProjection -Registry $bad
Require ($missing.games[0].coverageStatus -ceq 'INCOMPLETE') 'MISSING_PROOF'
# Additional invariants: title conflict, missing title, failed scan, unknown-empty and distinct roots.
$bad=New-T1Registry
$u2=New-RmSyntheticUnit -Id 'unit-2'
$u2 | Add-Member -NotePropertyName title -NotePropertyValue 'Different title'
$bad.libraries[0].snapshots[1].units=@($bad.libraries[0].snapshots[1].units[0],$u2)
$conflict=New-RmCatalogueProjection -Registry $bad
Require ($conflict.games[0].quality -ceq 'CONTRADICTORY' -and $null -eq $conflict.games[0].title) 'TITLE_CONFLICT_NOT_GUESSED'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].units[0].PSObject.Properties.Remove('title')
$unknown=New-RmCatalogueProjection -Registry $bad
Require ($null -eq $unknown.games[0].title -and @($unknown.diagnostics | Where-Object { $_.code -ceq 'CATALOG_TITLE_UNKNOWN' }).Count -eq 1) 'TITLE_UNKNOWN'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].scanStatus='failed'
$bad.libraries[0].lastVerifiedCompleteRef='a'
$failed=New-RmCatalogueProjection -Registry $bad
Require ($failed.games[0].coverageStatus -ceq 'INCOMPLETE') 'FAILED_SCAN_NOT_COMPLETE'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].units=@()
$bad.libraries[0].snapshots[1].scopeIsExhaustive=$false
$bad.libraries[0].lastVerifiedCompleteRef='a'
$zero=New-RmCatalogueProjection -Registry $bad
Require (@($zero.diagnostics | Where-Object { $_.code -ceq 'CATALOG_ZERO_NOT_EMPTY_PROOF' }).Count -eq 1) 'ZERO_UNPROVEN'
$bad=New-T1Registry
$bad.libraries[0].snapshots[1].units=@()
$empty=New-RmCatalogueProjection -Registry $bad
Require ($empty.gameCount -eq 0 -and @($empty.diagnostics | Where-Object { $_.code -ceq 'CATALOG_ZERO_NOT_EMPTY_PROOF' }).Count -eq 0) 'EMPTY_PROVEN'
$bad=New-T1Registry
$bad.libraries[0].sources=@(
 [pscustomobject]@{sourceKey='source-1';rootKey='root-1';domains=@('ROMS')},
 [pscustomobject]@{sourceKey='source-2';rootKey='root-2';domains=@('ROMS')}
)
$multi=New-RmCatalogueProjection -Registry $bad
Require ($multi.games[0].coverageStatus -ceq 'INCOMPLETE') 'MULTI_ROOT_UNPROVEN'
$dated=New-T1Registry
$dated.libraries[0].snapshots[0].capturedAtUtc='2026-10-21T12:00:00Z'
$dated.libraries[0].snapshots[1].capturedAtUtc='2026-10-21T12:00:00.900Z'
$latest=New-RmCatalogueProjection -Registry $dated
Require ($latest.games[0].provenance.snapshotId -ceq 'b') 'FRACTIONAL_UTC_LATEST'
$first=& (Join-Path $PSScriptRoot 'Export-SyntheticCatalogue.ps1')
$second=& (Join-Path $PSScriptRoot 'Export-SyntheticCatalogue.ps1')
Require ($first -ceq $second) 'DETERMINISTIC_JSON'
Write-Output 'CATALOG_T1_SYNTHETIC_PASS'
