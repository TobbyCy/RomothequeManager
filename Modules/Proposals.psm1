Set-StrictMode -Version 2.0
function Get-RmSemanticDigest {param($Descriptor) return Get-RmDigest $Descriptor}
function New-RmDecisionProposals {
 param($Catalogue,$Registry,$Associations,$HashEvidence=@())
 $list=New-Object 'System.Collections.Generic.List[object]';$number=0
 $unitByKey=@{};foreach($g in $Catalogue.games){foreach($v in $g.variants){foreach($u in $v.romUnits){$unitByKey[$u.unitKey]=$u}}}
 foreach($r in @($Catalogue.metadataReconciliation.unresolved)+@($Catalogue.metadataReconciliation.folders)){
  $e=$r.sourceEntry;if(!$e.metadataEntryId -or $e.identityStatus -ne 'exact-unique'){continue}
  $candidates=@(Sort-RmOrdinal @($r.candidates|ForEach-Object{[pscustomobject]@{gameId=$_.gameId;variantId=$_.variantId;romUnitId=$unitByKey[$_.unitKey].romUnitId;kind=$_.kind;entryPath=(Get-RmNormalizedLocator $_.entryPath);matchingFiles=@(Sort-RmOrdinal @($_.matchingFiles|ForEach-Object{[pscustomobject]@{relativePath=(Get-RmNormalizedLocator $_.relativePath);role=$_.role}}) 'relativePath')}}) 'romUnitId')
  $target=$null;if($r.candidateUnit-and$r.classification -ne 'AMBIGUOUS'){$c=$r.candidateUnit;$target=[pscustomobject]@{scope='unit';gameId=$c.gameId;variantId=$c.variantId;romUnitId=$unitByKey[$c.unitKey].romUnitId}}
  $evidence=[pscustomobject]@{sourceLocatorNormalized=$e.sourceLocatorNormalized;romBasenamesNormalized=@(Sort-RmOrdinal @($candidates|ForEach-Object{$_.matchingFiles}|ForEach-Object{[IO.Path]::GetFileName($_.relativePath)}|Select-Object -Unique));relatedVariantUnitKeys=@(Sort-RmOrdinal $r.relatedVariantUnitKeys)}
  $hashes=@($HashEvidence|Where-Object{(Get-RmProperty $_ 'complete' $false)-eq$true -and (Get-RmProperty $_ 'metadataEntryId') -eq $e.metadataEntryId}|ForEach-Object{
   $diagnostic=$_
   if($diagnostic.algorithm -ne 'SHA256'){throw 'HASH_DIAGNOSTIC_ALGORITHM_INVALID'}
   foreach($fact in $diagnostic.files){
    if($fact.sha256 -cnotmatch '^[0-9a-f]{64}$'-or$fact.romUnitId -notin @($candidates|ForEach-Object{$_.romUnitId})){throw 'HASH_DIAGNOSTIC_EVIDENCE_INVALID'}
    $currentUnit=@($unitByKey.Values|Where-Object{$_.romUnitId -eq $fact.romUnitId})[0]
    $expected=Get-RmProperty $diagnostic.unitObservationDigests $fact.romUnitId
    if($currentUnit.observationDigest -ne $expected){throw 'HASH_DIAGNOSTIC_STALE_RESCAN_REQUIRED'}
   }
   [pscustomobject]@{algorithm=$_.algorithm;files=@($_.files|ForEach-Object{[pscustomobject]@{romUnitId=$_.romUnitId;relativePath=(Get-RmNormalizedLocator $_.relativePath);sha256=$_.sha256}}|Sort-Object romUnitId,relativePath);comparison=$_.comparison}
  }|Sort-Object @{Expression={Get-RmDigest $_}})
  $hashes=@(Sort-RmOrdinal @($hashes|Group-Object {Get-RmDigest $_}|ForEach-Object{[pscustomobject]@{key=$_.Name;proof=$_.Group[0]}}) 'key'|ForEach-Object{$_.proof})
  # Descriptive <name> is never read here. Profiles use only explicit structural fields.
  $descriptor=[pscustomobject]@{semanticModelVersion='1.0.0';libraryId=$Registry.libraryId;proposalFamily='metadata-reconciliation';sourceIdentity=[pscustomobject]@{documentId=$e.documentId;metadataEntryId=$e.metadataEntryId;entryKind=$e.entryKind;identityAnchorSha256=$e.identityAnchorSha256};classification=$r.classification;rule=$r.rule;ruleVersion='1.0.0';algorithmVersion='reconciliation-0.3.1';evidenceProfile='rm-structure-v1';proposedTarget=$target;candidateSet=$candidates;structuralEvidence=$evidence;targetedHashEvidence=$hashes}
  $semantic=Get-RmSemanticDigest $descriptor;$known=@($Registry.proposalIdentities|Where-Object{$_.proposalSemanticDigest -eq $semantic})
  if($known.Count -eq 0){$pi=[pscustomobject]@{proposalId=(New-RmId);proposalSemanticDigest=$semantic};$Registry.proposalIdentities+=@($pi)}else{$pi=$known[0]}
  $state='available';$prior=@($Associations.decisions|Where-Object{$_.type -in @('proposal.reject','proposal.defer') -and $_.state -in @('rejected','pending') -and $_.origin.proposalSemanticDigest -eq $semantic})
  if($prior.Count -gt 0){$last=$prior[$prior.Count-1];if($last.type -eq 'proposal.reject'){$state='rejected'}else{$state='pending'}}
  $number++;$p=[pscustomobject]@{number=$number;scanId=$Catalogue.scan.scanId;proposalId=$pi.proposalId;proposalFamily='metadata-reconciliation';proposalSemanticDigest=$semantic;proposalDigest=$null;descriptor=$descriptor;state=$state;sourceEntry=$e;classification=$r.classification;rule=$r.rule;confidenceInitial=$r.confidence;reason=$r.reason;target=$target;candidates=$candidates;candidateSetDigest=(Get-RmDigest $candidates);normalApprovalAllowed=($r.classification -ne 'AMBIGUOUS');humanChoiceRequired=($r.classification -eq 'AMBIGUOUS')}
  $p.proposalDigest=Get-RmDigest ([pscustomobject]@{scanId=$p.scanId;number=$number;proposalId=$p.proposalId;proposalSemanticDigest=$semantic;versionId=$e.versionId;state=$state})
  $list.Add($p)
 }
 return ,$list.ToArray()
}
function Get-RmCandidateHashes {
 [CmdletBinding()]param($Catalogue,[int]$ProposalNumber,[string[]]$RomUnitIds,[switch]$ExplicitRequest,[scriptblock]$Cancellation)
 if(!$ExplicitRequest){throw 'TARGETED_HASH_REQUIRES_EXPLICIT_REQUEST'}
 $p=@($Catalogue.decisionProposals|Where-Object{$_.number -eq $ProposalNumber});if($p.Count -ne 1-or$p[0].classification -ne 'AMBIGUOUS'){throw 'AMBIGUOUS_PROPOSAL_REQUIRED'};$p=$p[0]
 if($RomUnitIds.Count -eq 0){throw 'EXPLICIT_HASH_CANDIDATES_REQUIRED'}
 $allowed=@($p.candidates|ForEach-Object{$_.romUnitId});foreach($id in $RomUnitIds){if($id -notin $allowed){throw 'HASH_CANDIDATE_OUTSIDE_SET'}}
 $facts=New-Object 'System.Collections.Generic.List[object]';$filemap=@{};foreach($f in $Catalogue.files){$filemap[$f.fileId]=$f}
 foreach($g in $Catalogue.games){foreach($v in $g.variants){foreach($u in $v.romUnits){if($u.romUnitId -notin $RomUnitIds){continue};foreach($fileId in $u.fileIds){
  $f=$filemap[$fileId];$path=$Catalogue.scan.rootObserved+'\'+$f.relativePath.Replace('/','\');$before=Get-RmFileStat $path
  if($before.Length -ne $f.sizeBytes-or$before.LastWriteTimeUtc.ToString('o')-ne$f.modifiedAtUtc){throw 'HASH_SOURCE_CHANGED_RESCAN_REQUIRED'}
  $stream=Open-RmFile $path ([IO.FileMode]::Open) ([IO.FileAccess]::Read) ([IO.FileShare]::Read);$sha=[Security.Cryptography.SHA256]::Create();$buf=New-Object byte[] (1024*1024)
  try {while(($n=$stream.Read($buf,0,$buf.Length))-gt 0){if($Cancellation-and(& $Cancellation)){throw 'HASH_CANCELLED'};$null=$sha.TransformBlock($buf,0,$n,$buf,0);Write-Progress -Activity 'SHA256 targeted candidates' -Status $f.relativePath -PercentComplete $(if($stream.Length){100*$stream.Position/$stream.Length}else{100})};$null=$sha.TransformFinalBlock($buf,0,0);$hash=([BitConverter]::ToString($sha.Hash)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$stream.Dispose();Write-Progress -Activity 'SHA256 targeted candidates' -Completed}
  $after=Get-RmFileStat $path;if($after.Length -ne $before.Length-or$after.LastWriteTimeUtc -ne $before.LastWriteTimeUtc){throw 'HASH_SOURCE_CHANGED_RESCAN_REQUIRED'}
  $facts.Add([pscustomobject]@{romUnitId=$u.romUnitId;relativePath=$f.relativePath;sha256=$hash})
 }}}}
 $all=@($facts.ToArray()|Sort-Object romUnitId,relativePath);$equal='not-comparable';if($all.Count -eq 2-and$RomUnitIds.Count -eq 2){if($all[0].sha256 -eq $all[1].sha256){$equal='equal-file-bytes'}else{$equal='different-file-bytes'}}
 $observations=[ordered]@{};foreach($g in $Catalogue.games){foreach($v in $g.variants){foreach($u in $v.romUnits){if($u.romUnitId -in $RomUnitIds){$observations[$u.romUnitId]=$u.observationDigest}}}}
 return [pscustomobject]@{metadataEntryId=$p.sourceEntry.metadataEntryId;algorithm='SHA256';complete=$true;files=$all;comparison=$equal;unitObservationDigests=[pscustomobject]$observations;observedAtUtc=[DateTime]::UtcNow.ToString('o');reportId=(New-RmId);automaticChoice=$false}
}
Export-ModuleMember -Function *-Rm*
