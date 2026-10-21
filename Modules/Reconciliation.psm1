Set-StrictMode -Version 2.0

function Get-RmMetadataEntryKey {
    param($Entry)
    return ($Entry.sourcePath+'#'+$Entry.entryIndex)
}
function Get-RmSeparatorKey {
    param([string]$FileName)
    # Explicit separator equivalence only: no accent folding, edit distance,
    # token sorting, region removal or disc-number removal.
    $ext=[IO.Path]::GetExtension($FileName).ToLowerInvariant()
    $stem=[IO.Path]::GetFileNameWithoutExtension($FileName).Normalize([Text.NormalizationForm]::FormC)
    return (([regex]::Replace($stem,'[ _\-()\[\]]+',' ').Trim()).ToLowerInvariant()+$ext)
}
function Get-RmApprovedPokemonStrictKey {
    param([string]$FileName)
    $leaf=[IO.Path]::GetFileName($FileName.Replace('/','\\')).Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
    $ext=[IO.Path]::GetExtension($leaf)
    $stem=[IO.Path]::GetFileNameWithoutExtension($leaf)
    return (([regex]::Replace($stem,'[ _\-\(\)]+',' ').Trim())+$ext)
}
function Add-RmReconcileIndex {
    param($Index,[string]$Key,$Value)
    if (!$Index.ContainsKey($Key)) {$Index[$Key]=New-Object 'System.Collections.Generic.List[object]'}
    $Index[$Key].Add($Value)
}
function New-RmReconcileCandidate {
    param($Unit,$File,$Associations,[string]$Rule,[string]$Confidence,[string]$Reason)
    $gid=Resolve-RmMergeTarget $Unit.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
    $vid=Resolve-RmMergeTarget $Unit.variantId $Associations.variantMerges 'sourceVariantId' 'targetVariantId'
    $member=$null
    if ($File) {$member=[pscustomobject]@{fileId=$File.fileId;relativePath=$File.relativePath;sizeBytes=$File.sizeBytes;modifiedAtUtc=$File.modifiedAtUtc;
        role=$(if ($File.relativePath -eq $Unit.entryPath) {'entry'} else {'dependency'})}}
    return [pscustomobject]@{unitKey=$Unit.technicalKey;gameId=$gid;variantId=$vid;console=$Unit.console;kind=$Unit.kind;
        romUnitId=$Unit.romUnitId;entryPath=$Unit.entryPath;matchedFile=$member;matchingFiles=@($(if ($member) {$member}));rule=$Rule;confidence=$Confidence;reason=$Reason;contentIdentity='not-verified'}
}
function Get-RmMetadataReconciliation {
    param($Context,$Metadata,$Units,$Inventory,$Associations)
    $files=@{};foreach ($f in $Inventory.files) {$files[$f.fileId]=$f}
    $exact=@{};$separator=@{};$stems=@{};$current=@{};$unitsByKey=@{};$bound=@{}
    foreach ($u in $Units) {
        $unitsByKey[$u.technicalKey]=$u
        Add-RmReconcileIndex $current $u.entryPath $u
        $stem=[IO.Path]::GetFileNameWithoutExtension($u.entryPath.Replace('/','\')).Normalize([Text.NormalizationForm]::FormC)
        if ($u.kind -in @('directory','ps3-package')) {$stem=[IO.Path]::GetFileName($u.entryPath.Replace('/','\')).Normalize([Text.NormalizationForm]::FormC)}
        Add-RmReconcileIndex $stems ($u.console+'|'+$stem) $u
        foreach ($m in $u.metadata) {Add-RmReconcileIndex $bound (Get-RmMetadataEntryKey $m) $u}
        foreach ($id in $u.fileIds) {
            $f=$files[$id];if ($f.type -eq 'auxiliary') {continue}
            $name=[IO.Path]::GetFileName($f.relativePath.Replace('/','\')).Normalize([Text.NormalizationForm]::FormC)
            $ref=[pscustomobject]@{unit=$u;file=$f}
            Add-RmReconcileIndex $exact ($u.console+'|'+$name) $ref
            Add-RmReconcileIndex $separator ($u.console+'|'+(Get-RmSeparatorKey $name)) $ref
        }
    }
    $records=New-Object 'System.Collections.Generic.List[object]'
    $entries=@($Metadata.entries)+@($Metadata.sources | ForEach-Object {$_.folderMetadata})
    foreach ($entry in $entries | Sort-Object console,sourcePath,entryIndex) {
        $key=Get-RmMetadataEntryKey $entry;$candidateMap=@{};$status='unresolved';$classification='MISSING_ROM';$rule='NO_SAFE_CANDIDATE';$confidence='NONE'
        $reason='No current ROM file found by an exact filename or the explicit separator-equivalence rule.'
        if ($entry.entryKind -eq 'folder') {
            $status='folder';$classification='FOLDER_METADATA';$rule='FOLDER_PRESERVED';$confidence='HIGH';$reason='Folder metadata retained separately; it cannot create a selectable game.'
            $folderUnits=@()
            if ($entry.resolvedPath) {
                $folderUnits=@($Units | Where-Object {$_.entryPath -eq $entry.resolvedPath -or $_.entryPath.StartsWith($entry.resolvedPath+'/',[StringComparison]::OrdinalIgnoreCase)})
                if ($folderUnits.Count -gt 0) {$rule='EXACT_FOLDER_CONTAINMENT'}
                else {
                    $leaf=[IO.Path]::GetFileName($entry.resolvedPath.Replace('/','\')).Normalize([Text.NormalizationForm]::FormC)
                    $sk=$entry.console+'|'+$leaf
                    if ($stems.ContainsKey($sk)) {$folderUnits=@($stems[$sk].ToArray());$rule='EXACT_FOLDER_LEAF_STEM';$confidence='MEDIUM'}
                }
            }
            foreach ($u in $folderUnits) {$candidateMap[$u.technicalKey]=New-RmReconcileCandidate $u $null $Associations $rule $confidence 'Existing unit covered by the folder path or exact folder-leaf stem; no association applied.'}
        } elseif ($bound.ContainsKey($key) -and $bound[$key].Count -eq 1) {
            $status='resolved';$classification=$null;$rule='EXACT_CURRENT_PATH';$confidence='HIGH';$reason='XML path already points to one current unit or one uniquely owned member file.'
            $u=$bound[$key][0];$f=$null
            if ($entry.resolvedPath -and $Inventory.byPath.ContainsKey($entry.resolvedPath)) {$f=$Inventory.byPath[$entry.resolvedPath]}
            $candidateMap[$u.technicalKey]=New-RmReconcileCandidate $u $f $Associations $rule $confidence $reason
        } elseif (!$entry.resolvedPath) {
            $classification='AMBIGUOUS';$rule='INVALID_XML_PATH';$reason='XML path is missing, repeated, absolute, traverses its boundary or otherwise invalid. No lookup attempted.'
        } else {
            $refs=@()
            if ($Inventory.byPath.ContainsKey($entry.resolvedPath)) {
                $physical=$Inventory.byPath[$entry.resolvedPath]
                foreach ($u in $Units) {if ($u.fileIds -contains $physical.fileId) {$refs+=@([pscustomobject]@{unit=$u;file=$physical})}}
                $rule='EXACT_CURRENT_MEMBER';$confidence='HIGH'
            } else {
                $name=[IO.Path]::GetFileName($entry.resolvedPath.Replace('/','\')).Normalize([Text.NormalizationForm]::FormC)
                $ek=$entry.console+'|'+$name
                if ($exact.ContainsKey($ek)) {
                    $refs=@($exact[$ek].ToArray());$classification='EXACT_MOVED';$rule='EXACT_FILENAME_SAME_CONSOLE';$confidence='HIGH'
                } else {
                    $sk=$entry.console+'|'+(Get-RmSeparatorKey $name)
                    if ([string]$entry.metadataEntryId -ceq '40da6a24-0913-468d-8a38-086b5369efab') {
                        # ONLY approved Pokemon metadata entry. No generalized promotion.
                        if ($entry.console -cne 'GBA') {throw 'EXACT_RENAMED_APPROVED_WRONG_CONSOLE'}
                        if ([IO.Path]::GetExtension($name).ToLowerInvariant() -cne '.gba') {throw 'EXACT_RENAMED_APPROVED_WRONG_EXTENSION'}
                        $targetStrictKey=Get-RmApprovedPokemonStrictKey $name
                        $allowed=@()
                        if ($separator.ContainsKey($sk)) {
                            foreach($candidateRef in @($separator[$sk].ToArray())) {
                                $candidateLeaf=[IO.Path]::GetFileName(([string]$candidateRef.file.relativePath).Replace('/','\\'))
                                if ((Get-RmApprovedPokemonStrictKey $candidateLeaf) -ceq $targetStrictKey -and ([IO.Path]::GetExtension($candidateLeaf).ToLowerInvariant()) -ceq '.gba') {
                                    $allowed+=@($candidateRef)
                                }
                            }
                        }
                        $approvedUnits=@($allowed|ForEach-Object{[string]$_.unit.romUnitId}|Sort-Object -Unique)
                        $approvedPaths=@($allowed|ForEach-Object{[string]$_.file.relativePath}|Sort-Object -Unique)
                        if ($approvedUnits.Count -ne 1 -or $approvedPaths.Count -ne 1 -or [string]$approvedUnits[0] -cne 'aad47f0d-2e5b-494b-bd31-ff3d809b66ea') {
                            throw 'EXACT_RENAMED_APPROVED_STRICT_SINGLE_CANDIDATE_GUARD_FAILED'
                        }
                        $refs=@($allowed)
                        $classification='EXACT_RENAMED'
                        $rule='STRICT_SEPARATOR_EQUIVALENCE_SINGLE_CANDIDATE'
                        $confidence='HIGH'
                    } elseif ($separator.ContainsKey($sk)) {
                        $refs=@($separator[$sk].ToArray());$classification='EXACT_RENAMED';$rule='EXPLICIT_SEPARATOR_EQUIVALENCE';$confidence='MEDIUM'
                    }
                }
            }
            foreach ($ref in $refs | Sort-Object {$_.file.relativePath}) {
                $u=$ref.unit
                if (!$candidateMap.ContainsKey($u.technicalKey)) {
                    $candidateMap[$u.technicalKey]=New-RmReconcileCandidate $u $ref.file $Associations $rule $confidence 'Candidate based on the physical inventory and an explicit name/path rule. File content identity has not been checked.'
                } else {
                    $another=New-RmReconcileCandidate $u $ref.file $Associations $rule $confidence 'Multiple matching physical members.'
                    $candidateMap[$u.technicalKey].matchingFiles+=@($another.matchedFile)
                    $candidateMap[$u.technicalKey].matchedFile=$null
                }
            }
            $matchedPaths=@($refs | ForEach-Object {$_.file.relativePath} | Sort-Object -Unique)
            if ($candidateMap.Count -gt 1 -or $matchedPaths.Count -gt 1 -or ($Inventory.byPath.ContainsKey($entry.resolvedPath) -and $candidateMap.Count -eq 0)) {
                $classification='AMBIGUOUS';$confidence='NONE';$reason='Multiple owning units or a physical file with no admissible ROM unit. No candidate selected.'
            } elseif ($candidateMap.Count -eq 1) {
                $reason='One candidate unit in this console; the original XML path is not bound. '
                if ($classification -eq 'EXACT_RENAMED') {$reason+='Only spaces, underscores, hyphens and round/square parentheses were treated as equivalent separators. Accents, token order, regions, disc numbers and extension were preserved. '}
                else {$reason+='The complete physical filename and extension match (case-insensitive, Unicode NFC), but its parent path differs. '}
                $reason+='This is a proposal only, not proof of identical content.'
            }
        }
        $candidates=@($candidateMap.Values | Sort-Object unitKey);$candidate=$null;if ($candidates.Count -eq 1 -and $classification -ne 'AMBIGUOUS') {$candidate=$candidates[0]}
        $records.Add([pscustomobject]@{entryKey=$key;sourceEntry=$entry;status=$status;classification=$classification;matchClassification=$classification;matchRule=$rule;matchConfidence=$confidence;
            rule=$rule;confidence=$confidence;reason=$reason;candidates=$candidates;candidateUnit=$candidate;
            duplicateOfEntryKey=$null;isDuplicate=$false;metadataConflicts=@();relatedVariantUnitKeys=@();applied=$false;selectable=$false})
    }
    # A unit is an exportable structural object, not one game per XML file or track.
    # Prefer a current binding/launcher, then the normalized structural locator.
    # XML index is snapshot-local and cannot choose the semantic primary record.
    $unitGroups=@{}
    foreach ($r in $records) {
        if ($r.status -eq 'folder' -or !$r.candidateUnit) {continue}
        Add-RmReconcileIndex $unitGroups $r.candidateUnit.unitKey $r
    }
    foreach ($group in $unitGroups.Values) {
        $sorted=@($group.ToArray() | Sort-Object @{Expression={if ($_.status -eq 'resolved') {0} else {1}}},
            @{Expression={if ($_.candidateUnit.matchedFile -and $_.candidateUnit.matchedFile.relativePath -eq $_.candidateUnit.entryPath) {0} else {1}}},
            @{Expression={$_.sourceEntry.sourcePath}},@{Expression={$_.sourceEntry.sourceLocatorNormalized}})
        $primary=$sorted[0]
        foreach ($r in $sorted | Select-Object -Skip 1) {
            $r.classification='DUPLICATE_METADATA';$r.isDuplicate=$true;$r.duplicateOfEntryKey=$primary.entryKey
            $r.rule='SAME_STRUCTURAL_UNIT_RECORDS'
            $fields=@(@($r.sourceEntry.raw.PSObject.Properties | ForEach-Object {$_.Name})+@($primary.sourceEntry.raw.PSObject.Properties | ForEach-Object {$_.Name}) | Sort-Object -Unique)
            foreach ($field in $fields) {
                if ($field -eq 'path') {continue}
                $left=$r.sourceEntry.raw.PSObject.Properties[$field];$right=$primary.sourceEntry.raw.PSObject.Properties[$field]
                if (!$left -or !$right -or ($left.Value | ConvertTo-Json -Compress -Depth 20) -cne ($right.Value | ConvertTo-Json -Compress -Depth 20)) {$r.metadataConflicts+=@($field)}
            }
            $r.reason+=' Another XML record refers or proposes to refer to the same structural unit (including track/disc member metadata). Primary record: '+$primary.entryKey+'. All XML fields remain preserved.'
            if ($r.metadataConflicts.Count -gt 0) {$r.reason+=' Differing fields retained for review: '+($r.metadataConflicts -join ',')+'. Duplicate classification is structural; the XML values are not necessarily identical.'}
        }
    }
    # Also explain repeated missing paths, without inventing a ROM candidate.
    $pathGroups=@{}
    foreach ($r in $records) {if ($r.status -eq 'unresolved' -and !$r.candidateUnit -and $r.candidates.Count -eq 0 -and $r.sourceEntry.resolvedPath) {Add-RmReconcileIndex $pathGroups $r.sourceEntry.resolvedPath $r}}
    foreach ($group in $pathGroups.Values) {
        $sorted=@($group.ToArray() | Sort-Object @{Expression={$_.sourceEntry.sourcePath}},@{Expression={$_.sourceEntry.sourceLocatorNormalized}})
        foreach ($r in $sorted | Select-Object -Skip 1) {$r.classification='DUPLICATE_METADATA';$r.rule='REPEATED_UNRESOLVED_XML_PATH';$r.confidence='HIGH';$r.isDuplicate=$true;$r.duplicateOfEntryKey=$sorted[0].entryKey;$r.reason+=' Repeated XML reference to the same absent path; no ROM candidate exists.'}
    }
    # Flag existing alternative formats only when the complete stem is identical.
    # Do not discard regions/discs and do not merge the games or ROM units.
    foreach ($group in $stems.Values) {
        $formats=@($group.ToArray() | Where-Object {$_.kind -notin @('directory','ps3-package')} | Sort-Object technicalKey)
        if ($formats.Count -lt 2 -or @($formats | ForEach-Object {[IO.Path]::GetExtension($_.entryPath)} | Sort-Object -Unique).Count -lt 2) {continue}
        $primary=$formats[0].technicalKey
        foreach ($r in $records) {
            if (!$r.candidateUnit -or $r.status -eq 'folder' -or $r.isDuplicate -or $r.candidateUnit.unitKey -eq $primary) {continue}
            if ($r.candidateUnit.unitKey -notin @($formats | ForEach-Object {$_.technicalKey})) {continue}
            $r.relatedVariantUnitKeys=@($formats | Where-Object {$_.technicalKey -ne $r.candidateUnit.unitKey} | ForEach-Object {$_.technicalKey})
            $r.classification='VARIANT_METADATA';$r.rule='EXACT_COMPLETE_STEM_DIFFERENT_FORMAT';$r.confidence='MEDIUM'
            $r.reason+=' Another physical format has the same complete stem in the same console. Format-variant relationship is only proposed; no canonical game grouping is applied.'

            # Strict format-preserving rule candidate (preview/read-only phase):
            # the historical metadata explicitly names the complete candidate filename including extension.
            # A same-stem alternate-format sibling must remain a distinct physical unit; no game/variant/unit merge is inferred.
            $sourceName=[IO.Path]::GetFileName(([string]$r.sourceEntry.resolvedPath).Replace('/','\')).Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
            $candidateName=[IO.Path]::GetFileName(([string]$r.candidateUnit.entryPath).Replace('/','\')).Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
            $candidateExt=[IO.Path]::GetExtension($candidateName).ToLowerInvariant()
            $relatedUnits=@($formats | Where-Object {$_.technicalKey -ne $r.candidateUnit.unitKey})
            $relatedExts=@($relatedUnits | ForEach-Object {[IO.Path]::GetExtension(([string]$_.entryPath).Replace('/','\')).ToLowerInvariant()} | Sort-Object -Unique)
            $candidateOutsideDuplicates=(([string]$r.candidateUnit.entryPath) -notmatch '(?i)(^|/)_DUPLICATES(/|$)')
            if($sourceName -ceq $candidateName -and $candidateExt -ceq '.chd' -and $relatedUnits.Count -eq 1 -and $relatedExts.Count -eq 1 -and $relatedExts[0] -ceq '.cue' -and $candidateOutsideDuplicates){
                $r.rule='FORMAT_PRESERVING_EXACT_FILENAME_WITH_ALTERNATE_FORMAT'
                $r.confidence='HIGH'
                $r.reason+=' The historical metadata complete filename including extension selects the CHD candidate exactly (case-insensitive Unicode NFC only). The same-stem CUE remains a separate physical unit; no game, variant or romUnit merge is inferred.'
            }
        }
    }
    foreach ($r in $records) {
        if ($r.status -ne 'unresolved') {continue}
        $severity='Info';$code='METADATA_UNRESOLVED'
        if ($r.classification -eq 'MISSING_ROM') {$severity='Warning';$code='ROM_NOT_FOUND'}
        elseif ($r.classification -eq 'AMBIGUOUS') {$severity='Warning';$code='METADATA_RECONCILIATION_AMBIGUOUS'}
        Add-RmAnomaly $Context $code ([string]$r.sourceEntry.resolvedPath) ($r.classification+' | '+$r.reason) $severity $r.sourceEntry.console
    }
    $resolved=@($records | Where-Object {$_.status -eq 'resolved'});$unresolved=@($records | Where-Object {$_.status -eq 'unresolved'});$folders=@($records | Where-Object {$_.status -eq 'folder'})
    return [pscustomobject]@{mode='read-only';appliedCount=0;resolved=$resolved;unresolved=$unresolved;folders=$folders;
        classificationCounts=@($records | Where-Object {$_.classification} | Group-Object classification | Sort-Object Name | ForEach-Object {[pscustomobject]@{classification=$_.Name;count=$_.Count}})}
}
function Set-RmConsoleSummary {
    param($Catalogue)
    $summaries=@()
    foreach ($old in $Catalogue.consoles) {
        $console=$old.console;$games=@($Catalogue.games | Where-Object {$_.console -eq $console});$variants=@($games | ForEach-Object {$_.variants})
        $resolved=@($Catalogue.metadataReconciliation.resolved | Where-Object {$_.sourceEntry.console -eq $console})
        $unresolved=@($Catalogue.metadataReconciliation.unresolved | Where-Object {$_.sourceEntry.console -eq $console})
        $folders=@($Catalogue.metadataReconciliation.folders | Where-Object {$_.sourceEntry.console -eq $console})
        $media=@{};foreach ($g in $games) {foreach ($m in $g.media) {$media[$m.fileId]=$true};foreach ($v in $g.variants) {foreach ($o in $v.mediaOverrides) {foreach ($m in $o.items) {$media[$m.fileId]=$true}}}}
        $legacySuggestions=@($Catalogue.suggestions | Where-Object {$_.console -eq $console})
        $summaries+=@([pscustomobject]@{console=$console;romUnitsPhysical=@($variants | ForEach-Object {$_.romUnits}).Count;canonicalGames=$games.Count;
            xmlGameEntries=($resolved.Count+$unresolved.Count);xmlFolderEntries=$folders.Count;metadataResolved=$resolved.Count;metadataUnresolved=$unresolved.Count;
            metadataDuplicates=@(@($resolved)+@($unresolved) | Where-Object {$_.isDuplicate}).Count;
            metadataDuplicatesResolved=@($resolved | Where-Object {$_.isDuplicate}).Count;
            metadataDuplicatesProposed=@($unresolved | Where-Object {$_.isDuplicate}).Count;
            anomalies=@($Catalogue.anomalies | Where-Object {$_.console -eq $console -and $_.severity -ne 'Info'}).Count;
            suggestions=($legacySuggestions.Count+$unresolved.Count+$folders.Count);
            reconciliationSuggestions=($unresolved.Count+$folders.Count);librarySuggestions=$legacySuggestions.Count;
            variants=$variants.Count;media=$media.Count;info=@($Catalogue.anomalies | Where-Object {$_.console -eq $console -and $_.severity -eq 'Info'}).Count;
            auxiliary=@($Catalogue.files | Where-Object {$_.console -eq $console -and $_.type -eq 'auxiliary'}).Count})
    }
    $Catalogue.consoles=$summaries
}
Export-ModuleMember -Function Get-RmMetadataReconciliation,Set-RmConsoleSummary
