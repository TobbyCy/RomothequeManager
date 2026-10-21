Set-StrictMode -Version 2.0

function Read-RmReferenceText {
    param($Context,[string]$Path)
    $native=Get-RmNativePath ($Context.Root+'\'+$Path.Replace('/','\'))
    try { return Read-RmAllText ($Context.Root+'\'+$Path.Replace('/','\')) (New-Object Text.UTF8Encoding($false,$true)) }
    catch [Text.DecoderFallbackException] {
        Add-RmAnomaly $Context 'REFERENCE_ENCODING_FALLBACK' $Path 'CUE/M3U decoded as Windows-1252 after invalid UTF-8.'
        return Read-RmAllText ($Context.Root+'\'+$Path.Replace('/','\')) ([Text.Encoding]::GetEncoding(1252))
    }
}

function Get-RmDependencyGraph {
    param($Context,$Inventory)
    $graph=@{}
    foreach ($f in $Inventory.files) {
        if ($f.relativePath -notlike 'ROMS/*' -or $f.extension -notin @('.cue','.m3u')) {continue}
        $refs=New-Object 'System.Collections.Generic.List[string]'
        $missing=New-Object 'System.Collections.Generic.List[string]'
        $complete=$true
        try {
            $text=Read-RmReferenceText $Context $f.relativePath
            $base=$f.relativePath.Substring(0,$f.relativePath.LastIndexOf('/'))
            foreach ($line in ($text -split '\r?\n')) {
                $ref=$null
                if ($f.extension -eq '.cue') {
                    if ($line -match '^\s*FILE\s+"([^"]+)"\s+\S+\s*$') {$ref=$Matches[1]}
                    elseif ($line -match '^\s*FILE\s+(.+?)\s+(BINARY|MOTOROLA|WAVE|AIFF|MP3)\s*$') {$ref=$Matches[1]}
                    elseif ($line -match '^\s*FILE\b') {throw 'Unsupported/malformed CUE FILE line.'}
                } elseif ($line.Trim() -and !$line.Trim().StartsWith('#')) { $ref=$line.Trim().Trim([char]0xFEFF) }
                if ($null -eq $ref) {continue}
                try { $resolved=Resolve-RmRelativePath $base $ref ('ROMS/'+$f.console) }
                catch {
                    $missing.Add($ref); $complete=$false
                    Add-RmAnomaly $Context 'ROM_REFERENCE_INVALID' $f.relativePath $_.Exception.Message 'Error' $f.console
                    continue
                }
                if ($Inventory.byPath.ContainsKey($resolved)) { if (!$refs.Contains($resolved)) {$refs.Add($resolved)} }
                else {
                    $missing.Add($resolved); $complete=$false
                    Add-RmAnomaly $Context 'ROM_DEPENDENCY_MISSING' $resolved ('Referenced by '+$f.relativePath) 'Error' $f.console
                }
            }
            if ($refs.Count -eq 0) {
                $complete=$false
                Add-RmAnomaly $Context 'ROM_REFERENCE_EMPTY' $f.relativePath 'Reference file has no resolvable dependencies.' 'Error' $f.console
            }
        } catch {
            $complete=$false
            Add-RmAnomaly $Context 'ROM_REFERENCE_READ_ERROR' $f.relativePath $_.Exception.Message 'Error' $f.console
        }
        $graph[$f.relativePath]=[pscustomobject]@{references=$refs.ToArray();missing=$missing.ToArray();complete=$complete}
    }
    return $graph
}

function Expand-RmDependency {
    param($Context,$Inventory,$Graph,[string]$Path,$Members,$Missing,$Visiting,$Visited,$State)
    if ($Visiting.ContainsKey($Path)) {
        $State.complete=$false
        Add-RmAnomaly $Context 'ROM_REFERENCE_CYCLE' $Path 'Cyclic CUE/M3U dependency.' 'Error'
        return
    }
    if ($Visited.ContainsKey($Path)) {return}
    $Visiting[$Path]=$true
    if ($Inventory.byPath.ContainsKey($Path)) { $Members[$Path]=$Inventory.byPath[$Path] }
    if ($Graph.ContainsKey($Path)) {
        $edge=$Graph[$Path]
        if (!$edge.complete) {$State.complete=$false}
        foreach ($m in $edge.missing) {$Missing[$m]=$true}
        foreach ($ref in $edge.references) { Expand-RmDependency $Context $Inventory $Graph $ref $Members $Missing $Visiting $Visited $State }
    }
    $Visiting.Remove($Path); $Visited[$Path]=$true
}

function Get-RmRomUnits {
    param($Context,$Inventory,$Metadata)
    $graph=Get-RmDependencyGraph $Context $Inventory
    $referenced=@{}
    foreach ($edge in $graph.Values) {foreach ($r in $edge.references) {$referenced[$r]=$true}}
    $units=New-Object 'System.Collections.Generic.List[object]'
    $folderMembers=@{}
    $folderPaths=New-Object 'System.Collections.Generic.List[string]'
    foreach ($f in $Inventory.files) {
        if ($f.relativePath -match '^(ROMS/PS3/.+)/PS3_GAME/PARAM\.SFO$') { $folderPaths.Add($Matches[1]) }
        elseif ($f.relativePath -match '^(ROMS/PS3/.+)/PARAM\.SFO$') {
            $p=$Matches[1]
            if ($Inventory.byPath.ContainsKey($p+'/USRDIR/EBOOT.BIN')) { $folderPaths.Add($p) }
        }
    }
    foreach ($folder in @($folderPaths | Sort-Object -Unique)) {
        # PS3_GAME is a component, never an additional game container.
        if ($folder -match '/PS3_GAME$') {continue}
        $members=@($Inventory.files | Where-Object {$_.relativePath.StartsWith($folder+'/',[StringComparison]::OrdinalIgnoreCase)})
        foreach ($f in $members) {$folderMembers[$f.relativePath]=$true}
        $units.Add([pscustomobject]@{technicalKey=(Get-RmTechnicalKey 'PS3' $folder);console='PS3';kind='directory';
            entryPath=$folder;fileIds=@($members | ForEach-Object {$_.fileId});missingDependencies=@();
            complete=$Inventory.complete;classificationEvidence=@('ps3-folder-layout');metadata=@()})
    }
    $packageFolders=@{}
    foreach ($f in $Inventory.files) {
        if ($f.console -ne 'PS3' -or $f.relativePath -notlike 'ROMS/PS3/*' -or $f.extension -ne '.pkg' -or $folderMembers.ContainsKey($f.relativePath)) {continue}
        $parent=$f.relativePath.Substring(0,$f.relativePath.LastIndexOf('/'))
        if (!$packageFolders.ContainsKey($parent)) {$packageFolders[$parent]=@()};$packageFolders[$parent]+=@($f)
    }
    foreach ($parent in @($packageFolders.Keys | Sort-Object)) {
        $pkgs=@($packageFolders[$parent] | Sort-Object relativePath)
        $raps=@($Inventory.files | Where-Object {$_.extension -eq '.rap' -and $_.relativePath.Substring(0,$_.relativePath.LastIndexOf('/')) -eq $parent})
        $ids=@($pkgs | ForEach-Object {$_.fileId})+@($raps | ForEach-Object {$_.fileId})
        $ambiguous=($pkgs.Count -ne 1 -or $raps.Count -gt 1 -or $parent -eq 'ROMS/PS3')
        $entry=$parent;$primary=$null;$roles=@()
        if (!$ambiguous) {
            $primary=$pkgs[0].fileId
            $roles+=@([pscustomobject]@{fileId=$primary;role='package-primary'})
            foreach ($rap in $raps) {$roles+=@([pscustomobject]@{fileId=$rap.fileId;role='package-rap'})}
        } else {Add-RmAnomaly $Context 'PS3_PACKAGE_AMBIGUOUS' $parent 'Multiple packages/RAPs or console-root package layout: primary roles require validation.' 'Warning' 'PS3'}
        foreach ($id in $ids) {$file=@($Inventory.files | Where-Object {$_.fileId -eq $id})[0];$folderMembers[$file.relativePath]=$true}
        $units.Add([pscustomobject]@{technicalKey=(Get-RmTechnicalKey 'PS3' $entry);console='PS3';kind='ps3-package';entryPath=$entry;
            fileIds=$ids;missingDependencies=@();complete=(!$ambiguous -and $Inventory.complete);
            classificationEvidence=@('package-folder-layout');metadata=@();primaryPackageFileId=$primary;
            packageRoles=$roles;packageRoleStatus=$(if ($ambiguous) {'needs-validation'} else {'single-package-single-folder'})})
    }
    foreach ($f in $Inventory.files | Sort-Object relativePath) {
        if ($f.relativePath -notlike 'ROMS/*' -or $folderMembers.ContainsKey($f.relativePath)) {continue}
        if (!$Context.Config.extensions.ContainsKey($f.console)) {continue}
        if ($Context.Config.extensions[$f.console] -notcontains $f.extension) {continue}
        if ($referenced.ContainsKey($f.relativePath)) {continue}
        $members=@{}; $missing=@{}; $state=@{complete=$true}
        Expand-RmDependency $Context $Inventory $graph $f.relativePath $members $missing @{} @{} $state
        $kind='file'; if ($graph.ContainsKey($f.relativePath)) {$kind='file-set'}
        $units.Add([pscustomobject]@{technicalKey=(Get-RmTechnicalKey $f.console $f.relativePath);console=$f.console;
            kind=$kind;entryPath=$f.relativePath;fileIds=@($members.Values | Sort-Object relativePath | ForEach-Object {$_.fileId});
            missingDependencies=@($missing.Keys | Sort-Object);complete=$state.complete;
            classificationEvidence=@('extension-candidate');metadata=@()})
        if ($f.extension -in @('.zip','.7z')) {
            Add-RmAnomaly $Context 'ARCHIVE_UNINSPECTED' $f.relativePath 'Archive contents were not inspected.' 'Warning' $f.console
        }
    }
    # Cycles can leave every reference file marked as subordinate. Retain an auditable unit.
    foreach ($path in @($graph.Keys | Sort-Object)) {
        $f=$Inventory.byPath[$path]
        $covered=@($units | Where-Object {$_.fileIds -contains $f.fileId})
        if ($covered.Count -gt 0 -or $folderMembers.ContainsKey($path)) {continue}
        $members=@{}; $missing=@{}; $state=@{complete=$true}
        Expand-RmDependency $Context $Inventory $graph $path $members $missing @{} @{} $state
        $units.Add([pscustomobject]@{technicalKey=(Get-RmTechnicalKey $f.console $path);console=$f.console;kind='file-set';
            entryPath=$path;fileIds=@($members.Values | Sort-Object relativePath | ForEach-Object {$_.fileId});
            missingDependencies=@($missing.Keys);complete=$state.complete;classificationEvidence=@('reference-structure');metadata=@()})
    }
    $byEntry=@{};$byFile=@{}
    foreach ($unit in $units) {Add-RmPhysicalUnitIndex $unit $byEntry $byFile}
    # Folder metadata is preserved as metadata, never promoted to an extra ROM.
    # An exact PS3 container path can enrich its already detected structural unit.
    foreach ($source in $Metadata.sources) {
        foreach ($entry in $source.folderMetadata) {
            $matches=@()
            if ($entry.resolvedPath -and $byEntry.ContainsKey($entry.resolvedPath)) {$matches=@($byEntry[$entry.resolvedPath] | Where-Object {$_.kind -in @('directory','ps3-package')})}
            if ($matches.Count -eq 1) {$matches[0].metadata+=@($entry)}
        }
    }
    foreach ($entry in $Metadata.entries) {
        $matches=@()
        if ($entry.resolvedPath) {
            if ($byEntry.ContainsKey($entry.resolvedPath)) {$matches=@($byEntry[$entry.resolvedPath])}
            if ($matches.Count -eq 0 -and $Inventory.byPath.ContainsKey($entry.resolvedPath)) {
                $file=$Inventory.byPath[$entry.resolvedPath]
                if ($byFile.ContainsKey($file.fileId)) {$matches=@($byFile[$file.fileId])}
            }
        }
        if ($matches.Count -eq 1) {
            $matches[0].metadata=@($matches[0].metadata)+@($entry)
            $matches[0].classificationEvidence=@($matches[0].classificationEvidence)+@('es-de-path')
            continue
        }
        if ($matches.Count -gt 1) {
            Add-RmAnomaly $Context 'METADATA_ROM_AMBIGUOUS' $entry.resolvedPath 'Metadata path belongs to several ROM units.' 'Warning' $entry.console
        }
        $path=$entry.resolvedPath
        # An exact XML path may supply structural evidence for a real file of an
        # uncommon extension. Never allocate an identity for an absent reference.
        if ($path -and $Inventory.byPath.ContainsKey($path) -and $matches.Count -eq 0 -and $Inventory.byPath[$path].type -ne 'auxiliary') {
            $unit=[pscustomobject]@{technicalKey=(Get-RmTechnicalKey $entry.console $path);console=$entry.console;
                kind='file';entryPath=$path;fileIds=@($Inventory.byPath[$path].fileId);missingDependencies=@();complete=$true;
                classificationEvidence=@('es-de-path');metadata=@($entry)}
            $units.Add($unit);Add-RmPhysicalUnitIndex $unit $byEntry $byFile
        }
    }
    $idMap=@{};foreach ($f in $Inventory.files) {$idMap[$f.fileId]=$f}
    foreach ($unit in $units) {
        foreach ($id in $unit.fileIds) {
            $f=$idMap[$id];if ($f.type -eq 'auxiliary') {continue};$f.type='rom';$role='rom-dependency';if ($f.relativePath -eq $unit.entryPath) {$role='rom-entry'}
            if ($unit.kind -eq 'ps3-package') {
                $assignedRole=@($unit.packageRoles | Where-Object {$_.fileId -eq $id})
                if ($assignedRole.Count -eq 1) {$role=$assignedRole[0].role} else {$role='package-unresolved'}
            }
            $f.roles=@(@($f.roles | Where-Object {$_ -ne 'unassigned'})+@($role) | Select-Object -Unique)
        }
    }
    return ,$units.ToArray()
}
Export-ModuleMember -Function Get-RmRomUnits

function Add-RmPhysicalUnitIndex {
    param($Unit,$ByEntry,$ByFile)
    if (!$ByEntry.ContainsKey($Unit.entryPath)) {$ByEntry[$Unit.entryPath]=@()}
    $ByEntry[$Unit.entryPath]+=@($Unit)
    foreach ($id in $Unit.fileIds) {
        if (!$ByFile.ContainsKey($id)) {$ByFile[$id]=@()}
        $ByFile[$id]+=@($Unit)
    }
}
