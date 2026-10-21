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
        param([string]$Id, [string]$Digest, [string]$Game='game-1',
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
    Assert-Synthetic ($result.counts.unchanged -eq 1 -and (Get-Classification $result 'unit-unchanged') -ceq 'unchanged') 'unchanged'
    Assert-Synthetic ($result.counts.modified -eq 1 -and (Get-Classification $result 'unit-modified') -ceq 'modified') 'modified'
    Assert-Synthetic ($result.counts.added -eq 1 -and (Get-Classification $result 'unit-added') -ceq 'added') 'added'
    Assert-Synthetic ($result.counts.notObserved -eq 1 -and (Get-Classification $result 'unit-not-observed') -ceq 'notObserved') 'observed-absence-not-deletion'
    Assert-Synthetic ($result.counts.notComparable -eq 1 -and (Get-Classification $result 'unit-conflict') -ceq 'notComparable') 'owner-conflict'
    Assert-Synthetic (@($result.entries | Where-Object { $_.classification -ceq 'deleted' }).Count -eq 0) 'never-infer-physical-deletion'
    $ids = @($result.entries | ForEach-Object { $_.romUnitId })
    Assert-Synthetic (($ids -join ',') -ceq (@($ids | Sort-Object) -join ',')) 'deterministic-identity-order'

    $empty = New-FixtureSnapshot -Generation 'empty'
    $later = New-FixtureSnapshot -Generation 'later' -At '2026-10-21T12:00:00Z'
    $emptyResult = Compare-RmInventorySnapshots $empty $later
    Assert-Synthetic ($emptyResult.entries.Count -eq 0 -and $emptyResult.counts.added -eq 0) 'empty-array-safe'
    $oldSingle = New-FixtureSnapshot -Generation 'old-single' -Units @((New-FixtureUnit 'unit-1' $a))
    $newSingle = New-FixtureSnapshot -Generation 'new-single' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' $a))
    $oldBlank = New-FixtureSnapshot -Generation 'old-blank'
    $newBlank = New-FixtureSnapshot -Generation 'new-blank' -At '2026-10-21T12:00:00Z'

    $partialAfter = New-FixtureSnapshot -Generation 'partial-after' -At '2026-10-21T12:00:00Z' -Scan 'partial'
    $r = Compare-RmInventorySnapshots $oldSingle $partialAfter
    Assert-Synthetic ((Get-Classification $r 'unit-1') -ceq 'notComparable' -and $r.counts.notObserved -eq 0) 'partial-after-never-disappearance'
    $partialBefore = New-FixtureSnapshot -Generation 'partial-before' -Scan 'partial'
    $r = Compare-RmInventorySnapshots $partialBefore $newSingle
    Assert-Synthetic ((Get-Classification $r 'unit-1') -ceq 'notComparable' -and $r.counts.added -eq 0) 'partial-before-never-added'
    $unverifiedScope = New-FixtureSnapshot -Generation 'scope-partial' -At '2026-10-21T12:00:00Z' -Scope 'partial'
    $r = Compare-RmInventorySnapshots $oldSingle $unverifiedScope
    Assert-Synthetic ((Get-Classification $r 'unit-1') -ceq 'notComparable') 'platform-partial-never-disappearance'

    $offline = New-FixtureSnapshot -Generation 'offline-cache' -At '2026-10-21T12:00:00Z' -Availability 'OFFLINE' -Freshness 'STALE' -Units @((New-FixtureUnit 'unit-1' $a))
    $r = Compare-RmInventorySnapshots $oldSingle $offline
    Assert-Synthetic ($r.counts.unchanged -eq 1 -and $r.afterAvailability -ceq 'OFFLINE' -and $r.afterFreshness -ceq 'STALE') 'offline-stale-cached-observation'
    $offlinePartial = New-FixtureSnapshot -Generation 'offline-partial' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Availability 'OFFLINE' -Freshness 'STALE'
    $r = Compare-RmInventorySnapshots $oldSingle $offlinePartial
    Assert-Synthetic ($r.counts.notObserved -eq 0 -and $r.counts.notComparable -eq 1 -and $r.afterAvailability -ceq 'OFFLINE') 'offline-partial-never-deleted'
    $failedAfter = New-FixtureSnapshot -Generation 'failed' -At '2026-10-21T12:00:00Z' -Scan 'failed'
    $r = Compare-RmInventorySnapshots $oldSingle $failedAfter
    Assert-Synthetic ($r.counts.notObserved -eq 0 -and $r.counts.notComparable -eq 1) 'failed-scan-no-removal'
    $missingDigest = New-FixtureSnapshot -Generation 'unknown-digest' -At '2026-10-21T12:00:00Z' -Units @((New-FixtureUnit 'unit-1' $null))
    $r = Compare-RmInventorySnapshots $oldSingle $missingDigest
    Assert-Synthetic ($r.counts.notComparable -eq 1 -and $r.entries[0].reason -ceq 'OBSERVATION_DIGEST_UNKNOWN') 'unknown-digest-not-equal'

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
