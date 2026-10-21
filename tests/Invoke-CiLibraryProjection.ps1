# Synthetic only. Windows PowerShell 5.1; never read a real library, ROM or StateRoot.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
try {
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {
        throw 'LIBRARY_TEST_REQUIRES_WINDOWS_PS51'
    }
    $root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    . (Join-Path $PSScriptRoot 'fixtures\SyntheticLibrarySnapshots.ps1')
    Import-Module (Join-Path $root 'Modules\LibrarySnapshotProjection.psm1') -Force -ErrorAction Stop
    $script:passed=0
    function Assert-View {
        param([bool]$Condition,[string]$Name)
        if (-not $Condition) { throw ('LIBRARY_FIXTURE_FAILED:'+$Name) }
        $script:passed++
    }
    function Assert-Refuses {
        param([scriptblock]$Action,[string]$Code)
        $message=''
        try { & $Action | Out-Null } catch { $message=$_.Exception.Message }
        Assert-View ($message.Contains($Code)) ('refuse-'+$Code)
    }
    $old=New-RmSyntheticSnapshot -Id 'snap-a' -Generation 'gen-a' -At '2026-10-20T12:00:00Z' -Units @((New-RmSyntheticUnit 'unit-same'),(New-RmSyntheticUnit 'unit-gone'))
    $new=New-RmSyntheticSnapshot -Id 'snap-b' -Generation 'gen-b' -At '2026-10-21T12:00:00Z' -Units @((New-RmSyntheticUnit 'unit-same'),(New-RmSyntheticUnit 'unit-added'))
    $reg=New-RmSyntheticRegistry -Snapshots @($old,$new)
    $cards=New-RmLibraryProjection -Registry $reg
    Assert-View ($cards.readOnly -and $cards.libraries.Count -eq 1 -and $cards.libraries[0].generations.Count -eq 2) 'online-registry-two-generations'
    Assert-View ($cards.libraries[0].lastVerifiedCompleteRef -ceq 'snap-b' -and $cards.libraries[0].lastObservedAtUtc -ceq '2026-10-21T12:00:00Z') 'verified-ref-and-date'
    $view=Get-RmLibraryComparisonView -Registry $reg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b'
    Assert-View ($view.readOnly -and $view.coverageStatus -ceq 'COMPLETE' -and $view.scopeCompared.Count -eq 1 -and $view.scopeNotComparable.Count -eq 0) 'online-complete-covered'
    Assert-View ($view.counts.ADDED -eq 1 -and $view.counts.UNCHANGED -eq 1 -and $view.counts.NOT_OBSERVED_IN_B_COVERED_SCOPE -eq 1 -and $view.countDenominator -eq 3) 'six-outcomes-from-existing-comparator'
    Assert-View ($view.before.snapshotId -ceq 'snap-a' -and $view.after.scanId -ceq 'scan-snap-b' -and $view.before.coverage[0].status -ceq 'complete') 'snapshot-refs-and-proof'

    $offline=New-RmSyntheticRegistry -Availability 'OFFLINE' -Freshness 'STALE' -Snapshots @($old,$new)
    $history=Get-RmLibraryComparisonView -Registry $offline -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b'
    Assert-View ($history.library.currentAvailability -ceq 'OFFLINE' -and $history.library.freshness -ceq 'STALE' -and $history.counts.ADDED -eq 1 -and $history.coverageStatus -ceq 'COMPLETE') 'offline-stale-historical-usable'
    Assert-View (@($history.limitations | Where-Object { $_ -ceq 'CURRENT_SOURCE_NOT_CONFIRMED_ONLINE' }).Count -eq 1) 'offline-current-limit'

    $partial=New-RmSyntheticSnapshot -Id 'snap-partial' -Generation 'gen-partial' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Units @((New-RmSyntheticUnit 'unit-same'),(New-RmSyntheticUnit 'unit-new'))
    $partialReg=New-RmSyntheticRegistry -Snapshots @($old,$partial) -LastComplete 'snap-a'
    $v=Get-RmLibraryComparisonView -Registry $partialReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-partial'
    Assert-View ($v.coverageStatus -ceq 'INCOMPLETE' -and $v.scopeNotComparable.Count -eq 1 -and $v.counts.UNKNOWN -eq 3 -and $v.counts.ADDED -eq 0) 'partial-b-fail-closed'
    Assert-View ($v.library.lastVerifiedCompleteRef -ceq 'snap-a') 'partial-does-not-promote-last-complete'
    $partialA=New-RmSyntheticSnapshot -Id 'snap-pa' -Generation 'gen-pa' -At '2026-10-20T12:00:00Z' -Scan 'partial' -Units @((New-RmSyntheticUnit 'unit-same'))
    $paReg=New-RmSyntheticRegistry -Snapshots @($partialA,$new) -LastComplete 'snap-b'
    $pa=Get-RmLibraryComparisonView -Registry $paReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-pa' -AfterSnapshotId 'snap-b'
    Assert-View ($pa.counts.UNKNOWN -eq 2 -and $pa.counts.ADDED -eq 0 -and $pa.coverageStatus -ceq 'INCOMPLETE') 'partial-a-fail-closed'

    $emptyPartial=New-RmSyntheticSnapshot -Id 'snap-empty' -Generation 'gen-empty' -At '2026-10-21T12:00:00Z' -Scan 'partial'
    $emptyA=New-RmSyntheticSnapshot -Id 'snap-empty-a' -Generation 'gen-empty-a' -At '2026-10-20T12:00:00Z' -Units @()
    $zeroReg=New-RmSyntheticRegistry -Snapshots @($emptyA,$emptyPartial) -LastComplete 'snap-empty-a'
    $zero=Get-RmLibraryComparisonView -Registry $zeroReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-empty-a' -AfterSnapshotId 'snap-empty'
    Assert-View ($zero.entries.Count -eq 0 -and $zero.coverageStatus -ceq 'INCOMPLETE' -and $zero.scopeNotComparable.Count -eq 1) 'zero-observed-not-complete'
    Assert-View (@($zero.limitations | Where-Object { $_ -ceq 'ZERO_OBSERVED_UNITS_NOT_PROOF_OF_EMPTY_LIBRARY' }).Count -eq 1) 'zero-observed-warning'

    $s1=[pscustomobject]@{sourceKey='source-1';rootKey='root-1';domains=@('ROMS','MEDIAS')}
    $s2=[pscustomobject]@{sourceKey='source-2';rootKey='root-2';domains=@('ROMS')}
    $multiReg=New-RmSyntheticRegistry -Sources @($s1,$s2) -Snapshots @($old,$new)
    $multi=Get-RmLibraryComparisonView -Registry $multiReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b'
    Assert-View ($multi.coverageStatus -ceq 'INCOMPLETE' -and $multi.counts.UNKNOWN -eq 3 -and $multi.scopeNotComparable.Count -eq 1) 'multi-root-domain-missing-proofs'
    $explicit=New-RmSyntheticSnapshot -Id 'snap-unproven' -Generation 'gen-unproven' -At '2026-10-21T12:00:00Z' -Exhaustive $false -Units @((New-RmSyntheticUnit 'unit-same'))
    $unprovenReg=New-RmSyntheticRegistry -Snapshots @($old,$explicit) -LastComplete 'snap-a'
    $unproven=Get-RmLibraryComparisonView -Registry $unprovenReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-unproven'
    Assert-View ($unproven.coverageStatus -ceq 'INCOMPLETE' -and $unproven.counts.UNKNOWN -eq 2) 'exhaustiveness-flag-not-inferred'

    $missingIndex=New-RmSyntheticRegistry -Index $false -Snapshots @($old,$new)
    Assert-Refuses { Get-RmLibraryComparisonView -Registry $missingIndex -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b' } 'LIBRARY_INDEX_INACCESSIBLE'
    Assert-Refuses { Get-RmLibraryComparisonView -Registry $reg -LibraryId 'other' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b' } 'LIBRARY_NOT_FOUND'
    Assert-Refuses { Get-RmLibraryComparisonView -Registry $reg -LibraryId 'lib-fixture' -BeforeSnapshotId 'missing' -AfterSnapshotId 'snap-b' } 'LIBRARY_SNAPSHOT_NOT_FOUND'
    Assert-Refuses { Get-RmLibraryComparisonView -Registry $reg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-b' -AfterSnapshotId 'snap-a' } 'SNAPSHOT_DATE_ORDER_INVALID'
    Assert-Refuses { Get-RmLibraryComparisonView -Registry $reg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-a' } 'LIBRARY_SNAPSHOT_REUSED'
    $wrongSchema=New-RmSyntheticSnapshot -Id 'bad-schema' -Generation 'gen-bad' -At '2026-10-21T12:00:00Z'
    $wrongSchema.schemaVersion='2.0.0'
    Assert-Refuses { New-RmLibraryProjection -Registry (New-RmSyntheticRegistry -Snapshots @($old,$wrongSchema)) } 'LIBRARY_SCHEMA_UNSUPPORTED'
    $badProof=New-RmSyntheticSnapshot -Id 'bad-proof' -Generation 'gen-bad' -At '2026-10-21T12:00:00Z' -SourceKey 'other'
    Assert-Refuses { New-RmLibraryProjection -Registry (New-RmSyntheticRegistry -Snapshots @($old,$badProof)) } 'LIBRARY_UNDECLARED_PROOF_PARTITION'
    $dup=New-RmSyntheticSnapshot -Id 'duplicate' -Generation 'gen-bad' -At '2026-10-21T12:00:00Z'
    $dup.coverageProof=@($dup.coverageProof[0],$dup.coverageProof[0])
    Assert-Refuses { New-RmLibraryProjection -Registry (New-RmSyntheticRegistry -Snapshots @($old,$dup)) } 'LIBRARY_DUPLICATE_PROOF_PARTITION'
    $again=Get-RmLibraryComparisonView -Registry $reg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b'
    Assert-View ((ConvertTo-Json -InputObject $view -Depth 20 -Compress) -ceq (ConvertTo-Json -InputObject $again -Depth 20 -Compress)) 'deterministic-repeated-view'
    Write-Host ('LIBRARY_SYNTHETIC_PASS | assertions='+$script:passed+' | synthetic=1 | real_io=0')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
