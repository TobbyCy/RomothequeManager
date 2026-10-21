Set-StrictMode -Version 2.0
function ConvertTo-RmRegistry2 {
 param($Registry)
 if($Registry.schemaVersion -eq '1.0.0'){
  $Registry.schemaVersion='2.0.0'
  foreach($p in @('documents','metadataEntries','romUnits','proposalIdentities')){Set-RmProperty $Registry $p @()}
  Set-RmProperty $Registry 'libraryId' (New-RmId)
 } elseif($Registry.schemaVersion -ne '2.0.0'){throw 'UNSUPPORTED_REGISTRY_SCHEMA'}
 Assert-RmRegistry2 $Registry
 return $Registry
}
function ConvertTo-RmAssociations3 {
 param($Associations,[string]$LibraryId)
 if($Associations.schemaVersion -in @('1.0.0','2.0.0')){
  if(!$Associations.PSObject.Properties['gameMediaAssignments']){Set-RmProperty $Associations 'gameMediaAssignments' @()}
  $Associations.schemaVersion='3.0.0'
  Set-RmProperty $Associations 'libraryId' $LibraryId
  Set-RmProperty $Associations 'decisions' @()
  Set-RmProperty $Associations 'transactions' @()
  Set-RmProperty $Associations 'applicationVersion' '0.3.2'
  Set-RmProperty $Associations 'createdAtUtc' ([DateTime]::UtcNow.ToString('o'))
  Set-RmProperty $Associations 'updatedAtUtc' $Associations.createdAtUtc
  Set-RmProperty $Associations 'lastTransactionId' $null
 } elseif($Associations.schemaVersion -ne '3.0.0'){throw 'UNSUPPORTED_ASSOCIATIONS_SCHEMA'}
 if($Associations.libraryId -ne $LibraryId){throw 'LIBRARY_ID_MISMATCH'}
 Assert-RmAssociations3 $Associations
 return $Associations
}
function Assert-RmAssociations3 {
 param($State)
 Assert-RmFields $State @('schemaVersion','applicationVersion','revision','libraryId','createdAtUtc','updatedAtUtc','lastTransactionId','gameMerges','variantMerges','mediaAssignments','gameMediaAssignments','history','decisions','transactions') @('schemaVersion','revision','libraryId','decisions','transactions')
 if($State.schemaVersion -ne '3.0.0'){throw 'ASSOCIATIONS_SCHEMA_REQUIRED'};Assert-RmUuid $State.libraryId
 $ids=@{};$requests=@{}
 foreach($d in $State.decisions){
  Assert-RmFields $d @('decisionId','createdAtUtc','createdBy','type','state','origin','source','target','payload','acceptedPreconditions','validityPolicy','note','supersedes','lifecycle') @('decisionId','createdAtUtc','type','state','origin','source','target','payload','validityPolicy','lifecycle')
  Assert-RmUuid $d.decisionId;if($ids.ContainsKey($d.decisionId)){throw 'DUPLICATE_DECISION_ID'};$ids[$d.decisionId]=$true
  if($d.type -notin @('metadata.link_game','metadata.link_variant','metadata.link_unit','variant.relate','metadata.prefer_source','metadata.override_display','proposal.reject','proposal.defer','metadata.keep_historical')){throw 'UNKNOWN_DECISION_TYPE'}
  if($d.state -notin @('active','pending','rejected','revoked','superseded')){throw 'INVALID_DECISION_STATE'}
  if($d.type -eq 'proposal.reject'-and$d.state -in @('active','pending')){throw 'REJECT_STATE_INVALID'}
  if($d.type -eq 'proposal.defer'-and$d.state -in @('active','rejected')){throw 'DEFER_STATE_INVALID'}
  Assert-RmFields $d.origin @('scanId','proposalId','number','proposalDigest','proposalSemanticDigest','semanticModelVersion','evidenceProfile','classification','rule','ruleVersion','algorithmVersion','confidenceInitial') @('scanId','proposalSemanticDigest')
  Assert-RmFields $d.validityPolicy @('sourceMode','contentMode','targetMode','candidateSetMode','proposalMode','explicitDependencies') @('sourceMode','contentMode','targetMode','candidateSetMode','proposalMode','explicitDependencies')
  if($d.source){Assert-RmFields $d.source @('documentId','metadataEntryId','versionId','contentVersionId','runtimeStatsVersionId','entryKind','relativeXmlPath','acceptedLocator','identityAnchorSha256','acceptedContentSha256','acceptedDocumentByteSha256','canonicalizationVersion','snapshotEntryKey') @('metadataEntryId','documentId');Assert-RmUuid $d.source.metadataEntryId}
  if($d.target){Assert-RmTargetShape $d.target}
  Assert-RmFields $d.payload @('fields','value','relation','otherTarget','mode','justification','binaryIdentityNotProvenAcknowledged','candidateSet','candidateSetDigest','unitObservationDigests','historicalSnapshot','preferenceScope','allowMissingFallback','reason')
  Assert-RmFields $d.acceptedPreconditions @('previewId','previewDigest','scanId','sourceIdentityResolutionDigest','stateGuards') @('previewId','scanId')
  if($d.type -like 'metadata.link_*'){if(!$d.source-or!$d.target){throw 'LINK_SOURCE_TARGET_REQUIRED'};$scope=$d.type.Substring(14);if($d.target.scope -ne $scope){throw 'LINK_SCOPE_SCHEMA_MISMATCH'}}
  if($d.type -eq 'metadata.override_display'){if(!$d.target-or@($d.payload.fields).Count -ne 1){throw 'OVERRIDE_SCHEMA_INVALID'};Assert-RmMetadataValue $d.payload.fields[0] $d.payload.value}
  if($d.type -eq 'metadata.keep_historical'-and(!$d.source-or$d.target-or!$d.payload.PSObject.Properties['historicalSnapshot'])){throw 'HISTORICAL_SCHEMA_INVALID'}
 }
 foreach($t in $State.transactions){Assert-RmFields $t @('transactionId','requestId','requestDigest','previewId','previewDigest','committedAtUtc','revisionBefore','identityRevisionBefore','catalogueScanBefore','revisionAfter','stateBeforeSha256','decisionIds','operations','changes') @('transactionId','requestId','requestDigest','revisionAfter');Assert-RmUuid $t.transactionId;if($requests.ContainsKey($t.requestId)){throw 'DUPLICATE_REQUEST_ID'};$requests[$t.requestId]=$true}
 Assert-RmDecisionDependencyGraph $State.decisions
}
function Assert-RmDecisionDependencyGraph {
 param($Decisions)
 $map=@{};foreach($d in $Decisions){$map[$d.decisionId]=$d};$visited=@{};$visiting=@{}
 function Visit-RmDependency([string]$Id){
  if($visiting.ContainsKey($Id)){throw 'DECISION_DEPENDENCY_CYCLE'}
  if($visited.ContainsKey($Id)){return};$visiting[$Id]=$true
  foreach($dep in $map[$Id].validityPolicy.explicitDependencies){if($dep.type -ne 'decision'){continue};Assert-RmUuid $dep.decisionId;if(!$map.ContainsKey($dep.decisionId)){throw 'DEPENDENCY_DECISION_NOT_FOUND'};Visit-RmDependency $dep.decisionId}
  $visiting.Remove($Id);$visited[$Id]=$true
 }
 foreach($id in $map.Keys){Visit-RmDependency $id}
}
function Assert-RmRegistry2 {
 param($Registry)
 Assert-RmFields $Registry @('schemaVersion','revision','games','variants','libraryId','documents','metadataEntries','romUnits','proposalIdentities') @('schemaVersion','revision','games','variants','libraryId','documents','metadataEntries','romUnits','proposalIdentities')
 if($Registry.schemaVersion -ne '2.0.0'){throw 'REGISTRY_SCHEMA_REQUIRED'};Assert-RmUuid $Registry.libraryId
 $ids=@{};$gids=@{};$vids=@{};$dids=@{};$anchors=@{};$keys=@{}
 foreach($g in $Registry.games){Assert-RmUuid $g.gameId;if($gids.ContainsKey($g.gameId)){throw 'DUPLICATE_GAME_ID'};$gids[$g.gameId]=$true}
 foreach($v in $Registry.variants){Assert-RmUuid $v.variantId;if($vids.ContainsKey($v.variantId)-or!$gids.ContainsKey($v.gameId)){throw 'INVALID_VARIANT_IDENTITY'};$vids[$v.variantId]=$true}
 foreach($d in $Registry.documents){Assert-RmFields $d @('documentId','relativePath','status','documentByteSha256') @('documentId','relativePath','status');Assert-RmUuid $d.documentId;if($dids.ContainsKey($d.documentId)){throw 'DUPLICATE_DOCUMENT_ID'};$dids[$d.documentId]=$true}
 foreach($u in $Registry.romUnits){Assert-RmFields $u @('romUnitId','gameId','variantId','technicalKey','lastSeenScanId','observationDigest') @('romUnitId','gameId','variantId','technicalKey');Assert-RmUuid $u.romUnitId;if($ids.ContainsKey($u.romUnitId)-or$keys.ContainsKey($u.technicalKey)-or!$gids.ContainsKey($u.gameId)-or!$vids.ContainsKey($u.variantId)){throw 'INVALID_ROM_UNIT_IDENTITY'};$ids[$u.romUnitId]=$true;$keys[$u.technicalKey]=$true}
 foreach($e in $Registry.metadataEntries){Assert-RmFields $e @('metadataEntryId','documentId','console','entryKind','sourceLocatorNormalized','identityAnchorSha256','status','lastSeenScanId','currentVersionId','versions') @('metadataEntryId','documentId','identityAnchorSha256','versions');Assert-RmUuid $e.metadataEntryId;if($ids.ContainsKey($e.metadataEntryId)-or$anchors.ContainsKey($e.identityAnchorSha256)-or!$dids.ContainsKey($e.documentId)){throw 'INVALID_METADATA_IDENTITY'};$ids[$e.metadataEntryId]=$true;$anchors[$e.identityAnchorSha256]=$true;$versions=@{};foreach($v in $e.versions){Assert-RmFields $v @('versionId','contentVersionId','runtimeStatsVersionId','observedAtUtc','scanId','raw','rawEntryXml','documentByteSha256') @('versionId','contentVersionId','runtimeStatsVersionId','rawEntryXml');if($versions.ContainsKey($v.versionId)){throw 'DUPLICATE_METADATA_VERSION'};$versions[$v.versionId]=$true}}
 $semantic=@{};foreach($p in $Registry.proposalIdentities){Assert-RmFields $p @('proposalId','proposalSemanticDigest') @('proposalId','proposalSemanticDigest');Assert-RmUuid $p.proposalId;if($semantic.ContainsKey($p.proposalSemanticDigest)){throw 'DUPLICATE_SEMANTIC_PROPOSAL'};$semantic[$p.proposalSemanticDigest]=$true}
}
function Assert-RmTargetShape {
 param($Target)
 Assert-RmFields $Target @('scope','gameId','variantId','romUnitId') @('scope','gameId')
 Assert-RmUuid $Target.gameId
 if($Target.scope -notin @('game','variant','unit')){throw 'INVALID_TARGET_SCOPE'}
 if($Target.scope -in @('variant','unit')){Assert-RmUuid (Get-RmProperty $Target 'variantId')}
 if($Target.scope -eq 'unit'){Assert-RmUuid (Get-RmProperty $Target 'romUnitId')}
 if($Target.scope -eq 'game' -and ((Get-RmProperty $Target 'variantId')-or(Get-RmProperty $Target 'romUnitId'))){throw 'TARGET_SCOPE_MISMATCH'}
 if($Target.scope -eq 'variant' -and (Get-RmProperty $Target 'romUnitId')){throw 'TARGET_SCOPE_MISMATCH'}
}
function Assert-RmMetadataValue {
 param([string]$Field,$Value)
 if($Field -in @('path','playcount','playtime','lastplayed') -or $Field -notin @('name','desc','rating','releasedate','developer','publisher','genre','players')){throw 'DISPLAY_FIELD_NOT_SUPPORTED'}
 if($Value -isnot [string]){throw 'DISPLAY_VALUE_MUST_BE_STRING'}
 if($Field -eq 'rating'){$r=0.0;if(![double]::TryParse($Value,[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$r)-or[double]::IsNaN($r)-or[double]::IsInfinity($r)-or$r -lt 0-or$r -gt 1){throw 'RATING_RANGE_INVALID'}}
 if($Field -eq 'releasedate' -and $Value -ne ''){$date=[DateTime]::MinValue;if(![DateTime]::TryParseExact($Value,'yyyyMMdd\THHmmss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::None,[ref]$date)){throw 'RELEASEDATE_INVALID'}}
}
Export-ModuleMember -Function *-Rm*
