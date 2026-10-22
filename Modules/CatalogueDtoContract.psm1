# T3 canonical candidate bridge and independent DTO validator.
# Pure in-memory; does not read files or invoke the catalogue producer from the validator.
Set-StrictMode -Version 2.0
function Assert-RmDto {
 param([bool]$Condition,[string]$Code)
 if (-not $Condition) { throw $Code }
}
function Get-RmDtoField {
 param($Object,[string]$Name)
 if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) { throw ('CATDTO_FIELD_MISSING:'+$Name) }
 return ,$Object.PSObject.Properties[$Name].Value
}
function Assert-RmDtoId {
 param($Value,[string]$Code)
 Assert-RmDto ($Value -is [string] -and -not [string]::IsNullOrWhiteSpace($Value)) $Code
}
function Assert-RmDtoUtc {
 param($Value)
 Assert-RmDto ($Value -is [string] -and $Value -cmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z
 return (New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal))
}
function Get-RmDtoKey {
 param([string[]]$Parts)
 $key=''
 foreach($part in $Parts) { $key+=([string]$part.Length)+':'+$part }
 return $key
}
function Assert-RmCanonicalCatalogueDto {
 [CmdletBinding()]
 param([Parameter(Mandatory=$true)]$Dto)
 Assert-RmDto ((Get-RmDtoField $Dto 'schemaVersion') -ceq '0.2.0-ui-catalogue-candidate') 'CATDTO_SCHEMA_UNSUPPORTED'
 Assert-RmDto ((Get-RmDtoField $Dto 'readOnly') -ceq $true) 'CATDTO_READONLY_REQUIRED'
 $registry=Get-RmDtoField $Dto 'registry'
 Assert-RmDto ((Get-RmDtoField $registry 'schemaVersion') -ceq '0.1.0-synthetic') 'CATDTO_REGISTRY_SCHEMA'
 Assert-RmDto ((Get-RmDtoField $registry 'readOnly') -ceq $true) 'CATDTO_REGISTRY_READONLY'
 $libraries=Get-RmDtoField $registry 'libraries'
 $games=Get-RmDtoField $Dto 'games'
 $diagnostics=Get-RmDtoField $Dto 'diagnostics'
 Assert-RmDto ($libraries -is [array] -and $games -is [array] -and $diagnostics -is [array]) 'CATDTO_ARRAY_REQUIRED'
 $libKeys=New-RmDtoSet
 $libMap=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
 $allProven=$true
 $latestByLibrary=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
 $observationsByLibrary=New-Object 'System.Collections.Generic.Dictionary[string,int]' ([StringComparer]::Ordinal)
 foreach($lib in $libraries) {
  $lid=Get-RmDtoField $lib 'libraryId';Assert-RmDtoId $lid 'CATDTO_LIBRARY_ID'
  Assert-RmDto ($libKeys.Add($lid)) 'CATDTO_DUPLICATE_LIBRARY'
  $libMap.Add($lid,$lib)
  $index=Get-RmDtoField $lib 'indexAccessible'
  Assert-RmDto ($index -is [bool]) 'CATDTO_INDEX_INVALID'
  if (-not $index) { $allProven=$false }
  $sources=Get-RmDtoField $lib 'sources'
  $platforms=Get-RmDtoField $lib 'platforms'
  $generations=Get-RmDtoField $lib 'generations'
  Assert-RmDto ($sources -is [array] -and $platforms -is [array] -and $generations -is [array]) 'CATDTO_REGISTRY_ARRAY'
  $snapshotIds=New-RmDtoSet;$generationIds=New-RmDtoSet
  $latest=$null
  foreach($gen in $generations) {
   $sid=Get-RmDtoField $gen 'snapshotId';$gid=Get-RmDtoField $gen 'generationId'
   Assert-RmDtoId $sid 'CATDTO_SNAPSHOT_ID';Assert-RmDtoId $gid 'CATDTO_GENERATION_ID'
   Assert-RmDto ($snapshotIds.Add($sid) -and $generationIds.Add($gid)) 'CATDTO_DUPLICATE_SNAPSHOT_GENERATION'
   $captured=Get-RmDtoField $gen 'capturedAtUtc'
   Assert-RmDtoUtc $captured
   if ($null -eq $latest -or [string]::CompareOrdinal($captured,$latest.capturedAtUtc) -gt 0 -or
      ($captured -ceq $latest.capturedAtUtc -and [string]::CompareOrdinal($sid,$latest.snapshotId) -gt 0)) { $latest=$gen }
   $proof=Get-RmDtoField $gen 'coverageProof'
   $coverage=Get-RmDtoField $gen 'coverage'
   $scan=Get-RmDtoField $gen 'scanStatus'
   $exhaustive=Get-RmDtoField $gen 'scopeIsExhaustive'
   Assert-RmDto ($proof -is [array] -and $coverage -is [array] -and $exhaustive -is [bool]) 'CATDTO_COVERAGE_FIELDS'
   # Recompute platform coverage from exact declared source/root/domain partitions.
   # A global "all covered" boolean is insufficient for mixed-platform snapshots.
   Assert-RmDto (@('complete','partial','failed') -ccontains $scan) 'CATDTO_SCAN_STATUS_INVALID'
   $expected=New-RmDtoSet
   $expectedByConsole=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
   $platformKeys=New-RmDtoSet
   foreach($platform in $platforms) {
    Assert-RmDtoId $platform 'CATDTO_PLATFORM_INVALID'
    Assert-RmDto ($platformKeys.Add($platform)) 'CATDTO_DUPLICATE_PLATFORM'
    $expectedByConsole.Add($platform,(New-RmDtoSet))
   }
   $sourceKeys=New-RmDtoSet
   foreach($source in $sources) {
    $sourceId=Get-RmDtoField $source 'sourceKey'
    $rootId=Get-RmDtoField $source 'rootKey'
    Assert-RmDtoId $sourceId 'CATDTO_SOURCE_ID'
    Assert-RmDtoId $rootId 'CATDTO_ROOT_ID'
    Assert-RmDto ($sourceKeys.Add($sourceId)) 'CATDTO_DUPLICATE_SOURCE'
    $domains=Get-RmDtoField $source 'domains'
    Assert-RmDto ($domains -is [array] -and $domains.Count -gt 0) 'CATDTO_SOURCE_DOMAINS'
    $domainKeys=New-RmDtoSet
    foreach($domain in $domains) {
     Assert-RmDto (@('ROMS','MEDIAS') -ccontains $domain -and $domainKeys.Add($domain)) 'CATDTO_DOMAIN_INVALID'
     foreach($platform in $platforms) {
      $k=Get-RmDtoKey @($platform,$sourceId,$rootId,$domain)
      [void]$expected.Add($k)
      [void]$expectedByConsole[$platform].Add($k)
     }
    }
   }
   Assert-RmDto ($platforms.Count -gt 0 -and $expected.Count -gt 0) 'CATDTO_EMPTY_DECLARED_SCOPE'
   $seen=New-RmDtoSet;$proven=New-RmDtoSet
   $partialProof=New-RmDtoSet
   foreach($part in $proof) {
    $console=Get-RmDtoField $part 'console'
    $sourceId=Get-RmDtoField $part 'sourceKey'
    $rootId=Get-RmDtoField $part 'rootKey'
    $domain=Get-RmDtoField $part 'domain'
    $partStatus=Get-RmDtoField $part 'status'
    Assert-RmDto (@('complete','partial','unobserved') -ccontains $partStatus) 'CATDTO_PROOF_STATUS_INVALID'
    $k=Get-RmDtoKey @($console,$sourceId,$rootId,$domain)
    Assert-RmDto ($expected.Contains($k) -and $seen.Add($k)) 'CATDTO_PROOF_PARTITION_INVALID'
    if ($partStatus -ceq 'complete') { [void]$proven.Add($k) }
    if ($partStatus -ceq 'partial') { [void]$partialProof.Add($k) }
   }
   $globallyComplete=($scan -ceq 'complete' -and $exhaustive -and $expected.Count -eq $proven.Count)
   $covKeys=New-RmDtoSet
   foreach($c in $coverage) {
    $console=Get-RmDtoField $c 'console'
    $claimed=Get-RmDtoField $c 'status'
    Assert-RmDto ($platformKeys.Contains($console) -and $covKeys.Add($console)) 'CATDTO_COVERAGE_CONSOLE'
    Assert-RmDto (@('complete','partial','unobserved') -ccontains $claimed) 'CATDTO_COVERAGE_STATUS_INVALID'
    $needed=$expectedByConsole[$console]
    $allConsoleComplete=$true;$hasObserved=$false;$hasPartial=$false
    foreach($k in $needed) {
     if (-not $proven.Contains($k)) { $allConsoleComplete=$false }
     if ($seen.Contains($k)) { $hasObserved=$true }
     if ($partialProof.Contains($k)) { $hasPartial=$true }
    }
    $calculated='unobserved'
    if ($scan -ceq 'complete' -and $exhaustive -and $allConsoleComplete) { $calculated='complete' }
    elseif ($hasPartial -or $hasObserved) { $calculated='partial' }
    if ($claimed -ceq 'complete' -and $calculated -cne 'complete') { throw 'CATDTO_COMPLETE_UNPROVEN' }
    if ($claimed -cne $calculated) {
     if ($calculated -ceq 'complete') { throw 'CATDTO_COMPLETE_COVERAGE_MISMATCH' }
     throw 'CATDTO_COVERAGE_STATUS_MISMATCH'
    }
   }
   Assert-RmDto ($covKeys.Count -eq @($platforms).Count) 'CATDTO_COVERAGE_CONSOLE_MISSING'
   if ($globallyComplete) {
    Assert-RmDto (@($coverage | Where-Object { $_.status -cne 'complete' }).Count -eq 0) 'CATDTO_COMPLETE_COVERAGE_MISMATCH'
   }
   $countKnown=Get-RmDtoField $gen 'countKnown'
   $unitCount=Get-RmDtoField $gen 'observedUnitCount'
   Assert-RmDto ($countKnown -is [bool]) 'CATDTO_GENERATION_COUNT_FLAG'
   if (-not $index) {
    Assert-RmDto (-not $countKnown -and $null -eq $unitCount) 'CATDTO_INDEX_COUNT_FORGED'
   } else {
    Assert-RmDto ($countKnown -and $unitCount -is [int] -and $unitCount -ge 0) 'CATDTO_GENERATION_COUNT_INVALID'
   }
  }
  if ($null -eq $latest) { $allProven=$false }
  else {
   $latestByLibrary.Add($lid,$latest)
   $observationsByLibrary.Add($lid,0)
   if ($latest.scanStatus -cne 'complete' -or -not $latest.scopeIsExhaustive -or
       @($latest.coverage | Where-Object { $_.status -cne 'complete' }).Count -ne 0) { $allProven=$false }
  }
 }
 $keys=New-RmDtoSet;$anyUnproven=$false
 $observationScopeKeys=New-RmDtoSet
 foreach($g in $games) {
  $lid=Get-RmDtoField $g 'libraryId';$gid=Get-RmDtoField $g 'gameId';$platform=Get-RmDtoField $g 'platform'
  Assert-RmDtoId $gid 'CATDTO_GAME_ID';Assert-RmDtoId $platform 'CATDTO_PLATFORM'
  Assert-RmDto ($libMap.ContainsKey($lid)) 'CATDTO_ORPHAN_LIBRARY'
  Assert-RmDto ($keys.Add((Get-RmDtoKey @($lid,$platform,$gid)))) 'CATDTO_DUPLICATE_GAME'
  $lib=$libMap[$lid]
  Assert-RmDto ($lib.indexAccessible -and @($lib.platforms) -ccontains $platform) 'CATDTO_GAME_SOURCE_INVALID'
  Assert-RmDto ((Get-RmDtoField $g 'availability') -ceq (Get-RmDtoField $lib 'currentAvailability') -and
                (Get-RmDtoField $g 'freshness') -ceq (Get-RmDtoField $lib 'freshness')) 'CATDTO_LIBRARY_STATE_FORGED'
  $title=Get-RmDtoField $g 'title'
  Assert-RmDto ($null -eq $title -or ($title -is [string] -and -not [string]::IsNullOrWhiteSpace($title))) 'CATDTO_TITLE_INVALID'
  $quality=Get-RmDtoField $g 'quality';$status=Get-RmDtoField $g 'status'
  $cover=Get-RmDtoField $g 'coverStatus'
  Assert-RmDto (@('KNOWN','STALE','PARTIALLY_OBSERVED','CONTRADICTORY','UNKNOWN') -ccontains $quality) 'CATDTO_QUALITY_INVALID'
  Assert-RmDto (@('KNOWN','INCOMPLETE') -ccontains $status) 'CATDTO_STATUS_INVALID'
  Assert-RmDto (@('KNOWN','UNKNOWN') -ccontains $cover) 'CATDTO_COVER_INVALID'
  $provenance=Get-RmDtoField $g 'provenance'
  $variants=Get-RmDtoField $g 'variants'
  $observations=Get-RmDtoField $g 'observations'
  Assert-RmDto ($provenance -is [array] -and $provenance.Count -eq 1 -and $variants -is [array] -and $observations -is [array] -and $observations.Count -gt 0) 'CATDTO_GAME_ARRAYS'
  $p=$provenance[0]
  Assert-RmDto ($p.libraryId -ceq $lid -and $p.source -ceq 'SYNTHETIC_SNAPSHOT') 'CATDTO_PROVENANCE_INVALID'
  $snap=@($lib.generations | Where-Object { $_.snapshotId -ceq $p.snapshotId })
  Assert-RmDto ($snap.Count -eq 1) 'CATDTO_ORPHAN_SNAPSHOT'
  $snap=$snap[0]
  Assert-RmDto ($snap.generationId -ceq $p.generationId -and $snap.capturedAtUtc -ceq $p.observedAtUtc) 'CATDTO_GENERATION_PROVENANCE_MISMATCH'
  Assert-RmDto ($latestByLibrary.ContainsKey($lid) -and $latestByLibrary[$lid].snapshotId -ceq $p.snapshotId) 'CATDTO_HISTORICAL_SNAPSHOT_AS_LATEST'
  $expectedCover=if ($snap.scanStatus -ceq 'complete' -and $snap.scopeIsExhaustive -and
    @($snap.coverage | Where-Object { $_.status -cne 'complete' }).Count -eq 0) {'KNOWN'} else {'UNKNOWN'}
  Assert-RmDto ($cover -ceq $expectedCover) 'CATDTO_COVERAGE_FORGED'
  Assert-RmDto ($status -ceq $(if($cover -ceq 'KNOWN'){'KNOWN'}else{'INCOMPLETE'})) 'CATDTO_STATUS_FORGED'
  if ($cover -cne 'KNOWN') { $anyUnproven=$true }
  if ($quality -ceq 'KNOWN') { Assert-RmDto ($cover -ceq 'KNOWN' -and $lib.freshness -ceq 'FRESH') 'CATDTO_QUALITY_FORGED' }
  if ($quality -ceq 'STALE') { Assert-RmDto ($lib.freshness -ceq 'STALE') 'CATDTO_STALE_FORGED' }
  if ($quality -ceq 'UNKNOWN') { Assert-RmDto ($lib.freshness -ceq 'UNKNOWN') 'CATDTO_UNKNOWN_QUALITY_FORGED' }
  $variantKeys=New-RmDtoSet
  foreach($v in $variants) {
   Assert-RmDtoId (Get-RmDtoField $v 'variantId') 'CATDTO_VARIANT_ID'
   Assert-RmDto ($variantKeys.Add($v.variantId)) 'CATDTO_DUPLICATE_VARIANT'
   [void](Get-RmDtoField $v 'label')
  }
  $unitKeys=New-RmDtoSet
  $titleEvidence=New-RmDtoSet
  foreach($o in $observations) {
   Assert-RmDto ($o.libraryId -ceq $lid -and $o.snapshotId -ceq $p.snapshotId -and $o.generationId -ceq $p.generationId) 'CATDTO_OBSERVATION_PROVENANCE'
   Assert-RmDto ($variantKeys.Contains($o.variantId)) 'CATDTO_ORPHAN_VARIANT'
   Assert-RmDtoId $o.romUnitId 'CATDTO_ROM_UNIT_ID'
   Assert-RmDto ($unitKeys.Add($o.romUnitId)) 'CATDTO_DUPLICATE_OBSERVATION'
   Assert-RmDto ($observationScopeKeys.Add((Get-RmDtoKey @($lid,$p.snapshotId,$o.romUnitId)))) 'CATDTO_CROSS_GAME_DUPLICATE_ROM'
   $observationsByLibrary[$lid]=[int]$observationsByLibrary[$lid]+1
   $disc=Get-RmDtoField $o 'discNumber'
   Assert-RmDto ($null -eq $disc -or ($disc -is [int] -and $disc -ge 1)) 'CATDTO_DISC_INVALID'
   $observedTitle=Get-RmDtoField $o 'observedTitle'
   if ($null -ne $observedTitle) { Assert-RmDtoId $observedTitle 'CATDTO_OBSERVED_TITLE_INVALID';[void]$titleEvidence.Add($observedTitle) }
   Assert-RmDto ($o.observationStatus -ceq $(if($cover -ceq 'KNOWN'){'OBSERVED'}else{'PARTIAL'})) 'CATDTO_OBSERVATION_STATUS_FORGED'
  }
  if ($titleEvidence.Count -gt 1) {
   Assert-RmDto ($quality -ceq 'CONTRADICTORY' -and $null -eq $title) 'CATDTO_TITLE_CONFLICT_FORGED'
  } elseif ($titleEvidence.Count -eq 0) {
   Assert-RmDto ($null -eq $title) 'CATDTO_TITLE_UNKNOWN_FORGED'
  } else {
   Assert-RmDto ($titleEvidence.Contains($title)) 'CATDTO_TITLE_PROVENANCE_MISMATCH'
   Assert-RmDto ($quality -cne 'CONTRADICTORY') 'CATDTO_TITLE_CONFLICT_FORGED'
  }
 }
 # Counts must agree with observed units when the catalogue claims exhaustive coverage.
 foreach($lib in $libraries) {
  $lid=$lib.libraryId
  if (-not $lib.indexAccessible -or -not $latestByLibrary.ContainsKey($lid)) { continue }
  $latest=$latestByLibrary[$lid]
  $reported=[int]$observationsByLibrary[$lid]
  if ($reported -gt $latest.observedUnitCount) { throw 'CATDTO_OBSERVATION_COUNT_EXCEEDS_SNAPSHOT' }
  if ($allProven -and $reported -ne $latest.observedUnitCount -and
      @($diagnostics | Where-Object { $_.code -ceq 'CATALOG_IDENTITY_AMBIGUOUS' -and $_.libraryId -ceq $lid }).Count -eq 0) {
   throw 'CATDTO_SNAPSHOT_OBSERVATION_COUNT_MISMATCH'
  }
 }
 $countKnown=Get-RmDtoField $Dto 'countKnown'
 $gameCount=Get-RmDtoField $Dto 'gameCount'
 Assert-RmDto ($countKnown -is [bool]) 'CATDTO_COUNT_FLAG'
 if (-not $allProven -or $anyUnproven -or @($diagnostics | Where-Object { $_.code -ceq 'CATALOG_IDENTITY_AMBIGUOUS' }).Count -gt 0) {
  Assert-RmDto (-not $countKnown -and $null -eq $gameCount) 'CATDTO_COUNT_FORGED'
 } elseif ($countKnown) {
  Assert-RmDto ($gameCount -is [int] -and $gameCount -eq $games.Count) 'CATDTO_COUNT_MISMATCH'
 } else { Assert-RmDto ($null -eq $gameCount) 'CATDTO_COUNT_FORGED' }
 return $true
}
function ConvertTo-RmCanonicalCatalogueDto {
 [CmdletBinding()]
 param([Parameter(Mandatory=$true)]$Catalogue)
 Assert-RmDto ($Catalogue.schemaVersion -ceq '0.1.0-ui-catalogue-t1-candidate') 'CATDTO_SOURCE_SCHEMA'
 $games=New-Object 'System.Collections.Generic.List[object]'
 foreach($g in $Catalogue.games) {
  $vars=New-Object 'System.Collections.Generic.List[object]'
  foreach($v in $g.variants) { $vars.Add([pscustomobject][ordered]@{variantId=$v;label=$v}) }
  $obs=New-Object 'System.Collections.Generic.List[object]'
  foreach($u in $g.units) {
   $obs.Add([pscustomobject][ordered]@{
    libraryId=$g.libraryId;snapshotId=$u.provenance.snapshotId;generationId=$u.provenance.generationId
    romUnitId=$u.romUnitId;variantId=$u.variantId;discNumber=$u.discNumber;observedTitle=$u.observedTitle
    observationStatus=$(if($g.coverageStatus -ceq 'COMPLETE'){'OBSERVED'}else{'PARTIAL'})
   })
  }
  $p=$g.provenance
  $games.Add([pscustomobject][ordered]@{
   libraryId=$g.libraryId;gameId=$g.gameId;platform=$g.platform;title=$g.title
   quality=$g.quality;status=$(if($g.coverageStatus -ceq 'COMPLETE'){'KNOWN'}else{'INCOMPLETE'})
   coverStatus=$(if($g.coverageStatus -ceq 'COMPLETE'){'KNOWN'}else{'UNKNOWN'})
   availability=$g.availability;freshness=$g.freshness
   variants=$vars.ToArray()
   provenance=@([pscustomobject][ordered]@{
    libraryId=$p.libraryId;snapshotId=$p.snapshotId;generationId=$p.generationId
    observedAtUtc=$p.observedAtUtc;source='SYNTHETIC_SNAPSHOT'
   })
   observations=$obs.ToArray()
  })
 }
 $dto=[pscustomobject][ordered]@{
  schemaVersion='0.2.0-ui-catalogue-candidate';readOnly=$true
  registry=[pscustomobject][ordered]@{schemaVersion='0.1.0-synthetic';readOnly=$true;libraries=@($Catalogue.libraries)}
  games=$games.ToArray();diagnostics=@($Catalogue.diagnostics)
  countKnown=[bool]$Catalogue.countKnown;gameCount=$Catalogue.gameCount
 }
 [void](Assert-RmCanonicalCatalogueDto -Dto $dto)
 return $dto
}
Export-ModuleMember -Function Assert-RmCanonicalCatalogueDto,ConvertTo-RmCanonicalCatalogueDto
) 'CATDTO_UTC_REQUIRED'
 $date=[DateTimeOffset]::MinValue
 Assert-RmDto ([DateTimeOffset]::TryParse($Value,[Globalization.CultureInfo]::InvariantCulture,
  [Globalization.DateTimeStyles]::AssumeUniversal,[ref]$date) -and
  $date.Offset -eq [TimeSpan]::Zero) 'CATDTO_UTC_INVALID'
}
function New-RmDtoSet {
 return (New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal))
}
function Get-RmDtoKey {
 param([string[]]$Parts)
 $key=''
 foreach($part in $Parts) { $key+=([string]$part.Length)+':'+$part }
 return $key
}
function Assert-RmCanonicalCatalogueDto {
 [CmdletBinding()]
 param([Parameter(Mandatory=$true)]$Dto)
 Assert-RmDto ((Get-RmDtoField $Dto 'schemaVersion') -ceq '0.2.0-ui-catalogue-candidate') 'CATDTO_SCHEMA_UNSUPPORTED'
 Assert-RmDto ((Get-RmDtoField $Dto 'readOnly') -ceq $true) 'CATDTO_READONLY_REQUIRED'
 $registry=Get-RmDtoField $Dto 'registry'
 Assert-RmDto ((Get-RmDtoField $registry 'schemaVersion') -ceq '0.1.0-synthetic') 'CATDTO_REGISTRY_SCHEMA'
 Assert-RmDto ((Get-RmDtoField $registry 'readOnly') -ceq $true) 'CATDTO_REGISTRY_READONLY'
 $libraries=Get-RmDtoField $registry 'libraries'
 $games=Get-RmDtoField $Dto 'games'
 $diagnostics=Get-RmDtoField $Dto 'diagnostics'
 Assert-RmDto ($libraries -is [array] -and $games -is [array] -and $diagnostics -is [array]) 'CATDTO_ARRAY_REQUIRED'
 $libKeys=New-RmDtoSet
 $libMap=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
 $allProven=$true
 foreach($lib in $libraries) {
  $lid=Get-RmDtoField $lib 'libraryId';Assert-RmDtoId $lid 'CATDTO_LIBRARY_ID'
  Assert-RmDto ($libKeys.Add($lid)) 'CATDTO_DUPLICATE_LIBRARY'
  $libMap.Add($lid,$lib)
  $index=Get-RmDtoField $lib 'indexAccessible'
  Assert-RmDto ($index -is [bool]) 'CATDTO_INDEX_INVALID'
  if (-not $index) { $allProven=$false }
  $sources=Get-RmDtoField $lib 'sources'
  $platforms=Get-RmDtoField $lib 'platforms'
  $generations=Get-RmDtoField $lib 'generations'
  Assert-RmDto ($sources -is [array] -and $platforms -is [array] -and $generations -is [array]) 'CATDTO_REGISTRY_ARRAY'
  $snapshotIds=New-RmDtoSet;$generationIds=New-RmDtoSet
  foreach($gen in $generations) {
   $sid=Get-RmDtoField $gen 'snapshotId';$gid=Get-RmDtoField $gen 'generationId'
   Assert-RmDtoId $sid 'CATDTO_SNAPSHOT_ID';Assert-RmDtoId $gid 'CATDTO_GENERATION_ID'
   Assert-RmDto ($snapshotIds.Add($sid) -and $generationIds.Add($gid)) 'CATDTO_DUPLICATE_SNAPSHOT_GENERATION'
   $proof=Get-RmDtoField $gen 'coverageProof'
   $coverage=Get-RmDtoField $gen 'coverage'
   $scan=Get-RmDtoField $gen 'scanStatus'
   $exhaustive=Get-RmDtoField $gen 'scopeIsExhaustive'
   Assert-RmDto ($proof -is [array] -and $coverage -is [array] -and $exhaustive -is [bool]) 'CATDTO_COVERAGE_FIELDS'
   # Recompute platform coverage from exact declared source/root/domain partitions.
   # A global "all covered" boolean is insufficient for mixed-platform snapshots.
   Assert-RmDto (@('complete','partial','failed') -ccontains $scan) 'CATDTO_SCAN_STATUS_INVALID'
   $expected=New-RmDtoSet
   $expectedByConsole=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
   $platformKeys=New-RmDtoSet
   foreach($platform in $platforms) {
    Assert-RmDtoId $platform 'CATDTO_PLATFORM_INVALID'
    Assert-RmDto ($platformKeys.Add($platform)) 'CATDTO_DUPLICATE_PLATFORM'
    $expectedByConsole.Add($platform,(New-RmDtoSet))
   }
   $sourceKeys=New-RmDtoSet
   foreach($source in $sources) {
    $sourceId=Get-RmDtoField $source 'sourceKey'
    $rootId=Get-RmDtoField $source 'rootKey'
    Assert-RmDtoId $sourceId 'CATDTO_SOURCE_ID'
    Assert-RmDtoId $rootId 'CATDTO_ROOT_ID'
    Assert-RmDto ($sourceKeys.Add($sourceId)) 'CATDTO_DUPLICATE_SOURCE'
    $domains=Get-RmDtoField $source 'domains'
    Assert-RmDto ($domains -is [array] -and $domains.Count -gt 0) 'CATDTO_SOURCE_DOMAINS'
    $domainKeys=New-RmDtoSet
    foreach($domain in $domains) {
     Assert-RmDto (@('ROMS','MEDIAS') -ccontains $domain -and $domainKeys.Add($domain)) 'CATDTO_DOMAIN_INVALID'
     foreach($platform in $platforms) {
      $k=Get-RmDtoKey @($platform,$sourceId,$rootId,$domain)
      [void]$expected.Add($k)
      [void]$expectedByConsole[$platform].Add($k)
     }
    }
   }
   Assert-RmDto ($platforms.Count -gt 0 -and $expected.Count -gt 0) 'CATDTO_EMPTY_DECLARED_SCOPE'
   $seen=New-RmDtoSet;$proven=New-RmDtoSet
   $partialProof=New-RmDtoSet
   foreach($part in $proof) {
    $console=Get-RmDtoField $part 'console'
    $sourceId=Get-RmDtoField $part 'sourceKey'
    $rootId=Get-RmDtoField $part 'rootKey'
    $domain=Get-RmDtoField $part 'domain'
    $partStatus=Get-RmDtoField $part 'status'
    Assert-RmDto (@('complete','partial','unobserved') -ccontains $partStatus) 'CATDTO_PROOF_STATUS_INVALID'
    $k=Get-RmDtoKey @($console,$sourceId,$rootId,$domain)
    Assert-RmDto ($expected.Contains($k) -and $seen.Add($k)) 'CATDTO_PROOF_PARTITION_INVALID'
    if ($partStatus -ceq 'complete') { [void]$proven.Add($k) }
    if ($partStatus -ceq 'partial') { [void]$partialProof.Add($k) }
   }
   $globallyComplete=($scan -ceq 'complete' -and $exhaustive -and $expected.Count -eq $proven.Count)
   $covKeys=New-RmDtoSet
   foreach($c in $coverage) {
    $console=Get-RmDtoField $c 'console'
    $claimed=Get-RmDtoField $c 'status'
    Assert-RmDto ($platformKeys.Contains($console) -and $covKeys.Add($console)) 'CATDTO_COVERAGE_CONSOLE'
    Assert-RmDto (@('complete','partial','unobserved') -ccontains $claimed) 'CATDTO_COVERAGE_STATUS_INVALID'
    $needed=$expectedByConsole[$console]
    $allConsoleComplete=$true;$hasObserved=$false;$hasPartial=$false
    foreach($k in $needed) {
     if (-not $proven.Contains($k)) { $allConsoleComplete=$false }
     if ($seen.Contains($k)) { $hasObserved=$true }
     if ($partialProof.Contains($k)) { $hasPartial=$true }
    }
    $calculated='unobserved'
    if ($scan -ceq 'complete' -and $exhaustive -and $allConsoleComplete) { $calculated='complete' }
    elseif ($hasPartial -or $hasObserved) { $calculated='partial' }
    if ($claimed -ceq 'complete' -and $calculated -cne 'complete') { throw 'CATDTO_COMPLETE_UNPROVEN' }
    if ($claimed -cne $calculated) { throw 'CATDTO_COVERAGE_STATUS_MISMATCH' }
   }
   Assert-RmDto ($covKeys.Count -eq @($platforms).Count) 'CATDTO_COVERAGE_CONSOLE_MISSING'
   if ($globallyComplete) {
    Assert-RmDto (@($coverage | Where-Object { $_.status -cne 'complete' }).Count -eq 0) 'CATDTO_COMPLETE_COVERAGE_MISMATCH'
   }
   $countKnown=Get-RmDtoField $gen 'countKnown'
   $unitCount=Get-RmDtoField $gen 'observedUnitCount'
   Assert-RmDto ($countKnown -is [bool]) 'CATDTO_GENERATION_COUNT_FLAG'
   if (-not $index) {
    Assert-RmDto (-not $countKnown -and $null -eq $unitCount) 'CATDTO_INDEX_COUNT_FORGED'
   } else {
    Assert-RmDto ($countKnown -and $unitCount -is [int] -and $unitCount -ge 0) 'CATDTO_GENERATION_COUNT_INVALID'
   }
  }
 }
 $keys=New-RmDtoSet;$anyUnproven=$false
 foreach($g in $games) {
  $lid=Get-RmDtoField $g 'libraryId';$gid=Get-RmDtoField $g 'gameId';$platform=Get-RmDtoField $g 'platform'
  Assert-RmDtoId $gid 'CATDTO_GAME_ID';Assert-RmDtoId $platform 'CATDTO_PLATFORM'
  Assert-RmDto ($libMap.ContainsKey($lid)) 'CATDTO_ORPHAN_LIBRARY'
  Assert-RmDto ($keys.Add((Get-RmDtoKey @($lid,$platform,$gid)))) 'CATDTO_DUPLICATE_GAME'
  $lib=$libMap[$lid]
  Assert-RmDto ($lib.indexAccessible -and @($lib.platforms) -ccontains $platform) 'CATDTO_GAME_SOURCE_INVALID'
  $title=Get-RmDtoField $g 'title'
  Assert-RmDto ($null -eq $title -or ($title -is [string] -and -not [string]::IsNullOrWhiteSpace($title))) 'CATDTO_TITLE_INVALID'
  $quality=Get-RmDtoField $g 'quality';$status=Get-RmDtoField $g 'status'
  $cover=Get-RmDtoField $g 'coverStatus'
  Assert-RmDto (@('KNOWN','STALE','PARTIALLY_OBSERVED','CONTRADICTORY','UNKNOWN') -ccontains $quality) 'CATDTO_QUALITY_INVALID'
  Assert-RmDto (@('KNOWN','INCOMPLETE') -ccontains $status) 'CATDTO_STATUS_INVALID'
  Assert-RmDto (@('KNOWN','UNKNOWN') -ccontains $cover) 'CATDTO_COVER_INVALID'
  $provenance=Get-RmDtoField $g 'provenance'
  $variants=Get-RmDtoField $g 'variants'
  $observations=Get-RmDtoField $g 'observations'
  Assert-RmDto ($provenance -is [array] -and $provenance.Count -eq 1 -and $variants -is [array] -and $observations -is [array] -and $observations.Count -gt 0) 'CATDTO_GAME_ARRAYS'
  $p=$provenance[0]
  Assert-RmDto ($p.libraryId -ceq $lid -and $p.source -ceq 'SYNTHETIC_SNAPSHOT') 'CATDTO_PROVENANCE_INVALID'
  $snap=@($lib.generations | Where-Object { $_.snapshotId -ceq $p.snapshotId })
  Assert-RmDto ($snap.Count -eq 1) 'CATDTO_ORPHAN_SNAPSHOT'
  $snap=$snap[0]
  Assert-RmDto ($snap.generationId -ceq $p.generationId -and $snap.capturedAtUtc -ceq $p.observedAtUtc) 'CATDTO_GENERATION_PROVENANCE_MISMATCH'
  $expectedCover=if ($snap.scanStatus -ceq 'complete' -and $snap.scopeIsExhaustive -and
    @($snap.coverage | Where-Object { $_.status -cne 'complete' }).Count -eq 0) {'KNOWN'} else {'UNKNOWN'}
  Assert-RmDto ($cover -ceq $expectedCover) 'CATDTO_COVERAGE_FORGED'
  Assert-RmDto ($status -ceq $(if($cover -ceq 'KNOWN'){'KNOWN'}else{'INCOMPLETE'})) 'CATDTO_STATUS_FORGED'
  if ($cover -cne 'KNOWN') { $anyUnproven=$true }
  if ($quality -ceq 'KNOWN') { Assert-RmDto ($cover -ceq 'KNOWN' -and $lib.freshness -ceq 'FRESH') 'CATDTO_QUALITY_FORGED' }
  if ($quality -ceq 'STALE') { Assert-RmDto ($lib.freshness -ceq 'STALE') 'CATDTO_STALE_FORGED' }
  if ($quality -ceq 'UNKNOWN') { Assert-RmDto ($lib.freshness -ceq 'UNKNOWN') 'CATDTO_UNKNOWN_QUALITY_FORGED' }
  $variantKeys=New-RmDtoSet
  foreach($v in $variants) {
   Assert-RmDtoId (Get-RmDtoField $v 'variantId') 'CATDTO_VARIANT_ID'
   Assert-RmDto ($variantKeys.Add($v.variantId)) 'CATDTO_DUPLICATE_VARIANT'
   [void](Get-RmDtoField $v 'label')
  }
  $unitKeys=New-RmDtoSet
  $titleEvidence=New-RmDtoSet
  foreach($o in $observations) {
   Assert-RmDto ($o.libraryId -ceq $lid -and $o.snapshotId -ceq $p.snapshotId -and $o.generationId -ceq $p.generationId) 'CATDTO_OBSERVATION_PROVENANCE'
   Assert-RmDto ($variantKeys.Contains($o.variantId)) 'CATDTO_ORPHAN_VARIANT'
   Assert-RmDtoId $o.romUnitId 'CATDTO_ROM_UNIT_ID'
   Assert-RmDto ($unitKeys.Add($o.romUnitId)) 'CATDTO_DUPLICATE_OBSERVATION'
   $observedTitle=Get-RmDtoField $o 'observedTitle'
   if ($null -ne $observedTitle) { Assert-RmDtoId $observedTitle 'CATDTO_OBSERVED_TITLE_INVALID';[void]$titleEvidence.Add($observedTitle) }
   Assert-RmDto ($o.observationStatus -ceq $(if($cover -ceq 'KNOWN'){'OBSERVED'}else{'PARTIAL'})) 'CATDTO_OBSERVATION_STATUS_FORGED'
  }
  if ($titleEvidence.Count -gt 1) {
   Assert-RmDto ($quality -ceq 'CONTRADICTORY' -and $null -eq $title) 'CATDTO_TITLE_CONFLICT_FORGED'
  } elseif ($titleEvidence.Count -eq 0) {
   Assert-RmDto ($null -eq $title) 'CATDTO_TITLE_UNKNOWN_FORGED'
  } else {
   Assert-RmDto ($titleEvidence.Contains($title)) 'CATDTO_TITLE_PROVENANCE_MISMATCH'
   Assert-RmDto ($quality -cne 'CONTRADICTORY') 'CATDTO_TITLE_CONFLICT_FORGED'
  }
 }
 $countKnown=Get-RmDtoField $Dto 'countKnown'
 $gameCount=Get-RmDtoField $Dto 'gameCount'
 Assert-RmDto ($countKnown -is [bool]) 'CATDTO_COUNT_FLAG'
 if (-not $allProven -or $anyUnproven -or @($diagnostics | Where-Object { $_.code -ceq 'CATALOG_IDENTITY_AMBIGUOUS' }).Count -gt 0) {
  Assert-RmDto (-not $countKnown -and $null -eq $gameCount) 'CATDTO_COUNT_FORGED'
 } elseif ($countKnown) {
  Assert-RmDto ($gameCount -is [int] -and $gameCount -eq $games.Count) 'CATDTO_COUNT_MISMATCH'
 } else { Assert-RmDto ($null -eq $gameCount) 'CATDTO_COUNT_FORGED' }
 return $true
}
function ConvertTo-RmCanonicalCatalogueDto {
 [CmdletBinding()]
 param([Parameter(Mandatory=$true)]$Catalogue)
 Assert-RmDto ($Catalogue.schemaVersion -ceq '0.1.0-ui-catalogue-t1-candidate') 'CATDTO_SOURCE_SCHEMA'
 $games=New-Object 'System.Collections.Generic.List[object]'
 foreach($g in $Catalogue.games) {
  $vars=New-Object 'System.Collections.Generic.List[object]'
  foreach($v in $g.variants) { $vars.Add([pscustomobject][ordered]@{variantId=$v;label=$v}) }
  $obs=New-Object 'System.Collections.Generic.List[object]'
  foreach($u in $g.units) {
   $obs.Add([pscustomobject][ordered]@{
    libraryId=$g.libraryId;snapshotId=$u.provenance.snapshotId;generationId=$u.provenance.generationId
    romUnitId=$u.romUnitId;variantId=$u.variantId;discNumber=$u.discNumber;observedTitle=$u.observedTitle
    observationStatus=$(if($g.coverageStatus -ceq 'COMPLETE'){'OBSERVED'}else{'PARTIAL'})
   })
  }
  $p=$g.provenance
  $games.Add([pscustomobject][ordered]@{
   libraryId=$g.libraryId;gameId=$g.gameId;platform=$g.platform;title=$g.title
   quality=$g.quality;status=$(if($g.coverageStatus -ceq 'COMPLETE'){'KNOWN'}else{'INCOMPLETE'})
   coverStatus=$(if($g.coverageStatus -ceq 'COMPLETE'){'KNOWN'}else{'UNKNOWN'})
   availability=$g.availability;freshness=$g.freshness
   variants=$vars.ToArray()
   provenance=@([pscustomobject][ordered]@{
    libraryId=$p.libraryId;snapshotId=$p.snapshotId;generationId=$p.generationId
    observedAtUtc=$p.observedAtUtc;source='SYNTHETIC_SNAPSHOT'
   })
   observations=$obs.ToArray()
  })
 }
 $dto=[pscustomobject][ordered]@{
  schemaVersion='0.2.0-ui-catalogue-candidate';readOnly=$true
  registry=[pscustomobject][ordered]@{schemaVersion='0.1.0-synthetic';readOnly=$true;libraries=@($Catalogue.libraries)}
  games=$games.ToArray();diagnostics=@($Catalogue.diagnostics)
  countKnown=[bool]$Catalogue.countKnown;gameCount=$Catalogue.gameCount
 }
 [void](Assert-RmCanonicalCatalogueDto -Dto $dto)
 return $dto
}
Export-ModuleMember -Function Assert-RmCanonicalCatalogueDto,ConvertTo-RmCanonicalCatalogueDto
