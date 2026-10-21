#Requires -Version 5.1
# Pure E1 projection helpers. Importing this module performs no filesystem operations.
Set-StrictMode -Version 2.0

function Get-RmE1Prop {
    param($Value,[string]$Name,$Default=$null)
    if ($null -eq $Value) { return $Default }
    $p=$Value.PSObject.Properties[$Name]
    if ($null -ne $p) { return $p.Value }
    return $Default
}

function Get-RmE1GameResolutionIndex {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Catalogue)
    $resolved=@{}
    $manual=@{}
    foreach ($id in @(Get-RmE1Prop $Catalogue 'manualMetadataResolvedIds' @())) {
        $s=[string]$id
        if ($s) { $manual[$s]=$true }
    }
    foreach ($record in @(Get-RmE1Prop $Catalogue.metadataReconciliation 'resolved' @())) {
        if ($null -eq $record) { continue }
        $source=Get-RmE1Prop $record 'sourceEntry'
        $id=[string](Get-RmE1Prop $source 'metadataEntryId' '')
        if (!$id -or (Get-RmE1Prop $source 'entryKind') -cne 'game') { throw ('E1_RESOLVED_NOT_GAME: '+$id) }
        if ($resolved.ContainsKey($id)) { throw ('E1_DUPLICATE_RESOLVED_GAME: '+$id) }
        $resolved[$id]=$true
    }
    foreach ($record in @(Get-RmE1Prop $Catalogue.metadataReconciliation 'unresolved' @())) {
        if ($null -eq $record) { continue }
        $source=Get-RmE1Prop $record 'sourceEntry'
        $id=[string](Get-RmE1Prop $source 'metadataEntryId' '')
        if ($manual.ContainsKey($id)) {
            if ((Get-RmE1Prop $source 'entryKind') -cne 'game') { throw ('E1_MANUAL_RESOLUTION_NOT_GAME: '+$id) }
            if ($resolved.ContainsKey($id)) { throw ('E1_MANUAL_RESOLUTION_DUPLICATE: '+$id) }
            $resolved[$id]=$true
        }
    }
    foreach ($id in @($manual.Keys)) {
        if (!$resolved.ContainsKey([string]$id)) { throw ('E1_MANUAL_RESOLUTION_NOT_FOUND: '+$id) }
    }
    $expected=Get-RmE1Prop $Catalogue.statistics 'metadataResolved'
    if ($null -ne $expected -and $resolved.Count -ne [int]$expected) { throw ('E1_RESOLVED_COUNT_MISMATCH: '+$resolved.Count+' expected '+$expected) }
    return ,$resolved
}

function Get-RmE1GameMetadataIndex {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Catalogue)
    $index=@{}
    foreach ($item in @(Get-RmE1Prop $Catalogue 'metadataIndex' @())) {
        if ($null -eq $item) { continue }
        $id=[string](Get-RmE1Prop $item 'metadataEntryId' '')
        if (!$id -or $index.ContainsKey($id)) { throw ('E1_METADATA_INDEX_DUPLICATE_OR_INVALID: '+$id) }
        $index[$id]=[string](Get-RmE1Prop $item 'entryKind' '')
    }
    return ,$index
}

function Select-RmE1ConfirmedGameLinks {
    [CmdletBinding()]
    param($Sources,[Parameter(Mandatory=$true)][hashtable]$ResolvedGameIds,[Parameter(Mandatory=$true)][hashtable]$MetadataKinds)
    $links=New-Object 'System.Collections.Generic.List[object]'
    foreach ($link in @($Sources)) {
        if ($null -eq $link) { continue }
        $id=[string](Get-RmE1Prop $link 'metadataEntryId' '')
        if (!$id -or !$ResolvedGameIds.ContainsKey($id)) { continue }
        if (!$MetadataKinds.ContainsKey($id) -or $MetadataKinds[$id] -cne 'game') { throw ('E1_RESOLVED_SOURCE_KIND_CONFLICT: '+$id) }
        $links.Add($link)
    }
    return ,([object[]]$links.ToArray())
}

function Get-RmE1UnitMetadataProjection {
    [CmdletBinding()]
    param($Sources,[Parameter(Mandatory=$true)][string]$VariantId,[Parameter(Mandatory=$true)][string]$RomUnitId,[Parameter(Mandatory=$true)][int]$VariantUnitCount)
    $matched=New-Object 'System.Collections.Generic.List[object]'
    $specific=New-Object 'System.Collections.Generic.List[string]'
    foreach ($link in @($Sources)) {
        if ($null -eq $link) { continue }
        $scope=[string](Get-RmE1Prop $link 'scope' '')
        if (($scope -eq 'unit' -and (Get-RmE1Prop $link 'romUnitId') -eq $RomUnitId) -or
            ($scope -eq 'variant' -and (Get-RmE1Prop $link 'variantId') -eq $VariantId) -or
            ($scope -eq 'game')) {
            $matched.Add($link)
            if ($scope -eq 'unit' -or ($scope -eq 'variant' -and $VariantUnitCount -eq 1)) {
                $specific.Add([string]$link.metadataEntryId)
            }
        }
    }
    $confirmed=@($specific.ToArray() | Sort-Object -Unique)
    $all=@($matched.ToArray() | ForEach-Object { [string]$_.metadataEntryId } | Sort-Object -Unique)
    $wider=@($all | Where-Object { $_ -notin $confirmed })
    $state='ROM_ONLY'
    if ($confirmed.Count -gt 0) { $state='ASSOCIATED' }
    elseif ($wider.Count -gt 0) { $state='BROAD_SCOPE_METADATA' }
    return [pscustomobject]@{metadataState=$state;confirmedMetadataEntryIds=$confirmed;widerScopeMetadataEntryIds=$wider}
}

