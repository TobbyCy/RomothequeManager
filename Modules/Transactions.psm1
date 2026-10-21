Set-StrictMode -Version 2.0
function Get-RmStateGuards {
 param([string]$OutputRoot)
 return [pscustomobject]@{catalogueSha256=(Get-RmByteDigest ($OutputRoot+'\catalogue.json'));registrySha256=(Get-RmByteDigest ($OutputRoot+'\identity_registry.json'));associationsSha256=(Get-RmByteDigest ($OutputRoot+'\associations.json'))}
}
function Assert-RmFreshScan {
 param($Catalogue,[string]$OutputRoot,$ExpectedGuards=$null)
 $current=Read-RmStrictJson ($OutputRoot+'\catalogue.json')
 if($current.scan.scanId -ne $Catalogue.scan.scanId){throw 'STALE_SCAN_RESCAN_REQUIRED'}
 if($ExpectedGuards-and(Get-RmDigest (Get-RmStateGuards $OutputRoot))-ne(Get-RmDigest $ExpectedGuards)){throw 'STALE_STATE_OR_XML_RESCAN_REQUIRED'}
 $context=[pscustomobject]@{Root=$Catalogue.scan.rootObserved;OutputRoot=$OutputRoot;Config=(Get-RmConfiguration);ScanId=$Catalogue.scan.scanId;Anomalies=(New-Object 'System.Collections.Generic.List[object]')}
 $inventory=Get-RmInventory $context;$now=Get-RmPhysicalSnapshot $context $inventory
 if(!$now.complete-or(Get-RmDigest $now)-ne(Get-RmDigest $Catalogue.physicalSnapshot)){throw 'STALE_SOURCE_RESCAN_REQUIRED'}
 if($Catalogue.scan.status -ne 'complete'){throw 'INCOMPLETE_SCAN_COMMIT_BLOCKED'}
}
function New-RmSourceReference {
 param($Entry,$Catalogue)
 $s=@($Catalogue.sources|Where-Object{$_.documentId -eq $Entry.documentId})[0]
 return [pscustomobject]@{documentId=$Entry.documentId;metadataEntryId=$Entry.metadataEntryId;versionId=$Entry.versionId;contentVersionId=$Entry.contentVersionId;runtimeStatsVersionId=$Entry.runtimeStatsVersionId;entryKind=$Entry.entryKind;relativeXmlPath=$Entry.sourcePath;acceptedLocator=$Entry.sourceLocatorNormalized;identityAnchorSha256=$Entry.identityAnchorSha256;acceptedContentSha256=$Entry.contentVersionId;acceptedDocumentByteSha256=$s.documentByteSha256;canonicalizationVersion='rm-xml-entry-v1';snapshotEntryKey=($Entry.sourcePath+'#'+$Entry.entryIndex)}
}
function New-RmPlannedDecision {
 param($Operation,$Catalogue,$Registry,[string]$PreviewId)
 Assert-RmFields $Operation @('type','proposalNumber','metadataEntryId','target','payload','note','decisionId','supersedes') @('type')
 $type=$Operation.type;$p=$null;$entry=$null;$source=$null;$target=Get-RmProperty $Operation 'target';$payload=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject (Get-RmProperty $Operation 'payload' ([pscustomobject]@{})) -Depth 60);$note=Get-RmProperty $Operation 'note' ''
 if($type -eq 'decision.revoke'){Assert-RmUuid $Operation.decisionId;return [pscustomobject]@{operation='revoke';decisionId=$Operation.decisionId;note=$note}}
 $allowed=@('metadata.link_game','metadata.link_variant','metadata.link_unit','variant.relate','metadata.prefer_source','metadata.override_display','proposal.reject','proposal.defer','metadata.keep_historical')
 if($type -notin $allowed){throw 'UNKNOWN_DECISION_TYPE'}
 $allowedPayload=@()
 switch -Wildcard ($type){
  'metadata.link_*' {$allowedPayload=@('mode','justification','binaryIdentityNotProvenAcknowledged','explicitDependencies')}
  'metadata.prefer_source' {$allowedPayload=@('fields','preferenceScope','allowMissingFallback','explicitDependencies')}
  'metadata.override_display' {$allowedPayload=@('fields','value','explicitDependencies')}
  'variant.relate' {$allowedPayload=@('relation','otherTarget','explicitDependencies')}
  'proposal.*' {$allowedPayload=@('reason')}
  'metadata.keep_historical' {$allowedPayload=@('reason')}
 }
 Assert-RmFields $payload $allowedPayload
 if($payload.PSObject.Properties['mode']-and$payload.mode -notin @('standard','human_ambiguous')){throw 'UNKNOWN_APPROVAL_MODE'}
 if($payload.PSObject.Properties['allowMissingFallback']-and$payload.allowMissingFallback -isnot [bool]){throw 'FALLBACK_MUST_BE_BOOLEAN'}
 $pn=Get-RmProperty $Operation 'proposalNumber'
 if($null -ne $pn){$p=@($Catalogue.decisionProposals|Where-Object{$_.number -eq $pn});if($p.Count -ne 1){throw 'PROPOSAL_NOT_FOUND_IN_SCAN'};$p=$p[0];$entry=$p.sourceEntry}
 $sourceId=Get-RmProperty $Operation 'metadataEntryId'
 if($sourceId){$matches=@($Catalogue.metadataIndex|Where-Object{$_.metadataEntryId -eq $sourceId-and$_.identityStatus -eq 'exact-unique'});if($matches.Count -ne 1){throw 'SOURCE_NOT_EXACT_UNIQUE'};$entry=$matches[0];if($p-and$p.sourceEntry.metadataEntryId -ne $sourceId){throw 'SOURCE_PROPOSAL_MISMATCH'}}
 if($entry){$source=New-RmSourceReference $entry $Catalogue}
 if($type -ne 'metadata.override_display'-and$type -ne 'variant.relate'-and!$source){throw 'SOURCE_REQUIRED'}
 if($type -in @('proposal.reject','proposal.defer','metadata.keep_historical')){$target=$null}
 elseif(!$target-and$p){$target=$p.target}
 $unitObs=[ordered]@{}
 if($target){$obs=Get-RmTargetObservation $Catalogue $target;if($target.scope -eq 'unit'){if(!$obs.unit.complete){throw 'INCOMPLETE_ROM_UNIT'};$unitObs[$target.romUnitId]=$obs.unit.observationDigest}}
 if($type -like 'metadata.link_*'){
  if(!$p){throw 'RECONCILIATION_PROPOSAL_REQUIRED'}
  $scope=$type.Substring('metadata.link_'.Length);if($scope -eq 'unit'){$scope='unit'}
  if(!$target-or$target.scope -ne $scope){throw 'LINK_TARGET_SCOPE_MISMATCH'}
  $candidate=@($p.candidates|Where-Object{$_.gameId -eq $target.gameId-and($scope -eq 'game'-or$_.variantId -eq $target.variantId)-and($scope -ne 'unit'-or$_.romUnitId -eq $target.romUnitId)})
  if($candidate.Count -eq 0){throw 'TARGET_OUTSIDE_CANDIDATE_SET'}
  if($p.classification -eq 'MISSING_ROM'){throw 'MISSING_ROM_NOT_LINKABLE'}
  if($p.classification -eq 'AMBIGUOUS'){
   if((Get-RmProperty $payload 'mode')-ne'human_ambiguous'-or$scope -ne 'unit'-or[string]::IsNullOrWhiteSpace([string](Get-RmProperty $payload 'justification'))-or(Get-RmProperty $payload 'binaryIdentityNotProvenAcknowledged')-isnot[bool]-or!(Get-RmProperty $payload 'binaryIdentityNotProvenAcknowledged')){throw 'AMBIGUOUS_EXPLICIT_UNIT_REASON_AND_CONSENT_REQUIRED'}
  }elseif((Get-RmProperty $payload 'mode')-eq'human_ambiguous'){throw 'HUMAN_AMBIGUOUS_MODE_REQUIRES_AMBIGUOUS_PROPOSAL'}
 }
 if($type -in @('proposal.reject','proposal.defer')-and!$p){throw 'PROPOSAL_REQUIRED'}
 if($type -in @('metadata.prefer_source','metadata.override_display','variant.relate')-and!$target){throw 'TARGET_REQUIRED'}
 if($type -eq 'metadata.override_display'){if(@($payload.fields).Count -ne 1){throw 'ONE_OVERRIDE_FIELD_REQUIRED'};Assert-RmMetadataValue $payload.fields[0] $payload.value;$source=$null}
 if($type -eq 'metadata.prefer_source'){if(@($payload.fields).Count -eq 0){throw 'PREFERENCE_FIELDS_REQUIRED'};foreach($f in $payload.fields){if($f -notin @('*','name','desc','rating','releasedate','developer','publisher','genre','players','playcount','playtime','lastplayed')){throw 'UNKNOWN_METADATA_PREFERENCE_FIELD'}}}
 if($type -eq 'variant.relate'){if($target.scope -ne 'variant'-or$payload.otherTarget.scope -ne 'variant'){throw 'VARIANT_RELATION_SCOPE_REQUIRED'};$other=Get-RmTargetObservation $Catalogue $payload.otherTarget;if($other.game.console -ne $obs.game.console-or$payload.otherTarget.variantId -eq $target.variantId){throw 'INVALID_VARIANT_RELATION'};if($payload.relation -notin @('alternative-format','multidisc','related-variant')){throw 'INVALID_VARIANT_RELATION_TYPE'}}
 $dependencies=@(Get-RmProperty $payload 'explicitDependencies' @())
 foreach($dep in $dependencies){if($dep.type -eq 'source_content'){Assert-RmFields $dep @('type','metadataEntryId','digest') @('type','metadataEntryId','digest');$s=@($Catalogue.metadataIndex|Where-Object{$_.metadataEntryId -eq $dep.metadataEntryId});if($s.Count -ne 1-or$s[0].contentVersionId -ne $dep.digest){throw 'EXPLICIT_DEPENDENCY_INVALID'}}elseif($dep.type -eq 'decision'){Assert-RmFields $dep @('type','decisionId') @('type','decisionId')}else{throw 'UNKNOWN_EXPLICIT_DEPENDENCY'}}
 $payload.PSObject.Properties.Remove('explicitDependencies')
 Set-RmProperty $payload 'unitObservationDigests' ([pscustomobject]$unitObs)
 if($p){Set-RmProperty $payload 'candidateSet' $p.candidates;Set-RmProperty $payload 'candidateSetDigest' $p.candidateSetDigest}
 if($type -eq 'metadata.keep_historical'){Set-RmProperty $payload 'historicalSnapshot' ([pscustomobject]@{raw=$entry.raw;rawEntryXml=$entry.rawEntryXml;versionId=$entry.versionId;contentVersionId=$entry.contentVersionId;runtimeStatsVersionId=$entry.runtimeStatsVersionId;selectable=$false})}
 $semantic=Get-RmDigest ([pscustomobject]@{type=$type;sourceId=$(if($source){$source.metadataEntryId}else{$null});target=$target;fields=(Get-RmProperty $payload 'fields');value=(Get-RmProperty $payload 'value');relation=(Get-RmProperty $payload 'relation');otherParticipant=(Get-RmProperty $payload 'otherTarget')})
 $origin=[pscustomobject]@{scanId=$Catalogue.scan.scanId;proposalId=$null;number=$null;proposalDigest=$null;proposalSemanticDigest=$semantic;semanticModelVersion='1.0.0';evidenceProfile='explicit-user-intent-v1';classification=$null;rule='USER_EXPLICIT_OPERATION';ruleVersion='1.0.0';algorithmVersion='decisions-0.3.2';confidenceInitial=$null}
 if($p){$origin.proposalId=$p.proposalId;$origin.number=$p.number;$origin.proposalDigest=$p.proposalDigest;$origin.proposalSemanticDigest=$p.proposalSemanticDigest;$origin.evidenceProfile=$p.descriptor.evidenceProfile;$origin.classification=$p.classification;$origin.rule=$p.rule;$origin.ruleVersion=$p.descriptor.ruleVersion;$origin.algorithmVersion=$p.descriptor.algorithmVersion;$origin.confidenceInitial=$p.confidenceInitial}
 $sourceMode='identity';$contentMode='follow_current';$targetMode='identity';$candidateMode='independent';$proposalMode='audit'
 if($target-and$target.scope -eq 'unit'){$targetMode='unit_observation'}
 if($p-and$type -like 'metadata.link_*'){$candidateMode='exact'}
 if($type -eq 'metadata.override_display'-or$type -eq 'variant.relate'){$sourceMode='independent';$contentMode='independent';$targetMode='identity'}
 if($type -eq 'metadata.keep_historical'){$sourceMode='snapshot';$contentMode='frozen';$targetMode='independent'}
 if($type -in @('proposal.reject','proposal.defer')){$sourceMode='semantic_proposal';$contentMode='independent';$targetMode='independent';$proposalMode='semantic_digest'}
 $state='active';if($type -eq 'proposal.reject'){$state='rejected'}elseif($type -eq 'proposal.defer'){$state='pending'}
 $decision=[pscustomobject]@{decisionId=(New-RmId);createdAtUtc=[DateTime]::UtcNow.ToString('o');createdBy='user';type=$type;state=$state;origin=$origin;source=$source;target=$target;payload=$payload;acceptedPreconditions=[pscustomobject]@{previewId=$PreviewId;scanId=$Catalogue.scan.scanId;sourceIdentityResolutionDigest=$(if($source){$source.identityAnchorSha256}else{$null})};validityPolicy=[pscustomobject]@{sourceMode=$sourceMode;contentMode=$contentMode;targetMode=$targetMode;candidateSetMode=$candidateMode;proposalMode=$proposalMode;explicitDependencies=$dependencies};note=$note;supersedes=(Get-RmProperty $Operation 'supersedes');lifecycle=@()}
 return [pscustomobject]@{operation='create';decision=$decision}
}
function Get-RmPlannedState {
 param($Before,$Plan,$Catalogue,$Registry)
 $after=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Before -Depth 80)
 foreach($op in $Plan){
  if($op.operation -eq 'revoke'){$old=@($after.decisions|Where-Object{$_.decisionId -eq $op.decisionId});if($old.Count -ne 1-or$old[0].state -in @('revoked','superseded')){throw 'REVOKE_DECISION_NOT_ACTIVE'};$old[0].state='revoked';$old[0].lifecycle+=@([pscustomobject]@{event='revoked';atUtc=[DateTime]::UtcNow.ToString('o');note=$op.note})}
  else{
   $d=$op.decision
   if($d.supersedes){$old=@($after.decisions|Where-Object{$_.decisionId -eq $d.supersedes});if($old.Count -ne 1-or$old[0].state -in @('revoked','superseded')){throw 'REPLACEMENT_DECISION_NOT_ACTIVE'};$old[0].state='superseded';$old[0].lifecycle+=@([pscustomobject]@{event='superseded';atUtc=$d.createdAtUtc;replacementDecisionId=$d.decisionId})}
   if($d.type -like 'metadata.link_*'-and@($after.decisions|Where-Object{$_.type -like 'metadata.link_*'-and$_.state -eq 'active'-and$_.source.metadataEntryId -eq $d.source.metadataEntryId}).Count){throw 'SOURCE_ALREADY_HAS_MANUAL_LINK_REPLACE_EXPLICITLY'}
   if($d.type -in @('proposal.reject','proposal.defer')-and@($after.decisions|Where-Object{$_.type -in @('proposal.reject','proposal.defer')-and$_.state -in @('pending','rejected')-and$_.origin.proposalSemanticDigest -eq $d.origin.proposalSemanticDigest}).Count){throw 'SEMANTIC_PROPOSAL_ALREADY_DECIDED'}
   if($d.type -in @('metadata.prefer_source','metadata.override_display')){foreach($existing in $after.decisions|Where-Object{$_.state -eq 'active'-and$_.type -eq $d.type-and(Get-RmDigest $_.target)-eq(Get-RmDigest $d.target)}){if(@($existing.payload.fields|Where-Object{$_ -in $d.payload.fields-or$_ -eq '*'-or$d.payload.fields -contains '*'}).Count){throw 'METADATA_POLICY_CONFLICT_REPLACE_EXPLICITLY'}}}
   if($d.type -eq 'variant.relate'){$pair=@($d.target.variantId,$d.payload.otherTarget.variantId|Sort-Object)-join '|';foreach($old in $after.decisions|Where-Object{$_.type -eq 'variant.relate'-and$_.state -eq 'active'}){$oldPair=@($old.target.variantId,$old.payload.otherTarget.variantId|Sort-Object)-join '|';if($pair -eq $oldPair-and$d.payload.relation -eq $old.payload.relation){throw 'DUPLICATE_VARIANT_RELATION'}}}
   $after.decisions+=@($d)
  }
 }
 foreach($d in $after.decisions|Where-Object{$_.state -eq 'active'-and$_.type -eq 'metadata.prefer_source'}){
  $requiredLinks=@($after.decisions|Where-Object{$_.state -eq 'active'-and$_.type -like 'metadata.link_*'-and$_.source.metadataEntryId -eq $d.source.metadataEntryId})
  if($requiredLinks.Count -eq 1-and@($d.validityPolicy.explicitDependencies|Where-Object{$_.type -eq 'decision'-and$_.decisionId -eq $requiredLinks[0].decisionId}).Count -eq 0){$d.validityPolicy.explicitDependencies+=@([pscustomobject]@{type='decision';decisionId=$requiredLinks[0].decisionId})}
 }
 Assert-RmAssociations3 $after
 Set-RmDecisionProjection $Catalogue $Registry $after
 foreach($op in $Plan|Where-Object{$_.operation -eq 'create'}){
  $d=$op.decision;$effect=@($Catalogue.decisionEffects|Where-Object{$_.decisionId -eq $d.decisionId})[0]
  if($effect.effectiveStatus -eq 'suspended'){if($effect.reasons -contains 'PREFERRED_SOURCE_UNLINKED'){throw 'PREFERRED_SOURCE_NOT_LINKED_TO_TARGET'};throw ('PLANNED_DECISION_NOT_EFFECTIVE: '+($effect.reasons -join ','))}
  if($d.type -eq 'metadata.prefer_source'){$o=Get-RmTargetObservation $Catalogue $d.target;$sources=@($o.game.metadataSources|Where-Object{$_.metadataEntryId -eq $d.source.metadataEntryId-and($_.scope -eq 'game'-or$d.target.scope -eq 'game'-or$_.variantId -eq $d.target.variantId)});if($sources.Count -ne 1){throw 'PREFERRED_SOURCE_NOT_LINKED_TO_TARGET'}}
 }
 return $after
}
function New-RmDecisionPreview {
 [CmdletBinding()]param([string]$OutputRoot,[string]$ExpectedScanId,[string]$RequestId,$Operations)
 Assert-RmUuid $RequestId
 $lock=Open-RmStateLock $OutputRoot
 try {
  $catalogue=Read-RmStrictJson ($OutputRoot+'\catalogue.json');if($catalogue.scan.scanId -ne $ExpectedScanId){throw 'STALE_SCAN_RESCAN_REQUIRED'}
  $registry=Read-RmStrictJson ($OutputRoot+'\identity_registry.json');$state=ConvertTo-RmAssociations3 (Get-RmAssociations $OutputRoot -ReadOnly) $registry.libraryId
  Assert-RmFreshScan $catalogue $OutputRoot
  if(@($Operations).Count -eq 0){throw 'EMPTY_BATCH'}
  $requestDigest=Get-RmDigest ([pscustomobject]@{requestId=$RequestId;scanId=$ExpectedScanId;operations=@($Operations)})
  $prior=@($state.transactions|Where-Object{$_.requestId -eq $RequestId});if($prior.Count){if($prior[0].requestDigest -ne $requestDigest){throw 'IDEMPOTENCY_REQUEST_COLLISION'};return [pscustomobject]@{alreadyCommitted=$true;transaction=$prior[0]}}
  $id=New-RmId;$plan=@($Operations|ForEach-Object{New-RmPlannedDecision $_ $catalogue $registry $id})
  $after=Get-RmPlannedState $state $plan $catalogue $registry
  $preview=[pscustomobject]@{schemaVersion='1.0.0';previewId=$id;previewDigest=$null;createdAtUtc=[DateTime]::UtcNow.ToString('o');scanId=$ExpectedScanId;libraryId=$registry.libraryId;requestId=$RequestId;requestDigest=$requestDigest;fixtureCommitAllowed=(Test-RmFixtureRoot $catalogue.scan.rootObserved);rootObserved=$catalogue.scan.rootObserved;guards=(Get-RmStateGuards $OutputRoot);revisionBefore=$state.revision;operations=$plan;effectsAfter=$catalogue.decisionEffects;decisionsBefore=$state.decisions;decisionsAfter=$after.decisions;candidateSetsDisplayed=@($plan|Where-Object{$_.operation -eq 'create'-and(Get-RmProperty $_.decision.payload 'mode')-eq'human_ambiguous'}|ForEach-Object{[pscustomobject]@{decisionId=$_.decision.decisionId;candidates=$_.decision.payload.candidateSet}});confirmationRequired=$true}
  Set-RmProperty $preview 'serializedStateBefore' (ConvertTo-RmWireAssociations $state)
  Set-RmProperty $preview 'serializedStateAfter' (ConvertTo-RmWireAssociations $after)
  $preview.previewDigest=Get-RmDigest $preview
  Write-RmJsonAtomic ($OutputRoot+'\Previews\'+$id+'.json') $preview
  Write-RmJsonAtomic ($OutputRoot+'\Previews\latest.json') ([pscustomobject]@{previewId=$id;previewDigest=$preview.previewDigest;scanId=$ExpectedScanId})
  return $preview
 }finally{$lock.Dispose()}
}

function New-RmRealCommitAuthorization {
 [CmdletBinding()]param([string]$OutputRoot,[string]$PreviewId,$Approval)
 Assert-RmFields $Approval @('schemaVersion','approvalId','decisionType','classification','rule','confidenceInitial','libraryId','documentId','metadataEntryId','identityAnchorSha256','sourceLocatorNormalized','target','targetEntryPath','expectedCandidateCount','candidateSetDigest','proposalSemanticDigest','expectedAssociationsSha256','requiredExecutionPhrase','noRomHashRequired','controlStatus') @('schemaVersion','approvalId','decisionType','classification','rule','libraryId','documentId','metadataEntryId','identityAnchorSha256','sourceLocatorNormalized','target','targetEntryPath','expectedCandidateCount','candidateSetDigest','proposalSemanticDigest','expectedAssociationsSha256','requiredExecutionPhrase')
 if($Approval.schemaVersion -ne '1.0.0'){throw 'REAL_APPROVAL_SCHEMA_UNSUPPORTED'}
 Assert-RmUuid $Approval.libraryId;Assert-RmUuid $Approval.documentId;Assert-RmUuid $Approval.metadataEntryId;Assert-RmTargetShape $Approval.target
 Assert-RmSha256 $Approval.identityAnchorSha256;Assert-RmSha256 $Approval.candidateSetDigest;Assert-RmSha256 $Approval.proposalSemanticDigest;Assert-RmSha256 $Approval.expectedAssociationsSha256
 if($Approval.decisionType -ne 'metadata.link_unit'-or$Approval.classification -ne 'EXACT_MOVED'){throw 'REAL_APPROVAL_SCOPE_NOT_ALLOWED'}
 if([int]$Approval.expectedCandidateCount -ne 1){throw 'REAL_APPROVAL_REQUIRES_SINGLE_CANDIDATE'}
 if([string]::IsNullOrWhiteSpace([string]$Approval.requiredExecutionPhrase)){throw 'REAL_APPROVAL_EXECUTION_PHRASE_REQUIRED'}
 $preview=Read-RmStrictJson ($OutputRoot+'\Previews\'+$PreviewId+'.json')
 if($preview.fixtureCommitAllowed-or(Test-RmFixtureRoot $preview.rootObserved)){throw 'REAL_AUTHORIZATION_REQUIRES_REAL_LIBRARY'}
 $saved=$preview.previewDigest;$preview.previewDigest=$null;$computed=Get-RmDigest $preview;$preview.previewDigest=$saved
 if($saved -ne $computed){throw 'PREVIEW_DIGEST_MISMATCH'}
 if($preview.libraryId -ne $Approval.libraryId){throw 'REAL_APPROVAL_LIBRARY_ID_MISMATCH'}
 if(@($preview.operations).Count -ne 1-or$preview.operations[0].operation -ne 'create'){throw 'REAL_APPROVAL_SINGLE_CREATE_REQUIRED'}
 $d=$preview.operations[0].decision
 if($d.type -ne $Approval.decisionType){throw 'REAL_APPROVAL_DECISION_TYPE_MISMATCH'}
 if(!$d.source-or$d.source.documentId -ne $Approval.documentId-or$d.source.metadataEntryId -ne $Approval.metadataEntryId-or$d.source.identityAnchorSha256 -ne $Approval.identityAnchorSha256-or$d.source.acceptedLocator -ne $Approval.sourceLocatorNormalized){throw 'REAL_APPROVAL_SOURCE_IDENTITY_MISMATCH'}
 if(!$d.target-or$d.target.scope -ne 'unit'-or$d.target.gameId -ne $Approval.target.gameId-or$d.target.variantId -ne $Approval.target.variantId-or$d.target.romUnitId -ne $Approval.target.romUnitId){throw 'REAL_APPROVAL_TARGET_MISMATCH'}
 if($d.origin.classification -ne $Approval.classification-or$d.origin.rule -ne $Approval.rule-or$d.origin.proposalSemanticDigest -ne $Approval.proposalSemanticDigest){throw 'REAL_APPROVAL_SEMANTIC_MISMATCH'}
 if(@($d.payload.candidateSet).Count -ne [int]$Approval.expectedCandidateCount-or$d.payload.candidateSetDigest -ne $Approval.candidateSetDigest){throw 'REAL_APPROVAL_CANDIDATE_SET_MISMATCH'}
 $approvedCandidate=$d.payload.candidateSet[0];if($approvedCandidate.entryPath -ne $Approval.targetEntryPath){throw 'REAL_APPROVAL_CANDIDATE_PATH_MISMATCH'}
 if((Get-RmByteDigest ($OutputRoot+'\associations.json')) -ne $Approval.expectedAssociationsSha256){throw 'REAL_APPROVAL_PERSISTENT_STATE_CHANGED'}
 $catalogue=Read-RmStrictJson ($OutputRoot+'\catalogue.json')
 if($catalogue.scan.scanId -ne $preview.scanId){throw 'STALE_SCAN_RESCAN_REQUIRED'}
 $proposal=@($catalogue.decisionProposals|Where-Object{$_.proposalSemanticDigest -eq $Approval.proposalSemanticDigest})
 if($proposal.Count -ne 1){throw 'REAL_APPROVAL_PROPOSAL_NOT_UNIQUE'};$proposal=$proposal[0]
 if($proposal.sourceEntry.metadataEntryId -ne $Approval.metadataEntryId-or$proposal.sourceEntry.identityStatus -ne 'exact-unique'){throw 'REAL_APPROVAL_SOURCE_NOT_EXACT_UNIQUE'}
 if($proposal.candidateSetDigest -ne $Approval.candidateSetDigest-or@($proposal.candidates).Count -ne [int]$Approval.expectedCandidateCount){throw 'REAL_APPROVAL_CURRENT_CANDIDATES_CHANGED'}
 if(!$proposal.target-or$proposal.target.gameId -ne $Approval.target.gameId-or$proposal.target.variantId -ne $Approval.target.variantId-or$proposal.target.romUnitId -ne $Approval.target.romUnitId){throw 'REAL_APPROVAL_CURRENT_TARGET_CHANGED'}
 if(@($proposal.descriptor.targetedHashEvidence).Count -ne 0){throw 'REAL_APPROVAL_UNEXPECTED_ROM_HASH_EVIDENCE'}
 Assert-RmFreshScan $catalogue $OutputRoot $preview.guards
 $auth=[pscustomobject]@{schemaVersion='1.0.0';authorizationId=(New-RmId);authorizationDigest=$null;createdAtUtc=[DateTime]::UtcNow.ToString('o');status='awaiting-control-execute';mode='real-commit';libraryId=$preview.libraryId;rootObserved=$preview.rootObserved;previewId=$preview.previewId;previewDigest=$preview.previewDigest;scanId=$preview.scanId;requestId=$preview.requestId;proposalSemanticDigest=$Approval.proposalSemanticDigest;metadataEntryId=$Approval.metadataEntryId;target=$Approval.target;candidateSetDigest=$Approval.candidateSetDigest;guards=$preview.guards;requiredExecutionPhrase=$Approval.requiredExecutionPhrase;associationsSha256=$Approval.expectedAssociationsSha256;romHashesUsed=$false}
 $auth.authorizationDigest=Get-RmDigest $auth
 Write-RmJsonAtomic ($OutputRoot+'\Previews\'+$preview.previewId+'.real-authorization.json') $auth
 return $auth
}
function Test-RmBatchAuthorizationKind {
 param([string]$Kind)
 if([string]::IsNullOrWhiteSpace($Kind)){return $false}
 if($Kind -ceq 'real-commit-batch-preflight'){return $true}
 return ($Kind -cmatch '^[a-z0-9][a-z0-9._-]*-real-commit-batch-preflight$')
}
function Test-RmBatchPermitKind {
 param([string]$Kind)
 if([string]::IsNullOrWhiteSpace($Kind)){return $false}
 if($Kind -ceq 'real-commit-batch-control-verdict'){return $true}
 return ($Kind -cmatch '^[a-z0-9][a-z0-9._-]*-real-commit-control-verdict$')
}
function Get-RmCommitRegistryIdentityCore {
 param($Registry)
 return [pscustomobject]@{
  schemaVersion=$Registry.schemaVersion
  libraryId=$Registry.libraryId
  games=@($Registry.games|Sort-Object gameId|ForEach-Object{[pscustomobject]@{gameId=$_.gameId;console=$_.console;name=$_.name}})
  variants=@($Registry.variants|Sort-Object variantId|ForEach-Object{[pscustomobject]@{variantId=$_.variantId;gameId=$_.gameId;console=$_.console;signature=$_.signature;name=$_.name;retired=$_.retired;aliases=@($_.aliases)}})
  documents=@($Registry.documents|Sort-Object documentId|ForEach-Object{[pscustomobject]@{documentId=$_.documentId;relativePath=$_.relativePath}})
  metadataEntries=@($Registry.metadataEntries|Sort-Object metadataEntryId|ForEach-Object{[pscustomobject]@{metadataEntryId=$_.metadataEntryId;documentId=$_.documentId;console=$_.console;entryKind=$_.entryKind;sourceLocatorNormalized=$_.sourceLocatorNormalized;identityAnchorSha256=$_.identityAnchorSha256}})
  romUnits=@($Registry.romUnits|Sort-Object romUnitId|ForEach-Object{[pscustomobject]@{romUnitId=$_.romUnitId;gameId=$_.gameId;variantId=$_.variantId;technicalKey=$_.technicalKey}})
  proposalIdentities=@($Registry.proposalIdentities|Sort-Object proposalId|ForEach-Object{[pscustomobject]@{proposalId=$_.proposalId;proposalSemanticDigest=$_.proposalSemanticDigest}})
 }
}
function Get-RmBatchFrozenDigestProfile {
 param($Authorization)
 $ordered=@($Authorization.decisions|Sort-Object index)
 $legacy=@($ordered|ForEach-Object{[pscustomobject]@{
  index=$_.index;decisionId=$_.decisionId;metadataEntryId=$_.metadataEntryId;
  gameId=$_.target.gameId;variantId=$_.target.variantId;romUnitId=$_.target.romUnitId;
  currentRom=$_.currentRom;candidateSetDigest=$_.candidateSetDigest;proposalSemanticDigest=$_.proposalSemanticDigest
 }})
 if((Get-RmDigest $legacy) -eq $Authorization.frozenBatchDigest){return 'legacy-v1'}
 $extended=@($ordered|ForEach-Object{[pscustomobject]@{
  index=$_.index;decisionId=$_.decisionId;proposalId=(Get-RmProperty $_ 'proposalId');documentId=(Get-RmProperty $_ 'documentId');metadataEntryId=$_.metadataEntryId;
  historicalLocator=(Get-RmProperty $_ 'historicalLocator');gameId=$_.target.gameId;variantId=$_.target.variantId;romUnitId=$_.target.romUnitId;
  currentRom=$_.currentRom;candidateSetDigest=$_.candidateSetDigest;proposalSemanticDigest=$_.proposalSemanticDigest
 }})
 if((Get-RmDigest $extended) -eq $Authorization.frozenBatchDigest){return 'extended-v2'}
 $genericEligible=$true
 foreach($a in $ordered){if(!$a.PSObject.Properties['operation'] -or !$a.PSObject.Properties['decisionDigest']){$genericEligible=$false;break}}
 if($genericEligible){
  $generic=@($ordered|ForEach-Object{[pscustomobject]@{index=$_.index;operation=$_.operation;decisionId=$_.decisionId;decisionDigest=$_.decisionDigest}})
  if((Get-RmDigest $generic) -eq $Authorization.frozenBatchDigest){return 'generic-v3'}
 }
 throw 'REAL_BATCH_FROZEN_DIGEST_RECOMPUTE_MISMATCH'
}
function Find-RmBatchControlPermit {
 param([string]$AuthorizationFullPath,$Authorization)
 $dir=Get-RmParentPath $AuthorizationFullPath
 $matches=New-Object 'System.Collections.Generic.List[object]'
 foreach($entry in @(Get-RmDirectoryEntries $dir|Where-Object{$_.Name -like '*.json'})){
  $path=$dir+'\'+$entry.Name
  if((Get-RmFullPath $path) -eq (Get-RmFullPath $AuthorizationFullPath)){continue}
  $candidate=$null
  try{$candidate=Read-RmStrictJson $path}catch{continue}
  if(!$candidate.PSObject.Properties['permitKind'] -or !$candidate.PSObject.Properties['authorizationId']){continue}
  if($candidate.authorizationId -ne $Authorization.authorizationId){continue}
  $matches.Add([pscustomobject]@{path=$path;permit=$candidate})
 }
 if($matches.Count -eq 0){throw 'REAL_BATCH_CONTROL_PERMIT_MISSING'}
 if($matches.Count -gt 1){throw 'REAL_BATCH_CONTROL_PERMIT_AMBIGUOUS'}
 return $matches[0]
}
function Assert-RmBatchCommitAuthorizationContract {
 param($Authorization,$Preview,[string]$ExecutionPhrase,$Permit)
 $auth=$Authorization
 Assert-RmFields $auth @('schemaVersion','authorizationKind','lot','scope','authorizationId','authorizationDigest','createdAtUtc','status','executable','commitAuthorized','libraryId','rootObserved','approvedPreviewId','approvedPreviewDigest','previewId','previewDigest','scanId','requestId','operationCount','guards','associationsSha256','registryIdentityDigest','physicalDigest','requiredFutureExecutionPhrase','romHashesUsed','frozenBatchDigest','decisions','projected') @('schemaVersion','authorizationKind','authorizationId','authorizationDigest','status','executable','commitAuthorized','libraryId','rootObserved','approvedPreviewId','approvedPreviewDigest','previewId','previewDigest','scanId','requestId','operationCount','guards','associationsSha256','registryIdentityDigest','physicalDigest','requiredFutureExecutionPhrase','romHashesUsed','frozenBatchDigest','decisions','projected')
 if($auth.schemaVersion -ne '1.0.0' -or !(Test-RmBatchAuthorizationKind $auth.authorizationKind)){throw 'REAL_BATCH_AUTHORIZATION_INVALID'}
 Assert-RmUuid $auth.authorizationId;Assert-RmUuid $auth.previewId;Assert-RmUuid $auth.scanId;Assert-RmUuid $auth.requestId
 if($auth.status -ne 'awaiting-control-commit-verdict' -or $auth.executable -ne $false -or $auth.commitAuthorized -ne $false){throw 'REAL_BATCH_PREFLIGHT_EVIDENCE_MUTATED'}
 if($auth.romHashesUsed -ne $false){throw 'REAL_BATCH_AUTHORIZATION_SCOPE_INVALID'}
 $count=[int]$auth.operationCount
 if($count -lt 1 -or @($auth.decisions).Count -ne $count){throw 'REAL_BATCH_AUTHORIZATION_SCOPE_INVALID'}
 $saved=$auth.authorizationDigest;$auth.authorizationDigest=$null;$computed=Get-RmDigest $auth;$auth.authorizationDigest=$saved
 if($saved -ne $computed){throw 'REAL_BATCH_AUTHORIZATION_DIGEST_MISMATCH'}
 if($ExecutionPhrase -cne $auth.requiredFutureExecutionPhrase){throw 'REAL_BATCH_EXECUTION_PHRASE_MISMATCH'}
 if($auth.previewId -ne $Preview.previewId-or$auth.previewDigest -ne $Preview.previewDigest-or$auth.scanId -ne $Preview.scanId-or$auth.requestId -ne $Preview.requestId-or$auth.libraryId -ne $Preview.libraryId-or$auth.rootObserved -ne $Preview.rootObserved){throw 'REAL_BATCH_AUTHORIZATION_PREVIEW_MISMATCH'}
 if((Get-RmDigest $auth.guards)-ne(Get-RmDigest $Preview.guards)){throw 'REAL_BATCH_AUTHORIZATION_GUARDS_MISMATCH'}
 if(@($Preview.operations).Count -ne $count){throw 'REAL_BATCH_PREVIEW_OPERATION_COUNT_CHANGED'}

 $expectedDecisionIds=@($auth.decisions|ForEach-Object{$_.decisionId}|Sort-Object)
 if(@($expectedDecisionIds|Select-Object -Unique).Count -ne $count){throw 'REAL_BATCH_AUTHORIZATION_DUPLICATE_DECISION_ID'}
 foreach($id in $expectedDecisionIds){Assert-RmUuid $id}
 $actualDecisionIds=New-Object 'System.Collections.Generic.List[string]'
 foreach($op in @($Preview.operations)){
  if($op.operation -eq 'create'){$actualDecisionIds.Add([string]$op.decision.decisionId)}
  elseif($op.PSObject.Properties['decisionId']){$actualDecisionIds.Add([string]$op.decisionId)}
  else{throw 'REAL_BATCH_PREVIEW_OPERATION_ID_MISSING'}
 }
 $actualSorted=@($actualDecisionIds.ToArray()|Sort-Object)
 if($actualSorted.Count -ne $count -or (Get-RmDigest $actualSorted) -ne (Get-RmDigest $expectedDecisionIds)){throw 'REAL_BATCH_PREVIEW_DECISION_SET_CHANGED'}

 foreach($a in @($auth.decisions)){
  Assert-RmFields $a @('index','console','gameName','decisionId','proposalId','proposalNumber','documentId','metadataEntryId','identityAnchorSha256','sourceXml','historicalLocator','target','currentRom','candidateSetDigest','proposalSemanticDigest','classification','rule','confidence','candidateCount','normalApprovalAllowed','targetedRomHashEvidenceCount','note','operation','decisionType','decisionDigest') @('index','decisionId')
  $expectedOperation=[string](Get-RmProperty $a 'operation' 'create')
  $ops=@($Preview.operations|Where-Object{if($_.operation -ne $expectedOperation){$false}elseif($_.operation -eq 'create'){$_.decision.decisionId -eq $a.decisionId}else{$_.decisionId -eq $a.decisionId}})
  if($ops.Count -ne 1){throw ('REAL_BATCH_DECISION_MISSING: '+$a.decisionId)}
  $op=$ops[0]
  if($a.PSObject.Properties['decisionDigest']){
   if($op.operation -ne 'create' -or (Get-RmDigest $op.decision) -ne $a.decisionDigest){throw ('REAL_BATCH_DECISION_DIGEST_CHANGED: '+$a.decisionId)}
   continue
  }
  if($op.operation -ne 'create'){throw ('REAL_BATCH_LEGACY_DECISION_REQUIRES_CREATE: '+$a.decisionId)}
  $d=$op.decision
  $expectedType=[string](Get-RmProperty $a 'decisionType' 'metadata.link_unit')
  if($d.type -ne $expectedType -or $d.state -ne 'active'){throw ('REAL_BATCH_DECISION_SHAPE_CHANGED: '+$a.decisionId)}
  foreach($required in @('metadataEntryId','documentId','identityAnchorSha256','historicalLocator','sourceXml','target','currentRom','candidateSetDigest','proposalSemanticDigest','rule','confidence','candidateCount','note')){if(!$a.PSObject.Properties[$required]){throw ('REAL_BATCH_AUTHORIZATION_DECISION_FIELD_MISSING: '+$required)}}
  if($d.source.metadataEntryId -ne $a.metadataEntryId -or $d.source.documentId -ne $a.documentId -or $d.source.identityAnchorSha256 -ne $a.identityAnchorSha256 -or $d.source.acceptedLocator -ne $a.historicalLocator -or $d.source.relativeXmlPath -ne $a.sourceXml){throw ('REAL_BATCH_SOURCE_CHANGED: '+$a.decisionId)}
  if($d.target.scope -ne $a.target.scope -or $d.target.gameId -ne $a.target.gameId -or $d.target.variantId -ne $a.target.variantId -or $d.target.romUnitId -ne $a.target.romUnitId){throw ('REAL_BATCH_TARGET_CHANGED: '+$a.decisionId)}
  if($a.PSObject.Properties['proposalId'] -and $d.origin.proposalId -ne $a.proposalId){throw ('REAL_BATCH_PROPOSAL_ID_CHANGED: '+$a.decisionId)}
  if($a.PSObject.Properties['proposalNumber'] -and [int]$d.origin.number -ne [int]$a.proposalNumber){throw ('REAL_BATCH_PROPOSAL_NUMBER_CHANGED: '+$a.decisionId)}
  if($d.origin.proposalSemanticDigest -ne $a.proposalSemanticDigest -or $d.origin.rule -ne $a.rule -or $d.origin.confidenceInitial -ne $a.confidence){throw ('REAL_BATCH_SEMANTICS_CHANGED: '+$a.decisionId)}
  if($a.PSObject.Properties['classification'] -and $d.origin.classification -ne $a.classification){throw ('REAL_BATCH_CLASSIFICATION_CHANGED: '+$a.decisionId)}
  if($a.PSObject.Properties['normalApprovalAllowed'] -and $a.normalApprovalAllowed -ne $true){throw ('REAL_BATCH_NORMAL_APPROVAL_CHANGED: '+$a.decisionId)}
  if($a.PSObject.Properties['targetedRomHashEvidenceCount'] -and [int]$a.targetedRomHashEvidenceCount -ne 0){throw ('REAL_BATCH_UNEXPECTED_ROM_HASH_EVIDENCE: '+$a.decisionId)}
  if(@($d.payload.candidateSet).Count -ne [int]$a.candidateCount -or $d.payload.candidateSetDigest -ne $a.candidateSetDigest){throw ('REAL_BATCH_CANDIDATE_SET_CHANGED: '+$a.decisionId)}
  if([int]$a.candidateCount -ne 1 -or $d.payload.candidateSet[0].entryPath -ne $a.currentRom){throw ('REAL_BATCH_CANDIDATE_PATH_CHANGED: '+$a.decisionId)}
  if($d.note -ne $a.note){throw ('REAL_BATCH_DECISION_NOTE_CHANGED: '+$a.decisionId)}
  if(@(Get-RmProperty $d.validityPolicy 'explicitDependencies' @()).Count -ne 0){throw ('REAL_BATCH_UNEXPECTED_DEPENDENCY: '+$a.decisionId)}
 }
 $frozenProfile=Get-RmBatchFrozenDigestProfile $auth

 Assert-RmFields $Permit @('schemaVersion','permitKind','lot','scope','authorizationId','authorizationDigest','frozenBatchDigest','previewId','previewDigest','scanId','requestId','operationCount','decisionIds','executionPhrase','atomicBatchRequired','postCommitControlRequired','remainingExactMovedAuthorized','commitAuthorized','verdict','verdictCode','createdAtUtc','permitDigest') @('schemaVersion','permitKind','authorizationId','authorizationDigest','frozenBatchDigest','previewId','previewDigest','scanId','requestId','operationCount','decisionIds','executionPhrase','atomicBatchRequired','postCommitControlRequired')
 if($Permit.schemaVersion -ne '1.0.0' -or !(Test-RmBatchPermitKind $Permit.permitKind)){throw 'REAL_BATCH_CONTROL_PERMIT_INVALID'}
 if($Permit.PSObject.Properties['commitAuthorized'] -and $Permit.commitAuthorized -ne $true){throw 'REAL_BATCH_CONTROL_PERMIT_NOT_AUTHORIZED'}
 if($Permit.PSObject.Properties['permitDigest']){$permitSaved=$Permit.permitDigest;$Permit.permitDigest=$null;$permitComputed=Get-RmDigest $Permit;$Permit.permitDigest=$permitSaved;if($permitSaved -ne $permitComputed){throw 'REAL_BATCH_CONTROL_PERMIT_DIGEST_MISMATCH'}}
 if($auth.PSObject.Properties['lot']){if(!$Permit.PSObject.Properties['lot'] -or [string]$Permit.lot -cne [string]$auth.lot){throw 'REAL_BATCH_CONTROL_PERMIT_CONTEXT_MISMATCH'}}
 if($auth.PSObject.Properties['scope']){if(!$Permit.PSObject.Properties['scope'] -or [string]$Permit.scope -cne [string]$auth.scope){throw 'REAL_BATCH_CONTROL_PERMIT_CONTEXT_MISMATCH'}}
 if($Permit.authorizationId -ne $auth.authorizationId -or $Permit.authorizationDigest -ne $auth.authorizationDigest -or $Permit.frozenBatchDigest -ne $auth.frozenBatchDigest -or $Permit.previewId -ne $auth.previewId -or $Permit.previewDigest -ne $auth.previewDigest -or $Permit.scanId -ne $auth.scanId -or $Permit.requestId -ne $auth.requestId){throw 'REAL_BATCH_CONTROL_PERMIT_AUTH_MISMATCH'}
 if([int]$Permit.operationCount -ne $count -or $Permit.executionPhrase -cne $ExecutionPhrase -or $Permit.atomicBatchRequired -ne $true -or $Permit.postCommitControlRequired -ne $true){throw 'REAL_BATCH_CONTROL_PERMIT_SCOPE_INVALID'}
 if($Permit.PSObject.Properties['remainingExactMovedAuthorized'] -and $Permit.remainingExactMovedAuthorized -ne $false){throw 'REAL_BATCH_CONTROL_PERMIT_SCOPE_INVALID'}
 $permitIds=@($Permit.decisionIds|Sort-Object)
 if($permitIds.Count -ne $count -or (Get-RmDigest $permitIds) -ne (Get-RmDigest $expectedDecisionIds)){throw 'REAL_BATCH_CONTROL_PERMIT_DECISION_SET_CHANGED'}
 return [pscustomobject]@{authorization=$auth;permit=$Permit;frozenProfile=$frozenProfile;operationCount=$count}
}
function Assert-RmRealCommitAuthorization {
 param([string]$OutputRoot,$Preview,[string]$AuthorizationPath,[string]$ExecutionPhrase)
 if([string]::IsNullOrWhiteSpace($AuthorizationPath)){throw 'REAL_COMMIT_AUTHORIZATION_REQUIRED'}
 if([string]::IsNullOrWhiteSpace($ExecutionPhrase)){throw 'REAL_COMMIT_EXECUTION_PHRASE_REQUIRED'}
 $authorizationFullPath=Get-RmFullPath $AuthorizationPath
 $auth=Read-RmStrictJson $authorizationFullPath

 # Legacy mono-decision authorization used by Lot 1. Kept byte-for-byte in behavior.
 if($auth.PSObject.Properties['mode']){
  Assert-RmFields $auth @('schemaVersion','authorizationId','authorizationDigest','createdAtUtc','status','mode','libraryId','rootObserved','previewId','previewDigest','scanId','requestId','proposalSemanticDigest','metadataEntryId','target','candidateSetDigest','guards','requiredExecutionPhrase','associationsSha256','romHashesUsed') @('schemaVersion','authorizationId','authorizationDigest','status','mode','libraryId','rootObserved','previewId','previewDigest','scanId','requestId','proposalSemanticDigest','metadataEntryId','target','candidateSetDigest','guards','requiredExecutionPhrase','associationsSha256','romHashesUsed')
  if($auth.schemaVersion -ne '1.0.0'-or$auth.mode -ne 'real-commit'-or$auth.status -ne 'awaiting-control-execute'){throw 'REAL_COMMIT_AUTHORIZATION_INVALID'}
  $saved=$auth.authorizationDigest;$auth.authorizationDigest=$null;$computed=Get-RmDigest $auth;$auth.authorizationDigest=$saved
  if($saved -ne $computed){throw 'REAL_COMMIT_AUTHORIZATION_DIGEST_MISMATCH'}
  if($ExecutionPhrase -cne $auth.requiredExecutionPhrase){throw 'REAL_COMMIT_EXECUTION_PHRASE_MISMATCH'}
  if($auth.previewId -ne $Preview.previewId-or$auth.previewDigest -ne $Preview.previewDigest-or$auth.scanId -ne $Preview.scanId-or$auth.requestId -ne $Preview.requestId-or$auth.libraryId -ne $Preview.libraryId-or$auth.rootObserved -ne $Preview.rootObserved){throw 'REAL_COMMIT_AUTHORIZATION_PREVIEW_MISMATCH'}
  if((Get-RmDigest $auth.guards)-ne(Get-RmDigest $Preview.guards)){throw 'REAL_COMMIT_AUTHORIZATION_GUARDS_MISMATCH'}
  if((Get-RmByteDigest ($OutputRoot+'\associations.json')) -ne $auth.associationsSha256){throw 'REAL_COMMIT_PERSISTENT_STATE_CHANGED'}
  return $auth
 }

 $permitMatch=Find-RmBatchControlPermit $authorizationFullPath $auth
 $contract=Assert-RmBatchCommitAuthorizationContract $auth $Preview $ExecutionPhrase $permitMatch.permit
 Assert-RmFields $auth.guards @('catalogueSha256','registrySha256','associationsSha256') @('catalogueSha256','registrySha256','associationsSha256')
 if($auth.associationsSha256 -ne $auth.guards.associationsSha256){throw 'REAL_BATCH_AUTHORIZATION_ASSOCIATION_GUARD_MISMATCH'}
 $assocPath=$OutputRoot+'\associations.json';$cataloguePath=$OutputRoot+'\catalogue.json';$registryPath=$OutputRoot+'\identity_registry.json'
 if((Get-RmByteDigest $assocPath) -ne $auth.associationsSha256){throw 'REAL_BATCH_PERSISTENT_STATE_CHANGED'}
 if((Get-RmByteDigest $cataloguePath) -ne $auth.guards.catalogueSha256){throw 'REAL_BATCH_CATALOGUE_GUARD_CHANGED'}
 if((Get-RmByteDigest $registryPath) -ne $auth.guards.registrySha256){throw 'REAL_BATCH_REGISTRY_GUARD_CHANGED'}
 $catalogue=Read-RmStrictJson $cataloguePath
 if($catalogue.scan.scanId -ne $auth.scanId){throw 'REAL_BATCH_SCAN_CHANGED'}
 if($catalogue.physicalSnapshot.inventoryDigest -ne $auth.physicalDigest){throw 'REAL_BATCH_PHYSICAL_DIGEST_CHANGED'}
 $registry=Read-RmStrictJson $registryPath
 if($registry.libraryId -ne $auth.libraryId){throw 'REAL_BATCH_REGISTRY_LIBRARY_CHANGED'}
 $identityDigest=Get-RmDigest (Get-RmCommitRegistryIdentityCore $registry)
 if($identityDigest -ne $auth.registryIdentityDigest){throw 'REAL_BATCH_REGISTRY_IDENTITY_DIGEST_CHANGED'}
 return $contract
}

function Get-RmCommitPublicationState {
 [CmdletBinding()]param(
  [Parameter(Mandatory=$true)][string]$OutputRoot,
  [Parameter(Mandatory=$true)][string]$RequestId,
  [string]$ExpectedRequestDigest,
  [string]$PreviewId,
  [string]$PreviewDigest,
  [object[]]$ExpectedDecisionIds=@(),
  [string]$ExpectedTransactionId,
  [switch]$FailureObserved,
  [switch]$PostCheckFailed,
  [string]$FailureMessage
 )
 Assert-RmUuid $RequestId
 $result=[pscustomobject]@{
  state='NOT_PUBLISHED';authoritativePublished=$false;automaticReplayAllowed=$false;commitMayProceed=$true;
  requestId=$RequestId;transaction=$null;transactionId=$null;journalStatus=$null;journalPath=$null;
  previousStateRecoverable=$false;failureMessage=$FailureMessage;reason=$null
 }
 try {
  $registryPath=$OutputRoot+'\identity_registry.json'
  if(!(Test-RmFileExists $registryPath)){throw 'IDENTITY_REGISTRY_MISSING'}
  $reg=Read-RmStrictJson $registryPath
  $state=ConvertTo-RmAssociations3 (Get-RmAssociations $OutputRoot -ReadOnly) $reg.libraryId
  $matches=@($state.transactions|Where-Object{$_.requestId -eq $RequestId})
  $historyDir=$OutputRoot+'\History'
  $requestJournals=@()
  if(Test-RmDirectoryExists $historyDir){
   foreach($entry in @(Get-RmDirectoryEntries $historyDir|Where-Object{$_.Name -like '*_journal.json'})){
    $j=Read-RmStrictJson ($historyDir+'\'+$entry.Name);if($j.transaction -and $j.transaction.requestId -eq $RequestId){$requestJournals+=@([pscustomobject]@{path=($historyDir+'\'+$entry.Name);journal=$j})}
   }
  }
  if($matches.Count -gt 1){throw 'MULTIPLE_AUTHORITATIVE_TRANSACTIONS_FOR_REQUEST'}
  if($matches.Count -eq 0){
   if(@($requestJournals|Where-Object{$_.journal.status -eq 'committed'}).Count -gt 0){throw 'COMMITTED_JOURNAL_WITHOUT_AUTHORITATIVE_TRANSACTION'}
   if(@($requestJournals|Where-Object{$_.journal.status -eq 'prepared'}).Count -gt 0 -or $FailureObserved){
    $result.state='FAILED_BEFORE_PUBLICATION';$result.commitMayProceed=$false;$result.reason='No authoritative transaction is published for the request; a failed attempt is recorded or an exception was observed.'
   }
   return $result
  }
  $tx=$matches[0];$result.transaction=$tx;$result.transactionId=$tx.transactionId
  if($ExpectedTransactionId -and $tx.transactionId -ne $ExpectedTransactionId){throw 'AUTHORITATIVE_TRANSACTION_ID_MISMATCH'}
  if($ExpectedRequestDigest -and $tx.requestDigest -ne $ExpectedRequestDigest){throw 'AUTHORITATIVE_REQUEST_DIGEST_MISMATCH'}
  if($PreviewId -and $tx.previewId -ne $PreviewId){throw 'AUTHORITATIVE_PREVIEW_ID_MISMATCH'}
  if($PreviewDigest -and $tx.previewDigest -ne $PreviewDigest){throw 'AUTHORITATIVE_PREVIEW_DIGEST_MISMATCH'}
  if(@($ExpectedDecisionIds).Count -gt 0){
   $expected=@($ExpectedDecisionIds|Sort-Object);$actual=@($tx.decisionIds|Sort-Object)
   if($expected.Count -ne $actual.Count -or (Get-RmDigest $expected) -ne (Get-RmDigest $actual)){throw 'AUTHORITATIVE_DECISION_SET_MISMATCH'}
  }
  $journalPath=$historyDir+'\'+$tx.transactionId+'_journal.json';$result.journalPath=$journalPath
  if(!(Test-RmFileExists $journalPath)){throw 'AUTHORITATIVE_TRANSACTION_JOURNAL_MISSING'}
  $journal=Read-RmStrictJson $journalPath;$result.journalStatus=$journal.status
  if(!$journal.transaction -or $journal.transaction.transactionId -ne $tx.transactionId -or $journal.transaction.requestId -ne $tx.requestId){throw 'AUTHORITATIVE_TRANSACTION_JOURNAL_MISMATCH'}
  if($journal.status -notin @('prepared','committed')){throw 'AUTHORITATIVE_TRANSACTION_JOURNAL_STATUS_INVALID'}
  $backupPath=$historyDir+'\associations_'+$tx.transactionId+'_before.json'
  $result.previousStateRecoverable=((Test-RmFileExists $backupPath)-or($journal.previousStateSha256 -eq (Get-RmByteDigest ($OutputRoot+'\associations.json'))))
  $result.authoritativePublished=$true;$result.commitMayProceed=$false
  if($journal.status -eq 'prepared' -or $FailureObserved -or $PostCheckFailed){
   $result.state='COMMITTED_BUT_POSTCHECK_FAILED';$result.reason='The expected transaction is authoritative; replay is forbidden and post-commit control/recovery is required.'
  }else{
   $result.state='COMMITTED';$result.reason='The expected transaction is authoritative and its journal is committed.'
  }
  return $result
 }catch{
  $result.state='RECOVERY_REQUIRED';$result.authoritativePublished=$false;$result.commitMayProceed=$false;$result.reason=$_.Exception.Message
  return $result
 }
}

function Write-RmAssociationsTransaction {
 param([string]$OutputRoot,$State,$Transaction,[ValidateSet('None','BeforeFlush','BeforeReplace','AfterReplace')][string]$FailurePoint='None')
 $path=Get-RmNativePath ($OutputRoot+'\associations.json');$tmp=$path+'.'+$Transaction.transactionId+'.tmp';$backupPlain=$OutputRoot+'\History\associations_'+$Transaction.transactionId+'_before.json';$backup=Get-RmNativePath $backupPlain
 New-RmDirectory (Get-RmParentPath $backupPlain)
 $prepared=[pscustomobject]@{status='prepared';transaction=$Transaction;previousExists=(Test-RmFileExists ($OutputRoot+'\associations.json'));previousStateSha256=(Get-RmByteDigest ($OutputRoot+'\associations.json'))}
 Write-RmJsonAtomic ($OutputRoot+'\History\'+$Transaction.transactionId+'_journal.json') $prepared
 try {
  Assert-RmAssociations3 $State;$wire=ConvertTo-RmWireAssociations $State;Assert-RmWireAssociations $wire
  $bytes=(New-Object Text.UTF8Encoding($false)).GetBytes((ConvertTo-Json -InputObject $wire -Depth 80))
  $s=Open-RmFile $tmp ([IO.FileMode]::CreateNew) ([IO.FileAccess]::Write) ([IO.FileShare]::None) -WriteThrough
  try{$s.Write($bytes,0,$bytes.Length);if($FailurePoint -eq 'BeforeFlush'){throw 'INJECTED_BEFORE_FLUSH'};$s.Flush($true)}finally{$s.Dispose()}
  $validated=Read-RmStrictJson $tmp;Assert-RmWireAssociations $validated
  if($FailurePoint -eq 'BeforeReplace'){throw 'INJECTED_BEFORE_REPLACE'}
  if(Test-RmFileExists ($OutputRoot+'\associations.json')){Replace-RmFile ($OutputRoot+'\associations.json') $tmp $backupPlain}else{Move-RmFile $tmp ($OutputRoot+'\associations.json')}
  if($FailurePoint -eq 'AfterReplace'){throw 'INJECTED_AFTER_REPLACE'}
  $prepared.status='committed';Write-RmJsonAtomic ($OutputRoot+'\History\'+$Transaction.transactionId+'_journal.json') $prepared
 }finally{if(Test-RmFileExists $tmp){Remove-RmFile $tmp}}
}
function Invoke-RmDecisionCommit {
 [CmdletBinding()]param([string]$OutputRoot,[string]$PreviewId,[string]$ConfirmedPreviewDigest,[switch]$ConfirmExplicitly,[ValidateSet('Fixture','Real')][string]$CommitMode='Fixture',[string]$RealAuthorizationPath,[string]$RealApprovalPhrase,[ValidateSet('None','BeforeFlush','BeforeReplace','AfterReplace','AfterPublication')][string]$FailurePoint='None')
 if(!$ConfirmExplicitly){throw 'EXPLICIT_PREVIEW_CONFIRMATION_REQUIRED'};Assert-RmUuid $PreviewId
 $lock=Open-RmStateLock $OutputRoot
 $sourceLeases=New-Object 'System.Collections.Generic.List[object]'
 try{
  $preview=Read-RmStrictJson ($OutputRoot+'\Previews\'+$PreviewId+'.json')
  $saved=$preview.previewDigest;$preview.previewDigest=$null;$computed=Get-RmDigest $preview;$preview.previewDigest=$saved
  if($saved -ne $computed-or$saved -ne $ConfirmedPreviewDigest){throw 'PREVIEW_DIGEST_MISMATCH'}
  $registry=Read-RmStrictJson ($OutputRoot+'\identity_registry.json');$state=ConvertTo-RmAssociations3 (Get-RmAssociations $OutputRoot -ReadOnly) $registry.libraryId
  $expectedDecisionIds=@($preview.operations|ForEach-Object{if($_.operation -eq 'create'){$_.decision.decisionId;if($_.decision.supersedes){$_.decision.supersedes}}else{$_.decisionId}})
  $existing=Get-RmCommitPublicationState -OutputRoot $OutputRoot -RequestId $preview.requestId -ExpectedRequestDigest $preview.requestDigest -PreviewId $PreviewId -PreviewDigest $ConfirmedPreviewDigest -ExpectedDecisionIds $expectedDecisionIds
  if($existing.state -eq 'RECOVERY_REQUIRED'){throw ('COMMIT_RECOVERY_REQUIRED: '+$existing.reason)}
  if($existing.authoritativePublished){
   $lock.Dispose();$lock=$null
   $post=Invoke-RmPostCommitControl $OutputRoot $preview.rootObserved $existing.transaction 'already-committed'
   Set-RmProperty $post 'publicationState' $existing.state
   Set-RmProperty $post 'automaticReplayPrevented' $true
   return $post
  }
  if($existing.state -eq 'FAILED_BEFORE_PUBLICATION'){throw 'PREVIOUS_COMMIT_ATTEMPT_FAILED_BEFORE_PUBLICATION_REVALIDATION_REQUIRED'}
  if($CommitMode -eq 'Fixture'){if(!$preview.fixtureCommitAllowed-or!(Test-RmFixtureRoot $preview.rootObserved)){throw 'REAL_LIBRARY_COMMIT_REQUIRES_EXPLICIT_REAL_MODE'}}
  else {if($preview.fixtureCommitAllowed-or(Test-RmFixtureRoot $preview.rootObserved)){throw 'REAL_COMMIT_MODE_REQUIRES_REAL_LIBRARY'};$null=Assert-RmRealCommitAuthorization $OutputRoot $preview $RealAuthorizationPath $RealApprovalPhrase}
  $latest=Read-RmStrictJson ($OutputRoot+'\Previews\latest.json');if($latest.previewId -ne $PreviewId){throw 'STALE_PREVIEW_RECREATE_REQUIRED'}
  $catalogue=Read-RmStrictJson ($OutputRoot+'\catalogue.json');if($catalogue.scan.scanId -ne $preview.scanId){throw 'STALE_SCAN_RESCAN_REQUIRED'}
  # Hold the XML/launcher sources and chosen unit files open without allowing
  # writes or deletion during preflight/publication. This reads no ROM bytes.
  $leasePaths=@($catalogue.physicalSnapshot.smallSourceDigests|ForEach-Object{$_.relativePath})
  foreach($op in $preview.operations){if($op.operation -eq 'create'-and$op.decision.target-and$op.decision.target.scope -eq 'unit'){$o=Get-RmTargetObservation $catalogue $op.decision.target;foreach($id in $o.unit.fileIds){$leasePaths+=@(@($catalogue.files|Where-Object{$_.fileId -eq $id})[0].relativePath)}}}
  foreach($relative in $leasePaths|Sort-Object -Unique){$sourceLeases.Add((Open-RmFile ($catalogue.scan.rootObserved+'\'+$relative.Replace('/','\')) ([IO.FileMode]::Open) ([IO.FileAccess]::Read) ([IO.FileShare]::Read)))}
  Assert-RmFreshScan $catalogue $OutputRoot $preview.guards
  $after=Get-RmPlannedState $state $preview.operations $catalogue $registry
  foreach($op in $preview.operations){if($op.operation -eq 'create'){Set-RmProperty $op.decision.acceptedPreconditions 'previewDigest' $ConfirmedPreviewDigest;Set-RmProperty $op.decision.acceptedPreconditions 'stateGuards' $preview.guards}}
  $after.revision=New-RmId
  $tx=[pscustomobject]@{transactionId=(New-RmId);requestId=$preview.requestId;requestDigest=$preview.requestDigest;previewId=$PreviewId;previewDigest=$ConfirmedPreviewDigest;committedAtUtc=[DateTime]::UtcNow.ToString('o');revisionBefore=$state.revision;identityRevisionBefore=$registry.revision;catalogueScanBefore=$catalogue.scan.scanId;revisionAfter=$after.revision;stateBeforeSha256=$preview.guards.associationsSha256;decisionIds=$expectedDecisionIds;operations=$preview.operations;changes=@()}
  foreach($id in $tx.decisionIds){$before=@($state.decisions|Where-Object{$_.decisionId -eq $id});$current=@($after.decisions|Where-Object{$_.decisionId -eq $id})[0];$tx.changes+=@([pscustomobject]@{decisionId=$id;fromState=$(if($before.Count){$before[0].state}else{$null});toState=$current.state;supersedesDecisionId=$current.supersedes});$current.lifecycle+=@([pscustomobject]@{event=$(if($before.Count){$current.state}else{'created'});transactionId=$tx.transactionId;atUtc=$tx.committedAtUtc;reason=$current.note})}
  $after.updatedAtUtc=$tx.committedAtUtc;$after.lastTransactionId=$tx.transactionId
  $after.transactions+=@($tx);Assert-RmAssociations3 $after
  # Recheck after constructing the full plan; publisher changes no source bytes.
  Assert-RmFreshScan $catalogue $OutputRoot $preview.guards
  $writerFailure=$(if($FailurePoint -eq 'AfterPublication'){'None'}else{$FailurePoint})
  try{
   Write-RmAssociationsTransaction $OutputRoot $after $tx $writerFailure
   if($FailurePoint -eq 'AfterPublication'){throw 'INJECTED_AFTER_PUBLICATION'}
  }catch{
   $publicationError=$_.Exception.Message
   $outcome=Get-RmCommitPublicationState -OutputRoot $OutputRoot -RequestId $preview.requestId -ExpectedRequestDigest $preview.requestDigest -PreviewId $PreviewId -PreviewDigest $ConfirmedPreviewDigest -ExpectedDecisionIds $expectedDecisionIds -ExpectedTransactionId $tx.transactionId -FailureObserved -PostCheckFailed -FailureMessage $publicationError
   foreach($lease in $sourceLeases){$lease.Dispose()};$sourceLeases.Clear();if($lock){$lock.Dispose();$lock=$null}
   if($outcome.state -eq 'COMMITTED_BUT_POSTCHECK_FAILED'){
    $post=Invoke-RmPostCommitControl $OutputRoot $preview.rootObserved $tx 'committed-postcheck-recovered'
    Set-RmProperty $post 'publicationState' 'COMMITTED_BUT_POSTCHECK_FAILED'
    Set-RmProperty $post 'originalError' $publicationError
    Set-RmProperty $post 'automaticReplayPrevented' $true
    Set-RmProperty $post 'recoveryState' $outcome
    return $post
   }
   if($outcome.state -eq 'RECOVERY_REQUIRED'){throw ('COMMIT_RECOVERY_REQUIRED: '+$publicationError+' | '+$outcome.reason)}
   throw
  }
  foreach($lease in $sourceLeases){$lease.Dispose()};$sourceLeases.Clear();$lock.Dispose();$lock=$null
  $post=Invoke-RmPostCommitControl $OutputRoot $preview.rootObserved $tx 'committed'
  if(!$post.PSObject.Properties['publicationState']){Set-RmProperty $post 'publicationState' $(if($post.controlScanStatus -eq 'complete'){'COMMITTED'}else{'COMMITTED_BUT_POSTCHECK_FAILED'})}
  Set-RmProperty $post 'automaticReplayPrevented' $true
  return $post
 }finally{foreach($lease in $sourceLeases){$lease.Dispose()};if($lock){$lock.Dispose()}}
}
function Invoke-RmPostCommitControl {
 param([string]$OutputRoot,[string]$Root,$Transaction,[string]$Status)
 try{
  $c=Invoke-RmScan $Root $OutputRoot
  return [pscustomobject]@{status=$Status;publicationState='COMMITTED';transaction=$Transaction;controlScanStatus=$c.scan.status;controlScanId=$c.scan.scanId;currentDecisionEffects=@($c.decisionEffects|Where-Object{$_.decisionId -in $Transaction.decisionIds});cacheRefreshRequired=$false;sourceWrites=0}
 }catch{
  return [pscustomobject]@{status=$Status;publicationState='COMMITTED_BUT_POSTCHECK_FAILED';transaction=$Transaction;controlScanStatus='failed';controlScanError=$_.Exception.Message;currentDecisionEffects=@();cacheRefreshRequired=$true;sourceWrites=0}
 }
}
function Get-RmTransactionRecovery {
 param([string]$OutputRoot)
 $reg=Read-RmStrictJson ($OutputRoot+'\identity_registry.json');$state=ConvertTo-RmAssociations3 (Get-RmAssociations $OutputRoot -ReadOnly) $reg.libraryId
 $dir=$OutputRoot+'\History';if(!(Test-RmDirectoryExists $dir)){return @()}
 return @((Get-RmDirectoryEntries $dir|Where-Object{$_.Name -like '*_journal.json'}|ForEach-Object{$journalPath=$dir+'\'+$_.Name;$j=Read-RmStrictJson $journalPath;$tx=@($state.transactions|Where-Object{$_.transactionId -eq $j.transaction.transactionId});[pscustomobject]@{transactionId=$j.transaction.transactionId;journalStatus=$j.status;authoritativeStatus=$(if($tx.Count){'committed'}else{'not-committed'});previousStateRecoverable=((Test-RmFileExists ($dir+'\associations_'+$j.transaction.transactionId+'_before.json'))-or$j.previousStateSha256 -eq (Get-RmByteDigest ($OutputRoot+'\associations.json')))}}))
}
Export-ModuleMember -Function New-RmDecisionPreview,New-RmRealCommitAuthorization,Assert-RmRealCommitAuthorization,Assert-RmBatchCommitAuthorizationContract,Invoke-RmDecisionCommit,Get-RmCommitPublicationState,Get-RmTransactionRecovery,Assert-RmFreshScan
