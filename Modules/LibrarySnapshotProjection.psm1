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
    $expected=@{}
    foreach ($platform in $Library.platforms) {
        foreach ($source in $Library.sources) {
            foreach ($domain in $source.domains) {
                $key=($platform+'|'+$source.sourceKey+'|'+$source.rootKey+'|'+$domain)
                $expected[$key]=$true
            }
        }
    }
    $seen=@{}
    foreach ($item in $proof) {
        $platform=Get-RmLibraryText $item 'console' 'coverageProof'
        $source=Get-RmLibraryText $item 'sourceKey' 'coverageProof'
        $root=Get-RmLibraryText $item 'rootKey' 'coverageProof'
        $domain=Get-RmLibraryText $item 'domain' 'coverageProof'
        $status=Get-RmLibraryText $item 'status' 'coverageProof'
        Assert-RmLibraryChoice $status @('complete','partial','unobserved') 'coverageProof.status'
        $key=($platform+'|'+$source+'|'+$root+'|'+$domain)
        if (-not $expected.ContainsKey($key)) { throw 'LIBRARY_UNDECLARED_PROOF_PARTITION' }
        if ($seen.ContainsKey($key)) { throw 'LIBRARY_DUPLICATE_PROOF_PARTITION' }
        $seen[$key]=$status
    }
    $coverage=New-Object 'System.Collections.Generic.List[object]'
    foreach ($platform in @($Library.platforms | Sort-Object -CaseSensitive)) {
        $allComplete=$true; $anyMissing=$false; $anyPartial=$false
        foreach ($source in $Library.sources) {
            foreach ($domain in $source.domains) {
                $key=($platform+'|'+$source.sourceKey+'|'+$source.rootKey+'|'+$domain)
                if (-not $seen.ContainsKey($key)) { $allComplete=$false; $anyMissing=$true }
                elseif ($seen[$key] -cne 'complete') {
                    $allComplete=$false
                    if ($seen[$key] -ceq 'partial') { $anyPartial=$true }
                    else { $anyMissing=$true }
                }
            }
        }
        $status='unobserved'
        if ($exhaustive -and $scan -ceq 'complete' -and $allComplete) { $status='complete' }
        elseif ($anyPartial -or ($seen.Count -gt 0)) { $status='partial' }
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
    $ids=@{}
    foreach ($lib in $libraries) {
        $id=Get-RmLibraryText $lib 'libraryId' 'library'
        [void](Get-RmLibraryText $lib 'name' 'library')
        if ($ids.ContainsKey($id)) { throw 'LIBRARY_DUPLICATE_ID' }
        $ids[$id]=$true
        $platforms=Get-RmLibraryRequired $lib 'platforms' 'library'
        $sources=Get-RmLibraryRequired $lib 'sources' 'library'
        $snapshots=Get-RmLibraryRequired $lib 'snapshots' 'library'
        Assert-RmLibraryArray $platforms 'platforms'
        Assert-RmLibraryArray $sources 'sources'
        Assert-RmLibraryArray $snapshots 'snapshots'
        if ($platforms.Count -eq 0 -or $sources.Count -eq 0) { throw 'LIBRARY_EMPTY_DECLARED_SCOPE' }
        $platformKeys=@{}; $sourceKeys=@{}
        foreach ($platform in $platforms) {
            if ($platform -isnot [string] -or $platform -cnotmatch '^[A-Z][A-Z0-9_-]*$') { throw 'LIBRARY_PLATFORM_INVALID' }
            if ($platformKeys.ContainsKey($platform)) { throw 'LIBRARY_DUPLICATE_PLATFORM' }
            $platformKeys[$platform]=$true
        }
        foreach ($source in $sources) {
            $key=Get-RmLibraryText $source 'sourceKey' 'source'
            $root=Get-RmLibraryText $source 'rootKey' 'source'
            $domains=Get-RmLibraryRequired $source 'domains' 'source'
            Assert-RmLibraryArray $domains 'source.domains'
            if ($domains.Count -eq 0) { throw 'LIBRARY_SOURCE_DOMAINS_EMPTY' }
            if ($sourceKeys.ContainsKey($key+'|'+$root)) { throw 'LIBRARY_DUPLICATE_SOURCE' }
            $sourceKeys[$key+'|'+$root]=$true
            $domainKeys=@{}
            foreach ($domain in $domains) {
                Assert-RmLibraryChoice $domain @('ROMS','MEDIAS') 'source.domain'
                if ($domainKeys.ContainsKey($domain)) { throw 'LIBRARY_DUPLICATE_DOMAIN' }
                $domainKeys[$domain]=$true
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
        $snapKeys=@{}; $genKeys=@{}
        foreach ($snap in $snapshots) {
            $snapId=Get-RmLibraryText $snap 'snapshotId' 'snapshot'
            [void](Get-RmLibraryText $snap 'scanId' 'snapshot')
            $gen=Get-RmLibraryText $snap 'generationId' 'snapshot'
            $date=Get-RmLibraryText $snap 'capturedAtUtc' 'snapshot'
            [void](Assert-RmLibraryDate $date)
            $units=Get-RmLibraryRequired $snap 'units' 'snapshot'
            Assert-RmLibraryArray $units 'snapshot.units'
            if ($snapKeys.ContainsKey($snapId) -or $genKeys.ContainsKey($gen)) { throw 'LIBRARY_DUPLICATE_SNAPSHOT_OR_GENERATION' }
            $snapKeys[$snapId]=$snap
            $genKeys[$gen]=$true
            [void](Get-RmLibraryCoverage $lib $snap)
        }
        if ($null -ne $last) {
            if ($last -isnot [string] -or -not $snapKeys.ContainsKey($last)) { throw 'LIBRARY_LAST_VERIFIED_REF_INVALID' }
            $lastSnapshot=$snapKeys[$last]
            if ($lastSnapshot.scanStatus -cne 'complete') { throw 'LIBRARY_LAST_VERIFIED_NOT_COMPLETE' }
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
            $coverage=@(Get-RmLibraryCoverage $lib $snap)
            $gens.Add([pscustomobject]@{
                snapshotId=$snap.snapshotId;scanId=$snap.scanId;generationId=$snap.generationId
                capturedAtUtc=$snap.capturedAtUtc;schemaVersion=$snap.schemaVersion
                scanStatus=$snap.scanStatus;scopeIsExhaustive=$snap.scopeIsExhaustive
                coverage=$coverage;coverageProof=@($snap.coverageProof)
                observedUnitCount=@($snap.units).Count
            })
        }
        $latest=$null
        if ($gens.Count -gt 0) { $latest=$gens[$gens.Count-1].capturedAtUtc }
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
Export-ModuleMember -Function New-RmLibraryProjection
