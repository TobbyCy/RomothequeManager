#Requires -Version 5.1
<#
.SYNOPSIS
    E1 preflight independent et sans moteur (Windows PowerShell 5.1).
.DESCRIPTION
    Controle les 21 sources Git, les 4 racines, l'identite logique du volume,
    les six JSON et la coherence du snapshot. Ne lance jamais E1, Import-Module,
    CommitGate, scan ni export. Par defaut: aucune ecriture de fichier.
    Deux switches explicites permettent, APRES REVUE, une sonde d'ecriture
    uniquement dans ProofOutputDirectory et la sauvegarde de rapports locaux.
    Les rapports prives NE doivent PAS etre transmis a GitHub.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$GitRoot,
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)][string]$StateRoot,
    [Parameter(Mandatory=$true)][string]$SnapshotRoot,
    [Parameter(Mandatory=$true)][string]$ProofOutputDirectory,
    [ValidatePattern('^[A-Za-z]$')][string]$ExpectedDriveLetter = 'H',
    [string]$ExpectedVolumeSerial,
    [string]$ExpectedDiskSerial,
    [string]$ExpectedSnapshotScanId,
    [string]$ExpectedLibraryId,
    [switch]$ProbeProofWrite,
    [switch]$SaveReports
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1 -or
    $PSVersionTable.PSEdition -ne 'Desktop' -or (-not [Environment]::Is64BitProcess)) {
    throw 'PREFLIGHT_REQUIRES_WINDOWS_PS51_X64'
}

# Verrou explicite : seul ce commit Git est admis. Ne pas changer sans verdict.
$ExpectedCommit = '00c47bb2bfcd60ed2e0b64f43869d169e41ffffe'
$ExpectedRepo = 'TobbyCy/RomothequeManager-App'
$files = @('associations.json','catalogue.json','identity_registry.json')
$checks = New-Object 'System.Collections.Generic.List[object]'
$fullHashes = @{}
$private = [ordered]@{}

