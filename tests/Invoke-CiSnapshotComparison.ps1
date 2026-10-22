# Deterministic, in-memory fixture checks for Issue #9.
# Runs in Windows PowerShell 5.1, does not invoke Initialize.ps1, scanners or StateRoot.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
try {
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {
        throw 'SNAPSHOT_TEST_REQUIRES_WINDOWS_PS51'
    }
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    Import-Module -Name (Join-Path $root 'Modules\SnapshotComparison.psm1') -Force -ErrorAction Stop

    function New-FixtureUnit {
        param([string]$Id, [AllowNull()][object]$Digest, [string]$Game='game-1',
              [string]$Variant='variant-1', [string]$Console='GBA')
        return [pscustomobject]@{
            romUnitId=$Id; gameId=$Game; variantId=$Variant
            console=$Console; observationDigest=$Digest
        }
    }
    function New-FixtureSnapshot {
        param([string]$Generation, [string]$At='2026-10-20T12:00:00Z',
              [string]$Scan='complete', [string]$Scope='complete',
              [object[]]$Units=@(), [string]$Availability='ONLINE',
              [string]$Freshness='FRESH', [string]$Library='library-fixture')
        return [pscustomobject]@{
            schemaVersion='1.0.0'
            libraryId=$Library
            generationId=$Generation
            capturedAtUtc=$At
            scanStatus=$Scan
            sourceAvailability=$Availability
            freshness=$Freshness
            coverage=@([pscustomobject]@{console='GBA'; status=$Scope})
            units=@($Units)
        }
    }
    function Assert-Synthetic {
        param([bool]$Condition, [string]$Case)
        if (-not $Condition) { throw ('SNAPSHOT_FIXTURE_FAILED:' + $Case) }
        $script:passed++
    }
    function Assert-ThrowsCode {
        param([scriptblock]$Action, [string]$Code)
        $observed = ''
        try { & $Action | Out-Null } catch { $observed = $_.Exception.Message }
        Assert-Synthetic ($observed.Contains($Code)) ('must-refuse-' + $Code)
    }
    function Get-Classification {
        param($Result, [string]$Id)
        $found=@($Result.entries | Where-Object { $_.romUnitId -ceq $Id })
        if ($found.Count -ne 1) { throw ('SNAPSHOT_FIXTURE_ID_NOT_UNIQUE:' + $Id) }
        return [string]$found[0].classification
    }

    $script:passed = 0
    $a = 'a' * 64; $b = 'b' * 64
    $before = New-FixtureSnapshot -Generation 'before' -Units @(
        (New-FixtureUnit 'unit-unchanged' $a),
        (New-FixtureUnit 'unit-modified' $a),
        (New-FixtureUnit 'unit-not-observed' $a),
        (New-FixtureUnit 'unit-conflict' $a)
    )
    $after = New-FixtureSnapshot -Generation 'after' -At '2026-10-21T12:00:00Z' -Units @(
        (New-FixtureUnit 'unit-unchanged' $a),
        (New-FixtureUnit 'unit-modified' $b),
        (New-FixtureUnit 'unit-added' $b),
        (New-FixtureUnit 'unit-conflict' $a -Variant 'variant-2')
    )
    $result = Compare-RmInventorySnapshots -Before $before -After $after
    Assert-Synthetic ($result.counts.UNCHANGED -eq 1 -and (Get-Classification $result 'unit-unchanged') -ceq 'UNCHANGED') 'unchanged'
    Assert-Synthetic ($result.counts.CHANGED -eq 1 -and (Get-Classification $result 'unit-modified') -ceq 'CHANGED') 'modified'
    Assert-Synthetic ($result.counts.ADDED -eq 1 -and (Get-Classification $result 'unit-added') -ceq 'ADDED') 'added'
    Assert-Synthetic ($result.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 1 -and (Get-Classification $result 'unit-not-observed') -ceq 'NOT_OBSERVED_IN_B_COVERED_SCOPE') 'observed-absence-not-deletion'
    Assert-Synthetic ($result.counts.NOT_COMPARABLE -eq 1 -and (Get-Classification $result 'unit-conflict') -ceq 'NOT_COMPARABLE') 'owner-conflict'
    Assert-Synthetic (@($result.entries | Where-Object { $_.classification -ceq 'DELETED' }).Count -eq 0) 'never-infer-physical-deletion'
    $ids = @($result.entries | ForEach-Object { $_.romUnitId })
    Assert-Synthetic (($ids -join ',') -ceq (@($ids | Sort-Object) -join ',')) 'deterministic-identity-order'

    # INT-03: romUnitId is ordinal. Case-distinct identities must never collapse.
    $caseBefore = New-FixtureSnapshot -Generation 'case-before' -Units @(
        (New-FixtureUnit 'Unit-A' $a), (New-FixtureUnit 'unit-a' $b)
    )
    $caseAfter = New-FixtureSnapshot -Generation 'case-after' -At '2026-10-21T12:00:00Z' -Units @(
        (New-FixtureUnit 'Unit-A' $a), (New-FixtureUnit 'unit-a' $b)
    )
    $caseResult = Compare-RmInventorySnapshots $caseBefore $caseAfter
    Assert-Synthetic ($caseResult.entries.Count -eq 2 -and $caseResult.counts.UNCHANGED -eq 2 -and
        (Get-Classification $caseResult 'Unit-A') -ceq 'UNCHANGED' -and
        (Get-Classification $caseResult 'unit-a') -ceq 'UNCHANGED') 'ordinal-case-distinct-identities'
    $caseOnlyBefore = New-FixtureSnapshot -Generation 'upper-only' -Units @((New-FixtureUnit 'Unit-A' $a))
    $caseOnlyAfter = New-FixtureSnapshot -Generation 'lower-only' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-a' $a))
    $caseDiff = Compare-RmInventorySnapshots $caseOnlyBefore $caseOnlyAfter
    Assert-Synthetic ($caseDiff.entries.Count -eq 2 -and $caseDiff.counts.ADDED -eq 1 -and
        $caseDiff.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 1 -and
        (Get-Classification $caseDiff 'Unit-A') -ceq 'NOT_OBSERVED_IN_B_COVERED_SCOPE' -and
        (Get-Classification $caseDiff 'unit-a') -ceq 'ADDED') 'ordinal-case-not-false-match'

    $empty = New-FixtureSnapshot -Generation 'empty'
    $later = New-FixtureSnapshot -Generation 'later' -At '2026-10-21T12:00:00Z'
    $emptyResult = Compare-RmInventorySnapshots $empty $later
    Assert-Synthetic ($emptyResult.entries.Count -eq 0 -and $emptyResult.counts.ADDED -eq 0) 'empty-array-safe'
    $oldSingle = New-FixtureSnapshot -Generation 'old-single' -Units @((New-FixtureUnit 'unit-1' $a))
    $newSingle = New-FixtureSnapshot -Generation 'new-single' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' $a))
    $oldBlank = New-FixtureSnapshot -Generation 'old-blank'
    $newBlank = New-FixtureSnapshot -Generation 'new-blank' -At '2026-10-21T12:00:00Z'

    $partialAfter = New-FixtureSnapshot -Generation 'partial-after' -At '2026-10-21T12:00:00Z' -Scan 'partial'
    $r = Compare-RmInventorySnapshots $oldSingle $partialAfter
    Assert-Synthetic ((Get-Classification $r 'unit-1') -ceq 'UNKNOWN' -and $r.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 0) 'partial-after-never-disappearance'
    $partialBefore = New-FixtureSnapshot -Generation 'partial-before' -Scan 'partial'
    $r = Compare-RmInventorySnapshots $partialBefore $newSingle
    Assert-Synthetic ((Get-Classification $r 'unit-1') -ceq 'UNKNOWN' -and $r.counts.ADDED -eq 0) 'partial-before-never-added'
    $unverifiedScope = New-FixtureSnapshot -Generation 'scope-partial' -At '2026-10-21T12:00:00Z' -Scope 'partial'
    $r = Compare-RmInventorySnapshots $oldSingle $unverifiedScope
    Assert-Synthetic ((Get-Classification $r 'unit-1') -ceq 'UNKNOWN') 'platform-partial-never-disappearance'

    # C1: both A and B must prove complete scan AND complete platform scope.
    $samePartialB = New-FixtureSnapshot -Generation 'same-partial-b' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Units @((New-FixtureUnit 'unit-1' $a))
    $r = Compare-RmInventorySnapshots $oldSingle $samePartialB
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and (Get-Classification $r 'unit-1') -ceq 'UNKNOWN' -and $r.counts.UNCHANGED -eq 0) 'C1-B-partial-same-unit-not-unchanged'
    $changedPartialB = New-FixtureSnapshot -Generation 'changed-partial-b' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Units @((New-FixtureUnit 'unit-1' $b))
    $r = Compare-RmInventorySnapshots $oldSingle $changedPartialB
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and $r.counts.CHANGED -eq 0) 'C1-B-partial-digest-change-not-changed'
    $addedPartialB = New-FixtureSnapshot -Generation 'added-partial-b' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Units @((New-FixtureUnit 'unit-1' $b))
    $r = Compare-RmInventorySnapshots $oldBlank $addedPartialB
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and $r.counts.ADDED -eq 0) 'C1-B-partial-presence-not-added'
    $partialAOldUnit = New-FixtureSnapshot -Generation 'partial-a-with-unit' -Scan 'partial' -Units @((New-FixtureUnit 'unit-1' $a))
    $r = Compare-RmInventorySnapshots $partialAOldUnit $newBlank
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and $r.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 0) 'C1-A-partial-no-absence-claim'
    $partialScopeSame = New-FixtureSnapshot -Generation 'scope-incomplete-b' -At '2026-10-21T12:00:00Z' -Scope 'partial' -Units @((New-FixtureUnit 'unit-1' $a))
    $r = Compare-RmInventorySnapshots $oldSingle $partialScopeSame
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and $r.counts.UNCHANGED -eq 0) 'C1-B-platform-partial-common-unit'
    $partialScopeA = New-FixtureSnapshot -Generation 'scope-incomplete-a' -Scope 'partial' -Units @((New-FixtureUnit 'unit-1' $a))
    $r = Compare-RmInventorySnapshots $partialScopeA $newSingle
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and $r.counts.UNCHANGED -eq 0) 'C1-A-platform-partial-common-unit'
    $undeclared = New-FixtureSnapshot -Generation 'no-declared-scope'
    $undeclared.coverage = @()
    $r = Compare-RmInventorySnapshots $undeclared $newSingle
    Assert-Synthetic ($r.counts.NOT_COMPARABLE -eq 1 -and $r.counts.UNKNOWN -eq 0 -and $r.entries[0].reason -ceq 'SCOPE_INTERSECTION_NOT_DECLARED') 'C2-undeclared-scope-not-comparable'

    # C2: observation digest is NOT a content-byte hash.
    $changedRow = @($result.entries | Where-Object { $_.romUnitId -ceq 'unit-modified' })[0]
    $sameRow = @($result.entries | Where-Object { $_.romUnitId -ceq 'unit-unchanged' })[0]
    Assert-Synthetic ($changedRow.changedFields.Count -eq 1 -and $changedRow.changedFields[0].field -ceq 'observationDigest' -and $changedRow.changedFields[0].evidence -ceq 'unit-observation-only' -and $changedRow.comparedFields.Count -eq 0) 'C2-changed-only-observation-evidence'
    Assert-Synthetic ($sameRow.comparedFields.Count -eq 1 -and $sameRow.comparedFields[0].field -ceq 'observationDigest' -and $sameRow.changedFields.Count -eq 0) 'C2-unchanged-only-compared-observation'
    $again = Compare-RmInventorySnapshots -Before $before -After $after
    Assert-Synthetic ((ConvertTo-Json -InputObject $result -Depth 12 -Compress) -ceq (ConvertTo-Json -InputObject $again -Depth 12 -Compress)) 'C2-deterministic-repeated-invocation'
    Assert-Synthetic (($result.counts.ADDED + $result.counts.CHANGED + $result.counts.UNCHANGED +
        $result.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE + $result.counts.UNKNOWN +
        $result.counts.NOT_COMPARABLE) -eq $result.entries.Count) 'C2-six-counts-exhaustive'

    $offline = New-FixtureSnapshot -Generation 'offline-cache' -At '2026-10-21T12:00:00Z' -Availability 'OFFLINE' -Freshness 'STALE' -Units @((New-FixtureUnit 'unit-1' $a))
    $r = Compare-RmInventorySnapshots $oldSingle $offline
    Assert-Synthetic ($r.counts.UNCHANGED -eq 1 -and $r.afterAvailability -ceq 'OFFLINE' -and $r.afterFreshness -ceq 'STALE') 'offline-stale-cached-observation'
    $offlinePartial = New-FixtureSnapshot -Generation 'offline-partial' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Availability 'OFFLINE' -Freshness 'STALE'
    $r = Compare-RmInventorySnapshots $oldSingle $offlinePartial
    Assert-Synthetic ($r.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 0 -and $r.counts.UNKNOWN -eq 1 -and $r.afterAvailability -ceq 'OFFLINE') 'offline-partial-never-deleted'
    $failedAfter = New-FixtureSnapshot -Generation 'failed' -At '2026-10-21T12:00:00Z' -Scan 'failed'
    $r = Compare-RmInventorySnapshots $oldSingle $failedAfter
    Assert-Synthetic ($r.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 0 -and $r.counts.UNKNOWN -eq 1) 'failed-scan-no-removal'
    $missingDigest = New-FixtureSnapshot -Generation 'unknown-digest' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' $null))
    $r = Compare-RmInventorySnapshots $oldSingle $missingDigest
    Assert-Synthetic ($r.counts.UNKNOWN -eq 1 -and $r.entries[0].reason -ceq 'OBSERVATION_DIGEST_UNKNOWN') 'unknown-digest-not-equal'

    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'other-lib' -Library 'other' -At '2026-10-21T12:00:00Z') } 'SNAPSHOT_LIBRARY_MISMATCH'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'old-single' -At '2026-10-21T12:00:00Z') } 'SNAPSHOT_GENERATION_REUSED'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $newSingle $oldSingle } 'SNAPSHOT_DATE_ORDER_INVALID'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'bad-date' -At '2026-10-21') } 'SNAPSHOT_UTC_DATE_REQUIRED'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'duplicate' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' $a),(New-FixtureUnit 'unit-1' $b))) } 'SNAPSHOT_DUPLICATE_UNIT_ID'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'bad-hash' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' 'bad'))) } 'SNAPSHOT_DIGEST_INVALID'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'scope-missing' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' $a -Console 'PS1'))) } 'SNAPSHOT_COVERAGE_SCOPE_MISSING'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'unknown-scope' -At '2026-10-21T12:00:00Z' -Scope 'unobserved' -Units @((New-FixtureUnit 'unit-1' $a))) } 'SNAPSHOT_UNIT_IN_UNOBSERVED_SCOPE'
    $hasFileId = New-FixtureUnit 'unit-1' $a
    $hasFileId | Add-Member -NotePropertyName fileId -NotePropertyValue 'file-1'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $oldSingle (New-FixtureSnapshot -Generation 'file-id' -At '2026-10-21T12:00:00Z' -Units @($hasFileId)) } 'SNAPSHOT_FILEID_NOT_PERSISTENT'
    $invalidScan = New-FixtureSnapshot -Generation 'invalid-scan' -Scan 'incomplete'
    Assert-ThrowsCode { Compare-RmInventorySnapshots $invalidScan $newBlank } 'SNAPSHOT_CHOICE_INVALID'
    $duplicates = New-FixtureSnapshot -Generation 'coverage-dupe'
    $duplicates.coverage = @($duplicates.coverage[0],$duplicates.coverage[0])
    Assert-ThrowsCode { Compare-RmInventorySnapshots $duplicates $newBlank } 'SNAPSHOT_DUPLICATE_COVERAGE'

    Write-Host ('SNAPSHOT_SYNTHETIC_PASS | assertions=' + $script:passed + ' | input=synthetic-memory-objects | physical_scans=0')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
