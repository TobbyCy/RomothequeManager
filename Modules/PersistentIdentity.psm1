Set-StrictMode -Version 2.0
function Set-RmPersistentIdentities {
 param($Context,$Inventory,$Metadata,$Units,$Registry)
 $Registry=ConvertTo-RmRegistry2 $Registry
 $map=@{};foreach($r in $Registry.romUnits){$map[$r.technicalKey]=$r}
 $fileMap=@{};foreach($f in $Inventory.files){$fileMap[$f.fileId]=$f}
 foreach($u in $Units){
  if(!$map.ContainsKey($u.technicalKey)){$r=[pscustomobject]@{romUnitId=(New-RmId);gameId=$u.gameId;variantId=$u.variantId;technicalKey=$u.technicalKey;lastSeenScanId=$Context.ScanId;observationDigest=$null};$Registry.romUnits+=@($r);$map[$u.technicalKey]=$r}
  $r=$map[$u.technicalKey];if($r.gameId -ne $u.gameId -or $r.variantId -ne $u.variantId){throw 'ROM_UNIT_OWNERSHIP_CHANGED'}
  $fs=@(Sort-RmOrdinal @($u.fileIds | ForEach-Object{$f=$fileMap[$_];[pscustomobject]@{relativePath=$f.relativePath;sizeBytes=$f.sizeBytes;modifiedAtUtc=$f.modifiedAtUtc;role=$(if($f.relativePath -eq $u.entryPath){'entry'}else{'dependency'})}}) 'relativePath')
  $obs=[pscustomobject]@{romUnitId=$r.romUnitId;kind=$u.kind;entryPath=$u.entryPath;files=$fs;missingDependencies=@(Sort-RmOrdinal $u.missingDependencies);complete=$u.complete}
  $r.observationDigest=Get-RmDigest $obs;$r.lastSeenScanId=$Context.ScanId
  Set-RmProperty $u 'romUnitId' $r.romUnitId;Set-RmProperty $u 'observationDigest' $r.observationDigest
 }
 $docs=@{};foreach($d in $Registry.documents){$docs[$d.relativePath]=$d;$d.status='missing'}
 foreach($s in $Metadata.sources){
  if(!$docs.ContainsKey($s.relativePath)){$d=[pscustomobject]@{documentId=(New-RmId);relativePath=$s.relativePath;status=$s.status;documentByteSha256=$null};$Registry.documents+=@($d);$docs[$s.relativePath]=$d}
  $d=$docs[$s.relativePath];$d.status=$s.status;$d.documentByteSha256=Get-RmByteDigest ($Context.Root+'\'+$s.relativePath.Replace('/','\'))
  if($s.status -eq 'loaded'-and$d.documentByteSha256 -ne $s.parsedByteSha256){throw 'XML_CHANGED_DURING_SCAN'}
  Set-RmProperty $s 'documentId' $d.documentId;Set-RmProperty $s 'documentByteSha256' $d.documentByteSha256
  # Byte archives are application state; never write back to MEDIAS.
  $archive=$Context.OutputRoot+'\MetadataVersions\'+$d.documentId+'\'+$d.documentByteSha256+'.xml'
  if(!(Test-RmFileExists $archive)){New-RmDirectory (Get-RmParentPath $archive);Copy-RmFile ($Context.Root+'\'+$s.relativePath.Replace('/','\')) $archive}
  if((Get-RmByteDigest $archive)-ne$d.documentByteSha256){throw 'XML_CHANGED_DURING_SCAN'}
 }
 $byAnchor=@{};foreach($r in $Registry.metadataEntries){$r.status='missing';if(!$byAnchor.ContainsKey($r.identityAnchorSha256)){$byAnchor[$r.identityAnchorSha256]=@()};$byAnchor[$r.identityAnchorSha256]+=@($r)}
 $entries=@($Metadata.entries)+@($Metadata.sources|ForEach-Object{$_.folderMetadata})
 $groups=@{}
 foreach($e in $entries){
  $d=$docs[$e.sourcePath];$locator=Get-RmNormalizedLocator $e.resolvedPath
  $anchor=[pscustomobject]@{documentId=$d.documentId;console=$e.console;entryKind=$e.entryKind;sourceLocatorNormalized=$locator}
  $sha=Get-RmDigest $anchor
  Set-RmProperty $e 'documentId' $d.documentId;Set-RmProperty $e 'sourceLocatorNormalized' $locator;Set-RmProperty $e 'identityAnchorSha256' $sha
  Set-RmProperty $e 'metadataDisplayName' (Get-RmProperty $e.raw 'name')
  if(!$groups.ContainsKey($sha)){$groups[$sha]=@()};$groups[$sha]+=@($e)
 }
 foreach($sha in $groups.Keys){
  $group=@($groups[$sha]);$known=@();if($byAnchor.ContainsKey($sha)){$known=@($byAnchor[$sha])}
  $unique=$group.Count -eq 1-and$known.Count -le 1-and$null -ne $group[0].sourceLocatorNormalized
  if(!$unique){foreach($e in $group){Set-RmProperty $e 'metadataEntryId' $null;Set-RmProperty $e 'identityStatus' 'ambiguous';Set-RmProperty $e 'versionId' $null;Set-RmProperty $e 'contentVersionId' $null;Set-RmProperty $e 'runtimeStatsVersionId' $null};foreach($r in $known){$r.status='ambiguous'};continue}
  $e=$group[0]
  if($known.Count -eq 0){$r=[pscustomobject]@{metadataEntryId=(New-RmId);documentId=$e.documentId;console=$e.console;entryKind=$e.entryKind;sourceLocatorNormalized=$e.sourceLocatorNormalized;identityAnchorSha256=$sha;status='present';lastSeenScanId=$Context.ScanId;currentVersionId=$null;versions=@()};$Registry.metadataEntries+=@($r)}else{$r=$known[0]}
  $full=Get-RmTextDigest ([Rm032.Canonical]::Xml($e.rawEntryXml,'full'));$content=Get-RmTextDigest ([Rm032.Canonical]::Xml($e.rawEntryXml,'content'));$stats=Get-RmTextDigest ([Rm032.Canonical]::Xml($e.rawEntryXml,'runtime'))
  if(@($r.versions|Where-Object{$_.versionId -eq $full}).Count -eq 0){$r.versions+=@([pscustomobject]@{versionId=$full;contentVersionId=$content;runtimeStatsVersionId=$stats;observedAtUtc=[DateTime]::UtcNow.ToString('o');scanId=$Context.ScanId;raw=$e.raw;rawEntryXml=$e.rawEntryXml;documentByteSha256=$docs[$e.sourcePath].documentByteSha256})}
  $r.currentVersionId=$full;$r.lastSeenScanId=$Context.ScanId;$r.status='present'
  Set-RmProperty $e 'metadataEntryId' $r.metadataEntryId;Set-RmProperty $e 'identityStatus' 'exact-unique';Set-RmProperty $e 'versionId' $full;Set-RmProperty $e 'contentVersionId' $content;Set-RmProperty $e 'runtimeStatsVersionId' $stats
 }
 # An unreadable document is never treated as proof that an entry disappeared.
 foreach($r in $Registry.metadataEntries){$d=@($Registry.documents|Where-Object{$_.documentId -eq $r.documentId})[0];if($d.status -eq 'error'){$r.status='unreadable'}}
 foreach($s in $Metadata.sources){if((Get-RmByteDigest ($Context.Root+'\'+$s.relativePath.Replace('/','\')))-ne$s.documentByteSha256){throw 'XML_CHANGED_DURING_SCAN'}}
}
function Get-RmPhysicalSnapshot {
 param($Context,$Inventory)
 $items=@($Inventory.files|ForEach-Object{[pscustomobject]@{relativePath=$_.relativePath;sizeBytes=$_.sizeBytes;modifiedAtUtc=$_.modifiedAtUtc}}|Sort-Object relativePath)
 $read=@($Inventory.files|Where-Object{$_.extension -in @('.cue','.m3u')-or$_.relativePath -match '^MEDIAS/[^/]+/metadata/es-de-gamelist\.xml$'}|Sort-Object relativePath|ForEach-Object{[pscustomobject]@{relativePath=$_.relativePath;sha256=(Get-RmByteDigest ($Context.Root+'\'+$_.relativePath.Replace('/','\')))}})
 return [pscustomobject]@{inventoryDigest=(Get-RmDigest ([pscustomobject]@{complete=$Inventory.complete;files=$items;directories=@($Inventory.directories|Sort-Object)}));smallSourceDigests=$read;complete=$Inventory.complete}
}
function Test-RmFixtureRoot {
 param([string]$Root)
 $boundary=Get-RmFullPath (Join-Path $PSScriptRoot '..\Tests\Fixtures')
 if(!(Test-RmContainedPath $boundary (Get-RmFullPath $Root))){return $false}
 Assert-RmOutputPath $Root $Root
 $marker=$Root+'\.rm-fixture.json';if(!(Test-RmFileExists $marker)){return $false}
 $m=Read-RmStrictJson $marker
 return $m.purpose -eq 'isolated-persistence-test' -and $m.root -eq (Get-RmFullPath $Root)
}
Export-ModuleMember -Function *-Rm*
