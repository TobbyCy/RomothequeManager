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
foreach ($vector in (New-RmUiProjectionVectors)) {
    $registry=New-RmLibraryProjection -Registry $vector.registry
    if ($registry.libraries.Count -ne 1) { throw 'UI_EXPORT_LIBRARY_CARD_COUNT_INVALID' }
    $card=$registry.libraries[0]
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
