Set-StrictMode -Version 2.0
function Get-RmIdentityState {
    param([string]$OutputRoot)
    $path=$OutputRoot+'\identity_registry.json'
    if (Test-RmFileExists $path) {
        $registry=Read-RmStrictJson $path
        if ($registry.schemaVersion -notin @('1.0.0','2.0.0')) {throw 'Unsupported identity registry schema.'}
        $aliases=@{};$ids=@{};$games=@{}
        foreach ($g in $registry.games) { if ($games.ContainsKey($g.gameId)) {throw 'Duplicate persistent gameId.'};$games[$g.gameId]=$g }
        foreach ($v in $registry.variants) {
            if ($ids.ContainsKey($v.variantId) -or !$games.ContainsKey($v.gameId)) {throw 'Invalid identity registry variant.'}
            $ids[$v.variantId]=$v
            if (!$v.retired) {foreach ($a in $v.aliases) {if ($aliases.ContainsKey($a)) {throw 'Duplicate active technical alias.'};$aliases[$a]=$true}}
        }
        return $registry
    }
    return [pscustomobject]@{schemaVersion='1.0.0';revision=(New-RmId);games=@();variants=@()}
}

function Get-RmAssociations {
    param([string]$OutputRoot,[switch]$ReadOnly)
    $path=$OutputRoot+'\associations.json'
    if (Test-RmFileExists $path) {
        $a=Read-RmStrictJson $path
        if ($a.schemaVersion -eq '1.0.0') {
            $a.schemaVersion='2.0.0';if (!$ReadOnly) {$a.revision=New-RmId}
            $a | Add-Member -NotePropertyName gameMediaAssignments -NotePropertyValue @()
        } elseif ($a.schemaVersion -notin @('2.0.0','3.0.0')) {throw 'Unsupported associations schema.'}
        if($a.schemaVersion -eq '3.0.0'){$registry=Read-RmStrictJson ($OutputRoot+'\identity_registry.json');$a=ConvertFrom-RmWireAssociations $a $registry}
        return $a
    }
    return [pscustomobject]@{schemaVersion='2.0.0';revision=$(if ($ReadOnly) {'read-only-empty'} else {New-RmId});gameMerges=@();variantMerges=@();mediaAssignments=@();gameMediaAssignments=@();history=@()}
}

function Set-RmUnitIdentities {
    param($Context,$Units,$Inventory,$Registry)
    $byKey=@{};$files=@{};foreach ($f in $Inventory.files) {$files[$f.fileId]=$f}
    foreach ($v in $Registry.variants) {if (!$v.retired) {foreach ($key in $v.aliases) {$byKey[$key]=$v}}}
    $seenInScan=@{}
    foreach ($unit in $Units) {
        $name=$null
        if ($unit.metadata.Count -gt 0) {
            $preferred=@($unit.metadata | Sort-Object @{Expression={if ($_.entryKind -eq 'game') {0} else {1}}},@{Expression={if ($_.resolvedPath -eq $unit.entryPath) {0} else {1}}},entryIndex)[0]
            $prop=$preferred.raw.PSObject.Properties['name']
            if ($prop -and ![string]::IsNullOrWhiteSpace([string]$prop.Value)) {$name=[string]$prop.Value}
        }
        if (!$name -and $unit.entryPath) {
            if ($unit.kind -in @('directory','ps3-package')) {$name=[IO.Path]::GetFileName($unit.entryPath.Replace('/','\'))}
            else {$name=[IO.Path]::GetFileNameWithoutExtension($unit.entryPath.Replace('/','\'))}
        }
        if (!$name) {$name='Unresolved metadata entry'}
        $signature=(@($unit.fileIds | ForEach-Object { [string]$files[$_].sizeBytes+'@'+$files[$_].modifiedAtUtc } | Sort-Object) -join '|')
        $identity=$null
        if ($byKey.ContainsKey($unit.technicalKey)) {$identity=$byKey[$unit.technicalKey]}
        else {
            $game=[pscustomobject]@{gameId=(New-RmId);console=$unit.console;name=$name;createdAtUtc=[DateTime]::UtcNow.ToString('o')}
            $Registry.games=@($Registry.games)+@($game)
            $identity=[pscustomobject]@{variantId=(New-RmId);gameId=$game.gameId;console=$unit.console;aliases=@($unit.technicalKey);
                signature=$signature;name=$name;lastSeenScanId=$Context.ScanId;retired=$false}
            $Registry.variants=@($Registry.variants)+@($identity);$byKey[$unit.technicalKey]=$identity
        }
        if ($seenInScan.ContainsKey($identity.variantId) -and $seenInScan[$identity.variantId] -ne $unit.technicalKey) {
            throw ('IDENTITY_ALIAS_COLLISION: several current paths claim variant '+$identity.variantId+'. Previous catalogue preserved; resolve registry aliases explicitly.')
        }
        $seenInScan[$identity.variantId]=$unit.technicalKey
        $identity.signature=$signature;$identity.lastSeenScanId=$Context.ScanId;$identity.name=$name
        foreach ($registeredGame in $Registry.games) {if ($registeredGame.gameId -eq $identity.gameId) {$registeredGame.name=$name;break}}
        $unit | Add-Member -NotePropertyName variantId -NotePropertyValue $identity.variantId
        $unit | Add-Member -NotePropertyName gameId -NotePropertyValue $identity.gameId
        $unit | Add-Member -NotePropertyName name -NotePropertyValue $name
    }
    $Registry.revision=New-RmId
}

function Resolve-RmMergeTarget {
    param([string]$Id,$Rules,[string]$SourceProperty,[string]$TargetProperty)
    $seen=@{}
    while ($true) {
        if ($seen.ContainsKey($Id)) {throw 'Association cycle refused.'};$seen[$Id]=$true
        $next=@($Rules | Where-Object {$_.$SourceProperty -eq $Id})
        if ($next.Count -eq 0) {return $Id}
        if ($next.Count -ne 1) {throw 'Multiple association targets refused.'}
        $Id=[string]$next[0].$TargetProperty
    }
}
Export-ModuleMember -Function *-Rm*