function Test-RmE1SafeRelativeSourcePath {
    [CmdletBinding()]
    param([AllowEmptyString()][string]$RelativePath)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or $RelativePath.Contains('\') -or
        $RelativePath -notmatch '^(ROMS|MEDIAS)/' -or $RelativePath.Contains(':') -or
        ($RelativePath.IndexOf([char]0) -ge 0)) { return $false }
    $parts=$RelativePath.Split('/')
    foreach ($part in $parts) {
        if ($part -eq '' -or $part -eq '.' -or $part -eq '..' -or $part.EndsWith('.') -or $part.EndsWith(' ')) { return $false }
    }
    return $true
}

function Assert-RmE1GameCoverage {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][hashtable]$ResolvedGameIds,[Parameter(Mandatory=$true)]$Units)
    $represented=@{}
    foreach ($unit in @($Units)) {
        foreach ($id in @($unit.confirmedMetadataEntryIds)+@($unit.widerScopeMetadataEntryIds)) {
            $key=[string]$id
            if (!$ResolvedGameIds.ContainsKey($key)) { throw ('E1_NON_RESOLVED_REFERENCE: '+$key) }
            $represented[$key]=$true
        }
    }
    foreach ($id in @($ResolvedGameIds.Keys)) {
        if (!$represented.ContainsKey([string]$id)) { throw ('E1_RESOLVED_GAME_WITHOUT_LINK: '+$id) }
    }
    return $true
}


function Get-RmE1ExportExceptions {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Catalogue)
    $manual=@{}
    foreach ($id in @(Get-RmE1Prop $Catalogue 'manualMetadataResolvedIds' @())) { $manual[[string]$id]=$true }
    $output=New-Object 'System.Collections.Generic.List[object]'
    foreach ($record in @(Get-RmE1Prop $Catalogue.metadataReconciliation 'unresolved' @())) {
        if ($null -eq $record) { continue }
        $source=Get-RmE1Prop $record 'sourceEntry'
        $id=[string](Get-RmE1Prop $source 'metadataEntryId' '')
        if ($manual.ContainsKey($id)) { continue }
        $class=[string](Get-RmE1Prop $record 'classification' '')
        $kind='UNRESOLVED_OTHER'
        if ($class -eq 'MISSING_ROM') { $kind='ESDE_ONLY' }
        elseif ($class -eq 'AMBIGUOUS') { $kind='AMBIGUOUS' }
        $output.Add([pscustomobject]@{
            metadataEntryId=$id;classification=$class;exportException=$kind;
            status=(Get-RmE1Prop $record 'status');candidateCount=@(Get-RmE1Prop $record 'candidates' @()).Count;
            candidateRomUnitIds=@(Get-RmE1Prop $record 'candidates' @() | ForEach-Object { Get-RmE1Prop $_ 'romUnitId' })
        })
    }
    return ,([object[]]$output.ToArray())
}

function Assert-RmE1UniquePhysicalPaths {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)]$Files)
    $seen=@{}
    foreach ($file in @($Files)) {
        $rel=[string](Get-RmE1Prop $file 'relativePath' '')
        if (!(Test-RmE1SafeRelativeSourcePath $rel)) { throw ('E1_UNSAFE_PHYSICAL_PATH: '+$rel) }
        $key=$rel.Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
        if ($seen.ContainsKey($key)) { throw ('E1_PHYSICAL_PATH_COLLISION: '+$rel) }
        $seen[$key]=$true
    }
    return $true
}

function Assert-RmE1SnapshotFingerprint {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$CurrentSha256,[Parameter(Mandatory=$true)][string]$ExpectedSha256)
    if (!$CurrentSha256 -or !$ExpectedSha256 -or $CurrentSha256 -cne $ExpectedSha256) {
        throw 'E1_SNAPSHOT_STALE'
    }
    return $true
}

Export-ModuleMember -Function Get-RmE1GameResolutionIndex,Get-RmE1GameMetadataIndex,Select-RmE1ConfirmedGameLinks,Get-RmE1UnitMetadataProjection,Test-RmE1SafeRelativeSourcePath,Assert-RmE1GameCoverage,Get-RmE1ExportExceptions,Assert-RmE1UniquePhysicalPaths,Assert-RmE1SnapshotFingerprint
