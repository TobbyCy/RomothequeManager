Set-StrictMode -Version 2.0
function Get-RmTargetObservation {
 param($Catalogue,$Target)
 Assert-RmTargetShape $Target
 $g=@($Catalogue.games|Where-Object{$_.gameId -eq $Target.gameId});if($g.Count -ne 1){throw 'TARGET_ABSENT'};$g=$g[0]
 if($Target.scope -eq 'game'){return [pscustomobject]@{game=$g;variant=$null;unit=$null}}
 $v=@($g.variants|Where-Object{$_.variantId -eq $Target.variantId});if($v.Count -ne 1){throw 'TARGET_VARIANT_ABSENT_OR_WRONG_OWNER'};$v=$v[0]
 if($Target.scope -eq 'variant'){return [pscustomobject]@{game=$g;variant=$v;unit=$null}}
 $u=@($v.romUnits|Where-Object{$_.romUnitId -eq $Target.romUnitId});if($u.Count -ne 1){throw 'TARGET_UNIT_ABSENT_OR_WRONG_OWNER'}
 return [pscustomobject]@{game=$g;variant=$v;unit=$u[0]}
}
function Get-RmDecisionEffect {
 param($Decision,$Catalogue,$Registry,$Associations)
 $d=$Decision;$reasons=New-Object 'System.Collections.Generic.List[string]';$audit=New-Object 'System.Collections.Generic.List[string]'
 $status='effective';if($d.state -in @('revoked','superseded')){$status=$d.state}
 if($d.source-and$d.type -in @('proposal.reject','proposal.defer')){
  $observed=@($Catalogue.metadataIndex|Where-Object{$_.metadataEntryId -eq $d.source.metadataEntryId-and$_.identityStatus -eq 'exact-unique'})
  if($observed.Count -eq 1){if($observed[0].contentVersionId -ne $d.source.contentVersionId){$audit.Add('SOURCE_CONTENT_CHANGED')};if($observed[0].runtimeStatsVersionId -ne $d.source.runtimeStatsVersionId){$audit.Add('SOURCE_RUNTIME_STATS_CHANGED')}}
 }
 if($status -eq 'effective'){
  if($d.type -in @('proposal.reject','proposal.defer')){
   $p=@($Catalogue.decisionProposals|Where-Object{$_.proposalSemanticDigest -eq $d.origin.proposalSemanticDigest})
   if($p.Count -eq 0){$status='historical';$reasons.Add('SEMANTIC_PROPOSAL_NOT_CURRENT')}else{$status=$d.state}
  } elseif($d.type -ne 'metadata.keep_historical'){
   if($d.source -and $d.validityPolicy.sourceMode -eq 'identity'){
    $s=@($Catalogue.metadataIndex|Where-Object{$_.metadataEntryId -eq $d.source.metadataEntryId-and$_.identityStatus -eq 'exact-unique'})
    if($s.Count -ne 1){$status='suspended';$reasons.Add('SOURCE_IDENTITY_UNRESOLVED_OR_MISSING')}else{
     $s=$s[0];if($s.identityAnchorSha256 -ne $d.source.identityAnchorSha256){$status='suspended';$reasons.Add('SOURCE_IDENTITY_CHANGED')}
     if($s.contentVersionId -ne $d.source.contentVersionId){$audit.Add('SOURCE_CONTENT_CHANGED')}
     if($s.runtimeStatsVersionId -ne $d.source.runtimeStatsVersionId){$audit.Add('SOURCE_RUNTIME_STATS_CHANGED')}
    }
   }
   if($d.target){try{$obs=Get-RmTargetObservation $Catalogue $d.target;if($d.target.scope -eq 'unit' -and $d.validityPolicy.targetMode -eq 'unit_observation'){$expected=Get-RmProperty $d.payload.unitObservationDigests $d.target.romUnitId;if($obs.unit.observationDigest -ne $expected){$status='suspended';$reasons.Add('ROM_UNIT_OBSERVATION_CHANGED')}}}catch{$status='suspended';$reasons.Add($_.Exception.Message)}}
   if($d.type -eq 'variant.relate'){try{$null=Get-RmTargetObservation $Catalogue $d.payload.otherTarget}catch{$status='suspended';$reasons.Add('RELATED_TARGET_ABSENT')}}
   if($d.validityPolicy.candidateSetMode -eq 'exact'){
    $p=@($Catalogue.decisionProposals|Where-Object{$_.sourceEntry.metadataEntryId -eq $d.source.metadataEntryId})
    if($p.Count -ne 1-or$p[0].candidateSetDigest -ne $d.payload.candidateSetDigest){$status='suspended';$reasons.Add('CANDIDATE_SET_CHANGED')}
   }
   foreach($dep in $d.validityPolicy.explicitDependencies){
    if($dep.type -eq 'source_content'){$s=@($Catalogue.metadataIndex|Where-Object{$_.metadataEntryId -eq $dep.metadataEntryId});if($s.Count -ne 1-or$s[0].contentVersionId -ne $dep.digest){$status='suspended';$reasons.Add('EXPLICIT_SOURCE_DEPENDENCY_CHANGED')}}
    elseif($dep.type -eq 'decision'){$parent=@($Associations.decisions|Where-Object{$_.decisionId -eq $dep.decisionId-and$_.state -eq 'active'});if($parent.Count -ne 1){$status='suspended';$reasons.Add('EXPLICIT_DECISION_DEPENDENCY_INACTIVE')}}
    else{$status='suspended';$reasons.Add('UNKNOWN_EXPLICIT_DEPENDENCY')}
   }
  }
 }
 return [pscustomobject]@{decisionId=$d.decisionId;type=$d.type;effectiveStatus=$status;reasons=$reasons.ToArray();auditEvents=$audit.ToArray()}
}
function Set-RmDecisionProjection {
 param($Catalogue,$Registry,$Associations)
 $effects=@($Associations.decisions|ForEach-Object{Get-RmDecisionEffect $_ $Catalogue $Registry $Associations})
 $effectmap=@{};foreach($e in $effects){$effectmap[$e.decisionId]=$e}
 # Propagate suspension through explicit dependencies, even if the parent's
 # stored human state is still active. A revoked decision remains in history.
 $changed=$true
 while($changed){$changed=$false;foreach($d in $Associations.decisions){if($effectmap[$d.decisionId].effectiveStatus -ne 'effective'){continue};foreach($dep in $d.validityPolicy.explicitDependencies){if($dep.type -eq 'decision'-and(!$effectmap.ContainsKey($dep.decisionId)-or$effectmap[$dep.decisionId].effectiveStatus -ne 'effective')){$effectmap[$d.decisionId].effectiveStatus='suspended';$effectmap[$d.decisionId].reasons+=@('EXPLICIT_DECISION_DEPENDENCY_SUSPENDED');$changed=$true;break}}}}
 $active=@($Associations.decisions|Where-Object{$effectmap[$_.decisionId].effectiveStatus -eq 'effective'})
 $entrymap=@{};foreach($e in $Catalogue.metadataIndex){if($e.metadataEntryId-and$e.identityStatus -eq 'exact-unique'){$entrymap[$e.metadataEntryId]=$e}}
 $manualIds=@{}
 foreach($g in $Catalogue.games){
  $links=New-Object 'System.Collections.Generic.List[object]';$ids=@{}
  foreach($v in $g.variants){foreach($e in $v.metadata){if($e.metadataEntryId -and !$ids.ContainsKey($e.metadataEntryId)){$links.Add([pscustomobject]@{metadataEntryId=$e.metadataEntryId;scope='variant';variantId=$v.variantId;romUnitId=$null;origin='exact_current_path';source=$e});$ids[$e.metadataEntryId]=$true}}}
  foreach($d in $active|Where-Object{$_.type -like 'metadata.link_*'-and$_.target.gameId -eq $g.gameId}){
   $e=$entrymap[$d.source.metadataEntryId];if($e-and!$ids.ContainsKey($e.metadataEntryId)){$links.Add([pscustomobject]@{metadataEntryId=$e.metadataEntryId;scope=$d.target.scope;variantId=(Get-RmProperty $d.target 'variantId');romUnitId=(Get-RmProperty $d.target 'romUnitId');origin=$d.decisionId;source=$e});$ids[$e.metadataEntryId]=$true;$manualIds[$e.metadataEntryId]=$true}
  }
  Set-RmProperty $g 'metadataSources' $links.ToArray()
  foreach($d in $active|Where-Object{$_.type -eq 'metadata.prefer_source'-and$_.target.gameId -eq $g.gameId}){
   $linked=@($links.ToArray()|Where-Object{if($_.metadataEntryId -ne $d.source.metadataEntryId){return $false};if($d.target.scope -eq 'game'-or$_.scope -eq 'game'){return $true};if($_.variantId -ne $d.target.variantId){return $false};if($d.target.scope -eq 'variant'-or$_.scope -eq 'variant'){return $true};return $_.romUnitId -eq $d.target.romUnitId})
   if(!$linked.Count){$effectmap[$d.decisionId].effectiveStatus='suspended';$effectmap[$d.decisionId].reasons+=@('PREFERRED_SOURCE_UNLINKED')}
  }
  $targets=@([pscustomobject]@{scope='game';gameId=$g.gameId})
  foreach($v in $g.variants){
   $targets+=@([pscustomobject]@{scope='variant';gameId=$g.gameId;variantId=$v.variantId})
   foreach($u in $v.romUnits){$targets+=@([pscustomobject]@{scope='unit';gameId=$g.gameId;variantId=$v.variantId;romUnitId=$u.romUnitId})}
  }
  foreach($target in $targets){
   $o=Get-RmTargetObservation $Catalogue $target;$container=$o.game;if($target.scope -eq 'variant'){$container=$o.variant}elseif($target.scope -eq 'unit'){$container=$o.unit}
   $view=[ordered]@{};$conflicts=@();$provenance=[ordered]@{}
   $relevant=@($links.ToArray()|Where-Object{if($target.scope -eq 'game'){$true}elseif($_.scope -eq 'game'){$true}elseif($target.scope -eq 'variant'){$_.variantId -eq $target.variantId}else{$_.scope -eq 'variant'-and$_.variantId -eq $target.variantId-or$_.scope -eq 'unit'-and$_.romUnitId -eq $target.romUnitId}})
   $policies=@($active|Where-Object{
    if($_.type -notin @('metadata.prefer_source','metadata.override_display')-or$effectmap[$_.decisionId].effectiveStatus -ne 'effective'){return $false}
    if($_.target.gameId -ne $target.gameId){return $false}
    switch($_.target.scope){game{return $true}variant{return (Get-RmProperty $target 'variantId') -eq $_.target.variantId}unit{return (Get-RmProperty $target 'romUnitId') -eq $_.target.romUnitId}}
   }|Sort-Object @{Expression={switch($_.target.scope){game{0}variant{1}unit{2}}}},createdAtUtc)
   foreach($field in @('name','desc','rating','releasedate','developer','publisher','genre','players','playcount','playtime','lastplayed')){
    $chosen=$null;$found=$false;$from=$null
    $overrides=@($policies|Where-Object{$_.type -eq 'metadata.override_display'-and$_.payload.fields -contains $field})
    if($overrides.Count){$d=$overrides[$overrides.Count-1];$chosen=$d.payload.value;$found=$true;$from=$d.decisionId}
    else{
     $pref=@($policies|Where-Object{$_.type -eq 'metadata.prefer_source'-and($_.payload.fields -contains $field-or$_.payload.fields -contains '*')}|Sort-Object @{Expression={switch($_.target.scope){game{0}variant{1}unit{2}}}},@{Expression={if($_.payload.fields -contains $field){1}else{0}}},createdAtUtc)
     if($pref.Count){$d=$pref[$pref.Count-1];$s=@($relevant|Where-Object{$_.metadataEntryId -eq $d.source.metadataEntryId});if($s.Count -eq 1){$prop=$s[0].source.raw.PSObject.Properties[$field];if($prop){$chosen=$prop.Value;$found=$true;$from=$d.decisionId}}}
     if(!$found-and($pref.Count -eq 0-or(Get-RmProperty $pref[$pref.Count-1].payload 'allowMissingFallback' $false))){$vals=@($relevant|Where-Object{$_.source.raw.PSObject.Properties[$field]});$distinct=@($vals|ForEach-Object{Get-RmDigest $_.source.raw.PSObject.Properties[$field].Value}|Select-Object -Unique);if($distinct.Count -eq 1){$chosen=$vals[0].source.raw.PSObject.Properties[$field].Value;$found=$true;$from='source-consensus'}elseif($distinct.Count -gt 1){$conflicts+=@($field)}}
    }
    if($found){
     $view[$field]=$chosen
     $trace=[pscustomobject]@{kind=$from;decisionId=$null;metadataEntryId=$null;currentVersionId=$null;versionIdAtApproval=$null;sources=@()}
     if($from -eq 'source-consensus'){$trace.sources=@($relevant|Where-Object{$_.source.raw.PSObject.Properties[$field]}|ForEach-Object{[pscustomobject]@{metadataEntryId=$_.metadataEntryId;currentVersionId=$_.source.versionId}})}
     else{$policy=@($policies|Where-Object{$_.decisionId -eq $from})[0];$trace.kind=$policy.type;$trace.decisionId=$policy.decisionId;if($policy.source){$trace.metadataEntryId=$policy.source.metadataEntryId;$trace.versionIdAtApproval=$policy.source.versionId;$trace.currentVersionId=$entrymap[$policy.source.metadataEntryId].versionId}}
     $provenance[$field]=$trace
    }
   }
   Set-RmProperty $container 'metadataDisplay' ([pscustomobject]@{values=[pscustomobject]$view;provenance=[pscustomobject]$provenance;conflicts=$conflicts;ratingOutOf5=$(if($view.Contains('rating')){try{[double]::Parse([string]$view.rating,[Globalization.CultureInfo]::InvariantCulture)*5}catch{$null}}else{$null})})
  }
 }
 Set-RmProperty $Catalogue 'decisionEffects' $effects
 Set-RmProperty $Catalogue 'manualMetadataResolvedIds' @($manualIds.Keys|Sort-Object)
 Set-RmProperty $Catalogue 'historicalMetadata' @($active|Where-Object{$_.type -eq 'metadata.keep_historical'}|ForEach-Object{[pscustomobject]@{decisionId=$_.decisionId;source=$_.source;snapshot=$_.payload.historicalSnapshot;selectable=$false}})
 Set-RmProperty $Catalogue 'variantRelations' @($active|Where-Object{$_.type -eq 'variant.relate'}|ForEach-Object{[pscustomobject]@{decisionId=$_.decisionId;target=$_.target;otherTarget=$_.payload.otherTarget;relation=$_.payload.relation;causesMerge=$false}})
}
Export-ModuleMember -Function *-Rm*
