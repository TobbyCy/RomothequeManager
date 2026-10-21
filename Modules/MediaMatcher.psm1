Set-StrictMode -Version 2.0
function Get-RmLooseName {
    param([string]$Text)
    if (!$Text) {return ''}
    return ([regex]::Replace($Text.Normalize([Text.NormalizationForm]::FormD),'\p{M}','') -replace '[^\p{L}\p{Nd}]','').ToLowerInvariant()
}

function Get-RmMediaMatches {
    param($Context,$Units,$Inventory,$Associations)
    $results=@{};$suggestions=New-Object 'System.Collections.Generic.List[object]'
    $media=@($Inventory.files | Where-Object {$_.type -eq 'media'})
    $exact=@{};$loose=@{};$romBasenames=@{}
    foreach ($f in $media) {
        $key=($f.console+'|'+$f.mediaStem).ToLowerInvariant()
        if (!$exact.ContainsKey($key)) {$exact[$key]=@()};$exact[$key]+=@($f)
        $lk=$f.console+'|'+(Get-RmLooseName ([IO.Path]::GetFileName($f.mediaStem)))
        if (!$loose.ContainsKey($lk)) {$loose[$lk]=@()};$loose[$lk]+=@($f)
    }
    foreach ($u in $Units) {
        if (!$u.entryPath) {continue}
        $base=[IO.Path]::GetFileNameWithoutExtension($u.entryPath.Replace('/','\'))
        $key=$u.console+'|'+$base
        $effectiveGameId=Resolve-RmMergeTarget $u.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
        if (!$romBasenames.ContainsKey($key)) {$romBasenames[$key]=@()};$romBasenames[$key]+=@($effectiveGameId)
    }
    foreach ($unit in $Units) {
        $chosen=@{};$methods=@{}
        foreach ($m in $unit.metadata) {
            foreach ($tag in @('image','thumbnail','video','marquee','manual','cover','backcover','fanart','screenshot','titlescreen','physicalmedia','miximage','3dbox')) {
                $p=$m.raw.PSObject.Properties[$tag];if (!$p) {continue}
                foreach ($ref in @($p.Value)) {
                    try {
                        $rp=Resolve-RmRelativePath ('ROMS/'+$unit.console) ([string]$ref) ''
                        if (!$Inventory.byPath.ContainsKey($rp) -or $Inventory.byPath[$rp].type -ne 'media') {throw 'Referenced media is absent, historical or outside media categories.'}
                        $f=$Inventory.byPath[$rp]
                        if ($f.console -ne $unit.console) {throw 'Cross-console media reference refused.'}
                        $chosen[$f.fileId]=$f;$methods[$f.fileId]='es-de-path'
                    } catch { Add-RmAnomaly $Context 'MEDIA_REFERENCE_UNRESOLVED' ([string]$ref) $_.Exception.Message 'Warning' $unit.console $unit.variantId }
                }
            }
        }
        if ($unit.entryPath) {
            $prefix='ROMS/'+$unit.console+'/'
            $tail=$unit.entryPath.Substring($prefix.Length)
            if ($unit.kind -notin @('directory','ps3-package')) {$tail=$tail.Substring(0,$tail.Length-[IO.Path]::GetExtension($tail).Length)}
            $key=$unit.console+'|'+$tail
            $candidates=@();if ($exact.ContainsKey($key)) {$candidates=@($exact[$key])}
            # A flattened stem is only exact enough when it identifies one unit.
            if ($candidates.Count -eq 0 -and $tail.Contains('/')) {
                $stem=[IO.Path]::GetFileName($tail.Replace('/','\'))
                $baseKey=$unit.console+'|'+$stem
                if ($exact.ContainsKey($baseKey) -and @($romBasenames[$baseKey] | Select-Object -Unique).Count -eq 1) {$candidates=@($exact[$baseKey])}
            }
            foreach ($group in @($candidates | Group-Object mediaType)) {
                if ($group.Count -eq 1) {
                    $f=$group.Group[0]
                    if (!$chosen.ContainsKey($f.fileId)) {$chosen[$f.fileId]=$f;$methods[$f.fileId]='exact-relative-stem'}
                } else { Add-RmAnomaly $Context 'MEDIA_AMBIGUOUS' $unit.entryPath ('Multiple '+$group.Name+' candidates.') 'Warning' $unit.console $unit.variantId }
            }
        }
        foreach ($rule in $Associations.mediaAssignments | Where-Object {$_.variantId -eq $unit.variantId}) {
            if ($Inventory.byPath.ContainsKey($rule.relativePath) -and $Inventory.byPath[$rule.relativePath].type -eq 'media') {
                $f=$Inventory.byPath[$rule.relativePath]
                if ($f.console -ne $unit.console) {throw 'Cross-console manual media assignment.'}
                $chosen[$f.fileId]=$f;$methods[$f.fileId]='manual'
            } else { Add-RmAnomaly $Context 'MANUAL_MEDIA_MISSING' $rule.relativePath 'Manually assigned media not found.' 'Warning' $unit.console $unit.variantId }
        }
        $types=@($chosen.Values | ForEach-Object {$_.mediaType} | Select-Object -Unique)
        $looseKeys=@($unit.console+'|'+(Get-RmLooseName $unit.name))
        if ($unit.entryPath) {$looseKeys+=@($unit.console+'|'+(Get-RmLooseName ([IO.Path]::GetFileNameWithoutExtension($unit.entryPath.Replace('/','\')))))}
        $proposed=@{}
        foreach ($lk in $looseKeys | Select-Object -Unique) {
            if (!$loose.ContainsKey($lk)) {continue}
            foreach ($f in $loose[$lk]) {
                if ($chosen.ContainsKey($f.fileId) -or $types -contains $f.mediaType -or $proposed.ContainsKey($f.fileId)) {continue}
                $proposed[$f.fileId]=$true
                $suggestions.Add([pscustomobject]@{kind='assign-media';sourceVariantId=$unit.variantId;targetVariantId=$null;
                    sourceGameId=$null;targetGameId=$null;relativePath=$f.relativePath;reason='Similar media name; manual validation required.'})
            }
        }
        $results[$unit.variantId]=@($chosen.Values | Sort-Object relativePath | ForEach-Object {
            [pscustomobject]@{fileId=$_.fileId;mediaType=$_.mediaType;relativePath=$_.relativePath;method=$methods[$_.fileId]}
        })
    }
    return [pscustomobject]@{matches=$results;suggestions=$suggestions.ToArray()}
}
Export-ModuleMember -Function Get-RmMediaMatches,Get-RmLooseName
