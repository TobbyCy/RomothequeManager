# T1 candidate catalogue projection. Synthetic in-memory records only; no I/O.
Set-StrictMode -Version 2.0
function Assert-RmCatText {
 param($Value,[string]$Code)
 if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value)) { throw $Code }
 return $Value
}
function Get-RmCatProperty {
 param($Object,[string]$Name)
 if ($null -eq $Object) { return $null }
 $p=$Object.PSObject.Properties[$Name]
 if ($null -eq $p) { return $null }
 return $p.Value
}
function New-RmCatSet { return (New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)) }
function Sort-RmCatOrdinal {
 param([object[]]$Items,[string]$Property)
 $map=New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([StringComparer]::Ordinal)
 foreach ($item in $Items) {
  $key=[string](Get-RmCatProperty $item $Property)
  if ($map.ContainsKey($key)) { throw 'CATALOG_SORT_KEY_COLLISION' }
  $map.Add($key,$item)
 }
 $result=New-Object 'System.Collections.Generic.List[object]'
 foreach ($value in $map.Values) { [void]$result.Add($value) }
 return ,$result.ToArray()
}
function New-RmCatalogueProjection {
 [CmdletBinding()]
 param([Parameter(Mandatory=$true)]$Registry)
 Import-Module (Join-Path $PSScriptRoot 'LibrarySnapshotProjection.psm1') -ErrorAction Stop
 $projected=New-RmLibraryProjection -Registry $Registry
 $allIndicesAccessible=$true
 $games=New-Object 'System.Collections.Generic.List[object]'
 $diagnostics=New-Object 'System.Collections.Generic.List[object]'
 $libraries=Sort-RmCatOrdinal -Items @($projected.libraries) -Property 'libraryId'
 foreach ($card in $libraries) {
  $raw=@($Registry.libraries | Where-Object { $_.libraryId -ceq $card.libraryId })[0]
  if (-not $card.indexAccessible) {
   $allIndicesAccessible=$false
   $diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_INDEX_INACCESSIBLE';libraryId=$card.libraryId;snapshotId=$null})
   continue
  }
  if (@($raw.snapshots).Count -eq 0) {
   $allIndicesAccessible=$false
   $diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_NO_SNAPSHOT';libraryId=$card.libraryId;snapshotId=$null})
   continue
  }
  $byId=New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([StringComparer]::Ordinal)
  foreach ($snap in $raw.snapshots) { $byId.Add([string]$snap.snapshotId,$snap) }
  $ordered=New-Object 'System.Collections.Generic.List[object]'
  foreach ($snap in $byId.Values) { $ordered.Add($snap) }
  $latest=$null
  foreach ($snap in $ordered) {
   if ($null -eq $latest -or [string]::CompareOrdinal($snap.capturedAtUtc,$latest.capturedAtUtc) -gt 0 -or
       ($snap.capturedAtUtc -ceq $latest.capturedAtUtc -and [string]::CompareOrdinal($snap.snapshotId,$latest.snapshotId) -gt 0)) { $latest=$snap }
  }
  $generation=@($card.generations | Where-Object { $_.snapshotId -ceq $latest.snapshotId })[0]
  $fullyCovered=($latest.scanStatus -ceq 'complete' -and $latest.scopeIsExhaustive -and
    @($generation.coverage).Count -eq @($card.platforms).Count -and
    @($generation.coverage | Where-Object { $_.status -cne 'complete' }).Count -eq 0)
  if (-not $fullyCovered) {
   $diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_COVERAGE_UNPROVEN';libraryId=$card.libraryId;snapshotId=$latest.snapshotId})
  }
  if (@($latest.units).Count -eq 0 -and -not $fullyCovered) {
   $diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_ZERO_NOT_EMPTY_PROOF';libraryId=$card.libraryId;snapshotId=$latest.snapshotId})
  }
  $unitKeys=New-RmCatSet
  $grouped=New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([StringComparer]::Ordinal)
  foreach ($unit in $latest.units) {
   $rom=Assert-RmCatText (Get-RmCatProperty $unit 'romUnitId') 'CATALOG_ROM_ID_REQUIRED'
   $platform=Assert-RmCatText (Get-RmCatProperty $unit 'console') 'CATALOG_PLATFORM_REQUIRED'
   if (@($card.platforms) -cnotcontains $platform) { throw 'CATALOG_PLATFORM_UNDECLARED' }
   if (-not $unitKeys.Add($rom)) { throw 'CATALOG_DUPLICATE_ROM_ID' }
   $game=Get-RmCatProperty $unit 'gameId';$variant=Get-RmCatProperty $unit 'variantId'
   if ($null -eq $game -or $null -eq $variant) {
    $diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_IDENTITY_AMBIGUOUS';libraryId=$card.libraryId;snapshotId=$latest.snapshotId})
    continue
   }
   [void](Assert-RmCatText $game 'CATALOG_GAME_ID_INVALID')
   [void](Assert-RmCatText $variant 'CATALOG_VARIANT_ID_INVALID')
   $key=('{0}:{1}{2}:{3}{4}:{5}' -f $platform.Length,$platform,$game.Length,$game,$card.libraryId.Length,$card.libraryId)
   if (-not $grouped.ContainsKey($key)) {
    $grouped.Add($key,[pscustomobject]@{libraryId=$card.libraryId;platform=$platform;gameId=$game;units=(New-Object 'System.Collections.Generic.List[object]');variants=(New-RmCatSet);titles=(New-RmCatSet)})
   }
   $group=$grouped[$key]
   [void]$group.variants.Add($variant)
   $title=Get-RmCatProperty $unit 'title'
   if ($null -ne $title) { [void](Assert-RmCatText $title 'CATALOG_TITLE_INVALID');[void]$group.titles.Add($title) }
   $disc=Get-RmCatProperty $unit 'discNumber'
   if ($null -ne $disc -and ($disc -isnot [int] -or $disc -lt 1)) { throw 'CATALOG_DISC_INVALID' }
   $group.units.Add([pscustomobject][ordered]@{
    romUnitId=$rom;variantId=$variant;discNumber=$disc;observedTitle=$title
    provenance=[pscustomobject][ordered]@{libraryId=$card.libraryId;snapshotId=$latest.snapshotId;generationId=$latest.generationId;observedAtUtc=$latest.capturedAtUtc}
   })
  }
  foreach ($group in $grouped.Values) {
   $titleList=New-Object 'System.Collections.Generic.List[string]'
   foreach ($t in $group.titles) { [void]$titleList.Add($t) }
   $titles=$titleList.ToArray();[Array]::Sort($titles,[StringComparer]::Ordinal)
   $variantList=New-Object 'System.Collections.Generic.List[string]'
   foreach ($v in $group.variants) { [void]$variantList.Add($v) }
   $variants=$variantList.ToArray();[Array]::Sort($variants,[StringComparer]::Ordinal)
   $units=Sort-RmCatOrdinal -Items @($group.units.ToArray()) -Property 'romUnitId'
   $quality=if (-not $fullyCovered) {'PARTIALLY_OBSERVED'} elseif ($card.freshness -ceq 'STALE') {'STALE'} elseif ($card.freshness -ceq 'FRESH') {'KNOWN'} else {'UNKNOWN'}
   if ($titles.Count -gt 1) { $quality='CONTRADICTORY';$diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_TITLE_CONFLICT';libraryId=$card.libraryId;snapshotId=$latest.snapshotId}) }
   if ($titles.Count -eq 0) { $diagnostics.Add([pscustomobject][ordered]@{code='CATALOG_TITLE_UNKNOWN';libraryId=$card.libraryId;snapshotId=$latest.snapshotId}) }
   $games.Add([pscustomobject][ordered]@{
    libraryId=$card.libraryId;platform=$group.platform;gameId=$group.gameId
    title=$(if($titles.Count -eq 1){$titles[0]}else{$null})
    quality=$quality;availability=$card.currentAvailability;freshness=$card.freshness
    coverageStatus=$(if($fullyCovered){'COMPLETE'}else{'INCOMPLETE'})
    variants=@($variants);units=@($units)
    provenance=[pscustomobject][ordered]@{libraryId=$card.libraryId;snapshotId=$latest.snapshotId;generationId=$latest.generationId;observedAtUtc=$latest.capturedAtUtc}
   })
  }
 }
 $gameMap=New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([StringComparer]::Ordinal)
 foreach($g in $games) {
  $key=('{0}:{1}{2}:{3}{4}:{5}' -f $g.libraryId.Length,$g.libraryId,$g.platform.Length,$g.platform,$g.gameId.Length,$g.gameId)
  $gameMap.Add($key,$g)
 }
 $orderedGames=New-Object 'System.Collections.Generic.List[object]'
 foreach ($item in $gameMap.Values) { [void]$orderedGames.Add($item) }
 return [pscustomobject][ordered]@{
  schemaVersion='0.1.0-ui-catalogue-t1-candidate';readOnly=$true
  libraries=@($libraries);games=$orderedGames.ToArray();diagnostics=$diagnostics.ToArray()
  countKnown=($allIndicesAccessible -and @($diagnostics | Where-Object { $_.code -ceq 'CATALOG_COVERAGE_UNPROVEN' -or $_.code -ceq 'CATALOG_IDENTITY_AMBIGUOUS' }).Count -eq 0)
  gameCount=$(if($allIndicesAccessible -and @($diagnostics | Where-Object { $_.code -ceq 'CATALOG_COVERAGE_UNPROVEN' -or $_.code -ceq 'CATALOG_IDENTITY_AMBIGUOUS' }).Count -eq 0){$gameMap.Count}else{$null})
  limitations=@('SYNTHETIC_ONLY','LATEST_SNAPSHOT_PER_LIBRARY','NOT_PHYSICAL_ROM_BYTES','NO_INFERRED_IDENTITY')
 }
}
function Search-RmCatalogue {
 [CmdletBinding()]
 param([Parameter(Mandatory=$true)]$Catalogue,[string]$Text,[string]$Platform,[string]$LibraryId,
  [string]$Availability,[string]$CoverageStatus,[string]$VariantId,[int]$Offset=0,[int]$Limit=100)
 if ($Catalogue.schemaVersion -cne '0.1.0-ui-catalogue-t1-candidate' -or $Catalogue.readOnly -ne $true) { throw 'CATALOG_SCHEMA_UNSUPPORTED' }
 if ($Offset -lt 0 -or $Limit -lt 1 -or $Limit -gt 1000) { throw 'CATALOG_PAGE_INVALID' }
 $matches=New-Object 'System.Collections.Generic.List[object]'
 foreach($g in $Catalogue.games) {
  if ($Platform -and $g.platform -cne $Platform) { continue }
  if ($LibraryId -and $g.libraryId -cne $LibraryId) { continue }
  if ($Availability -and $g.availability -cne $Availability) { continue }
  if ($CoverageStatus -and $g.coverageStatus -cne $CoverageStatus) { continue }
  if ($VariantId -and @($g.variants) -cnotcontains $VariantId) { continue }
  if ($Text -and ([string]$g.title).IndexOf($Text,[StringComparison]::OrdinalIgnoreCase) -lt 0 -and
      ([string]$g.gameId).IndexOf($Text,[StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
  $matches.Add($g)
 }
 $page=New-Object 'System.Collections.Generic.List[object]'
 for($i=$Offset;$i -lt [Math]::Min($matches.Count,$Offset+$Limit);$i++){ $page.Add($matches[$i]) }
 return [pscustomobject][ordered]@{total=$matches.Count;offset=$Offset;limit=$Limit;items=@($page.ToArray())}
}
Export-ModuleMember -Function New-RmCatalogueProjection,Search-RmCatalogue
