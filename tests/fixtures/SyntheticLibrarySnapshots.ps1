# Synthetic fixtures only. Dot-source from the read-only PowerShell 5.1 harness.
function New-RmSyntheticUnit {
    param([string]$Id, [string]$Digest=('a'*64), [string]$Console='GBA')
    [pscustomobject]@{romUnitId=$Id;gameId='game-1';variantId='variant-1';console=$Console;observationDigest=$Digest}
}
function New-RmSyntheticSnapshot {
    param([string]$Id,[string]$Generation,[string]$At,[string]$Scan='complete',
          [string]$Proof='complete',[bool]$Exhaustive=$true,[object[]]$Units=@(),
          [string]$SourceKey='source-1',[string]$RootKey='root-1',[string]$Domain='ROMS')
    [pscustomobject]@{
        snapshotId=$Id;scanId=('scan-'+$Id);generationId=$Generation;capturedAtUtc=$At
        schemaVersion='1.0.0';scanStatus=$Scan;scopeIsExhaustive=$Exhaustive
        coverageProof=@([pscustomobject]@{console='GBA';sourceKey=$SourceKey;rootKey=$RootKey;domain=$Domain;status=$Proof})
        units=@($Units)
    }
}
function New-RmSyntheticRegistry {
    param([string]$Availability='ONLINE',[string]$Freshness='FRESH',[bool]$Index=$true,
          [object[]]$Snapshots=@(),[object[]]$Sources=@(),[string]$LastComplete='snap-b')
    if ($Sources.Count -eq 0) {
        $Sources=@([pscustomobject]@{sourceKey='source-1';rootKey='root-1';domains=@('ROMS')})
    }
    [pscustomobject]@{libraries=@([pscustomobject]@{
        libraryId='lib-fixture';name='Bibliotheque fictive';platforms=@('GBA')
        sources=@($Sources);currentAvailability=$Availability
        availabilityObservedAtUtc=$(if($Availability -ceq 'UNKNOWN'){$null}else{'2026-10-21T10:00:00Z'})
        freshness=$Freshness;indexAccessible=$Index
        lastVerifiedCompleteRef=$LastComplete;snapshots=@($Snapshots)
    })}
}
