# Synthetic PowerShell 5.1 UI DTO exporter. No real ROM/SSD/StateRoot access.
[CmdletBinding()]
param([string]$OutputJson,[string]$OutputJavaScript)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {
    throw 'UI_EXPORT_REQUIRES_WINDOWS_PS51'
}
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'fixtures\SyntheticLibrarySnapshots.ps1')
. (Join-Path $PSScriptRoot 'fixtures\UiProjectionVectors.ps1')
Import-Module (Join-Path $root 'Modules\LibrarySnapshotProjection.psm1') -Force -ErrorAction Stop
$types=@('ADDED','CHANGED','UNCHANGED','NOT_OBSERVED_IN_B_COVERED_SCOPE','UNKNOWN','NOT_COMPARABLE')
$allLibraries=New-Object 'System.Collections.Generic.List[object]'
$allComparisons=New-Object 'System.Collections.Generic.List[object]'
$references=New-Object 'System.Collections.Generic.List[object]'
$libraryIds=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($vector in (New-RmUiProjectionVectors)) {
    $registry=New-RmLibraryProjection -Registry $vector.registry
    if ($registry.libraries.Count -ne 1) { throw 'UI_EXPORT_LIBRARY_CARD_COUNT_INVALID' }
    $card=$registry.libraries[0]
    if (-not $libraryIds.Add($card.libraryId)) { throw 'UI_EXPORT_DUPLICATE_LIBRARY_ID' }
    if ($card.generations.Count -ne 2) { throw ('UI_EXPORT_GENERATION_COUNT:'+$vector.scenarioId) }
    foreach ($generation in $card.generations) {
        if ($generation.snapshotId -ceq $generation.generationId) { throw 'UI_EXPORT_SNAPSHOT_GENERATION_ID_CONFLATED' }
        if ($generation.capturedAtUtc -cnotmatch 'Z
    if ($card.indexAccessible) {
        $view=Get-RmLibraryComparisonView -Registry $vector.registry -LibraryId $card.libraryId -BeforeSnapshotId $vector.before -AfterSnapshotId $vector.after
        if ($view.library.libraryId -cne $card.libraryId -or
            $view.before.snapshotId -cne $vector.before -or
            $view.after.snapshotId -cne $vector.after -or
            $view.before.generationId -ceq $view.after.generationId) { throw 'UI_EXPORT_COMPARISON_REFERENCE_MISMATCH' }
        if ($null -eq $view.before.coverageProof -or $null -eq $view.after.coverageProof -or
            $null -eq $view.before.scopeIsExhaustive -or $null -eq $view.after.scopeIsExhaustive) {
            throw 'UI_EXPORT_COMPARISON_PROOF_MISSING'
        }
        $expected=$vector.expectedCoverageStatus
        if ($view.coverageStatus -cne $expected) { throw ('UI_EXPORT_COVERAGE_MISMATCH:'+$vector.scenarioId) }
        $sum=0
        foreach ($type in $types) {
            $value=$view.counts.PSObject.Properties[$type].Value
            if ($value -isnot [int] -or $value -lt 0) { throw ('UI_EXPORT_COUNT_INVALID:'+$type) }
            $sum+=$value
            if ($vector.expectedCounts.ContainsKey($type) -and $value -ne $vector.expectedCounts[$type]) {
                throw ('UI_EXPORT_EXPECTED_COUNT_MISMATCH:'+$vector.scenarioId+':'+$type)
            }
        }
        foreach ($entry in $view.entries) {
            if ($types -cnotcontains $entry.classification -or [string]::IsNullOrWhiteSpace($entry.reason)) {
                throw 'UI_EXPORT_INVALID_ENTRY_CLASS_OR_REASON'
            }
        }
        if ($sum -ne $view.countDenominator -or $sum -ne @($view.entries).Count) { throw 'UI_EXPORT_COUNT_SUM_MISMATCH' }
        if ($view.coverageStatus -ceq 'COMPLETE' -and $view.scopeNotComparable.Count -ne 0) { throw 'UI_EXPORT_UNPROVEN_COMPLETE' }
        $allComparisons.Add($view)
    } else {
        foreach ($generation in $card.generations) {
            if ($generation.countKnown -or $null -ne $generation.observedUnitCount) { throw 'UI_EXPORT_INACCESSIBLE_COUNT_EXPOSED' }
        }
    }
    $references.Add([pscustomobject]@{
        scenarioId=$vector.scenarioId;libraryId=$card.libraryId
        beforeSnapshotId=$vector.before;afterSnapshotId=$vector.after
        comparisonAvailable=[bool]$card.indexAccessible
        expectedCoverageStatus=$vector.expectedCoverageStatus
        expectedCounts=$vector.expectedCounts
    })
}
$payload=[pscustomobject]@{
    registry=[pscustomobject]@{schemaVersion='0.1.0-synthetic';readOnly=$true;libraries=$allLibraries.ToArray()}
    comparisons=$allComparisons.ToArray()
    referenceVectors=$references.ToArray()
}
$json=ConvertTo-Json -InputObject $payload -Depth 30 -Compress
$roundTrip=ConvertFrom-Json -InputObject $json
if (@($roundTrip.registry.libraries).Count -ne 7 -or @($roundTrip.comparisons).Count -ne 6 -or @($roundTrip.referenceVectors).Count -ne 7) { throw 'UI_EXPORT_ROUNDTRIP_CARD_COUNT_INVALID' }
if ($OutputJson) { [IO.File]::WriteAllText($OutputJson,$json,[Text.UTF8Encoding]::new($false)) }
if ($OutputJavaScript) {
    [IO.File]::WriteAllText($OutputJavaScript,('window.RM_PROJECTION_DATA='+$json+';'),[Text.UTF8Encoding]::new($false))
}
# Stdout always contains the deterministic data payload, not logs.
Write-Output $json
 -or $null -eq $generation.coverageProof -or
            $null -eq $generation.scopeIsExhaustive) { throw 'UI_EXPORT_MISSING_UTC_OR_PROOF' }
        if ($generation.scanStatus -cne 'complete' -and
            @($generation.coverage | Where-Object { $_.status -ceq 'complete' }).Count -gt 0) {
            throw 'UI_EXPORT_INCOMPLETE_SCAN_CLAIMED_COMPLETE'
        }
    }
    $allLibraries.Add($card)
    if ($card.indexAccessible) {
        $view=Get-RmLibraryComparisonView -Registry $vector.registry -LibraryId $card.libraryId -BeforeSnapshotId $vector.before -AfterSnapshotId $vector.after
        $expected=$vector.expectedCoverageStatus
        if ($view.coverageStatus -cne $expected) { throw ('UI_EXPORT_COVERAGE_MISMATCH:'+$vector.scenarioId) }
        $sum=0
        foreach ($type in $types) {
            $value=$view.counts.PSObject.Properties[$type].Value
            if ($value -isnot [int] -or $value -lt 0) { throw ('UI_EXPORT_COUNT_INVALID:'+$type) }
            $sum+=$value
            if ($vector.expectedCounts.ContainsKey($type) -and $value -ne $vector.expectedCounts[$type]) {
                throw ('UI_EXPORT_EXPECTED_COUNT_MISMATCH:'+$vector.scenarioId+':'+$type)
            }
        }
        if ($sum -ne $view.countDenominator -or $sum -ne @($view.entries).Count) { throw 'UI_EXPORT_COUNT_SUM_MISMATCH' }
        if ($view.coverageStatus -ceq 'COMPLETE' -and $view.scopeNotComparable.Count -ne 0) { throw 'UI_EXPORT_UNPROVEN_COMPLETE' }
        $allComparisons.Add($view)
    } else {
        foreach ($generation in $card.generations) {
            if ($generation.countKnown -or $null -ne $generation.observedUnitCount) { throw 'UI_EXPORT_INACCESSIBLE_COUNT_EXPOSED' }
        }
    }
    $references.Add([pscustomobject]@{
        scenarioId=$vector.scenarioId;libraryId=$card.libraryId
        beforeSnapshotId=$vector.before;afterSnapshotId=$vector.after
        comparisonAvailable=[bool]$card.indexAccessible
        expectedCoverageStatus=$vector.expectedCoverageStatus
        expectedCounts=$vector.expectedCounts
    })
}
$payload=[pscustomobject]@{
    registry=[pscustomobject]@{schemaVersion='0.1.0-synthetic';readOnly=$true;libraries=$allLibraries.ToArray()}
    comparisons=$allComparisons.ToArray()
}
$json=ConvertTo-Json -InputObject $payload -Depth 30 -Compress
$roundTrip=ConvertFrom-Json -InputObject $json
if ($roundTrip.registry.libraries.Count -ne 6 -or $roundTrip.comparisons.Count -ne 5) { throw 'UI_EXPORT_ROUNDTRIP_CARD_COUNT_INVALID' }
if ($OutputJson) { [IO.File]::WriteAllText($OutputJson,$json,[Text.UTF8Encoding]::new($false)) }
if ($OutputJavaScript) {
    [IO.File]::WriteAllText($OutputJavaScript,('window.RM_PROJECTION_DATA='+$json+';'),[Text.UTF8Encoding]::new($false))
}
# Stdout always contains the deterministic data payload, not logs.
Write-Output $json
