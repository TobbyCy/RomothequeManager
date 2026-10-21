Set-StrictMode -Version 2.0
function Set-RmGameMediaModel {
    param($Context,$Catalogue,$Inventory,$Associations)
    $used=@{}
    foreach ($game in $Catalogue.games) {
        $common=New-Object 'System.Collections.Generic.List[object]'
        $manualCommon=@{}
        foreach ($rule in $Associations.gameMediaAssignments) {
            $gid=Resolve-RmMergeTarget $rule.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
            if ($gid -ne $game.gameId) {continue}
            if (!$Inventory.byPath.ContainsKey($rule.relativePath) -or $Inventory.byPath[$rule.relativePath].type -ne 'media') {
                Add-RmAnomaly $Context 'MANUAL_MEDIA_MISSING' $rule.relativePath 'Common game media missing.' 'Warning' $game.console;continue
            }
            $f=$Inventory.byPath[$rule.relativePath]
            if ($f.console -ne $game.console) {throw 'Cross-console common media assignment.'}
            $manualCommon[$f.fileId]=[pscustomobject]@{fileId=$f.fileId;relativePath=$f.relativePath;mediaType=$f.mediaType;method='manual-game'}
        }
        $all=@{};foreach ($v in $game.variants) {foreach ($m in $v.media) {$all[$m.fileId]=$m}}
        foreach ($m in $manualCommon.Values) {$all[$m.fileId]=$m}
        $types=@($all.Values | ForEach-Object {$_.mediaType} | Sort-Object -Unique)
        foreach ($v in $game.variants) {$v | Add-Member -NotePropertyName mediaOverrides -NotePropertyValue @()}
        foreach ($type in $types) {
            $items=@($all.Values | Where-Object {$_.mediaType -eq $type} | Sort-Object relativePath)
            $manual=@($manualCommon.Values | Where-Object {$_.mediaType -eq $type} | Sort-Object relativePath)
            if ($manual.Count -gt 0) {$items=$manual}
            $sameSet=$true;$baseline=$null
            foreach ($v in $game.variants) {
                $key=@($v.media | Where-Object {$_.mediaType -eq $type} | ForEach-Object {$_.fileId} | Sort-Object -Unique) -join '|'
                if ($null -eq $baseline) {$baseline=$key} elseif ($baseline -ne $key) {$sameSet=$false}
            }
            if ($items.Count -eq 1 -or $sameSet -or $manual.Count -gt 0) {foreach ($m in $items) {$common.Add($m)}}
            foreach ($v in $game.variants) {
                $specific=@($v.media | Where-Object {$_.mediaType -eq $type} | Sort-Object relativePath)
                $manualVariant=@($specific | Where-Object {$_.method -eq 'manual'})
                $needsOverride=($items.Count -gt 1 -and !$sameSet -and $manual.Count -eq 0 -and $specific.Count -gt 0)
                if ($manual.Count -gt 0 -and $manualVariant.Count -gt 0) {$specific=$manualVariant;$needsOverride=$true}
                if ($needsOverride) {$v.mediaOverrides+=@([pscustomobject]@{mediaType=$type;action='replace';items=$specific})}
            }
        }
        $game | Add-Member -NotePropertyName media -NotePropertyValue @($common.ToArray() | Sort-Object relativePath)
        foreach ($v in $game.variants) {
            $effective=@{};foreach ($m in $game.media) {$effective[$m.fileId]=$m}
            foreach ($over in $v.mediaOverrides) {
                foreach ($id in @($effective.Keys)) {if ($effective[$id].mediaType -eq $over.mediaType) {$effective.Remove($id)}}
                foreach ($m in $over.items) {$effective[$m.fileId]=$m}
            }
            [long]$bytes=0;foreach ($m in $effective.Values) {$bytes+=$Inventory.byPath[$m.relativePath].sizeBytes;$used[$m.fileId]=$true}
            $v.sizes.mediaKnownBytes=$bytes;$v.status.mediaPresentTypes=@($effective.Values | ForEach-Object {$_.mediaType} | Sort-Object -Unique)
            $v.PSObject.Properties.Remove('media')
        }
    }
    foreach ($game in $Catalogue.games) {foreach ($v in $game.variants) {foreach ($unit in $v.romUnits) {foreach ($id in $unit.fileIds) {$used[$id]=$true}}}}
    $Catalogue.unassigned=@($Catalogue.files | Where-Object {$_.type -notin @('metadata','legacy','auxiliary') -and !$used.ContainsKey($_.fileId)} | ForEach-Object {$_.fileId})
}
Export-ModuleMember -Function Set-RmGameMediaModel
