Set-StrictMode -Version 2.0
# The internal execution DTO is not the on-disk contract. This boundary gives
# associations.json the field names and policies of the validated Rev.3.
function ConvertTo-RmWireDecision {
 param($Decision)
 $d=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Decision -Depth 80)
 $p=$d.payload;$origin=$d.origin
 Set-RmProperty $origin 'proposalNumber' $origin.number;$origin.PSObject.Properties.Remove('number')
 if($d.source){Set-RmProperty $d.source 'observedXmlRelativePath' $d.source.relativeXmlPath;$d.source.PSObject.Properties.Remove('relativeXmlPath')}
 $accepted=$d.acceptedPreconditions
 $observed=@();foreach($prop in $p.unitObservationDigests.PSObject.Properties){$observed+=@([pscustomobject]@{romUnitId=$prop.Name;observationDigest=$prop.Value})}
 Set-RmProperty $accepted 'observedUnitDigests' $observed
 Set-RmProperty $accepted 'candidateSetDigest' (Get-RmProperty $p 'candidateSetDigest')
 $depends=@($d.validityPolicy.explicitDependencies|Where-Object{$_.type -eq 'decision'}|ForEach-Object{$_.decisionId})
 $wirePayload=[ordered]@{dependsOnDecisionIds=$depends}
 switch -Wildcard ($d.type){
  'metadata.link_*' {
   $wirePayload.displayPreference='none';$wirePayload.contentIdentity='not-verified'
   if((Get-RmProperty $p 'mode')-eq'human_ambiguous'){$wirePayload.humanChoice=[pscustomobject]@{mode='human_ambiguous';romUnitId=$d.target.romUnitId;justification=$p.justification;binaryIdentityNotProvenAcknowledged=$p.binaryIdentityNotProvenAcknowledged;allCandidates=$p.candidateSet}}
  }
  'metadata.prefer_source' {
   $wirePayload.preferredSource=[pscustomobject]@{documentId=$d.source.documentId;metadataEntryId=$d.source.metadataEntryId;mode='follow_current';versionIdAtApproval=$d.source.versionId}
   $wirePayload.fields=$p.fields;if($p.fields -contains '*'){$wirePayload.fields='all'}
   $wirePayload.fallback='none';if(Get-RmProperty $p 'allowMissingFallback' $false){$wirePayload.fallback='missing-only'}
  }
  'metadata.override_display' {$values=[ordered]@{};$values[$p.fields[0]]=$p.value;$wirePayload.values=[pscustomobject]$values;$wirePayload.provenance='user'}
  'variant.relate' {
   $wirePayload.relationKind=$p.relation.Replace('-','_');$wirePayload.participants=@([pscustomobject]@{gameId=$d.target.gameId;variantId=$d.target.variantId},[pscustomobject]@{gameId=$p.otherTarget.gameId;variantId=$p.otherTarget.variantId})|Sort-Object variantId
   $wirePayload.contentIdentity='not-verified';$d.target=$null
  }
  'proposal.*' {$wirePayload.proposalScope=[pscustomobject]@{proposalFamily='metadata_reconciliation';proposalSemanticDigest=$origin.proposalSemanticDigest;semanticModelVersion=$origin.semanticModelVersion;sourceVersionIdAtDecision=$d.source.versionId;classification=$origin.classification;candidateSetDigestAtDecision=(Get-RmProperty $p 'candidateSetDigest')};$wirePayload.reason=Get-RmProperty $p 'reason'}
  'metadata.keep_historical' {$wirePayload.fixedVersionId=$d.source.versionId;$wirePayload.selectable=$false;$wirePayload.reason=Get-RmProperty $p 'reason'}
 }
 $d.payload=[pscustomobject]$wirePayload
 $policy=$d.validityPolicy
 $policy.sourceMode=switch($policy.sourceMode){identity{'follow_identity'}snapshot{'fixed_snapshot'}default{$policy.sourceMode}}
 $policy.contentMode=switch($policy.contentMode){frozen{'fixed_version'}default{$policy.contentMode}}
 $policy.targetMode=switch($policy.targetMode){identity{'scope_ids'}independent{'none'}default{$policy.targetMode}}
 if($d.type -eq 'variant.relate'){$policy.targetMode='participants_ids'}
 $policy.candidateSetMode=switch($policy.candidateSetMode){exact{'track_set'}default{'none'}}
 if($policy.proposalMode -eq 'audit'){$policy.proposalMode='none'}
 Set-RmProperty $d 'userNote' $d.note;$d.PSObject.Properties.Remove('note')
 Set-RmProperty $d 'supersedesDecisionId' $d.supersedes;$d.PSObject.Properties.Remove('supersedes')
 return $d
}
function ConvertFrom-RmWireDecision {
 param($Decision,$Registry)
 $d=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Decision -Depth 80)
 $origin=$d.origin;Set-RmProperty $origin 'number' $origin.proposalNumber;$origin.PSObject.Properties.Remove('proposalNumber')
 if($d.source){Set-RmProperty $d.source 'relativeXmlPath' $d.source.observedXmlRelativePath;$d.source.PSObject.Properties.Remove('observedXmlRelativePath')}
 $wire=$d.payload;$p=[ordered]@{};$unitObs=[ordered]@{}
 foreach($o in $d.acceptedPreconditions.observedUnitDigests){$unitObs[$o.romUnitId]=$o.observationDigest}
 $p.unitObservationDigests=[pscustomobject]$unitObs
 if($d.acceptedPreconditions.candidateSetDigest){$p.candidateSetDigest=$d.acceptedPreconditions.candidateSetDigest}
 $d.acceptedPreconditions.PSObject.Properties.Remove('observedUnitDigests');$d.acceptedPreconditions.PSObject.Properties.Remove('candidateSetDigest')
 switch -Wildcard ($d.type){
  'metadata.link_*' {if($wire.PSObject.Properties['humanChoice']){$c=$wire.humanChoice;$p.mode=$c.mode;$p.justification=$c.justification;$p.binaryIdentityNotProvenAcknowledged=$c.binaryIdentityNotProvenAcknowledged;$p.candidateSet=$c.allCandidates}}
  'metadata.prefer_source' {$p.fields=@($wire.fields);if($wire.fields -ceq 'all'){$p.fields=@('*')};$p.allowMissingFallback=$wire.fallback -eq 'missing-only'}
  'metadata.override_display' {$keys=@($wire.values.PSObject.Properties);if($keys.Count -ne 1){throw 'MULTIFIELD_OVERRIDE_REQUIRES_SEPARATE_DECISIONS'};$p.fields=@($keys[0].Name);$p.value=$keys[0].Value}
  'variant.relate' {$parts=@($wire.participants);$d.target=[pscustomobject]@{scope='variant';gameId=$parts[0].gameId;variantId=$parts[0].variantId};$p.otherTarget=[pscustomobject]@{scope='variant';gameId=$parts[1].gameId;variantId=$parts[1].variantId};$p.relation=$wire.relationKind.Replace('_','-')}
  'proposal.*' {$p.reason=Get-RmProperty $wire 'reason';$p.candidateSetDigest=$wire.proposalScope.candidateSetDigestAtDecision}
  'metadata.keep_historical' {
   $record=@($Registry.metadataEntries|Where-Object{$_.metadataEntryId -eq $d.source.metadataEntryId});$version=@($record|ForEach-Object{$_.versions}|Where-Object{$_.versionId -eq $wire.fixedVersionId})
   if($version.Count -ne 1){throw 'HISTORICAL_VERSION_UNAVAILABLE_REGISTRY_REQUIRED'}
   $v=$version[0];$p.historicalSnapshot=[pscustomobject]@{raw=$v.raw;rawEntryXml=$v.rawEntryXml;versionId=$v.versionId;contentVersionId=$v.contentVersionId;runtimeStatsVersionId=$v.runtimeStatsVersionId;selectable=$false};$p.reason=Get-RmProperty $wire 'reason'
  }
 }
 $d.payload=[pscustomobject]$p;$policy=$d.validityPolicy
 $policy.sourceMode=switch($policy.sourceMode){follow_identity{'identity'}fixed_snapshot{'snapshot'}default{$policy.sourceMode}}
 $policy.contentMode=switch($policy.contentMode){fixed_version{'frozen'}default{$policy.contentMode}}
 $policy.targetMode=switch($policy.targetMode){scope_ids{'identity'}participants_ids{'identity'}none{'independent'}default{$policy.targetMode}}
 $policy.candidateSetMode=switch($policy.candidateSetMode){track_set{'exact'}default{'independent'}}
 if($policy.proposalMode -eq 'none'){$policy.proposalMode='audit'}
 Set-RmProperty $d 'note' $d.userNote;$d.PSObject.Properties.Remove('userNote');Set-RmProperty $d 'supersedes' $d.supersedesDecisionId;$d.PSObject.Properties.Remove('supersedesDecisionId')
 return $d
}
function ConvertTo-RmWireAssociations {
 param($State)
 $s=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $State -Depth 80)
 Set-RmProperty $s 'legacyHistory' $s.history;$s.PSObject.Properties.Remove('history')
 $s.decisions=@($s.decisions|ForEach-Object{ConvertTo-RmWireDecision $_})
 $s.transactions=@($s.transactions|ForEach-Object{
  $t=$_;$events=@();$changes=@()
  foreach($op in $t.operations){if($op.operation -eq 'create'){$changes+=@([pscustomobject]@{decisionId=$op.decision.decisionId;fromState=$null;toState=$op.decision.state;supersedesDecisionId=$op.decision.supersedes});$events+=@([pscustomobject]@{decisionId=$op.decision.decisionId;type='created';atUtc=$t.committedAtUtc;reason=$op.decision.note})}else{$changes+=@([pscustomobject]@{decisionId=$op.decisionId;fromState='active';toState='revoked';supersedesDecisionId=$null});$events+=@([pscustomobject]@{decisionId=$op.decisionId;type='revoked';atUtc=$t.committedAtUtc;reason=$op.note})}}
  $action='apply';if(@($t.operations|Where-Object{$_.operation -eq 'revoke'}).Count){$action='revoke'};if(@($t.operations|Where-Object{$_.operation -eq 'create'-and$_.decision.supersedes}).Count){$action='replace'}
  [pscustomobject]@{transactionId=$t.transactionId;requestId=$t.requestId;requestDigest=$t.requestDigest;action=$action;committedAtUtc=$t.committedAtUtc;actor='user';expectedScanId=(Get-RmProperty $t 'catalogueScanBefore');revisionsBefore=[pscustomobject]@{associations=$(if($t.stateBeforeSha256 -eq 'absent'){$null}else{$t.revisionBefore});identityRegistry=(Get-RmProperty $t 'identityRevisionBefore');catalogue=(Get-RmProperty $t 'catalogueScanBefore')};revisionAfter=$t.revisionAfter;previewId=$t.previewId;previewDigest=(Get-RmProperty $t 'previewDigest');decisionIds=$t.decisionIds;changes=$(if($t.PSObject.Properties['changes']){$t.changes}else{$changes});previousStateSha256=$(if($t.stateBeforeSha256 -eq 'absent'){$null}else{$t.stateBeforeSha256});backupRelativePath=$(if($t.stateBeforeSha256 -eq 'absent'){$null}else{'History/associations_'+$t.transactionId+'_before.json'});reason=$null;events=$events;executionOperations=$t.operations}
 })
 return $s
}
function ConvertFrom-RmWireAssociations {
 param($State,$Registry)
 Assert-RmWireAssociations $State
 $s=ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $State -Depth 80)
 Set-RmProperty $s 'history' $s.legacyHistory;$s.PSObject.Properties.Remove('legacyHistory')
 $s.decisions=@($s.decisions|ForEach-Object{ConvertFrom-RmWireDecision $_ $Registry})
 $s.transactions=@($s.transactions|ForEach-Object{$t=$_;[pscustomobject]@{transactionId=$t.transactionId;requestId=$t.requestId;requestDigest=$t.requestDigest;previewId=$t.previewId;previewDigest=$t.previewDigest;committedAtUtc=$t.committedAtUtc;revisionBefore=$t.revisionsBefore.associations;identityRevisionBefore=$t.revisionsBefore.identityRegistry;catalogueScanBefore=$t.revisionsBefore.catalogue;revisionAfter=$t.revisionAfter;stateBeforeSha256=$(if($t.previousStateSha256){$t.previousStateSha256}else{'absent'});decisionIds=$t.decisionIds;operations=$t.executionOperations;changes=$t.changes}})
 return $s
}
function Assert-RmWireAssociations {
 param($State)
 Assert-RmFields $State @('schemaVersion','applicationVersion','libraryId','revision','createdAtUtc','updatedAtUtc','lastTransactionId','gameMerges','variantMerges','mediaAssignments','gameMediaAssignments','legacyHistory','decisions','transactions') @('schemaVersion','applicationVersion','libraryId','revision','createdAtUtc','updatedAtUtc','lastTransactionId','legacyHistory','decisions','transactions')
 if($State.schemaVersion -ne '3.0.0'){throw 'WIRE_ASSOCIATIONS_SCHEMA_REQUIRED'}
 Assert-RmUuid $State.libraryId;Assert-RmUuid $State.revision;Assert-RmUtcDate $State.createdAtUtc;Assert-RmUtcDate $State.updatedAtUtc
 foreach($d in $State.decisions){
  Assert-RmFields $d @('decisionId','createdAtUtc','createdBy','type','state','origin','source','target','payload','acceptedPreconditions','validityPolicy','userNote','supersedesDecisionId','lifecycle') @('decisionId','type','state','origin','payload','validityPolicy','userNote','supersedesDecisionId')
  Assert-RmUuid $d.decisionId
  Assert-RmUtcDate $d.createdAtUtc
  Assert-RmSha256 $d.origin.proposalSemanticDigest
  if($d.origin.confidenceInitial -notin @($null,'HIGH','MEDIUM','NONE')){throw 'CONFIDENCE_ENUM_INVALID'}
  if($d.source){foreach($n in @('identityAnchorSha256','acceptedContentSha256','acceptedDocumentByteSha256')){Assert-RmSha256 (Get-RmProperty $d.source $n)}}
  foreach($o in $d.acceptedPreconditions.observedUnitDigests){Assert-RmSha256 $o.observationDigest}
  if($d.validityPolicy.sourceMode -notin @('follow_identity','fixed_snapshot','semantic_proposal','independent')-or$d.validityPolicy.contentMode -notin @('follow_current','fixed_version','independent')-or$d.validityPolicy.targetMode -notin @('scope_ids','unit_observation','participants_ids','none')-or$d.validityPolicy.candidateSetMode -notin @('track_set','none')-or$d.validityPolicy.proposalMode -notin @('semantic_digest','none')){throw 'WIRE_POLICY_ENUM_INVALID'}
  $allowed=@('dependsOnDecisionIds')
  switch -Wildcard($d.type){'metadata.link_*'{$allowed+=@('displayPreference','contentIdentity','humanChoice')}'metadata.prefer_source'{$allowed+=@('preferredSource','fields','fallback')}'metadata.override_display'{$allowed+=@('values','provenance')}'variant.relate'{$allowed+=@('relationKind','participants','contentIdentity')}'proposal.*'{$allowed+=@('proposalScope','reason')}'metadata.keep_historical'{$allowed+=@('fixedVersionId','selectable','reason')}default{throw 'UNKNOWN_WIRE_DECISION_TYPE'}}
  Assert-RmFields $d.payload $allowed
  $expected=@(Sort-RmOrdinal @($d.validityPolicy.explicitDependencies|Where-Object{$_.type -eq 'decision'}|ForEach-Object{$_.decisionId}));$declared=@(Sort-RmOrdinal @($d.payload.dependsOnDecisionIds))
  if(($expected -join '|')-ne($declared -join '|')){throw 'WIRE_DEPENDENCY_MISMATCH'}
  if($d.type -like 'metadata.link_*'-or$d.type -eq 'metadata.prefer_source'){if($d.validityPolicy.sourceMode -ne 'follow_identity'-or$d.validityPolicy.contentMode -ne 'follow_current'){throw 'LINK_PREFERENCE_MUST_FOLLOW_IDENTITY_CONTENT'}}
  if($d.type -eq 'metadata.override_display'-and($d.validityPolicy.sourceMode -ne 'independent'-or$d.validityPolicy.contentMode -ne 'independent'-or$d.source)){throw 'OVERRIDE_MUST_BE_INDEPENDENT'}
  if($d.type -eq 'metadata.keep_historical'-and($d.validityPolicy.sourceMode -ne 'fixed_snapshot'-or$d.validityPolicy.contentMode -ne 'fixed_version'-or$d.target)){throw 'HISTORICAL_FIXED_VERSION_REQUIRED'}
  if($d.type -in @('proposal.reject','proposal.defer')-and($d.validityPolicy.sourceMode -ne 'semantic_proposal'-or$d.validityPolicy.proposalMode -ne 'semantic_digest'-or$d.target)){throw 'SEMANTIC_REJECT_DEFER_POLICY_REQUIRED'}
  if($d.type -like 'metadata.link_*'-and($d.payload.displayPreference -ne 'none'-or$d.payload.contentIdentity -ne 'not-verified')){throw 'IMPLICIT_LINK_PREFERENCE_FORBIDDEN'}
  if($d.type -eq 'metadata.override_display'){if($d.payload.provenance -ne 'user'){throw 'OVERRIDE_PROVENANCE_INVALID'};foreach($p in $d.payload.values.PSObject.Properties){Assert-RmMetadataValue $p.Name $p.Value}}
  if($d.type -eq 'metadata.keep_historical'-and$d.payload.selectable -ne $false){throw 'HISTORICAL_MUST_NOT_BE_SELECTABLE'}
  if($d.type -eq 'metadata.prefer_source'-and($d.payload.preferredSource.mode -ne 'follow_current'-or$d.payload.fallback -notin @('none','missing-only'))){throw 'PREFERENCE_FOLLOW_CURRENT_REQUIRED'}
  if($d.type -eq 'metadata.prefer_source'){Assert-RmFields $d.payload.preferredSource @('documentId','metadataEntryId','mode','versionIdAtApproval') @('metadataEntryId','mode');if($d.payload.preferredSource.metadataEntryId -ne $d.source.metadataEntryId){throw 'PREFERRED_SOURCE_ID_MISMATCH'}}
  if($d.type -in @('proposal.reject','proposal.defer')){Assert-RmFields $d.payload.proposalScope @('proposalFamily','proposalSemanticDigest','semanticModelVersion','sourceVersionIdAtDecision','classification','candidateSetDigestAtDecision') @('proposalSemanticDigest');if($d.payload.proposalScope.proposalSemanticDigest -ne $d.origin.proposalSemanticDigest){throw 'SEMANTIC_SCOPE_MISMATCH'}}
  if($d.payload.PSObject.Properties['humanChoice']){Assert-RmFields $d.payload.humanChoice @('mode','romUnitId','justification','binaryIdentityNotProvenAcknowledged','allCandidates') @('mode','romUnitId','justification','binaryIdentityNotProvenAcknowledged','allCandidates');if($d.payload.humanChoice.romUnitId -ne $d.target.romUnitId-or!$d.payload.humanChoice.binaryIdentityNotProvenAcknowledged-or[string]::IsNullOrWhiteSpace($d.payload.humanChoice.justification)){throw 'HUMAN_CHOICE_EVIDENCE_REQUIRED'}}
 }
 foreach($t in $State.transactions){Assert-RmFields $t @('transactionId','requestId','requestDigest','action','committedAtUtc','actor','expectedScanId','revisionsBefore','revisionAfter','previewId','previewDigest','decisionIds','changes','previousStateSha256','backupRelativePath','reason','events','executionOperations') @('transactionId','requestId','action','previewDigest','changes','events','executionOperations');Assert-RmUuid $t.transactionId;Assert-RmUuid $t.requestId;Assert-RmUtcDate $t.committedAtUtc;if($t.action -notin @('apply','revoke','replace')){throw 'TRANSACTION_ACTION_INVALID'}}
}
function Assert-RmUtcDate {
 param($Value)
 $parsed=[DateTimeOffset]::MinValue
 if($Value -isnot [string]-or!($Value.EndsWith('Z')-or$Value.EndsWith('+00:00'))-or![DateTimeOffset]::TryParse($Value,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::None,[ref]$parsed)-or$parsed.Offset -ne [TimeSpan]::Zero){throw 'UTC_ISO_DATE_REQUIRED'}
}
function Assert-RmSha256 {param($Value) if($Value -isnot [string]-or$Value -cnotmatch '^[0-9a-f]{64}$'){throw 'SHA256_HEX_REQUIRED'}}
Export-ModuleMember -Function *-Rm*
