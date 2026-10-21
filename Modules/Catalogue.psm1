Set-StrictMode -Version 2.0

function New-RmSuggestions {
    param($Context,$Units,$Registry,$Associations,$Media)
    $items=New-Object 'System.Collections.Generic.List[object]'
    $mediaSuggestions=@{}
    foreach ($s in $Media.suggestions) {
        $unit=@($Units | Where-Object {$_.variantId -eq $s.sourceVariantId})[0]
        $gid=Resolve-RmMergeTarget $unit.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
        if (@($Associations.gameMediaAssignments | Where-Object {
            $_.relativePath -eq $s.relativePath -and (Resolve-RmMergeTarget $_.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId') -eq $gid
        }).Count -gt 0) {continue}
        $key=$unit.console+'|'+$s.relativePath
        if (!$mediaSuggestions.ContainsKey($key)) {
            $s.sourceGameId=$gid
            $s | Add-Member -NotePropertyName sourceGameIds -NotePropertyValue @($gid)
            $s.reason='Common media candidate; approve the displayed game IDs explicitly.'
            $mediaSuggestions[$key]=$s
        } else {$mediaSuggestions[$key].sourceGameIds=@(@($mediaSuggestions[$key].sourceGameIds)+@($gid) | Select-Object -Unique)}
    }
    foreach ($key in @($mediaSuggestions.Keys | Sort-Object)) {$items.Add($mediaSuggestions[$key])}
    foreach ($unit in $Units) {
        if ($unit.fileIds.Count -eq 0 -and $unit.metadata.Count -gt 0) {
            $items.Add([pscustomobject]@{kind='review-missing-rom';sourceVariantId=$unit.variantId;targetVariantId=$null;
                sourceGameId=$unit.gameId;targetGameId=$null;relativePath=$unit.entryPath;reason='Human review required; missing XML path is never matched automatically.'})
        }
        if ($unit.kind -eq 'ps3-package' -and $unit.packageRoleStatus -eq 'needs-validation') {
            $items.Add([pscustomobject]@{kind='review-package-roles';sourceVariantId=$unit.variantId;targetVariantId=$null;
                sourceGameId=$unit.gameId;targetGameId=$null;relativePath=$unit.entryPath;reason='Human review required; primary package/RAP roles are ambiguous.'})
        }
    }
    foreach ($group in @($Units | Group-Object { $_.console+'|'+(Get-RmLooseName $_.name) })) {
        if ($group.Count -lt 2) {continue}
        $sorted=@($group.Group | Sort-Object technicalKey);$target=$sorted[0]
        foreach ($source in $sorted[1..($sorted.Count-1)]) {
            $sg=Resolve-RmMergeTarget $source.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
            $tg=Resolve-RmMergeTarget $target.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
            if ($sg -eq $tg) {continue}
            $items.Add([pscustomobject]@{kind='merge-games';sourceVariantId=$source.variantId;targetVariantId=$target.variantId;
                sourceGameId=$sg;targetGameId=$tg;relativePath=$null;reason='Similar titles; retain separate variants in one game only after approval.'})
        }
    }
    # Explicit disc markers produce suggestions, never automatic merges.
    $discGroups=@{}
    foreach ($u in $Units) {
        if (!$u.entryPath) {continue}
        $stem=[IO.Path]::GetFileNameWithoutExtension($u.entryPath.Replace('/','\'))
        if ($stem -notmatch '(?i)(?:\(|\[|\s)(?:disc|disk|cd)\s*\d+(?:\)|\])?') {continue}
        $key=$u.console+'|'+(Get-RmLooseName ($stem -replace '(?i)(?:\(|\[|\s)(?:disc|disk|cd)\s*\d+(?:\)|\])?',''))
        if (!$discGroups.ContainsKey($key)) {$discGroups[$key]=@()};$discGroups[$key]+=@($u)
    }
    foreach ($group in $discGroups.Values) {
        if ($group.Count -lt 2) {continue}
        $sorted=@($group | Sort-Object technicalKey);$target=$sorted[0]
        foreach ($source in $sorted[1..($sorted.Count-1)]) {
            $sv=Resolve-RmMergeTarget $source.variantId $Associations.variantMerges 'sourceVariantId' 'targetVariantId'
            $tv=Resolve-RmMergeTarget $target.variantId $Associations.variantMerges 'sourceVariantId' 'targetVariantId'
            if ($sv -eq $tv) {continue}
            $items.Add([pscustomobject]@{kind='merge-variants';sourceVariantId=$sv;targetVariantId=$tv;
                sourceGameId=$source.gameId;targetGameId=$target.gameId;relativePath=$null;reason='Disc markers detected; combine ROM units only after approval.'})
        }
    }
    $current=@{};foreach ($u in $Units) {$current[$u.variantId]=$true}
    foreach ($u in $Units) {
        $record=@($Registry.variants | Where-Object {$_.variantId -eq $u.variantId})[0]
        if (!$record.signature) {continue}
        foreach ($old in $Registry.variants) {
            if ($old.retired -or $current.ContainsKey($old.variantId) -or $old.console -ne $u.console -or $old.signature -ne $record.signature) {continue}
            $items.Add([pscustomobject]@{kind='rebind-identity';sourceVariantId=$u.variantId;targetVariantId=$old.variantId;
                sourceGameId=$u.gameId;targetGameId=$old.gameId;relativePath=$u.entryPath;
                reason='Absent identity with same size/date fingerprint. Not proof of identical content.'})
        }
    }
    $number=0
    $unitMap=@{};foreach ($u in $Units) {$unitMap[$u.variantId]=$u}
    foreach ($s in $items) {
        $number++;$s | Add-Member -NotePropertyName number -NotePropertyValue $number
        $s | Add-Member -NotePropertyName console -NotePropertyValue $unitMap[$s.sourceVariantId].console
        $s | Add-Member -NotePropertyName applicable -NotePropertyValue $false
    }
    return ,$items.ToArray()
}

function New-RmCatalogue {
    param($Context,$Inventory,$Metadata,$Units,$Registry,$Associations,$Media)
    $variants=@{};$identity=@{};$registryGames=@{};$files=@{}
    foreach ($v in $Registry.variants) {$identity[$v.variantId]=$v}
    foreach ($g in $Registry.games) {$registryGames[$g.gameId]=$g}
    foreach ($f in $Inventory.files) {$files[$f.fileId]=$f}
    foreach ($r in $Associations.gameMerges) {
        if (!$registryGames.ContainsKey($r.sourceGameId) -or !$registryGames.ContainsKey($r.targetGameId) -or
            $registryGames[$r.sourceGameId].console -ne $registryGames[$r.targetGameId].console) {throw 'Invalid persistent game association.'}
    }
    foreach ($r in $Associations.variantMerges) {
        if (!$identity.ContainsKey($r.sourceVariantId) -or !$identity.ContainsKey($r.targetVariantId) -or
            $identity[$r.sourceVariantId].console -ne $identity[$r.targetVariantId].console) {throw 'Invalid persistent variant association.'}
    }
    foreach ($u in $Units) {
        $vid=Resolve-RmMergeTarget $u.variantId $Associations.variantMerges 'sourceVariantId' 'targetVariantId'
        if (!$variants.ContainsKey($vid)) {
            $vIdentity=$identity[$vid]
            $gid=Resolve-RmMergeTarget $vIdentity.gameId $Associations.gameMerges 'sourceGameId' 'targetGameId'
            $variants[$vid]=[pscustomobject]@{variantId=$vid;gameId=$gid;name=$vIdentity.name;console=$vIdentity.console;
                technicalKeys=@();metadata=@();romUnits=@();media=@();sizes=$null;status=$null}
        }
        $v=$variants[$vid];$v.technicalKeys+=@($u.technicalKey);$v.metadata+=@($u.metadata)
        $romUnit=[pscustomobject]@{romUnitId=$u.romUnitId;observationDigest=$u.observationDigest;unitKey=$u.technicalKey;kind=$u.kind;entryPath=$u.entryPath;
            fileIds=$u.fileIds;missingDependencies=$u.missingDependencies;complete=$u.complete;classificationEvidence=$u.classificationEvidence;
            contentValidation='not-performed'}
        if ($u.kind -eq 'ps3-package') {
            $romUnit | Add-Member -NotePropertyName primaryPackageFileId -NotePropertyValue $u.primaryPackageFileId
            $romUnit | Add-Member -NotePropertyName packageRoles -NotePropertyValue $u.packageRoles
            $romUnit | Add-Member -NotePropertyName packageRoleStatus -NotePropertyValue $u.packageRoleStatus
        }
        $v.romUnits+=@($romUnit)
        $v.media+=@($Media.matches[$u.variantId])
    }
    $games=@{};$used=@{};$romUsage=@{}
    foreach ($v in $variants.Values | Sort-Object console,name,variantId) {
        $romIds=@($v.romUnits | ForEach-Object {$_.fileIds} | Select-Object -Unique)
        $uniqueMedia=@{};foreach ($m in $v.media) {$uniqueMedia[$m.fileId]=$m};$v.media=@($uniqueMedia.Values | Sort-Object relativePath)
        [long]$romBytes=0;[long]$mediaBytes=0
        foreach ($id in $romIds) {
            $romBytes+=$files[$id].sizeBytes;$used[$id]=$true
            if (!$romUsage.ContainsKey($id)) {$romUsage[$id]=@()};$romUsage[$id]+=@($v.variantId)
        }
        foreach ($m in $v.media) {$mediaBytes+=$files[$m.fileId].sizeBytes;$used[$m.fileId]=$true}
        $complete=($Inventory.complete -and @($v.romUnits | Where-Object {!$_.complete}).Count -eq 0)
        $romStatus='resolved';if ($romIds.Count -eq 0) {$romStatus='missing'} elseif (!$complete) {$romStatus='incomplete'}
        $v.sizes=[pscustomobject]@{romKnownBytes=$romBytes;mediaKnownBytes=$mediaBytes;isComplete=$complete}
        $ms='missing';if ($v.metadata.Count -gt 0) {$ms='loaded'}
        $v.status=[pscustomobject]@{rom=$romStatus;metadata=$ms;mediaPresentTypes=@($v.media | ForEach-Object {$_.mediaType} | Select-Object -Unique)}
        if (!$games.ContainsKey($v.gameId)) {
            $g=$registryGames[$v.gameId];$games[$v.gameId]=[pscustomobject]@{gameId=$g.gameId;console=$g.console;name=$g.name;variants=@()}
        }
        $games[$v.gameId].variants+=@($v)
    }
    foreach ($id in $romUsage.Keys) {
        if (@($romUsage[$id] | Select-Object -Unique).Count -gt 1) {
            Add-RmAnomaly $Context 'SHARED_ROM_FILE' $files[$id].relativePath 'Physical file referenced by several variants.'
        }
    }
    [long]$romTotal=0;[long]$mediaTotal=0;[long]$indexedRomTotal=0
    foreach ($f in $Inventory.files) {
        if ($f.relativePath -like 'ROMS/*') {$romTotal+=$f.sizeBytes}
        elseif ($f.relativePath -like 'MEDIAS/*') {$mediaTotal+=$f.sizeBytes}
        if ($romUsage.ContainsKey($f.fileId)) {$indexedRomTotal+=$f.sizeBytes}
    }
    $consoleSummaries=@()
    foreach ($console in @($Inventory.directories | Where-Object {$_ -match '^ROMS/[^/]+$'} | ForEach-Object {$_.Split('/')[1]} | Sort-Object)) {
        $consoleSummaries+=@([pscustomobject]@{console=$console;gameCount=@($games.Values | Where-Object {$_.console -eq $console}).Count;
            variantCount=@($variants.Values | Where-Object {$_.console -eq $console}).Count;
            metadataEntryCount=@($Metadata.entries | Where-Object {$_.console -eq $console}).Count})
    }
    $unassigned=@($Inventory.files | Where-Object {$_.type -notin @('metadata','legacy','auxiliary') -and !$used.ContainsKey($_.fileId)} | ForEach-Object {$_.fileId})
    foreach ($id in $unassigned) {
        if ($files[$id].relativePath -like 'ROMS/*') {Add-RmAnomaly $Context 'ROM_UNCLASSIFIED' $files[$id].relativePath 'File not assigned to a ROM unit; retained in inventory.' 'Warning' $files[$id].console}
    }
    foreach ($f in $Inventory.files | Where-Object {$_.type -eq 'auxiliary'}) {Add-RmAnomaly $Context 'AUXILIARY_FILE' $f.relativePath 'Auxiliary file retained in physical inventory.' 'Info' $f.console}
    $status='complete';if (!$Inventory.complete -or @($Metadata.sources | Where-Object {$_.status -eq 'error'}).Count -gt 0) {$status='partial'}
    return [pscustomobject]@{schemaVersion=$Context.Config.schemaVersion;applicationVersion=$Context.Config.applicationVersion;
        generatedAtUtc=[DateTime]::UtcNow.ToString('o');scan=[pscustomobject]@{scanId=$Context.ScanId;rootObserved=$Context.Root;
            status=$status;inventoryComplete=$Inventory.complete;identityRevision=$Registry.revision;associationsRevision=$Associations.revision;
            associationsReadOnly=$true;reconciliationReadOnly=$true;
            durationSeconds=0;runtimeVersion=$PSVersionTable.PSVersion.ToString();runtimeEdition=$PSVersionTable.PSEdition;hashMode='none'};
        sources=$Metadata.sources;consoles=$consoleSummaries;games=@($games.Values | Sort-Object console,name,gameId);
        files=$Inventory.files;unassigned=$unassigned;auxiliary=@($Inventory.files | Where-Object {$_.type -eq 'auxiliary'} | ForEach-Object {$_.fileId});legacy=@($Inventory.files | Where-Object {$_.type -eq 'legacy'} | ForEach-Object {$_.fileId});
        statistics=[pscustomobject]@{gameCount=$games.Count;variantCount=$variants.Count;fileCount=$Inventory.files.Count;
            romPhysicalKnownBytes=$romTotal;romIndexedUniqueBytes=$indexedRomTotal;mediaPhysicalKnownBytes=$mediaTotal};
        metadataReconciliation=$null;anomalies=@();suggestions=@()}
}

function Write-RmReports {
    param($Context,$Catalogue)
    $stem=$Context.OutputRoot+'\Reports\'+$Context.ScanId
    New-RmDirectory (Get-RmParentPath $stem)
    $summary=@('# Analyse ROMothèque', '', 'Version : '+$Catalogue.applicationVersion,
        'Scan : '+$Catalogue.scan.scanId, 'Source : '+$Context.Root, 'État : '+$Catalogue.scan.status,
        'Jeux : '+$Catalogue.statistics.gameCount, 'Variantes : '+$Catalogue.statistics.variantCount,
        'Fichiers physiques : '+$Catalogue.statistics.fileCount,
        'Octets ROMS connus : '+$Catalogue.statistics.romPhysicalKnownBytes,
        'Octets ROMS indexés uniques : '+$Catalogue.statistics.romIndexedUniqueBytes,
        'Octets MEDIAS connus (legacy inclus) : '+$Catalogue.statistics.mediaPhysicalKnownBytes,
        'Anomalies (Warning/Error) : '+@($Catalogue.anomalies | Where-Object {$_.severity -ne 'Info'}).Count,
        'Informations : '+@($Catalogue.anomalies | Where-Object {$_.severity -eq 'Info'}).Count,
        'Metadonnees resolues (projection) : '+$Catalogue.statistics.metadataResolved,
        'Metadonnees non resolues (projection) : '+$Catalogue.statistics.metadataUnresolved,
        'Entrees metadata game : '+$Catalogue.statistics.metadataGameEntries,
        'Suggestions : '+($Catalogue.suggestions.Count+$Catalogue.metadataReconciliation.unresolved.Count+$Catalogue.metadataReconciliation.folders.Count),
        '', 'Les tailles représentent les fichiers inventoriés, sans compression estimée.',
        'Aucun hash de ROM calculé. Les extensions fournissent des candidats, sans certifier le contenu.',
        '', 'Reconciliation en lecture seule : aucune proposition appliquee, associations.json inchange.',
        'metadataResolved/unresolved comptent les entrees game ; leur somme egale xmlGameEntries.',
        'metadataDuplicates est un sous-ensemble des entrees game, pas un compteur disjoint.',
        'suggestions = propositions unresolved + dossiers + suggestions de bibliotheque ; toutes non applicables en 0.3.1.')
    $table=@('','## Resume par console','','| Console | romUnitsPhysical | canonicalGames | xmlGameEntries | xmlFolderEntries | metadataResolved | metadataUnresolved | metadataDuplicates | anomalies | suggestions |','|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|')
    foreach ($c in $Catalogue.consoles) {$table+=@('| '+$c.console+' | '+$c.romUnitsPhysical+' | '+$c.canonicalGames+' | '+$c.xmlGameEntries+' | '+$c.xmlFolderEntries+' | '+$c.metadataResolved+' | '+$c.metadataUnresolved+' | '+$c.metadataDuplicates+' | '+$c.anomalies+' | '+$c.suggestions+' |')}
    $summary+=$table
    $summary+=@('','## Suggestions a valider','')
    foreach ($s in $Catalogue.suggestions) {
        $ids=$s.sourceGameId;if ($s.PSObject.Properties['sourceGameIds']) {$ids=@($s.sourceGameIds) -join ','}
        $summary+=@(($s.number.ToString()+'. '+$s.kind+' | games='+$ids+' | '+$s.sourceVariantId+' -> '+$s.targetVariantId+' | '+$s.relativePath+' | '+$s.reason))
    }
    Write-RmAllLines ($stem+'.md') ([string[]]$summary) (New-Object Text.UTF8Encoding($false))
    $csv=@($Catalogue.anomalies | ConvertTo-Csv -NoTypeInformation -Delimiter ';')
    if ($csv.Count -eq 0) {$csv=@('"anomalyId";"code";"severity";"console";"variantId";"relativePath";"message"')}
    Write-RmAllLines ($stem+'.csv') ([string[]]$csv) (New-Object Text.UTF8Encoding($true))
    $consoleCsv=@($Catalogue.consoles | ConvertTo-Csv -NoTypeInformation -Delimiter ';')
    Write-RmAllLines ($stem+'_consoles.csv') ([string[]]$consoleCsv) (New-Object Text.UTF8Encoding($true))
    # Keep only two full catalogues; dated reports do not duplicate the entire physical index.
    Write-RmJsonAtomic ($stem+'.json') ([pscustomobject]@{applicationVersion=$Catalogue.applicationVersion;
        scan=$Catalogue.scan;statistics=$Catalogue.statistics;consoles=$Catalogue.consoles;sources=$Catalogue.sources;
        anomalies=$Catalogue.anomalies;suggestions=$Catalogue.suggestions;
        reconciliationCounts=$Catalogue.metadataReconciliation.classificationCounts})
    Write-RmJsonAtomic ($stem+'_reconciliation.json') $Catalogue.metadataReconciliation
    $rows=@($Catalogue.metadataReconciliation.resolved)+@($Catalogue.metadataReconciliation.unresolved)+@($Catalogue.metadataReconciliation.folders)
    $csvRows=@($rows | ForEach-Object {
        $r=$_
        [pscustomobject]@{console=$r.sourceEntry.console;entryKey=$r.entryKey;sourcePath=$r.sourceEntry.sourcePath;entryIndex=$r.sourceEntry.entryIndex;
            entryKind=$r.sourceEntry.entryKind;xmlPath=$r.sourceEntry.resolvedPath;status=$r.status;classification=$r.classification;
            matchClassification=$r.matchClassification;matchRule=$r.matchRule;matchConfidence=$r.matchConfidence;rule=$r.rule;confidence=$r.confidence;reason=$r.reason;
            candidateUnitKeys=(@($r.candidates | ForEach-Object {$_.unitKey}) -join ' | ');
            candidatePaths=(@($r.candidates | ForEach-Object {$_.entryPath}) -join ' | ');
            candidateFilePaths=(@($r.candidates | ForEach-Object {$_.matchingFiles} | ForEach-Object {$_.relativePath}) -join ' | ');
            duplicateOfEntryKey=$r.duplicateOfEntryKey;metadataConflicts=($r.metadataConflicts -join ',');applied=$false;sourceEntryXml=$r.sourceEntry.rawEntryXml}
    } | ConvertTo-Csv -NoTypeInformation -Delimiter ';')
    if ($csvRows.Count -eq 0) {$csvRows=@('"console";"entryKey";"classification";"rule";"confidence";"reason";"candidateUnitKeys"')}
    Write-RmAllLines ($stem+'_reconciliation.csv') ([string[]]$csvRows) (New-Object Text.UTF8Encoding($true))
    $recMd=@('# Reconciliation ES-DE — lecture seule','','Scan : '+$Context.ScanId,'Aucune proposition appliquee. Identite de contenu non verifiee.','')
    foreach ($r in @($Catalogue.metadataReconciliation.unresolved)+@($Catalogue.metadataReconciliation.folders)) {
        $recMd+=@('## '+$r.sourceEntry.console+' / '+$r.entryKey,
            'Classe : '+$r.classification+' ; regle : '+$r.rule+' ; confiance : '+$r.confidence,
            'XML : '+$r.sourceEntry.resolvedPath,
            'Unites candidates : '+(@($r.candidates | ForEach-Object {$_.entryPath+' ['+$_.unitKey+']'}) -join ' | '),
            'Raison : '+$r.reason,'')
    }
    Write-RmAllLines ($stem+'_reconciliation.md') ([string[]]$recMd) (New-Object Text.UTF8Encoding($false))
    $log=$Context.OutputRoot+'\Logs\'+$Context.ScanId+'_Scan.log'
    New-RmDirectory (Get-RmParentPath $log)
    $logStream=Open-RmFile $log ([IO.FileMode]::Create) ([IO.FileAccess]::Write) ([IO.FileShare]::None)
    $writer=New-Object IO.StreamWriter($logStream,(New-Object Text.UTF8Encoding($false)),65536,$false)
    try {
        foreach ($line in @('UTC='+$Catalogue.generatedAtUtc,'SOURCE='+$Context.Root,'OUTPUT='+$Context.OutputRoot,
            'STATUS='+$Catalogue.scan.status,'DURATION_SECONDS='+$Catalogue.scan.durationSeconds,
            'FILES='+$Catalogue.statistics.fileCount,'HASH_MODE=none')) {$writer.WriteLine($line)}
        foreach ($f in $Catalogue.files) {$writer.WriteLine('INDEXED | '+$f.relativePath+' | '+$f.sizeBytes)}
        foreach ($a in $Catalogue.anomalies) {$writer.WriteLine($a.severity+' | '+$a.code+' | '+$a.relativePath+' | '+$a.message)}
    } finally {$writer.Dispose()}
}

function Invoke-RmScan {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$Root,[string]$OutputRoot,[string[]]$HashEvidencePaths=@())
    $Root=Get-RmFullPath $Root
    if (!(Test-RmDirectoryExists ($Root+'\ROMS')) -or
        !(Test-RmDirectoryExists ($Root+'\MEDIAS'))) {throw 'Root must contain ROMS and MEDIAS directories.'}
    if (!$OutputRoot) {throw 'EXPLICIT_ISOLATED_OUTPUT_ROOT_REQUIRED_FOR_032_DELIVERY'}
    $OutputRoot=Get-RmFullPath $OutputRoot;Assert-RmOutputPath $Root $OutputRoot
    if(!(Test-RmFixtureRoot $Root)-and(Test-RmContainedPath $Root $OutputRoot)){throw 'REAL_LIBRARY_READ_ONLY_REQUIRES_OUTPUT_OUTSIDE_SOURCE'}
    $lock=Open-RmStateLock $OutputRoot
    $timer=[Diagnostics.Stopwatch]::StartNew()
    try {
        $context=[pscustomobject]@{Root=$Root;OutputRoot=$OutputRoot;Config=(Get-RmConfiguration);ScanId=(New-RmId);
            Anomalies=(New-Object 'System.Collections.Generic.List[object]')}
        $registry=Get-RmIdentityState $OutputRoot;$registryBeforeSchema=$registry.schemaVersion
        $gameIdsBefore=@($registry.games|ForEach-Object{$_.gameId});$variantIdsBefore=@($registry.variants|ForEach-Object{$_.variantId});$unitCountBefore=@(Get-RmProperty $registry 'romUnits' @()).Count
        $registry=ConvertTo-RmRegistry2 $registry
        $associations=Get-RmAssociations $OutputRoot -ReadOnly;$associationsBeforeSchema=$associations.schemaVersion
        if(Test-RmFileExists ($OutputRoot+'\associations.json')){$associationsBeforeSchema=(Read-RmStrictJson ($OutputRoot+'\associations.json')).schemaVersion}else{$associationsBeforeSchema='absent'}
        $associationDigestBefore=Get-RmByteDigest ($OutputRoot+'\associations.json')
        $associations=ConvertTo-RmAssociations3 $associations $registry.libraryId
        $inventory=Get-RmInventory $context
        $metadata=Get-RmMetadata $context $inventory
        $units=Get-RmRomUnits $context $inventory $metadata
        Set-RmUnitIdentities $context $units $inventory $registry
        Set-RmPersistentIdentities $context $inventory $metadata $units $registry
        $reconciliation=Get-RmMetadataReconciliation $context $metadata $units $inventory $associations
        $media=Get-RmMediaMatches $context $units $inventory $associations
        $catalogue=New-RmCatalogue $context $inventory $metadata $units $registry $associations $media
        Set-RmGameMediaModel $context $catalogue $inventory $associations
        $catalogue.metadataReconciliation=$reconciliation
        Set-RmProperty $catalogue 'libraryId' $registry.libraryId
        Set-RmProperty $catalogue 'metadataIndex' @(@($metadata.entries)+@($metadata.sources|ForEach-Object{$_.folderMetadata}))
        Set-RmProperty $catalogue 'physicalSnapshot' (Get-RmPhysicalSnapshot $context $inventory)
        Set-RmProperty $catalogue.scan 'fixtureCommitAllowed' (Test-RmFixtureRoot $Root)
        $hashEvidence=@($HashEvidencePaths|ForEach-Object{
            $diagnosticPath=Get-RmFullPath $_
            if(!(Test-RmContainedPath ($OutputRoot+'\Diagnostics') $diagnosticPath)){throw 'HASH_DIAGNOSTIC_MUST_BELONG_TO_THIS_STATE'}
            Read-RmStrictJson $diagnosticPath
        })
        Set-RmProperty $catalogue 'decisionProposals' (New-RmDecisionProposals $catalogue $registry $associations $hashEvidence)
        Set-RmDecisionProjection $catalogue $registry $associations
        $catalogue.suggestions=New-RmSuggestions $context $units $registry $associations $media
        $catalogue.anomalies=$context.Anomalies.ToArray();Set-RmConsoleSummary $catalogue
        foreach($c in $catalogue.consoles){
            Set-RmProperty $c 'metadataResolvedDirect' $c.metadataResolved
            $manual=@($catalogue.metadataIndex|Where-Object{$_.console -eq $c.console-and$_.metadataEntryId -in $catalogue.manualMetadataResolvedIds-and$_.entryKind -eq 'game'-and$_.metadataEntryId -in @($reconciliation.unresolved|ForEach-Object{$_.sourceEntry.metadataEntryId})}).Count
            Set-RmProperty $c 'metadataResolvedManual' $manual
            $c.metadataResolved+=$manual;$c.metadataUnresolved-=$manual
        }
        # The projected per-console counters are the single source of truth for
        # global metadata status. Never recompute the global unresolved count from
        # raw reconciliation after persistent decisions have been projected.
        [int]$metadataResolvedGlobal=($catalogue.consoles|Measure-Object -Property metadataResolved -Sum).Sum
        [int]$metadataUnresolvedGlobal=($catalogue.consoles|Measure-Object -Property metadataUnresolved -Sum).Sum
        [int]$metadataGameEntriesGlobal=($catalogue.consoles|Measure-Object -Property xmlGameEntries -Sum).Sum
        if(($metadataResolvedGlobal+$metadataUnresolvedGlobal)-ne$metadataGameEntriesGlobal){
            throw 'METADATA_GLOBAL_CONSOLE_INVARIANT_FAILED'
        }
        Set-RmProperty $catalogue.statistics 'metadataResolved' $metadataResolvedGlobal
        Set-RmProperty $catalogue.statistics 'metadataUnresolved' $metadataUnresolvedGlobal
        Set-RmProperty $catalogue.statistics 'metadataGameEntries' $metadataGameEntriesGlobal
        $timer.Stop();$catalogue.scan.durationSeconds=$timer.Elapsed.TotalSeconds
        # Identity first: a crash before catalogue publication must not lose allocated IDs.
        Assert-RmRegistry2 $registry
        Write-RmJsonAtomic ($OutputRoot+'\identity_registry.json') $registry -KeepPrevious
        # Scan never creates/migrates/publishes associations.json, including fixtures.
        Write-RmJsonAtomic ($OutputRoot+'\Reports\'+$context.ScanId+'_migration.json') ([pscustomobject]@{scanId=$context.ScanId;registrySchemaBefore=$registryBeforeSchema;registrySchemaAfter=$registry.schemaVersion;associationsSchemaBefore=$associationsBeforeSchema;associationsProjectedSchema=$associations.schemaVersion;associationFileWritten=$false;associationBytesBefore=$associationDigestBefore;associationBytesAfter=(Get-RmByteDigest ($OutputRoot+'\associations.json'));gameIdsPreserved=(@($gameIdsBefore|Where-Object{$_ -notin @($registry.games|ForEach-Object{$_.gameId})}).Count -eq 0);variantIdsPreserved=(@($variantIdsBefore|Where-Object{$_ -notin @($registry.variants|ForEach-Object{$_.variantId})}).Count -eq 0);romUnitIdsAdded=(@($registry.romUnits).Count-$unitCountBefore);metadataEntriesTracked=@($registry.metadataEntries).Count})
        Write-RmJsonAtomic ($OutputRoot+'\Reports\'+$context.ScanId+'_proposals.json') $catalogue.decisionProposals
        Write-RmReports $context $catalogue
        Write-RmJsonAtomic ($OutputRoot+'\catalogue.json') $catalogue -KeepPrevious
        return $catalogue
    } catch {
        # Bootstrap/state failures are fatal, but must leave the published catalogue untouched.
        $fatalPath=$OutputRoot+'\Logs\'+(New-RmId)+'_ScanFailed.log'
        try {
            New-RmDirectory (Get-RmParentPath $fatalPath)
            Write-RmAllText $fatalPath ('UTC='+[DateTime]::UtcNow.ToString('o')+"`r`nSOURCE="+$Root+"`r`nERROR="+$_.Exception.Message) (New-Object Text.UTF8Encoding($false))
        } catch {Write-Warning 'Could not write failure log.'}
        throw
    } finally {$lock.Dispose()}
}
Export-ModuleMember -Function Invoke-RmScan
