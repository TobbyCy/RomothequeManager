#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)][string]$StateRoot,
    [Parameter(Mandatory=$true)][string]$SnapshotRoot,
    [Parameter(Mandatory=$true)][string]$ProofOutputDirectory
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
if($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1 -or $PSVersionTable.PSEdition -ne 'Desktop') {throw 'PS51_DESKTOP_REQUIRED'}
# Deny Windows junctions, symlinks and other reparse points in every path ancestor.
# This deliberately fails closed for redirections, including a linked output ancestor.
function Assert-NoReparseAncestors([string]$Path,[string]$Role) {
    $full=[IO.Path]::GetFullPath($Path)
    $cursor=New-Object IO.DirectoryInfo($full)
    while($null -ne $cursor) {
        if (!$cursor.Exists) { throw ('E1_PATH_COMPONENT_MISSING: '+$Role+': '+$cursor.FullName) }
        $attributes=$cursor.Attributes
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw ('E1_REPARSE_PATH_DENIED: '+$Role+': '+$cursor.FullName)
        }
        $cursor=$cursor.Parent
    }
}
function Normalize-ExistingDirectory([string]$Path,[string]$Role) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw ('E1_EMPTY_PATH: '+$Role) }
    $full=[IO.Path]::GetFullPath($Path)
    if (!(Test-Path -LiteralPath $full -PathType Container)) { throw ('ROOT_NOT_FOUND: '+$Role+': '+$full) }
    Assert-NoReparseAncestors $full $Role
    $volume=[IO.Path]::GetPathRoot($full)
    if (!$full.Equals($volume,[StringComparison]::OrdinalIgnoreCase)) { $full=$full.TrimEnd('\') }
    return $full
}
function Test-PathOverlap([string]$Left,[string]$Right) {
    $l=$Left.TrimEnd('\')
    $r=$Right.TrimEnd('\')
    if ($l.Equals($r,[StringComparison]::OrdinalIgnoreCase)) { return $true }
    return ($l.StartsWith(($r+'\'),[StringComparison]::OrdinalIgnoreCase) -or
            $r.StartsWith(($l+'\'),[StringComparison]::OrdinalIgnoreCase))
}
$Root=Normalize-ExistingDirectory $Root 'Root'
$StateRoot=Normalize-ExistingDirectory $StateRoot 'StateRoot'
$SnapshotRoot=Normalize-ExistingDirectory $SnapshotRoot 'SnapshotRoot'
$ProofOutputDirectory=Normalize-ExistingDirectory $ProofOutputDirectory 'ProofOutputDirectory'
$runId=[guid]::NewGuid().ToString()
# Stage under the already validated destination; NEVER derive a write root from $env:TEMP.
$proof=Join-Path $ProofOutputDirectory ('RM_EXPORT_E1_STAGING_'+$runId)
function Assert-SafeOutput {
    Assert-NoReparseAncestors $Root 'Root'
    Assert-NoReparseAncestors $StateRoot 'StateRoot'
    Assert-NoReparseAncestors $SnapshotRoot 'SnapshotRoot'
    Assert-NoReparseAncestors $ProofOutputDirectory 'ProofOutputDirectory'
    # Module bootstrap uses Add-Type: the .NET compiler may create TEMP files.
    # Forbid redirected compiler temporaries from resolving into protected roots.
    foreach($tempValue in @($env:TEMP,$env:TMP,[IO.Path]::GetTempPath())) {
        if ([string]::IsNullOrWhiteSpace([string]$tempValue)) { continue }
        $safeTemp=Normalize-ExistingDirectory ([string]$tempValue) 'CompilerTemp'
        foreach($protected in @($Root,$StateRoot,$SnapshotRoot)) {
            if (Test-PathOverlap $safeTemp $protected) {
                throw ('E1_TEMP_OVERLAPS_PROTECTED_ROOT: '+$safeTemp)
            }
        }
    }
    foreach($protected in @($Root,$StateRoot,$SnapshotRoot)) {
        if (Test-PathOverlap $ProofOutputDirectory $protected) {
            throw ('OUTPUT_OVERLAPS_PROTECTED_ROOT: '+$ProofOutputDirectory+' versus '+$protected)
        }
    }
    if (Test-Path -LiteralPath $proof) {
        Assert-NoReparseAncestors $proof 'StagingRoot'
        if ((Test-PathOverlap $proof $Root) -or (Test-PathOverlap $proof $StateRoot) -or (Test-PathOverlap $proof $SnapshotRoot)) {
            throw 'E1_STAGING_OVERLAPS_PROTECTED_ROOT'
        }
    }
}
Assert-SafeOutput
# Random stage names and a non-force directory creation prevent overwrite of existing targets.
New-Item -ItemType Directory -Path $proof -ErrorAction Stop | Out-Null
Assert-SafeOutput
$checks=New-Object 'System.Collections.Generic.List[object]'
$anomalies=New-Object 'System.Collections.Generic.List[object]'
function Check([string]$code,$actual,$expected){
    $ok=([string]$actual -ceq [string]$expected)
    $checks.Add([pscustomobject]@{code=$code;pass=$ok;actual=[string]$actual;expected=[string]$expected})
    if(!$ok){throw ('GUARD_FAILED '+$code+': '+[string]$actual+' != '+[string]$expected)}
}
function Add-Issue([string]$code,[string]$id,[string]$detail){$anomalies.Add([pscustomobject]@{code=$code;id=$id;detail=$detail})}
function Val($o,[string]$name,$default=$null){if($null -eq $o){return $default};$prop=$o.PSObject.Properties[$name];if($null -ne $prop){return $prop.Value};return $default}
function Write-Json([string]$name,$obj){Assert-SafeOutput;$value=ConvertTo-Json -InputObject $obj -Depth 85;[IO.File]::WriteAllText((Join-Path $proof $name),[string]$value,(New-Object Text.UTF8Encoding($false)))}
function Get-SourceSha256NormalizingCheckoutEol([string]$Path) {
    # Git text=auto checks out LF blobs as CRLF on Windows; hash the SAME source bytes
    # with only that reversible checkout EOL conversion removed, never skip SHA checking.
    $bytes=[IO.File]::ReadAllBytes($Path)
    $buffer=New-Object IO.MemoryStream
    try {
        for($i=0;$i -lt $bytes.Length;$i++) {
            if($bytes[$i] -eq 13 -and ($i+1) -lt $bytes.Length -and $bytes[$i+1] -eq 10) {continue}
            $buffer.WriteByte($bytes[$i])
        }
        $sha=[Security.Cryptography.SHA256]::Create()
        try {return ([BitConverter]::ToString($sha.ComputeHash($buffer.ToArray()))).Replace('-','').ToLowerInvariant()}
        finally {$sha.Dispose()}
    } finally {$buffer.Dispose()}
}

$dirReparseCache=@{}
function Source-Info([string]$rel) {
    $out=[pscustomobject]@{relativePath=$rel;safePath=$false;exists=$false;sizeBytes=$null;modifiedAtUtc=$null;issue=$null}
    if(!(Test-RmE1SafeRelativeSourcePath $rel)){$out.issue='RELATIVE_PATH_INVALID';return $out}
    $segments=$rel.Split('/')
    if(@($segments|Where-Object{$_ -eq '' -or $_ -eq '.' -or $_ -eq '..'}).Count -gt 0){$out.issue='SEGMENT_INVALID';return $out}
    $full=[IO.Path]::GetFullPath((Join-Path $Root ($rel.Replace('/','\'))))
    if(!$full.StartsWith(($Root+'\'),[StringComparison]::OrdinalIgnoreCase)){$out.issue='ESCAPES_SOURCE_ROOT';return $out}
    $parent=[IO.Path]::GetDirectoryName($full)
    $cursor=$parent
    while($cursor -and $cursor.StartsWith(($Root+'\'),[StringComparison]::OrdinalIgnoreCase)) {
        if(!$dirReparseCache.ContainsKey($cursor)) {
            try {$d=Get-Item -LiteralPath $cursor -Force -ErrorAction Stop;$dirReparseCache[$cursor]=(($d.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)}
            catch {$dirReparseCache[$cursor]=$false}
        }
        if(!$dirReparseCache[$cursor]){$out.issue='REPARSE_OR_INACCESSIBLE_DIRECTORY';return $out}
        $cursor=[IO.Path]::GetDirectoryName($cursor)
    }
    $out.safePath=$true
    try {
        $f=Get-Item -LiteralPath $full -Force -ErrorAction Stop
        if(($f.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){$out.issue='REPARSE_FILE';return $out}
        if($f -isnot [IO.FileInfo]){$out.issue='NOT_REGULAR_FILE';return $out}
        $out.exists=$true;$out.sizeBytes=[long]$f.Length;$out.modifiedAtUtc=$f.LastWriteTimeUtc.ToString('o')
    } catch {$out.issue='FILE_MISSING_OR_INACCESSIBLE'}
    return $out
}
$statShaBefore=@{};$statShaAfter=@{};$filesToWatch=@('associations.json','catalogue.json','identity_registry.json')
try {
    Write-Host '=== EXPORT ENGINE E1 : READ-ONLY INVENTORY / DEV ==='
    Write-Host 'NO FULL SCAN / NO TRANSACTION / NO COPY / NO ROM HASH'
    Write-Host '[1/5] Read consistent existing modules, snapshot and persistent baseline'
    Import-Module (Join-Path $PSScriptRoot 'ExportE1.psm1') -Force
    . (Join-Path (Split-Path -Parent $PSScriptRoot) 'Initialize.ps1')
    Check 'TRANSACTIONS_MODULE_UNCHANGED' (Get-SourceSha256NormalizingCheckoutEol (Join-Path (Split-Path -Parent $PSScriptRoot) 'Modules\Transactions.psm1')) '827bf312b10cef21059d3921ce5f0d47e4b475e64014a68df03ede47c8b063af'
    foreach($dir in @($StateRoot,$SnapshotRoot)){
        foreach($f in $filesToWatch){$p=Join-Path $dir $f;if(!(Test-Path -LiteralPath $p -PathType Leaf)){throw ('MISSING_STATE_FILE: '+$p)};$statShaBefore[$dir+'|'+$f]=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $assoc=Read-RmStrictJson (Join-Path $SnapshotRoot 'associations.json')
    $registry=Read-RmStrictJson (Join-Path $SnapshotRoot 'identity_registry.json')
    $cat=Read-RmStrictJson (Join-Path $SnapshotRoot 'catalogue.json')
    $realAssoc=Read-RmStrictJson (Join-Path $StateRoot 'associations.json')
    Check 'SNAPSHOT_DECISIONS_MATCH_REAL' @($assoc.decisions).Count @($realAssoc.decisions).Count
    Check 'SNAPSHOT_TRANSACTIONS_MATCH_REAL' @($assoc.transactions).Count @($realAssoc.transactions).Count
    Check 'SAME_ASSOCIATIONS_AS_REAL' $statShaBefore[$SnapshotRoot+'|associations.json'] $statShaBefore[$StateRoot+'|associations.json']
    Check 'LIBRARY_ID_REGISTRY_CATALOGUE' $registry.libraryId $cat.libraryId
    Check 'SNAPSHOT_SCAN_COMPLETE' $cat.scan.status 'complete'
    Check 'SNAPSHOT_HASH_MODE' $cat.scan.hashMode 'none'
    Check 'SNAPSHOT_SCAN_SOURCE' ([IO.Path]::GetFullPath([string]$cat.scan.rootObserved).TrimEnd('\')) $Root
    Check 'METADATA_ACCOUNTING' ([int]$cat.statistics.metadataResolved+[int]$cat.statistics.metadataUnresolved) ([int]$cat.statistics.metadataGameEntries)
    $effects=@($cat.decisionEffects|Where-Object{$null -ne $_})
    Check 'DECISION_EFFECT_COUNT' $effects.Count @($assoc.decisions).Count
    Check 'EFFECTIVE_DECISIONS' @($effects|Where-Object{$_.effectiveStatus -eq 'effective'}).Count @($assoc.decisions|Where-Object{$_.state -eq 'active'}).Count
    $resolvedGameIds=Get-RmE1GameResolutionIndex $cat
    $metadataKinds=Get-RmE1GameMetadataIndex $cat
    # Reference all fileIds through the existing catalogue, never recreate unit identities.
    Check 'SAFE_UNIQUE_PHYSICAL_PATHS' (Assert-RmE1UniquePhysicalPaths @($cat.files)) $true
    $byId=@{};$byPath=@{};foreach($f in @($cat.files)){
        $id=[string]$f.fileId
        if($byId.ContainsKey($id)){throw ('DUPLICATE_FILE_ID: '+$id)}
        $byId[$id]=$f
        $key=([string]$f.relativePath).Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
        if($byPath.ContainsKey($key)){throw ('DUPLICATE_RELATIVE_PATH: '+$key)}
        $byPath[$key]=$id
    }
    Write-Host '[2/5] Build inventory of games / variants / units from catalogue'
    $fileObservations=@{};$rows=New-Object 'System.Collections.Generic.List[object]';$uniqueReady=@{}
    $gameIds=@{};$variantIds=@{};$unitIds=@{}
    foreach($g in @($cat.games)){
        if($gameIds.ContainsKey([string]$g.gameId)){throw 'DUPLICATE_GAME_ID'};$gameIds[[string]$g.gameId]=$true
        $sourceLinks=Select-RmE1ConfirmedGameLinks (Val $g 'metadataSources' @()) $resolvedGameIds $metadataKinds
        foreach($v in @($g.variants)){
            if($variantIds.ContainsKey([string]$v.variantId)){throw 'DUPLICATE_VARIANT_ID'};$variantIds[[string]$v.variantId]=$true
            if($v.gameId -cne $g.gameId){throw ('VARIANT_OWNER_INVALID: '+$v.variantId)}
            foreach($u in @($v.romUnits)){
                if($unitIds.ContainsKey([string]$u.romUnitId)){throw 'DUPLICATE_ROM_UNIT_ID'};$unitIds[[string]$u.romUnitId]=$true
                $missing=New-Object 'System.Collections.Generic.List[string]'
                $unitFileIds=@($u.fileIds|Where-Object{$null -ne $_}|Sort-Object -Unique)
                $byteCount=[long]0
                foreach($fileId in $unitFileIds){
                    $fileId=[string]$fileId
                    if(!$byId.ContainsKey($fileId)){$missing.Add('FILE_ID_NOT_IN_CATALOGUE:'+ $fileId);continue}
                    $f=$byId[$fileId]
                    if(!$fileObservations.ContainsKey($fileId)){$fileObservations[$fileId]=Source-Info ([string]$f.relativePath)}
                    $obs=$fileObservations[$fileId]
                    if(!$obs.safePath -or !$obs.exists){$missing.Add('FILE_UNAVAILABLE:'+ $f.relativePath+' '+$obs.issue);continue}
                    if([long]$obs.sizeBytes -ne [long]$f.sizeBytes -or [string]$obs.modifiedAtUtc -cne [string]$f.modifiedAtUtc){$missing.Add('FILE_OBSERVATION_CHANGED:'+ $f.relativePath);continue}
                    $byteCount+=[long]$f.sizeBytes
                }
                foreach($dep in @($u.missingDependencies|Where-Object{$null -ne $_})){$missing.Add('DECLARED_MISSING_DEPENDENCY:'+ [string]$dep)}
                if(!$u.complete){$missing.Add('CATALOGUE_UNIT_INCOMPLETE')}
                if($u.kind -eq 'ps3-package' -and (Val $u 'packageRoleStatus' '') -eq 'needs-validation'){$missing.Add('PACKAGE_ROLES_UNCONFIRMED')}
                if($unitFileIds.Count -eq 0){$missing.Add('UNIT_WITHOUT_FILES')}
                $projection=Get-RmE1UnitMetadataProjection -Sources $sourceLinks -VariantId ([string]$v.variantId) -RomUnitId ([string]$u.romUnitId) -VariantUnitCount (@($v.romUnits).Count)
                $metaState=$projection.metadataState
                $ready=($missing.Count -eq 0)
                if($ready){foreach($fileId in $unitFileIds){$f=$byId[[string]$fileId];$key=([string]$f.relativePath).Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant();$uniqueReady[$key]=$f}}
                $rows.Add([pscustomobject]@{gameId=$g.gameId;variantId=$v.variantId;romUnitId=$u.romUnitId;console=$g.console;gameName=$g.name;variantName=$v.name;kind=$u.kind;entryPath=$u.entryPath;unitComplete=[bool]$u.complete;physicalEligibleInSnapshot=$ready;metadataState=$metaState;confirmedMetadataEntryIds=@($projection.confirmedMetadataEntryIds);widerScopeMetadataEntryIds=@($projection.widerScopeMetadataEntryIds);fileIds=$unitFileIds;fileCount=$unitFileIds.Count;knownBytes=$byteCount;missingOrChanged=@($missing.ToArray());mediaScope='E3_PENDING'})
                if(!$ready){Add-Issue 'BLOCKED_UNIT' ([string]$u.romUnitId) (@($missing.ToArray()) -join ' | ')}
            }
        }
    }
    Check 'ALL_RESOLVED_GAMES_REPRESENTED' (Assert-RmE1GameCoverage $resolvedGameIds $rows.ToArray()) $true
    Write-Host '[3/5] Derive dynamic exceptions from CURRENT snapshot, not hardcoded names'
    $exceptions=Get-RmE1ExportExceptions $cat
    Check 'DERIVED_UNRESOLVED_COUNT' @($exceptions).Count ([int]$cat.statistics.metadataUnresolved)
    # E1 inventories file identity, not media transfer plans or metadata reconstruction.
    [long]$uniqueBytes=0;foreach($f in $uniqueReady.Values){$uniqueBytes+=[long]$f.sizeBytes}
    $readyRows=@($rows.ToArray()|Where-Object{$_.physicalEligibleInSnapshot})
    $blockedRows=@($rows.ToArray()|Where-Object{!$_.physicalEligibleInSnapshot})
    $summary=[pscustomobject]@{
        project='RomothequeManager';version='v0.3.2 Rev.3';milestone='E1_READONLY_INVENTORY_DEV';status='E1_SNAPSHOT_INVENTORY_COMPLETE_NOT_EXPORT_AUTHORIZATION';runId=$runId;createdAtUtc=[DateTime]::UtcNow.ToString('o')
        root=$Root;snapshotRoot=$SnapshotRoot;sourceScanId=$cat.scan.scanId;sourceCatalogueSha256=$statShaBefore[$SnapshotRoot+'|catalogue.json'];sourceRegistrySha256=$statShaBefore[$SnapshotRoot+'|identity_registry.json'];sourceAssociationsSha256=$statShaBefore[$SnapshotRoot+'|associations.json'];statePersistentSha256=$statShaBefore[$StateRoot+'|associations.json']
        games=$gameIds.Count;variants=$variantIds.Count;physicalUnits=$unitIds.Count;readyUnitsInSnapshot=$readyRows.Count;blockedUnits=$blockedRows.Count;uniqueReferencedReadyFiles=$uniqueReady.Count;knownUniqueReadyRomBytes=$uniqueBytes
        resolvedGameReferences=$resolvedGameIds.Count;confirmedGameLinksOnly=$true;resolvedMetadata=[int]$cat.statistics.metadataResolved;unresolvedMetadata=[int]$cat.statistics.metadataUnresolved;metadataEntries=[int]$cat.statistics.metadataGameEntries;dynamicExceptionCount=$exceptions.Count
        generatedFrom='Export/Invoke-ExportE1InventoryReadOnly.ps1';fileObservationsChecked=$fileObservations.Count;sourceHashMode='NONE';sourceRomHashes=0;fullPhysicalScan=$false;physicalCopy=$false;transactionsExecuted=$false
        IMPORTANT='E1 eligibility only from existing snapshot and directly observed referenced files; no detection of unknown new files and no E6 permission; freshness must be rechecked before future E5/E6.'
    }
    Write-Host '[4/5] Verify persistent state and scan snapshot did not change'
    foreach($dir in @($StateRoot,$SnapshotRoot)){
        foreach($f in $filesToWatch){$p=Join-Path $dir $f;$statShaAfter[$dir+'|'+$f]=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant();Check ('INPUT_UNCHANGED_'+$dir+'_'+$f) $statShaAfter[$dir+'|'+$f] $statShaBefore[$dir+'|'+$f]}
    }
    Write-Host '[5/5] Write DEV proof ZIP only in Downloads'
    Write-Json '00_SUMMARY.json' $summary
    Write-Json '01_UNITS.json' @($rows.ToArray())
    Write-Json '02_EXCEPTIONS.json' $exceptions
    Write-Json '03_FILE_OBSERVATIONS.json' @($fileObservations.Values|Sort-Object relativePath)
    Write-Json '04_ANOMALIES.json' @($anomalies.ToArray())
    Write-Json '05_CHECKS.json' @($checks.ToArray())
    Assert-SafeOutput
    $csvPath=Join-Path $proof 'UNITS.csv'
    @($rows.ToArray()|Sort-Object console,gameName,entryPath|Select-Object gameId,variantId,romUnitId,console,gameName,variantName,kind,entryPath,physicalEligibleInSnapshot,metadataState,fileCount,knownBytes) | Export-Csv -LiteralPath $csvPath -Encoding UTF8 -NoTypeInformation
    $report=@('# EXPORT ENGINE E1 — INVENTAIRE DEV READ-ONLY','','**Pas un plan de copie**.','',('Scan existant : '+$cat.scan.scanId),('Jeux : '+$gameIds.Count+' ; variantes : '+$variantIds.Count+' ; unités : '+$unitIds.Count),('Unités physiquement admissibles dans ce snapshot : '+$readyRows.Count),('Unités bloquées : '+$blockedRows.Count),('Fichiers ROM uniques référencés par unités prêtes : '+$uniqueReady.Count),('Volume ROM connu (sans double comptage) : '+$uniqueBytes+' octets'),('Metadata : '+$summary.resolvedMetadata+' resolved / '+$summary.unresolvedMetadata+' unresolved / '+$summary.metadataEntries+' total'),('Exceptions dérivées : '+$exceptions.Count),'','Les nouveaux fichiers non repertories dans le snapshot ne peuvent pas etre verifies sans nouveau scan. E1 ne prepare aucune destination et ne lie aucun media par ressemblance. Aucun hash ROM.')
    Assert-SafeOutput
    [IO.File]::WriteAllLines((Join-Path $proof 'REPORT.md'),[string[]]$report,(New-Object Text.UTF8Encoding($false)))
    $zip=Join-Path $ProofOutputDirectory ('RM_EXPORT_E1_READONLY_DEV_'+$runId+'_PROOFS.zip')
    Assert-SafeOutput
    Compress-Archive -Path (Join-Path $proof '*') -DestinationPath $zip -CompressionLevel Optimal -ErrorAction Stop
    Assert-SafeOutput
    Write-Host ('E1 : COMPLETE / '+$rows.Count+' units / '+$readyRows.Count+' ready / '+$blockedRows.Count+' blocked')
    Write-Host ('Metadata from chosen snapshot : '+$summary.resolvedMetadata+' / '+$summary.unresolvedMetadata)
    Write-Host ('Proof ZIP : '+$zip)
    Write-Host ('SHA256    : '+(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)
    Write-Host 'STOP : NO PHYSICAL EXPORT / CHAT CONTROLE'
} catch {
    $e=$_.Exception.Message
    # If an output root or any ancestor became a reparse point, do not write a diagnostic there.
    Assert-SafeOutput
    Write-Json 'ERROR.json' ([pscustomobject]@{status='E1_INCOMPLETE';error=$e;stack=$_.ScriptStackTrace;runId=$runId;noCopy=$true;noTransaction=$true})
    $diag=Join-Path $ProofOutputDirectory ('RM_EXPORT_E1_DEV_'+$runId+'_DIAGNOSTIC.zip')
    Assert-SafeOutput
    Compress-Archive -Path (Join-Path $proof '*') -DestinationPath $diag -CompressionLevel Optimal -ErrorAction Stop
    Assert-SafeOutput
    Write-Host ('E1 INCOMPLETE: '+$e) -ForegroundColor Yellow
    Write-Host ('Diagnostic: '+$diag)
    throw
}