function Add-Check([string]$Id,[string]$Status,[string]$Reason) {
    if ($Status -cnotin @('PASS','FAIL','HOLD')) {throw 'PREFLIGHT_INVALID_CHECK_STATUS'}
    $checks.Add([pscustomobject]@{id=$Id;status=$Status;detail=$Reason}) | Out-Null
}
function Test-Check([string]$Id,[bool]$Value,[string]$Reason) {
    Add-Check $Id $(if($Value){'PASS'}else{'FAIL'}) $Reason
}
function Normalize-Directory([string]$InputPath,[string]$Role) {
    if ([string]::IsNullOrWhiteSpace($InputPath)) {throw ('EMPTY_PATH_'+$Role)}
    $full = [IO.Path]::GetFullPath($InputPath)
    if (-not (Test-Path -LiteralPath $full -PathType Container)) {throw ('DIRECTORY_NOT_FOUND_'+$Role)}
    Assert-NoReparse $full $Role
    $drive = [IO.Path]::GetPathRoot($full)
    if (-not $full.Equals($drive,[StringComparison]::OrdinalIgnoreCase)) {$full=$full.TrimEnd('\')}
    return $full
}
function Assert-NoReparse([string]$InputPath,[string]$Role) {
    $path = [IO.Path]::GetFullPath($InputPath)
    $current = New-Object IO.DirectoryInfo($path)
    while ($null -ne $current) {
        if (-not $current.Exists) {throw ('PATH_COMPONENT_NOT_FOUND_'+$Role)}
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw ('REPARSE_POINT_DENIED_'+$Role)
        }
        $current = $current.Parent
    }
}
function Test-Overlap([string]$A,[string]$B) {
    $a1=$A.TrimEnd('\'); $b1=$B.TrimEnd('\')
    if ($a1.Equals($b1,[StringComparison]::OrdinalIgnoreCase)) {return $true}
    return ($a1.StartsWith(($b1+'\'),[StringComparison]::OrdinalIgnoreCase) -or
            $b1.StartsWith(($a1+'\'),[StringComparison]::OrdinalIgnoreCase))
}
function Assert-RegularFile([string]$Parent,[string]$Name) {
    $path=Join-Path $Parent $Name
    Assert-NoReparse ([IO.Path]::GetDirectoryName($path)) 'JsonParent'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw 'REQUIRED_JSON_FILE_MISSING'}
    $item=Get-Item -LiteralPath $path -Force
    if ($item -isnot [IO.FileInfo] -or
        (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw 'REPARSE_OR_NONREGULAR_JSON_FILE'
    }
    return $path
}
function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Read-Json([string]$Path) {
    # Le catalogue peut depasser 90 Mo. Un seul objet JSON est parse a la fois.
    $utf8 = New-Object Text.UTF8Encoding($false,$true)
    $raw = [IO.File]::ReadAllText($Path,$utf8)
    return ConvertFrom-Json -InputObject $raw -ErrorAction Stop
}
function Get-TextHash([string]$Text) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try {
        $v=[Text.Encoding]::UTF8.GetBytes($Text)
        return ([BitConverter]::ToString($sha.ComputeHash($v))).Replace('-','').ToLowerInvariant()
    } finally {$sha.Dispose()}
}
function Invoke-GitText([string[]]$CommandArgs) {
    $old=$env:GIT_OPTIONAL_LOCKS
    $env:GIT_OPTIONAL_LOCKS='0'
    try {
        $response = @(& $script:gitExe.Source -C $script:GitRoot @CommandArgs 2>&1)
        if ($LASTEXITCODE -ne 0) {throw ('GIT_COMMAND_FAILED_'+$CommandArgs[0])}
        return ,@($response | ForEach-Object { [string]$_ })
    } finally {
        if ($null -eq $old) {Remove-Item Env:\GIT_OPTIONAL_LOCKS -ErrorAction SilentlyContinue}
        else {$env:GIT_OPTIONAL_LOCKS=$old}
    }
}
function Get-GitBlobSha256([string]$RelativePath) {
    # On hache le blob Git original, non le checkout susceptible d'etre en CRLF.
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName=$script:gitExe.Source
    $psi.Arguments='-C "'+$script:GitRoot+'" cat-file blob "'+$script:ExpectedCommit+':'+$RelativePath+'"'
    $psi.UseShellExecute=$false
    $psi.RedirectStandardOutput=$true
    $psi.RedirectStandardError=$true
    $psi.CreateNoWindow=$true
    $p=[Diagnostics.Process]::Start($psi)
    $sha=[Security.Cryptography.SHA256]::Create()
    try {
        $digest=$sha.ComputeHash($p.StandardOutput.BaseStream)
        $errors=$p.StandardError.ReadToEnd()
        $p.WaitForExit()
        if ($p.ExitCode -ne 0) {throw 'GIT_BLOB_UNAVAILABLE'}
        return ([BitConverter]::ToString($digest)).Replace('-','').ToLowerInvariant()
    } finally {$sha.Dispose();$p.Dispose()}
}
function Get-CheckoutEolNormalizedSha256([string]$Path) {
    # Strictement CRLF -> LF, sur octets. Aucune reecriture du fichier.
    $bytes=[IO.File]::ReadAllBytes($Path)
    $buf=New-Object IO.MemoryStream
    $sha=[Security.Cryptography.SHA256]::Create()
    try {
        for($i=0;$i -lt $bytes.Length;$i++) {
            if($bytes[$i] -eq 13 -and ($i+1) -lt $bytes.Length -and $bytes[$i+1] -eq 10){continue}
            $buf.WriteByte($bytes[$i])
        }
        return ([BitConverter]::ToString($sha.ComputeHash($buf.ToArray()))).Replace('-','').ToLowerInvariant()
    } finally {$sha.Dispose();$buf.Dispose()}
}
function File-PrivateWrite([string]$Path,[string]$Content) {
    $bytes=(New-Object Text.UTF8Encoding($false)).GetBytes($Content)
    $stream=New-Object IO.FileStream($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {$stream.Write($bytes,0,$bytes.Length)} finally {$stream.Dispose()}
}

# Les 19 empreintes historiques proviennent du CI-0 approuve (baseline immuable).
$expectedSources = [ordered]@{
 'Initialize.ps1'='178c318fc270c3afc090c8edd644dbb47656e61c19c30b3894ca07780af430c6'
 'Modules/Associations.psm1'='f16335429ccb2252210aadb242a051ed626a850d3729e5879c8a390d358780d2'
 'Modules/Canonical.psm1'='bae923a4585c40b4a4d20d8b1caa9e16bedf54be31a9ea8952f5b523842b2864'
 'Modules/Catalogue.psm1'='3631c5adad951628cb8312311c9af9d88b4af172dfcf81aaad0b5a3df75076ae'
 'Modules/Configuration.psm1'='2a90ecd75fe137a4f5c02ae542fe132af98079664842c9df9819c54660424424'
 'Modules/DecisionSchema.psm1'='0a42e17d5f06a7c8cc3cb64ee3dab6402df805fd4d8e00be95a85105aa25e793'
 'Modules/Identity.psm1'='b9ba591f35f46b0cafa1b62595a21146db9e98415f3a0af2db2426c54c80eb28'
 'Modules/Inventory.psm1'='3511e9857f186ca39eb10b4b6a0adc728792cf3f59942ac4c4fcd4c6b15edc83'
 'Modules/MediaMatcher.psm1'='6be444fe3db82c92d15b4414ce9aab3a39e9870267235fb9e0051d407322a5a4'
 'Modules/MediaModel.psm1'='b3bcfbe222886f6b78a104b6d17c553c12fbf4ad556e315edd9969672894a423'
 'Modules/Metadata.psm1'='328f73b259c435887ee17d122d2fa83d72bc76c1174dc1fcc41bedbfad8e9b77'
 'Modules/PersistentIdentity.psm1'='58e1d8203c71bc36ad00d294602e12a4310718a53a572dcfdf45014a576020b2'
 'Modules/Projection.psm1'='1b915efacc23f2fb3f79fca7b1b18cb2eb3e46bee359376edbf4d0a0fb5b81df'
 'Modules/Proposals.psm1'='42a6c951fc87f8658e2f3b5113af50b4c79ceb21b02412e051c3da4edb851201'
 'Modules/Reconciliation.psm1'='08ba836d5f70cffc1caf1795176f5e405633116d63ceee6870424bdf80355405'
 'Modules/RomResolver.psm1'='a01c347d20fabfae58b34065adf9a5fa0e172780bca5e92ec98a3528d4cff240'
 'Modules/StateCodec.psm1'='fb6a46bc6e9e5786e3800c67e82abe6937343d8f19952b861f053ade6786e1d4'
 'Modules/Transactions.psm1'='827bf312b10cef21059d3921ce5f0d47e4b475e64014a68df03ede47c8b063af'
 'Modules/Utils.psm1'='28318a751b276250d4b3224ba89008261714dfa4f680b859023fb31400a7c827'
 'Export/ExportE1.psm1'='bab2e86103f14ef862a4864356c3e9849a927f266ab572fc7a21b558f81655ab'
 'Export/Invoke-ExportE1InventoryReadOnly.ps1'='bce63a2b879ea272f7633df8da5472a649fd065221d37989a3e1d69bc5ae0f00'
}

try {
    $GitRoot=Normalize-Directory $GitRoot 'GitRoot'
    $Root=Normalize-Directory $Root 'Root'
    $StateRoot=Normalize-Directory $StateRoot 'StateRoot'
    $SnapshotRoot=Normalize-Directory $SnapshotRoot 'SnapshotRoot'
    $ProofOutputDirectory=Normalize-Directory $ProofOutputDirectory 'ProofOutputDirectory'

    # La separation cherche uniquement les relations ancetre/descendant, pas un parent commun.
    foreach($protected in @($Root,$StateRoot,$SnapshotRoot,$GitRoot)) {
        if(Test-Overlap $ProofOutputDirectory $protected) {throw 'PROOF_OUTPUT_OVERLAPS_PROTECTED_ROOT'}
    }
    $isolated=@($GitRoot,$Root,$StateRoot,$SnapshotRoot)
    for($x=0;$x -lt $isolated.Count;$x++) {
        for($y=$x+1;$y -lt $isolated.Count;$y++) {
            if(Test-Overlap $isolated[$x] $isolated[$y]) {throw 'DEV_LIBRARY_STATE_SNAPSHOT_NOT_ISOLATED'}
        }
    }
    foreach($item in @($env:TEMP,$env:TMP,[IO.Path]::GetTempPath())) {
        if([string]::IsNullOrWhiteSpace([string]$item)) {throw 'COMPILER_TEMP_UNSET'}
        $t=Normalize-Directory ([string]$item) 'CompilerTemp'
        foreach($protected in @($Root,$StateRoot,$SnapshotRoot)) {
            if(Test-Overlap $t $protected) {throw 'COMPILER_TEMP_OVERLAPS_PROTECTED_ROOT'}
        }
    }
    Add-Check 'PATHS_NO_REPARSE_AND_SEPARATE' 'PASS' 'Ancetres sans reparse; aucune inclusion entre preuves et racines protegees.'
    Add-Check 'TEMP_TMP_DOTNET' 'PASS' 'Dossiers temporaires existants, sans redirection dans les racines protegees.'
    $private.paths=[ordered]@{git=$GitRoot;library=$Root;state=$StateRoot;snapshot=$SnapshotRoot;proofs=$ProofOutputDirectory}

    # Le StateRoot PROD attendu est derive de l'environnement LOCALAPPDATA actif.
    if([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {throw 'LOCALAPPDATA_MISSING'}
    $expectedState=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'RomothequeManager\RealCommitLot1')).TrimEnd('\')
    Test-Check 'STATE_ROOT_EXACT' $StateRoot.Equals($expectedState,[StringComparison]::OrdinalIgnoreCase) 'StateRoot = LOCALAPPDATA/RomothequeManager/RealCommitLot1.'

    # Controle Git avec GIT_OPTIONAL_LOCKS=0 ; pas d'appel reseau ni de commande de checkout.
    $script:gitExe=Get-Command git.exe -ErrorAction Stop
    $top=([string]((Invoke-GitText @('rev-parse','--show-toplevel'))[0])).Trim()
    Test-Check 'GIT_ROOT_EXACT' ([IO.Path]::GetFullPath($top).TrimEnd('\').Equals($GitRoot,[StringComparison]::OrdinalIgnoreCase)) 'Le dossier specifie est la racine du clone.'
    $gitHead=([string]((Invoke-GitText @('rev-parse','HEAD'))[0])).Trim().ToLowerInvariant()
    Test-Check 'GIT_COMMIT_LOCKED' ($gitHead -ceq $ExpectedCommit) 'HEAD = commit E1 approuve, pas simplement main.'
    $gitStatus=@(Invoke-GitText @('status','--porcelain=v1','--untracked-files=all'))
    Test-Check 'GIT_CLEAN' ($gitStatus.Count -eq 0 -or ($gitStatus.Count -eq 1 -and [string]::IsNullOrWhiteSpace($gitStatus[0]))) 'Aucun fichier modifie ou non suivi dans le clone.'
    $remote=([string]((Invoke-GitText @('remote','get-url','origin'))[0])).Trim()
    $isRemote=($remote -match '^(https://github\.com/|git@github\.com:|ssh://git@github\.com/)TobbyCy/RomothequeManager-App(\.git)?$')
    Test-Check 'GIT_ORIGIN' $isRemote 'Origin correspond au depot officiel.'
    $private.git=[ordered]@{head=$gitHead;origin=$remote}

    # Double preuve : blob Git original ET fichier checkout (CRLF accepte uniquement si equivalent LF).
    $gitMatches=0;$checkoutMatches=0;$historical=0;$e1=0
    foreach($pair in $expectedSources.GetEnumerator()) {
        $name=[string]$pair.Key;$expect=[string]$pair.Value
        $full=Join-Path $GitRoot ($name.Replace('/','\'))
        Assert-NoReparse ([IO.Path]::GetDirectoryName($full)) 'GitSourceParent'
        if(-not (Test-Path -LiteralPath $full -PathType Leaf)) {throw 'SOURCE_FILE_MISSING'}
        $source=Get-Item -LiteralPath $full -Force
        if(($source.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {throw 'SOURCE_REPARSE_DENIED'}
        if($source.Length -ge 2000000) {throw 'SOURCE_FILE_TOO_LARGE'}
        $blob=Get-GitBlobSha256 $name
        $loc=Get-Sha256 $full
        if($loc -cne $expect) {$loc=Get-CheckoutEolNormalizedSha256 $full}
        if($blob -ceq $expect) {$gitMatches++}
        if($loc -ceq $expect) {$checkoutMatches++}
        if($name.StartsWith('Export/')){$e1++}else{$historical++}
        $private['source_'+$name]=[ordered]@{expected=$expect;gitBlob=$blob;checkoutCanonical=$loc}
    }
    Test-Check 'GIT_BLOBS_21_SHA' ($gitMatches -eq 21) 'Empreintes des 19 sources historiques + 2 sources E1.'
    Test-Check 'CHECKOUT_21_SHA' ($checkoutMatches -eq 21) 'Octets de travail conformes; seule conversion CRLF->LF permise.'
    Test-Check 'SOURCE_SET_19_PLUS_2' ($historical -eq 19 -and $e1 -eq 2) '19 fichiers historiques et 2 scripts Export.'
    $manifest=Read-Json (Assert-RegularFile $GitRoot 'tests\ci-export-sources.json')
    $manifestEntries=@($manifest.sources)
    $validManifest=($manifest.schemaVersion -ceq '1.0.0' -and $manifestEntries.Count -eq 2)
    foreach($row in $manifestEntries) {
        $key=[string]$row.path
        if(-not $expectedSources.Contains($key) -or $key -notlike 'Export/*' -or
           ([string]$row.sha256).ToLowerInvariant() -cne [string]$expectedSources[$key]){$validManifest=$false}
    }
    Test-Check 'EXPORT_MANIFEST_2_SHA' $validManifest 'Manifeste E1 approuve et conforme aux constantes immuables.'

    # Le preflight ne parcourt aucune arborescence de ROM. Controle identite du volume choisi.
    $driveRoot=[IO.Path]::GetPathRoot($Root)
    $driveLetter=$ExpectedDriveLetter.ToUpperInvariant()
    Test-Check 'EXPECTED_DRIVE_LETTER' ($driveRoot.Equals(($driveLetter+':\'),[StringComparison]::OrdinalIgnoreCase)) 'Racine de la bibliotheque sur la lettre attendue.'
    $volumes=@(Get-CimInstance -ClassName Win32_LogicalDisk -Filter ("DeviceID='"+$driveLetter+":'"))
    if($volumes.Count -ne 1) {throw 'DRIVE_VOLUME_NOT_IDENTIFIED'}
    $v=$volumes[0]
    $actualSerial=([string]$v.VolumeSerialNumber).ToUpperInvariant()
    if([string]::IsNullOrWhiteSpace($ExpectedVolumeSerial)) {
        Add-Check 'SSD_VOLUME_SERIAL_APPROVED' 'HOLD' 'Numero de serie du volume a confirmer par le Chat Controle.'
    } else {
        Test-Check 'SSD_VOLUME_SERIAL_APPROVED' ($actualSerial -ceq $ExpectedVolumeSerial.Trim().ToUpperInvariant()) 'Identifiant de volume compare a la valeur approuvee.'
    }
    $diskDetails=$null
    try {
        $partition=Get-Partition -DriveLetter $driveLetter -ErrorAction Stop
        $disk=Get-Disk -Number $partition.DiskNumber -ErrorAction Stop
        $diskDetails=[ordered]@{number=$disk.Number;friendlyName=$disk.FriendlyName;serialNumber=$disk.SerialNumber;busType=[string]$disk.BusType;uniqueId=$disk.UniqueId}
        if([string]::IsNullOrWhiteSpace($ExpectedDiskSerial)) {
            Add-Check 'SSD_DISK_WITNESS' 'HOLD' 'Identite materielle relevee, serie physique a approuver.'
        } else {
            Test-Check 'SSD_DISK_WITNESS' ([string]$disk.SerialNumber).Trim().Equals($ExpectedDiskSerial.Trim(),[StringComparison]::OrdinalIgnoreCase) 'Serie disque comparee a la valeur approuvee.'
        }
    } catch {
        Add-Check 'SSD_DISK_WITNESS' 'HOLD' 'Identification disque non accessible : verification manuelle necessaire.'
    }
    $private.volume=[ordered]@{drive=$driveLetter;volumeLabel=$v.VolumeName;serialNumber=$actualSerial;fileSystem=$v.FileSystem;driveType=$v.DriveType;sizeBytes=$v.Size;disk=$diskDetails}

    # Six JSON precis seulement ; le snapshot donne doit etre selectionne explicitement.
    $jsonPaths=@{}
    foreach($kind in @('StateRoot','SnapshotRoot')) {
        $dir=if($kind -eq 'StateRoot'){$StateRoot}else{$SnapshotRoot}
        foreach($name in $files) {
            $key=$kind+'|'+$name
            $jsonPaths[$key]=Assert-RegularFile $dir $name
            $fullHashes[$key]=Get-Sha256 $jsonPaths[$key]
        }
    }
    Add-Check 'SIX_JSON_PRESENT_AND_SHA256' 'PASS' 'Six fichiers JSON reguliere, sans reparse, avec SHA256 releves.'
    $assoc=Read-Json $jsonPaths['SnapshotRoot|associations.json']
    $stateAssoc=Read-Json $jsonPaths['StateRoot|associations.json']
    $snapshotReg=Read-Json $jsonPaths['SnapshotRoot|identity_registry.json']
    $stateReg=Read-Json $jsonPaths['StateRoot|identity_registry.json']
    $snapshotCat=Read-Json $jsonPaths['SnapshotRoot|catalogue.json']
    $stateCat=Read-Json $jsonPaths['StateRoot|catalogue.json']

    $snapDecisions=@($assoc.decisions).Count
    $stateDecisions=@($stateAssoc.decisions).Count
    $snapTransactions=@($assoc.transactions).Count
    $stateTransactions=@($stateAssoc.transactions).Count
    Test-Check 'DECISIONS_545_BOTH' ($snapDecisions -eq 545 -and $stateDecisions -eq 545) '545 decisions dans snapshot et StateRoot (reference historique).'
    Test-Check 'TRANSACTIONS_7_BOTH' ($snapTransactions -eq 7 -and $stateTransactions -eq 7) '7 transactions dans snapshot et StateRoot.'
    Test-Check 'ASSOCIATIONS_BYTES_IDENTICAL' ($fullHashes['StateRoot|associations.json'] -ceq $fullHashes['SnapshotRoot|associations.json']) 'SHA256 des associations strictement identiques.'
    Test-Check 'ACTIVE_DECISIONS_545' (@($assoc.decisions|Where-Object{$_.state -eq 'active'}).Count -eq 545) 'Les 545 decisions du snapshot sont actives.'
    # The snapshot and the persistent catalogue are intentionally DIFFERENT scans.
    # Both share the same libraryId; neither is selected or replaced automatically.
    $libraryIds=@([string]$assoc.libraryId,[string]$stateAssoc.libraryId,
                  [string]$snapshotReg.libraryId,[string]$stateReg.libraryId,
                  [string]$snapshotCat.libraryId,[string]$stateCat.libraryId)
    $firstLibraryId=$libraryIds[0]
    Test-Check 'LIBRARY_ID_ALL_SIX' (-not [string]::IsNullOrWhiteSpace($firstLibraryId) -and
        @($libraryIds | Where-Object {$_ -cne $firstLibraryId}).Count -eq 0) 'libraryId identique dans les deux associations, deux registres et deux catalogues.'
    Test-Check 'PERSISTENT_METADATA_HISTORICAL_1038_4' ([int]$stateCat.statistics.metadataResolved -eq 1038 -and
        [int]$stateCat.statistics.metadataUnresolved -eq 4 -and
        [int]$stateCat.statistics.metadataGameEntries -eq 1042) 'Le catalogue persistant conserve son etat historique 1038/4 (total 1042).'
    Test-Check 'ISOLATED_METADATA_HISTORICAL_1041_1' ([int]$snapshotCat.statistics.metadataResolved -eq 1041 -and
        [int]$snapshotCat.statistics.metadataUnresolved -eq 1 -and
        [int]$snapshotCat.statistics.metadataGameEntries -eq 1042) 'Le snapshot isole conserve 1041/1 (total 1042), sans publication.'
    Test-Check 'PERSISTENT_METADATA_ACCOUNTING' (([int]$stateCat.statistics.metadataResolved +
        [int]$stateCat.statistics.metadataUnresolved) -eq [int]$stateCat.statistics.metadataGameEntries) 'Comptabilite metadata du catalogue persistant.'
    Test-Check 'SNAPSHOT_NOT_PERSISTENT_SCAN' (-not [string]::IsNullOrWhiteSpace([string]$stateCat.scan.scanId) -and
        -not [string]::IsNullOrWhiteSpace([string]$snapshotCat.scan.scanId) -and
        [string]$stateCat.scan.scanId -cne [string]$snapshotCat.scan.scanId) 'Snapshot isole et catalogue persistant ont deux scanId distincts.'
    # This only verifies observations at this moment; it does NOT establish historical non-publication.
    if([string]::IsNullOrWhiteSpace($ExpectedLibraryId)) {
        Add-Check 'LIBRARY_ID_EXPLICIT_APPROVAL' 'HOLD' 'libraryId exact extrait localement, approbation non fournie.'
    } else {
        Test-Check 'LIBRARY_ID_EXPLICIT_APPROVAL' ([string]$snapshotReg.libraryId -ceq $ExpectedLibraryId) 'libraryId conforme a la reference approuvee.'
    }
    $scan=$snapshotCat.scan
    Test-Check 'SNAPSHOT_COMPLETE' ([string]$scan.status -ceq 'complete') 'Etat du scan complet.'
    Test-Check 'SNAPSHOT_HASHMODE_NONE' ([string]$scan.hashMode -ceq 'none') 'Pas de hashing ROM durant le scan selectionne.'
    $observed=[IO.Path]::GetFullPath([string]$scan.rootObserved).TrimEnd('\')
    Test-Check 'SNAPSHOT_ROOT_OBSERVED' ($observed.Equals($Root,[StringComparison]::OrdinalIgnoreCase)) 'rootObserved correspond exactement a la racine choisie.'
    if([string]::IsNullOrWhiteSpace($ExpectedSnapshotScanId)) {
        Add-Check 'SNAPSHOT_SCANID_EXPLICIT_APPROVAL' 'HOLD' 'scanId exact extrait localement, approbation non fournie.'
    } else {
        Test-Check 'SNAPSHOT_SCANID_EXPLICIT_APPROVAL' ([string]$scan.scanId -ceq $ExpectedSnapshotScanId) 'scanId conforme a la reference approuvee.'
    }
    Test-Check 'METADATA_ACCOUNTING' (([int]$snapshotCat.statistics.metadataResolved+[int]$snapshotCat.statistics.metadataUnresolved) -eq [int]$snapshotCat.statistics.metadataGameEntries) 'Comptabilite resolved + unresolved = game entries.'
    $private.snapshot=[ordered]@{scanId=$scan.scanId;libraryId=$snapshotReg.libraryId;rootObserved=$scan.rootObserved;status=$scan.status;hashMode=$scan.hashMode;metadataResolved=$snapshotCat.statistics.metadataResolved;metadataUnresolved=$snapshotCat.statistics.metadataUnresolved;metadataGameEntries=$snapshotCat.statistics.metadataGameEntries;decisionsSnapshot=$snapDecisions;transactionsSnapshot=$snapTransactions;decisionsState=$stateDecisions;transactionsState=$stateTransactions}
    $private.persistentCatalogue=[ordered]@{scanId=$stateCat.scan.scanId;libraryId=$stateCat.libraryId;metadataResolved=$stateCat.statistics.metadataResolved;metadataUnresolved=$stateCat.statistics.metadataUnresolved;metadataGameEntries=$stateCat.statistics.metadataGameEntries}
    $snapshotCat=$null;$stateCat=$null # liberer la memoire apres inspection des deux catalogues

    foreach($key in @($jsonPaths.Keys)) {
        $after=Get-Sha256 $jsonPaths[$key]
        Test-Check ('INPUT_UNCHANGED_'+$key.Replace('|','_').Replace('.','_')) ($after -ceq $fullHashes[$key]) 'SHA256 recontrole apres les lectures et le preflight.'
        $private['json_'+$key]=[ordered]@{before=$fullHashes[$key];after=$after}
    }

    # La verification ACL est statique; seule la sonde facultative prouve CreateNew/Delete.
    $acl=Get-Acl -LiteralPath $ProofOutputDirectory -ErrorAction Stop
    Add-Check 'PROOF_DIRECTORY_ACL_READ' 'PASS' 'ACL accessibles ; droits effectifs d ecriture non prouves sans sonde.'
    if($ProbeProofWrite) {
        Assert-NoReparse $ProofOutputDirectory 'ProofOutputDirectory'
        $candidate=Join-Path $ProofOutputDirectory ('RM_E1_PREFLIGHT_PROBE_'+[guid]::NewGuid().ToString('N')+'.tmp')
        $stream=New-Object IO.FileStream($candidate,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try {$stream.WriteByte(80)} finally {$stream.Dispose()}
        Assert-NoReparse $ProofOutputDirectory 'ProofOutputDirectory'
        Remove-Item -LiteralPath $candidate -Force -ErrorAction Stop
        Add-Check 'PROOF_DIRECTORY_CREATE_DELETE' 'PASS' 'Sonde CreateNew/Delete uniquement dans ProofOutputDirectory.'
    } else {Add-Check 'PROOF_DIRECTORY_CREATE_DELETE' 'HOLD' 'HOLD ATTENDU : sondage des droits d ecriture interdit en mode sans ecriture ; tous les controles de lecture peuvent etre PASS.'}

    $failed=@($checks|Where-Object{$_.status -eq 'FAIL'}).Count
    $holds=@($checks|Where-Object{$_.status -eq 'HOLD'}).Count
    $result=if($failed -gt 0){'BLOCKED'}elseif($holds -gt 0){'REVIEW_REQUIRED'}else{'PREFLIGHT_PASS_NOT_E1_AUTHORIZATION'}
    $readChecks=@($checks | Where-Object { $_.id -ne 'PROOF_DIRECTORY_CREATE_DELETE' })
    $readResult=if(@($readChecks|Where-Object{$_.status -eq 'FAIL'}).Count -gt 0){'FAIL'}elseif(@($readChecks|Where-Object{$_.status -eq 'HOLD'}).Count -gt 0){'HOLD'}else{'PASS'}
    $stamp=[DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $redacted=[ordered]@{
        schemaVersion='1.0.0';control='E1_LOCAL_PREFLIGHT_ONLY';createdAtUtc=$stamp
        gitCommit=$ExpectedCommit;status=$result;readOnlyChecks=$readResult;proofWritePermission=$(if($ProbeProofWrite){'VERIFIED_SYNTHETIC_OR_EXPLICIT'}else{'HOLD_NOT_TESTED'});failed=$failed;holds=$holds
        driveLetter=$driveLetter;volumeSerialApproved=($checks|Where-Object{$_.id -eq 'SSD_VOLUME_SERIAL_APPROVED'}|Select-Object -First 1).status
        snapshotScanIdFingerprint=(Get-TextHash ([string]$scan.scanId)).Substring(0,12)
        libraryIdFingerprint=(Get-TextHash ([string]$snapshotReg.libraryId)).Substring(0,12)
        allStateHashesEqualAfter=(@($checks|Where-Object{$_.id -like 'INPUT_UNCHANGED_*' -and $_.status -eq 'PASS'}).Count -eq 6)
        checks=@($checks.ToArray());realE1Executed=$false;sourceScanExecuted=$false
        romFilesCopied=0;transactionsExecuted=0;stateWritten=$false;personalPathsIncluded=$false
        reviewRequired=$true
    }
    Write-Host ('E1_PREFLIGHT_ONLY status='+$result+' readOnlyChecks='+$readResult+' fail='+$failed+' hold='+$holds)
    foreach($c in $checks) {Write-Host ($c.status+' | '+$c.id+' | '+$c.detail)}
    Write-Host ('scanId_fingerprint='+$redacted.snapshotScanIdFingerprint+' libraryId_fingerprint='+$redacted.libraryIdFingerprint)

    if($SaveReports) {
        Assert-NoReparse $ProofOutputDirectory 'ProofOutputDirectory'
        $id=[guid]::NewGuid().ToString('N')
        $safeFile=Join-Path $ProofOutputDirectory ('RM_E1_PREFLIGHT_REDACTED_'+$id+'.json')
        $privateFile=Join-Path $ProofOutputDirectory ('RM_E1_PREFLIGHT_PRIVATE_'+$id+'.json')
        $private['status']=$result
        $private['approvedCommit']=$ExpectedCommit
        $private['noE1Executed']=$true
        $private['generatedUtc']=$stamp
        # Ce rapport PRIVE contient les chemins, identifiants et SHA complets: NE PAS PUBLIER.
        File-PrivateWrite $safeFile (ConvertTo-Json -InputObject $redacted -Depth 12)
        File-PrivateWrite $privateFile (ConvertTo-Json -InputObject $private -Depth 12)
        Assert-NoReparse $ProofOutputDirectory 'ProofOutputDirectory'
        Write-Host 'LOCAL_REPORTS_CREATED: two JSON files under approved ProofOutputDirectory (private + redacted)'
    }
    if($failed -gt 0) {exit 2}
    if($holds -gt 0) {exit 3}
    exit 0
} catch {
    # Ne pas divulguer les chemins/identifiants dans un message d erreur partageable.
    [Console]::Error.WriteLine('E1_PREFLIGHT_STOP: '+$_.Exception.Message.Split(':')[0])
    exit 4
}
