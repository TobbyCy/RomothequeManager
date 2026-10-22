# In-memory library registry projection for synthetic inputs only (Issue #10).
# No filesystem scans, no StateRoot, no ROM access or persistence.
Set-StrictMode -Version 2.0

function Get-RmLibraryRequired {
    param($Record,[string]$Name,[string]$Context)
    if ($null -eq $Record) { throw ('LIBRARY_NULL:'+$Context) }
    $prop = $Record.PSObject.Properties[$Name]
    if ($null -eq $prop) { throw ('LIBRARY_FIELD_MISSING:'+$Context+'.'+$Name) }
    return ,$prop.Value
}
function Get-RmLibraryText {
    param($Record,[string]$Name,[string]$Context)
    $value=Get-RmLibraryRequired $Record $Name $Context
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        throw ('LIBRARY_FIELD_INVALID:'+$Context+'.'+$Name)
    }
    return $value
}
function Assert-RmLibraryDate {
    param([string]$Value)
    if ($Value -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$') {
        throw 'LIBRARY_UTC_DATE_REQUIRED'
    }
    $parsed=[DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($Value,[Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal,[ref]$parsed) -or
        $parsed.Offset -ne [TimeSpan]::Zero) { throw 'LIBRARY_UTC_DATE_INVALID' }
    return $parsed
}
function Assert-RmLibraryChoice {
    param([string]$Value,[string[]]$Choices,[string]$Context)
    if ($Choices -cnotcontains $Value) { throw ('LIBRARY_CHOICE_INVALID:'+$Context) }
}
function Assert-RmLibraryArray {
    param($Value,[string]$Context)
    if ($Value -isnot [array]) { throw ('LIBRARY_ARRAY_REQUIRED:'+$Context) }
}
# Identifiers are case-sensitive ordinal strings; no delimiter-based composite keys.
function New-RmOrdinalSet { return ,(New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)) }
function New-RmOrdinalMap { return ,(New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)) }
function Get-RmPartitionKey {
    param([string]$Console,[string]$SourceKey,[string]$RootKey,[string]$Domain)
    # Length-prefix each segment so embedded separators never collide.
    return ('{0}:{1}{2}:{3}{4}:{5}{6}:{7}' -f $Console.Length,$Console,$SourceKey.Length,$SourceKey,$RootKey.Length,$RootKey,$Domain.Length,$Domain)
}
function Get-RmLibraryCoverage {
    param($Library,$Snapshot)
    $scan=Get-RmLibraryText $Snapshot 'scanStatus' 'snapshot'
    Assert-RmLibraryChoice $scan @('complete','partial','failed') 'scanStatus'
    $version=Get-RmLibraryText $Snapshot 'schemaVersion' 'snapshot'
    if ($version -cne '1.0.0') { throw 'LIBRARY_SCHEMA_UNSUPPORTED' }
    $proof=Get-RmLibraryRequired $Snapshot 'coverageProof' 'snapshot'
    Assert-RmLibraryArray $proof 'coverageProof'
    $exhaustive=Get-RmLibraryRequired $Snapshot 'scopeIsExhaustive' 'snapshot'
    if ($exhaustive -isnot [bool]) { throw 'LIBRARY_SCOPE_PROOF_INVALID' }
    $expected=New-RmOrdinalMap
    foreach ($platform in $Library.platforms) {
        foreach ($source in $Library.sources) {
            foreach ($domain in $source.domains) {
                $key=Get-RmPartitionKey $platform $source.sourceKey $source.rootKey $domain
                $expected[$key]=$true
            }
        }
    }
    $seen=New-RmOrdinalMap
    foreach ($item in $proof) {
        $platform=Get-RmLibraryText $item 'console' 'coverageProof'
        $source=Get-RmLibraryText $item 'sourceKey' 'coverageProof'
        $root=Get-RmLibraryText $item 'rootKey' 'coverageProof'
        $domain=Get-RmLibraryText $item 'domain' 'coverageProof'
        $status=Get-RmLibraryText $item 'status' 'coverageProof'
        Assert-RmLibraryChoice $status @('complete','partial','unobserved') 'coverageProof.status'
        $key=Get-RmPartitionKey $platform $source $root $domain
        if (-not $expected.ContainsKey($key)) { throw 'LIBRARY_UNDECLARED_PROOF_PARTITION' }
        if ($seen.ContainsKey($key)) { throw 'LIBRARY_DUPLICATE_PROOF_PARTITION' }
        $seen[$key]=$status
    }
    $coverage=New-Object 'System.Collections.Generic.List[object]'
    foreach ($platform in @($Library.platforms | Sort-Object -CaseSensitive)) {
        $allComplete=$true; $anyMissing=$false; $anyPartial=$false; $anyObserved=$false
        foreach ($source in $Library.sources) {
            foreach ($domain in $source.domains) {
                $key=Get-RmPartitionKey $platform $source.sourceKey $source.rootKey $domain
                if (-not $seen.ContainsKey($key)) { $allComplete=$false; $anyMissing=$true }
                else {
                    # Only this console's partitions count as observations.
                    # Proofs for another platform cannot make this console PARTIAL.
                    if ($seen[$key] -ceq 'complete' -or $seen[$key] -ceq 'partial') {
                        $anyObserved=$true
                    }
                    if ($seen[$key] -cne 'complete') {
                        $allComplete=$false
                        if ($seen[$key] -ceq 'partial') { $anyPartial=$true }
                        else { $anyMissing=$true }
                    }
                }
            }
        }
        $status='unobserved'
        if ($exhaustive -and $scan -ceq 'complete' -and $allComplete) { $status='complete' }
        elseif ($anyPartial -or $anyObserved) { $status='partial' }
        $coverage.Add([pscustomobject]@{
            console=$platform;status=$status
            reason=$(if ($status -ceq 'complete') {'ALL_DECLARED_PARTITIONS_PROVEN'}
                elseif (-not $exhaustive) {'SCOPE_EXHAUSTIVENESS_NOT_PROVEN'}
                elseif ($scan -cne 'complete') {'SCAN_NOT_COMPLETE'}
                elseif ($anyMissing) {'PARTITION_NOT_OBSERVED'}
                else {'PARTITION_NOT_COMPLETE'})
        })
    }
    return ,$coverage.ToArray()
}
function Assert-RmLibraryRegistry {
    param($Registry)
    $libraries=Get-RmLibraryRequired $Registry 'libraries' 'registry'
    Assert-RmLibraryArray $libraries 'libraries'
    $ids=New-RmOrdinalSet
    foreach ($lib in $libraries) {
        $id=Get-RmLibraryText $lib 'libraryId' 'library'
        [void](Get-RmLibraryText $lib 'name' 'library')
        if (-not $ids.Add($id)) { throw 'LIBRARY_DUPLICATE_ID' }
        $platforms=Get-RmLibraryRequired $lib 'platforms' 'library'
        $sources=Get-RmLibraryRequired $lib 'sources' 'library'
        $snapshots=Get-RmLibraryRequired $lib 'snapshots' 'library'
        Assert-RmLibraryArray $platforms 'platforms'
        Assert-RmLibraryArray $sources 'sources'
        Assert-RmLibraryArray $snapshots 'snapshots'
        if ($platforms.Count -eq 0 -or $sources.Count -eq 0) { throw 'LIBRARY_EMPTY_DECLARED_SCOPE' }
        $platformKeys=New-RmOrdinalSet; $sourceKeys=New-RmOrdinalSet
        foreach ($platform in $platforms) {
            if ($platform -isnot [string] -or $platform -cnotmatch '^[A-Z][A-Z0-9_-]*$') { throw 'LIBRARY_PLATFORM_INVALID' }
            if (-not $platformKeys.Add($platform)) { throw 'LIBRARY_DUPLICATE_PLATFORM' }
        }
        foreach ($source in $sources) {
            $key=Get-RmLibraryText $source 'sourceKey' 'source'
            $root=Get-RmLibraryText $source 'rootKey' 'source'
            $domains=Get-RmLibraryRequired $source 'domains' 'source'
            Assert-RmLibraryArray $domains 'source.domains'
            if ($domains.Count -eq 0) { throw 'LIBRARY_SOURCE_DOMAINS_EMPTY' }
            if (-not $sourceKeys.Add($key)) { throw 'LIBRARY_DUPLICATE_SOURCE' }
            $domainKeys=New-RmOrdinalSet
            foreach ($domain in $domains) {
                Assert-RmLibraryChoice $domain @('ROMS','MEDIAS') 'source.domain'
                if (-not $domainKeys.Add($domain)) { throw 'LIBRARY_DUPLICATE_DOMAIN' }
            }
        }
        $availability=Get-RmLibraryText $lib 'currentAvailability' 'library'
        $freshness=Get-RmLibraryText $lib 'freshness' 'library'
        Assert-RmLibraryChoice $availability @('ONLINE','OFFLINE','INACCESSIBLE','UNKNOWN') 'availability'
        Assert-RmLibraryChoice $freshness @('FRESH','STALE','UNKNOWN') 'freshness'
        $index=Get-RmLibraryRequired $lib 'indexAccessible' 'library'
        if ($index -isnot [bool]) { throw 'LIBRARY_INDEX_FLAG_INVALID' }
        $observed=Get-RmLibraryRequired $lib 'availabilityObservedAtUtc' 'library'
        if ($null -ne $observed) { [void](Assert-RmLibraryDate $observed) }
        if ($availability -ceq 'UNKNOWN' -and $null -ne $observed) { throw 'LIBRARY_UNKNOWN_AVAILABILITY_HAS_DATE' }
        if ($availability -cne 'UNKNOWN' -and $null -eq $observed) { throw 'LIBRARY_AVAILABILITY_DATE_MISSING' }
        $last=Get-RmLibraryRequired $lib 'lastVerifiedCompleteRef' 'library'
        $snapKeys=New-RmOrdinalMap; $genKeys=New-RmOrdinalSet
        foreach ($snap in $snapshots) {
            $snapId=Get-RmLibraryText $snap 'snapshotId' 'snapshot'
            [void](Get-RmLibraryText $snap 'scanId' 'snapshot')
            $gen=Get-RmLibraryText $snap 'generationId' 'snapshot'
            $date=Get-RmLibraryText $snap 'capturedAtUtc' 'snapshot'
            [void](Assert-RmLibraryDate $date)
            $units=Get-RmLibraryRequired $snap 'units' 'snapshot'
            Assert-RmLibraryArray $units 'snapshot.units'
            if ($snapKeys.ContainsKey($snapId) -or -not $genKeys.Add($gen)) { throw 'LIBRARY_DUPLICATE_SNAPSHOT_OR_GENERATION' }
            $snapKeys[$snapId]=$snap
            [void](Get-RmLibraryCoverage $lib $snap)
        }
        if ($null -ne $last) {
            if ($last -isnot [string] -or -not $snapKeys.ContainsKey($last)) { throw 'LIBRARY_LAST_VERIFIED_REF_INVALID' }
            $lastSnapshot=$snapKeys[$last]
            if ($lastSnapshot.scanStatus -cne 'complete') { throw 'LIBRARY_LAST_VERIFIED_NOT_COMPLETE' }
            $lastCoverage=Get-RmLibraryCoverage $lib $lastSnapshot
            if (@($lastCoverage | Where-Object { $_.status -cne 'complete' }).Count -gt 0) {
                throw 'LIBRARY_LAST_VERIFIED_SCOPE_NOT_COMPLETE'
            }
        }
    }
}
function New-RmLibraryProjection {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Registry)
    Assert-RmLibraryRegistry $Registry
    $cards=New-Object 'System.Collections.Generic.List[object]'
    foreach ($lib in @($Registry.libraries | Sort-Object -Property libraryId -CaseSensitive)) {
        $gens=New-Object 'System.Collections.Generic.List[object]'
        foreach ($snap in @($lib.snapshots | Sort-Object -Property capturedAtUtc,generationId -CaseSensitive)) {
            $coverage=Get-RmLibraryCoverage $lib $snap
            $gens.Add([pscustomobject]@{
                snapshotId=$snap.snapshotId;scanId=$snap.scanId;generationId=$snap.generationId
                capturedAtUtc=$snap.capturedAtUtc;schemaVersion=$snap.schemaVersion
                scanStatus=$snap.scanStatus;scopeIsExhaustive=$snap.scopeIsExhaustive
                coverage=$coverage;coverageProof=@($snap.coverageProof)
                countKnown=[bool]$lib.indexAccessible
                observedUnitCount=$(if ($lib.indexAccessible) { @($snap.units).Count } else { $null })
            })
        }
        # Never infer the latest observation from a culture-dependent textual
        # sort: ".900Z" and "Z" do not sort in chronological order.
        $latest=$null
        $latestStamp=[DateTimeOffset]::MinValue
        $latestId=$null
        foreach ($gen in $gens) {
            $stamp=Assert-RmLibraryDate $gen.capturedAtUtc
            if ($null -eq $latest -or $stamp -gt $latestStamp -or
                ($stamp -eq $latestStamp -and
                 [string]::CompareOrdinal($gen.snapshotId,$latestId) -gt 0)) {
                $latest=$gen.capturedAtUtc
                $latestStamp=$stamp
                $latestId=$gen.snapshotId
            }
        }
        $cards.Add([pscustomobject]@{
            libraryId=$lib.libraryId;name=$lib.name;platforms=@($lib.platforms)
            sources=@($lib.sources);currentAvailability=$lib.currentAvailability
            availabilityObservedAtUtc=$lib.availabilityObservedAtUtc;freshness=$lib.freshness
            indexAccessible=$lib.indexAccessible;lastVerifiedCompleteRef=$lib.lastVerifiedCompleteRef
            lastObservedAtUtc=$latest;generations=$gens.ToArray()
        })
    }
    return [pscustomobject]@{schemaVersion='0.1.0-synthetic';readOnly=$true;libraries=$cards.ToArray()}
}
# Adapter: the comparator is imported, never copied or reimplemented.
# It reads already supplied synthetic snapshots and creates a view DTO in memory.
function Get-RmLibraryComparisonView {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]$Registry,
        [Parameter(Mandatory=$true)][string]$LibraryId,
        [Parameter(Mandatory=$true)][string]$BeforeSnapshotId,
        [Parameter(Mandatory=$true)][string]$AfterSnapshotId
    )
    $catalog=New-RmLibraryProjection -Registry $Registry
    $selected=@($catalog.libraries | Where-Object { $_.libraryId -ceq $LibraryId })
    if ($selected.Count -ne 1) { throw 'LIBRARY_NOT_FOUND' }
    $card=$selected[0]
    if (-not $card.indexAccessible) { throw 'LIBRARY_INDEX_INACCESSIBLE' }
    $source=@($Registry.libraries | Where-Object { $_.libraryId -ceq $LibraryId })[0]
    $a=@($source.snapshots | Where-Object { $_.snapshotId -ceq $BeforeSnapshotId })
    $b=@($source.snapshots | Where-Object { $_.snapshotId -ceq $AfterSnapshotId })
    if ($a.Count -ne 1 -or $b.Count -ne 1) { throw 'LIBRARY_SNAPSHOT_NOT_FOUND' }
    $a=$a[0]; $b=$b[0]
    if ($a.snapshotId -ceq $b.snapshotId) { throw 'LIBRARY_SNAPSHOT_REUSED' }
    $aCoverage=Get-RmLibraryCoverage $source $a
    $bCoverage=Get-RmLibraryCoverage $source $b
    $aInput=[pscustomobject]@{
        schemaVersion=$a.schemaVersion;libraryId=$source.libraryId
        generationId=$a.generationId;capturedAtUtc=$a.capturedAtUtc
        scanStatus=$a.scanStatus;sourceAvailability='UNKNOWN';freshness='UNKNOWN'
        coverage=@($aCoverage);units=@($a.units)
    }
    $bInput=[pscustomobject]@{
        schemaVersion=$b.schemaVersion;libraryId=$source.libraryId
        generationId=$b.generationId;capturedAtUtc=$b.capturedAtUtc
        scanStatus=$b.scanStatus;sourceAvailability='UNKNOWN';freshness='UNKNOWN'
        coverage=@($bCoverage);units=@($b.units)
    }
    # The dependency is the pure comparator from PR #26. Do not infer outcomes here.
    Import-Module -Name (Join-Path $PSScriptRoot 'SnapshotComparison.psm1') -ErrorAction Stop
    $diff=Compare-RmInventorySnapshots -Before $aInput -After $bInput
    $compared=New-Object 'System.Collections.Generic.List[object]'
    $excluded=New-Object 'System.Collections.Generic.List[object]'
    foreach ($console in @($source.platforms | Sort-Object -CaseSensitive)) {
        $beforeScope=@($aCoverage | Where-Object { $_.console -ceq $console })[0]
        $afterScope=@($bCoverage | Where-Object { $_.console -ceq $console })[0]
        if ($beforeScope.status -ceq 'complete' -and $afterScope.status -ceq 'complete') {
            $compared.Add([pscustomobject]@{console=$console;status='complete';reason='PROVEN_IN_A_AND_B'})
        } else {
            $excluded.Add([pscustomobject]@{
                console=$console;status='INCOMPLETE'
                beforeStatus=$beforeScope.status;afterStatus=$afterScope.status
                beforeReason=$beforeScope.reason;afterReason=$afterScope.reason
            })
        }
    }
    $aRef=@($card.generations | Where-Object { $_.snapshotId -ceq $BeforeSnapshotId })[0]
    $bRef=@($card.generations | Where-Object { $_.snapshotId -ceq $AfterSnapshotId })[0]
    $limitations=New-Object 'System.Collections.Generic.List[string]'
    $limitations.Add('HISTORICAL_OBSERVATIONS_NOT_PHYSICAL_ROM_BYTES')
    $limitations.Add('COUNTS_ONLY_OBSERVED_UNION_A_B')
    if ($excluded.Count -gt 0) { $limitations.Add('SCOPE_INTERSECTION_NOT_FULLY_PROVEN') }
    if ($card.currentAvailability -cne 'ONLINE') { $limitations.Add('CURRENT_SOURCE_NOT_CONFIRMED_ONLINE') }
    if ($card.freshness -cne 'FRESH') { $limitations.Add('HISTORICAL_FRESHNESS_NOT_CURRENT') }
    if (@($a.units).Count -eq 0 -and @($b.units).Count -eq 0 -and $excluded.Count -gt 0) {
        $limitations.Add('ZERO_OBSERVED_UNITS_NOT_PROOF_OF_EMPTY_LIBRARY')
    }
    return [pscustomobject]@{
        schemaVersion='0.1.0-synthetic';readOnly=$true
        library=[pscustomobject]@{
            libraryId=$card.libraryId;name=$card.name;platforms=$card.platforms
            currentAvailability=$card.currentAvailability
            availabilityObservedAtUtc=$card.availabilityObservedAtUtc
            freshness=$card.freshness;indexAccessible=$card.indexAccessible
            lastVerifiedCompleteRef=$card.lastVerifiedCompleteRef
        }
        before=$aRef;after=$bRef
        scopeCompared=$compared.ToArray()
        scopeNotComparable=$excluded.ToArray()
        coverageStatus=$(if ($excluded.Count -eq 0) {'COMPLETE'} else {'INCOMPLETE'})
        counts=$diff.counts;entries=$diff.entries
        countDenominator=@($diff.entries).Count
        limitations=$limitations.ToArray()
    }
}
Export-ModuleMember -Function New-RmLibraryProjection,Get-RmLibraryComparisonView
