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
        Assert-View ($message.Contains($Code)) ('refuse-'+$Code+';observed='+$message)
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
    $multiReg=New-RmSyntheticRegistry -Sources @($s1,$s2) -Snapshots @($old,$new) -LastComplete $null
    $multi=Get-RmLibraryComparisonView -Registry $multiReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b'
    Assert-View ($multi.coverageStatus -ceq 'INCOMPLETE' -and $multi.counts.UNKNOWN -eq 3 -and $multi.scopeNotComparable.Count -eq 1) 'multi-root-domain-missing-proofs'
    $unverifiedLast=New-RmSyntheticRegistry -Sources @($s1,$s2) -Snapshots @($old,$new)
    Assert-Refuses { New-RmLibraryProjection -Registry $unverifiedLast } 'LIBRARY_LAST_VERIFIED_SCOPE_NOT_COMPLETE'
    $sourceDuplicate=[pscustomobject]@{sourceKey='source-1';rootKey='root-other';domains=@('ROMS')}
    Assert-Refuses { New-RmLibraryProjection -Registry (New-RmSyntheticRegistry -Sources @($s1,$sourceDuplicate) -Snapshots @($old,$new) -LastComplete $null) } 'LIBRARY_DUPLICATE_SOURCE'
    # Complete multi-source/domain comparison requires EVERY declared partition.
    $allProof=@(
        [pscustomobject]@{console='GBA';sourceKey='source-1';rootKey='root-1';domain='ROMS';status='complete'},
        [pscustomobject]@{console='GBA';sourceKey='source-1';rootKey='root-1';domain='MEDIAS';status='complete'},
        [pscustomobject]@{console='GBA';sourceKey='source-2';rootKey='root-2';domain='ROMS';status='complete'}
    )
    $multiA=New-RmSyntheticSnapshot -Id 'multi-a' -Generation 'multi-gen-a' -At '2026-10-20T12:00:00Z' -Units @((New-RmSyntheticUnit 'unit-same'))
    $multiB=New-RmSyntheticSnapshot -Id 'multi-b' -Generation 'multi-gen-b' -At '2026-10-21T12:00:00Z' -Units @((New-RmSyntheticUnit 'unit-same'))
    $multiA.coverageProof=@($allProof)
    $multiB.coverageProof=@($allProof)
    $provenMulti=New-RmSyntheticRegistry -Sources @($s1,$s2) -Snapshots @($multiA,$multiB) -LastComplete 'multi-b'
    $multiFull=Get-RmLibraryComparisonView -Registry $provenMulti -LibraryId 'lib-fixture' -BeforeSnapshotId 'multi-a' -AfterSnapshotId 'multi-b'
    Assert-View ($multiFull.coverageStatus -ceq 'COMPLETE' -and $multiFull.counts.UNCHANGED -eq 1) 'multi-root-all-partitions-explicitly-proven'
    $unknownLive=New-RmSyntheticRegistry -Availability 'UNKNOWN' -Freshness 'UNKNOWN' -Snapshots @($old,$new)
    $unknownCard=New-RmLibraryProjection -Registry $unknownLive
    Assert-View ($unknownCard.libraries[0].currentAvailability -ceq 'UNKNOWN' -and $null -eq $unknownCard.libraries[0].availabilityObservedAtUtc) 'unknown-live-no-fabricated-timestamp'
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
    # Ordinal policy: case-only IDs remain distinct; delimiter-containing roots cannot collide.
    $caseA=New-RmSyntheticRegistry -Snapshots @($old,$new)
    $caseB=New-RmSyntheticRegistry -Snapshots @($old,$new)
    $caseB.libraries[0].libraryId='Lib-fixture'
    $caseRegistry=[pscustomobject]@{libraries=@($caseA.libraries[0],$caseB.libraries[0])}
    Assert-View ((New-RmLibraryProjection -Registry $caseRegistry).libraries.Count -eq 2) 'ordinal-library-id'
    $caseSnap=New-RmSyntheticSnapshot -Id 'Snap-a' -Generation 'Gen-a' -At '2026-10-19T12:00:00Z'
    $caseGen=New-RmSyntheticSnapshot -Id 'snap-a' -Generation 'gen-a' -At '2026-10-18T12:00:00Z'
    $caseSnapReg=New-RmSyntheticRegistry -Snapshots @($caseSnap,$caseGen) -LastComplete 'Snap-a'
    Assert-View ((New-RmLibraryProjection -Registry $caseSnapReg).libraries[0].generations.Count -eq 2) 'ordinal-snapshot-generation-id'
    $root1=[pscustomobject]@{sourceKey='a|b';rootKey='c';domains=@('ROMS')}
    $root2=[pscustomobject]@{sourceKey='a';rootKey='b|c';domains=@('ROMS')}
    $rootA=New-RmSyntheticSnapshot -Id 'root-a' -Generation 'root-gen-a' -At '2026-10-20T12:00:00Z'
    $rootB=New-RmSyntheticSnapshot -Id 'root-b' -Generation 'root-gen-b' -At '2026-10-21T12:00:00Z'
    $rootA.coverageProof=@([pscustomobject]@{console='GBA';sourceKey='a|b';rootKey='c';domain='ROMS';status='complete'})
    $rootB.coverageProof=@([pscustomobject]@{console='GBA';sourceKey='a';rootKey='b|c';domain='ROMS';status='complete'})
    $rootReg=New-RmSyntheticRegistry -Sources @($root1,$root2) -Snapshots @($rootA,$rootB) -LastComplete $null
    $rootView=Get-RmLibraryComparisonView -Registry $rootReg -LibraryId 'lib-fixture' -BeforeSnapshotId 'root-a' -AfterSnapshotId 'root-b'
    Assert-View ($rootView.coverageStatus -ceq 'INCOMPLETE' -and $rootView.scopeNotComparable.Count -eq 1) 'delimiters-do-not-create-false-completeness'
    $caseSource=[pscustomobject]@{sourceKey='Source-1';rootKey='Root-1';domains=@('ROMS')}
    $caseRoot=[pscustomobject]@{sourceKey='source-1';rootKey='root-1';domains=@('ROMS')}
    $caseRoots=New-RmSyntheticRegistry -Sources @($caseSource,$caseRoot) -Snapshots @($old,$new) -LastComplete $null
    Assert-View ((New-RmLibraryProjection -Registry $caseRoots).libraries[0].generations[0].coverage[0].status -cne 'complete') 'case-only-partitions-not-aggregated'
    $inaccessible=New-RmLibraryProjection -Registry $missingIndex
    Assert-View (-not $inaccessible.libraries[0].generations[0].countKnown -and $null -eq $inaccessible.libraries[0].generations[0].observedUnitCount) 'index-inaccessible-count-null'
    $emptyUnknown=New-RmSyntheticSnapshot -Id 'unknown-empty' -Generation 'gen-unknown-empty' -At '2026-10-21T12:00:00Z' -Scan 'failed' -Exhaustive $false
    $emptyUnknown.coverageProof=@()
    $unknownRegistry=New-RmSyntheticRegistry -Snapshots @($emptyUnknown) -LastComplete $null
    $unknownProjection=New-RmLibraryProjection -Registry $unknownRegistry
    Assert-View ($unknownProjection.libraries[0].generations[0].coverage[0].status -cne 'complete') 'failed-no-proof-never-complete'
    $noExhaust=New-RmSyntheticSnapshot -Id 'no-exhaust' -Generation 'gen-no-exhaust' -At '2026-10-21T12:00:00Z' -Exhaustive $false
    $noExhaust.coverageProof=@()
    $noExhaustProjection=New-RmLibraryProjection -Registry (New-RmSyntheticRegistry -Snapshots @($noExhaust) -LastComplete $null)
    Assert-View ($noExhaustProjection.libraries[0].generations[0].coverage[0].status -cne 'complete') 'empty-proof-not-exhaustive'
    # S3: mutations must reach the actual registry/projection validators.
    $caseCollision=New-RmSyntheticRegistry -Snapshots @($old,$new)
    $caseCollision.libraries[0].snapshots[1].snapshotId=$caseCollision.libraries[0].snapshots[0].snapshotId
    Assert-Refuses { New-RmLibraryProjection -Registry $caseCollision } 'LIBRARY_DUPLICATE_SNAPSHOT_OR_GENERATION'
    $generationCollision=New-RmSyntheticRegistry -Snapshots @($old,$new)
    $generationCollision.libraries[0].snapshots[1].generationId=$generationCollision.libraries[0].snapshots[0].generationId
    Assert-Refuses { New-RmLibraryProjection -Registry $generationCollision } 'LIBRARY_DUPLICATE_SNAPSHOT_OR_GENERATION'
    $invalidLast=New-RmSyntheticRegistry -Snapshots @($old,$partial) -LastComplete 'snap-partial'
    Assert-Refuses { New-RmLibraryProjection -Registry $invalidLast } 'LIBRARY_LAST_VERIFIED_NOT_COMPLETE'
    $noIndex=New-RmSyntheticRegistry -Index $false -Snapshots @($old,$new)
    Assert-Refuses { Get-RmLibraryComparisonView -Registry $noIndex -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b' } 'LIBRARY_INDEX_INACCESSIBLE'
    $caseDuplicate=New-RmSyntheticRegistry -Snapshots @($old,$new) -LastComplete 'snap-a'
    $caseDuplicate.libraries[0].snapshots[1].snapshotId='SNAP-A'
    Assert-View ((New-RmLibraryProjection -Registry $caseDuplicate).libraries[0].generations.Count -eq 2) 'ordinal-snapshot-case-only-distinct'
    $again=Get-RmLibraryComparisonView -Registry $reg -LibraryId 'lib-fixture' -BeforeSnapshotId 'snap-a' -AfterSnapshotId 'snap-b'
    Assert-View ((ConvertTo-Json -InputObject $view -Depth 20 -Compress) -ceq (ConvertTo-Json -InputObject $again -Depth 20 -Compress)) 'deterministic-repeated-view'
    # STAB-03A: a proof on GBA must not promote an unobserved PS1 scope.
    $twoPlatforms=New-RmSyntheticRegistry -Snapshots @($old,$new) -LastComplete $null
    $twoPlatforms.libraries[0].platforms=@('GBA','PS1')
    $twoPlatformCards=New-RmLibraryProjection -Registry $twoPlatforms
    $actualCoverage=@($twoPlatformCards.libraries[0].generations | Where-Object { $_.snapshotId -ceq 'snap-b' })[0].coverage
    Assert-View (@($actualCoverage | Where-Object { $_.console -ceq 'GBA' -and $_.status -ceq 'complete' }).Count -eq 1) 'gba-proof-remains-complete'
    Assert-View (@($actualCoverage | Where-Object { $_.console -ceq 'PS1' -and $_.status -ceq 'unobserved' }).Count -eq 1) 'foreign-platform-proof-not-partial'
    $twoPlatforms.libraries[0].snapshots[1].coverageProof[0].status='partial'
    $partialPlatform=New-RmLibraryProjection -Registry $twoPlatforms
    $partialCov=@($partialPlatform.libraries[0].generations | Where-Object { $_.snapshotId -ceq 'snap-b' })[0].coverage
    Assert-View (@($partialCov | Where-Object { $_.console -ceq 'GBA' -and $_.status -ceq 'partial' }).Count -eq 1 -and
                 @($partialCov | Where-Object { $_.console -ceq 'PS1' -and $_.status -ceq 'unobserved' }).Count -eq 1) 'platform-local-partial-not-global'

    # STAB-03A/INT-01: UTC fraction must win over the same second without
    # a fractional component, regardless of input snapshot enumeration order.
    $fractionEarly=New-RmSyntheticSnapshot -Id 'clock-a' -Generation 'clock-ga' -At '2026-10-21T12:00:00Z'
    $fractionLate=New-RmSyntheticSnapshot -Id 'clock-b' -Generation 'clock-gb' -At '2026-10-21T12:00:00.900Z'
    $clockRegistry=New-RmSyntheticRegistry -Snapshots @($fractionLate,$fractionEarly) -LastComplete 'clock-b'
    $clockProjection=New-RmLibraryProjection -Registry $clockRegistry
    Assert-View ($clockProjection.libraries[0].lastObservedAtUtc -ceq '2026-10-21T12:00:00.900Z') 'fractional-utc-chronological-latest'
    $clockRegistry.libraries[0].snapshots=@($fractionEarly,$fractionLate)
    Assert-View ((New-RmLibraryProjection -Registry $clockRegistry).libraries[0].lastObservedAtUtc -ceq
                 $clockProjection.libraries[0].lastObservedAtUtc) 'fractional-utc-input-order-independent'

    Write-Host ('LIBRARY_SYNTHETIC_PASS | assertions='+$script:passed+' | synthetic=1 | real_io=0')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
