#Requires -Version 5.1
# True end-to-end invocation of Export E1, only on disposable SYNTHETIC state and fake ROM bytes.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) { throw 'PS51_REQUIRED' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$launcher = Join-Path $repo 'Export\Invoke-ExportE1InventoryReadOnly.ps1'
$ps51 = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
if (!(Test-Path -LiteralPath $ps51)) { throw 'PS51_EXE_MISSING' }
$workspace = Join-Path $env:RUNNER_TEMP ('rm-e1-e2e-' + [guid]::NewGuid().ToString('N'))
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { throw 'CI_RUNNER_TEMP_REQUIRED' }
$root = Join-Path $workspace 'Library'
$state = Join-Path $workspace 'State'
$snapshot = Join-Path $workspace 'Snapshot'
$output = Join-Path $workspace 'Proofs'
$junction = Join-Path $workspace 'RedirectJunction'
$junctionNested = Join-Path $output 'NestedRedirect'
$encoding = New-Object Text.UTF8Encoding($false)
function Require([bool]$Ok,[string]$Description) { if (!$Ok) { throw ('E1_INTEGRATION_FAILED: '+$Description) } }
function SaveJson([string]$File,$Data) { $json=ConvertTo-Json -InputObject $Data -Depth 90;[IO.File]::WriteAllText($File,$json,$encoding) }
function HashState {
    $result=@{}
    foreach ($folder in @($state,$snapshot)) {
        foreach ($name in @('associations.json','catalogue.json','identity_registry.json')) {
            $file=Join-Path $folder $name
            $result[$file]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        }
    }
    return ,$result
}
function ConfirmUnchanged($Expected) {
    $now=HashState
    foreach ($key in $Expected.Keys) { Require ($now[$key] -ceq $Expected[$key]) ('STATE_SHA_CHANGED '+$key) }
}
function InvokeLauncher([string]$ProofDir,[string]$Name) {
    $std = Join-Path $workspace ($Name + '.stdout.txt')
    $err = Join-Path $workspace ($Name + '.stderr.txt')
    $args = @('-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',('"'+$launcher+'"'),'-Root',('"'+$root+'"'),'-StateRoot',('"'+$state+'"'),'-SnapshotRoot',('"'+$snapshot+'"'),'-ProofOutputDirectory',('"'+$ProofDir+'"'))
    $p=Start-Process -FilePath $ps51 -ArgumentList $args -Wait -PassThru -RedirectStandardOutput $std -RedirectStandardError $err -WindowStyle Hidden
    $combined = ([IO.File]::ReadAllText($std) + "`n" + [IO.File]::ReadAllText($err))
    return [pscustomobject]@{Exit=$p.ExitCode;Text=$combined}
}
function NewJunction([string]$Link,[string]$Target) {
    # mkdir link is not a normal directory: verify it is genuinely a reparse point.
    # Native PowerShell junction creation avoids cmd.exe /c quoting ambiguities.
    try { $null=New-Item -ItemType Junction -Path $Link -Target $Target -ErrorAction Stop }
    catch { throw ('JUNCTION_CREATION_FAILED: '+$Link+' : '+$_.Exception.Message) }
    $item=Get-Item -LiteralPath $Link -Force
    Require (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) ('NOT_A_REAL_JUNCTION: '+$Link)
}
function RemoveJunction([string]$Link) {
    if (Test-Path -LiteralPath $Link) {
        $item=Get-Item -LiteralPath $Link -Force
        Require (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) ('JUNCTION_REPLACED: '+$Link)
        [IO.Directory]::Delete($Link,$false)
    }
}
try {
    foreach ($dir in @($workspace,$root,$state,$snapshot,$output,(Join-Path $root 'ROMS\GBA'))) {
        [IO.Directory]::CreateDirectory($dir) | Out-Null
    }
    $libraryId=[guid]::NewGuid().ToString()
    $scanId=[guid]::NewGuid().ToString()
    $f1=Join-Path $root 'ROMS\GBA\one.gba'
    $f2=Join-Path $root 'ROMS\GBA\two.gba'
    [IO.File]::WriteAllBytes($f1,[byte[]](1,2,3,4,5,6,7))
    [IO.File]::WriteAllBytes($f2,[byte[]](9,8,7,6))
    $files=@(
        [ordered]@{fileId='file-one';relativePath='ROMS/GBA/one.gba';sizeBytes=([long](Get-Item $f1).Length);modifiedAtUtc=(Get-Item $f1).LastWriteTimeUtc.ToString('o')},
        [ordered]@{fileId='file-two';relativePath='ROMS/GBA/two.gba';sizeBytes=([long](Get-Item $f2).Length);modifiedAtUtc=(Get-Item $f2).LastWriteTimeUtc.ToString('o')}
    )
    $catalogue=[ordered]@{
        libraryId=$libraryId
        scan=[ordered]@{scanId=$scanId;status='complete';hashMode='none';rootObserved=$root}
        statistics=[ordered]@{metadataResolved=2;metadataUnresolved=1;metadataGameEntries=3}
        manualMetadataResolvedIds=@()
        decisionEffects=@()
        files=$files
        metadataIndex=@(
            [ordered]@{metadataEntryId='game-one';entryKind='game'},
            [ordered]@{metadataEntryId='game-two';entryKind='game'},
            [ordered]@{metadataEntryId='folder-ps3';entryKind='folder'},
            [ordered]@{metadataEntryId='missing-game';entryKind='game'}
        )
        metadataReconciliation=[ordered]@{
            resolved=@(
                [ordered]@{sourceEntry=[ordered]@{metadataEntryId='game-one';entryKind='game'};classification='EXACT'},
                [ordered]@{sourceEntry=[ordered]@{metadataEntryId='game-two';entryKind='game'};classification='DUPLICATE_METADATA'}
            )
            unresolved=@([ordered]@{sourceEntry=[ordered]@{metadataEntryId='missing-game';entryKind='game'};classification='MISSING_ROM';status='unresolved';candidates=@()})
        }
        games=@(
            [ordered]@{gameId='game-id-one';name='Synthetic One';console='GBA';metadataSources=@(
                [ordered]@{metadataEntryId='game-one';scope='unit';romUnitId='unit-one'},
                [ordered]@{metadataEntryId='game-two';scope='unit';romUnitId='unit-one'},
                [ordered]@{metadataEntryId='folder-ps3';scope='unit';romUnitId='unit-one'}
            );variants=@([ordered]@{variantId='variant-one';gameId='game-id-one';name='Synthetic One';romUnits=@(
                [ordered]@{romUnitId='unit-one';kind='file';entryPath='ROMS/GBA/one.gba';fileIds=@('file-one');complete=$true;missingDependencies=@()}
            )})},
            [ordered]@{gameId='game-id-two';name='ROM_ONLY Synthetic';console='GBA';metadataSources=@();variants=@([ordered]@{variantId='variant-two';gameId='game-id-two';name='Synthetic Two';romUnits=@(
                [ordered]@{romUnitId='unit-two';kind='file';entryPath='ROMS/GBA/two.gba';fileIds=@('file-two');complete=$true;missingDependencies=@()}
            )})}
        )
    }
    $assoc=[ordered]@{schemaVersion='3.0.0';libraryId=$libraryId;decisions=@();transactions=@()}
    $registry=[ordered]@{schemaVersion='2.0.0';libraryId=$libraryId;games=@();variants=@()}
    foreach ($dir in @($state,$snapshot)) {
        SaveJson (Join-Path $dir 'associations.json') $assoc
        SaveJson (Join-Path $dir 'catalogue.json') $catalogue
        SaveJson (Join-Path $dir 'identity_registry.json') $registry
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $inputHashes=HashState
    $nominal=InvokeLauncher $output 'nominal'
    Require ($nominal.Exit -eq 0) ('NOMINAL_FAILED: '+$nominal.Text)
    ConfirmUnchanged $inputHashes
    $zips=@(Get-ChildItem -LiteralPath $output -File -Filter 'RM_EXPORT_E1_READONLY_DEV_*_PROOFS.zip')
    Require ($zips.Count -eq 1) ('EXPECTED_ONE_NOMINAL_ZIP: '+$zips.Count)
    $archive=[IO.Compression.ZipFile]::OpenRead($zips[0].FullName)
    try {
        $names=@($archive.Entries|ForEach-Object{$_.FullName})
        foreach($expected in @('00_SUMMARY.json','01_UNITS.json','02_EXCEPTIONS.json','03_FILE_OBSERVATIONS.json','04_ANOMALIES.json','05_CHECKS.json','UNITS.csv','REPORT.md')) {
            Require ($expected -in $names) ('ZIP_MISSING: '+$expected)
        }
        function JsonFromZip([string]$Name) {
            $entry=$archive.GetEntry($Name)
            $reader=New-Object IO.StreamReader($entry.Open())
            try {return (ConvertFrom-Json -InputObject $reader.ReadToEnd())} finally {$reader.Dispose()}
        }
        $summary=JsonFromZip '00_SUMMARY.json'
        $units=@(JsonFromZip '01_UNITS.json')
        $exceptions=@(JsonFromZip '02_EXCEPTIONS.json')
        $checks=@(JsonFromZip '05_CHECKS.json')
        Require ($summary.physicalUnits -eq 2 -and $summary.readyUnitsInSnapshot -eq 2) 'EXPECTED_2_READY_UNITS'
        Require ($summary.resolvedGameReferences -eq 2) 'EXPECTED_2_RESOLVED_GAMES'
        Require ($summary.uniqueReferencedReadyFiles -eq 2) 'EXPECTED_2_FILES'
        Require ($summary.knownUniqueReadyRomBytes -eq 11) 'EXPECTED_11_BYTES'
        Require ($summary.dynamicExceptionCount -eq 1) 'EXPECTED_ONE_EXCEPTION'
        Require ($units.Count -eq 2) ('UNIT_CARDINALITY_DRIFT: '+$units.Count)
        $associated=@($units|Where-Object{$_.metadataState -eq 'ASSOCIATED'})
        $romOnly=@($units|Where-Object{$_.metadataState -eq 'ROM_ONLY'})
        Require ($associated.Count -eq 1 -and $romOnly.Count -eq 1) 'EXPECTED_ASSOCIATED_ROM_ONLY'
        Require (@($associated[0].confirmedMetadataEntryIds).Count -eq 2) 'MANY_TO_ONE_LOST'
        Require (@($associated[0].confirmedMetadataEntryIds) -notcontains 'folder-ps3') 'FOLDER_LEAKED_INTO_GAME'
        Require (@($exceptions).Count -eq 1 -and $exceptions[0].exportException -eq 'ESDE_ONLY') 'ESDE_ONLY_EXCEPTION_LOST'
        Require (@($checks | Where-Object { !$_.pass }).Count -eq 0) 'CHECKS_CONTAIN_FAILURES'
        Write-Host ('E1_E2E_NOMINAL_PASS: 2 units, game=2, folder excluded, metadata many-to-one, ROM_ONLY, ESDE_ONLY, 8 ZIP entries')
    } finally {$archive.Dispose()}
    # A bad rootObserved must be rejected and create diagnostic only OUTSIDE protected roots.
    $catalogue.scan.rootObserved=Join-Path $workspace 'WrongLibrary'
    SaveJson (Join-Path $snapshot 'catalogue.json') $catalogue
    $changedHashes=HashState
    $negative=InvokeLauncher $output 'invalid-snapshot'
    Require ($negative.Exit -ne 0) 'BAD_SNAPSHOT_ACCEPTED'
    Require ($negative.Text -match 'GUARD_FAILED SNAPSHOT_SCAN_SOURCE') ('UNEXPECTED_NEGATIVE_FAILURE: '+$negative.Text)
    $diagnostics=@(Get-ChildItem -LiteralPath $output -File -Filter 'RM_EXPORT_E1_DEV_*_DIAGNOSTIC.zip')
    Require ($diagnostics.Count -eq 1) 'EXPECTED_DIAGNOSTIC_ZIP'
    $diagZip=[IO.Compression.ZipFile]::OpenRead($diagnostics[0].FullName)
    try {Require ($null -ne $diagZip.GetEntry('ERROR.json')) 'DIAGNOSTIC_ERROR_JSON_MISSING'} finally {$diagZip.Dispose()}
    ConfirmUnchanged $changedHashes
    Write-Host 'E1_E2E_NEGATIVE_PASS: stale root observed -> nonzero exit, diagnostic ZIP, unchanged state'
    $originalTemp=$env:TEMP
    try {
        $env:TEMP=$root
        $badTemp=InvokeLauncher $output 'protected-temp'
        Require ($badTemp.Exit -ne 0 -and $badTemp.Text -match 'E1_TEMP_OVERLAPS_PROTECTED_ROOT') ('TEMP_REDIRECT_NOT_REJECTED: '+$badTemp.Text)
    } finally { $env:TEMP=$originalTemp }
    ConfirmUnchanged $changedHashes
    Require (@(Get-ChildItem -LiteralPath $root -Force).Count -eq 1) 'COMPILER_TEMP_WROTE_TO_LIBRARY'
    Write-Host 'E1_E2E_TEMP_REDIRECT_PASS: compiler TEMP under protected Root rejected before bootstrap'
    # Path safety attacks must be rejected before output writes.
    $catalogue.scan.rootObserved=$root
    SaveJson (Join-Path $snapshot 'catalogue.json') $catalogue
    $safeHashes=HashState
    $initialRootEntries=@(Get-ChildItem -LiteralPath $root -Force).Count
    NewJunction $junction $root
    $badDirect=InvokeLauncher $junction 'reparse-output'
    Require ($badDirect.Exit -ne 0 -and $badDirect.Text -match 'E1_REPARSE_PATH_DENIED') ('JUNCTION_NOT_REJECTED: '+$badDirect.Text)
    ConfirmUnchanged $safeHashes
    Require (@(Get-ChildItem -LiteralPath $root -Force).Count -eq $initialRootEntries) 'JUNCTION_WROTE_IN_LIBRARY'
    RemoveJunction $junction
    NewJunction $junctionNested $root
    $badNested=InvokeLauncher (Join-Path $junctionNested 'ROMS') 'nested-reparse'
    Require ($badNested.Exit -ne 0 -and $badNested.Text -match 'E1_REPARSE_PATH_DENIED') ('NESTED_JUNCTION_NOT_REJECTED: '+$badNested.Text)
    ConfirmUnchanged $safeHashes
    RemoveJunction $junctionNested
    $badLexical=InvokeLauncher (Join-Path $root 'ROMS') 'lexically-nested'
    Require ($badLexical.Exit -ne 0 -and $badLexical.Text -match 'OUTPUT_OVERLAPS_PROTECTED_ROOT') ('LEXICAL_NESTING_NOT_REJECTED: '+$badLexical.Text)
    ConfirmUnchanged $safeHashes
    Write-Host 'E1_E2E_PATH_SAFETY_PASS: direct junction, nested junction, lexical containment, TEMP within Root'
    Write-Host 'CI-3 PASS | nominal=1 | controlledFailure=1 | redirectedDestinations=4 | stateHashes=UNCHANGED'
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
} finally {
    # Remove ONLY our private synthetic tree; no PROD file is ever in scope.
    if (Test-Path -LiteralPath $junction) { RemoveJunction $junction }
    if (Test-Path -LiteralPath $junctionNested) { RemoveJunction $junctionNested }
    if (Test-Path -LiteralPath $workspace) { Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue }
}
