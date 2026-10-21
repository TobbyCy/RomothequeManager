# Versioned synthetic UI reference vectors; no user files.
function New-RmUiProjectionVectors {
    $u1=New-RmSyntheticUnit 'unit-common'
    $u2=New-RmSyntheticUnit 'unit-removed'
    $u3=New-RmSyntheticUnit 'unit-new'
    $a=New-RmSyntheticSnapshot -Id 'snap-a' -Generation 'gen-a' -At '2026-10-20T12:00:00Z' -Units @($u1,$u2)
    $b=New-RmSyntheticSnapshot -Id 'snap-b' -Generation 'gen-b' -At '2026-10-21T12:00:00Z' -Units @($u1,$u3)
    $partial=New-RmSyntheticSnapshot -Id 'snap-partial' -Generation 'gen-partial' -At '2026-10-21T12:00:00Z' -Scan 'partial' -Units @($u1,$u3)
    $zeroA=New-RmSyntheticSnapshot -Id 'snap-zero-a' -Generation 'gen-zero-a' -At '2026-10-20T12:00:00Z'
    $zeroB=New-RmSyntheticSnapshot -Id 'snap-zero-b' -Generation 'gen-zero-b' -At '2026-10-21T12:00:00Z' -Exhaustive $false -Scan 'failed'
    $zeroB.coverageProof=@()
    $srcA=[pscustomobject]@{sourceKey='source-1';rootKey='root-1';domains=@('ROMS')}
    $srcB=[pscustomobject]@{sourceKey='source-2';rootKey='root-2';domains=@('ROMS')}
    $defs=@(
        [pscustomobject]@{scenarioId='online-complete';registry=(New-RmSyntheticRegistry -Snapshots @($a,$b));before='snap-a';after='snap-b';expectedCoverageStatus='COMPLETE';expectedCounts=@{ADDED=1;UNCHANGED=1;NOT_OBSERVED_IN_B_COVERED_SCOPE=1}},
        [pscustomobject]@{scenarioId='offline-stale';registry=(New-RmSyntheticRegistry -Availability 'OFFLINE' -Freshness 'STALE' -Snapshots @($a,$b));before='snap-a';after='snap-b';expectedCoverageStatus='COMPLETE';expectedCounts=@{ADDED=1;UNCHANGED=1;NOT_OBSERVED_IN_B_COVERED_SCOPE=1}},
        [pscustomobject]@{scenarioId='partial-b';registry=(New-RmSyntheticRegistry -Snapshots @($a,$partial) -LastComplete 'snap-a');before='snap-a';after='snap-partial';expectedCoverageStatus='INCOMPLETE';expectedCounts=@{UNKNOWN=3}},
        [pscustomobject]@{scenarioId='zero-unproven';registry=(New-RmSyntheticRegistry -Snapshots @($zeroA,$zeroB) -LastComplete 'snap-zero-a');before='snap-zero-a';after='snap-zero-b';expectedCoverageStatus='INCOMPLETE';expectedCounts=@{}},
        [pscustomobject]@{scenarioId='multi-root-incomplete';registry=(New-RmSyntheticRegistry -Sources @($srcA,$srcB) -Snapshots @($a,$b) -LastComplete $null);before='snap-a';after='snap-b';expectedCoverageStatus='INCOMPLETE';expectedCounts=@{UNKNOWN=3}},
        [pscustomobject]@{scenarioId='index-inaccessible';registry=(New-RmSyntheticRegistry -Index $false -Snapshots @($a,$b));before='snap-a';after='snap-b';expectedCoverageStatus=$null;expectedCounts=@{}}
    )
    # Namespace every identity; keep cross-scenario references deterministic.
    foreach ($v in $defs) {
        # Isolate the shared synthetic fixture objects before namespace mutation.
        $v.registry=(ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $v.registry -Depth 20 -Compress))
        $v.registry.libraries[0].libraryId=('lib-'+$v.scenarioId)
        foreach ($snap in $v.registry.libraries[0].snapshots) {
            $snap.snapshotId=($v.scenarioId+'-'+$snap.snapshotId)
            $snap.generationId=($v.scenarioId+'-'+$snap.generationId)
            $snap.scanId=('scan-'+$snap.snapshotId)
        }
        $v.registry.libraries[0].lastVerifiedCompleteRef=$(if($null -eq $v.registry.libraries[0].lastVerifiedCompleteRef){$null}else{$v.scenarioId+'-'+$v.registry.libraries[0].lastVerifiedCompleteRef})
        $v.before=($v.scenarioId+'-'+$v.before)
        $v.after=($v.scenarioId+'-'+$v.after)
    }
    return ,$defs
}
