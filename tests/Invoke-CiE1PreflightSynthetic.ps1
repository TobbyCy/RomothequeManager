#Requires -Version 5.1
# Executes only in disposable GitHub Windows runner synthetic fixtures.
# NO source ROM, PROD state, private IDs or physical library are accessed.
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
if ($env:GITHUB_ACTIONS -ne 'true' -or [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {throw 'CI_WINDOWS_RUNNER_REQUIRED'}
if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {throw 'PS51_REQUIRED'}
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$preflight=Join-Path $repo 'Preflight\Invoke-E1RealLibraryPreflightReadOnly.ps1'
$ps51=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$approved='00c47bb2bfcd60ed2e0b64f43869d169e41ffffe'
$workspace=Join-Path $env:RUNNER_TEMP ('rm-e1-local-preflight-ci-'+[guid]::NewGuid().ToString('N'))
$gitRoot=Join-Path $workspace 'ApprovedGitWorktree'
$root=Join-Path $workspace 'SyntheticLibrary'
$localAppData=Join-Path $workspace 'LocalAppData'
$state=Join-Path $localAppData 'RomothequeManager\RealCommitLot1'
$snapshot=Join-Path $workspace 'GbaSnapshot'
$proofs=Join-Path $workspace 'Evidence'
$logs=Join-Path $workspace 'TestLogs'
$junction=Join-Path $workspace 'ProofJunction'
$symlink=Join-Path $workspace 'ProofSymlink'
$encoding=New-Object Text.UTF8Encoding($false)
$drive=([IO.Path]::GetPathRoot($workspace)).Substring(0,1)
$libId=[guid]::NewGuid().ToString()
$scanId=[guid]::NewGuid().ToString()
$persistScanId=[guid]::NewGuid().ToString()
$serial='CI-UNIT-1234'
$diskSerial='CI-SYNTHETIC-DISK'
$passed=0
function Require([bool]$Value,[string]$Message) {if(!$Value){throw ('PREFLIGHT_CI_FAILURE: '+$Message)}}
function Json([string]$File,$Data) { [IO.File]::WriteAllText($File,(ConvertTo-Json -InputObject $Data -Depth 40),$encoding) }
function HashInputs {
    $out=@{}
    foreach($dir in @($state,$snapshot)) {
        foreach($name in @('associations.json','catalogue.json','identity_registry.json')) {
            $path=Join-Path $dir $name
            $out[$path]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    return ,$out
}
function Assert-InputsUnchanged($Before) {
    foreach($key in $Before.Keys) {Require ((Get-FileHash -LiteralPath $key -Algorithm SHA256).Hash -ceq $Before[$key]) 'STATE_CHANGED'}
}
function Assert-NoEvidence {
    Require (@(Get-ChildItem -LiteralPath $proofs -Force).Count -eq 0) 'PROOF_DIRECTORY_MUTATED_WITHOUT_WRITE_SWITCH'
}
function PsLiteral([string]$Value) { return "'"+($Value.Replace("'","''"))+"'" }
function Invoke-Preflight([string]$Scenario,[string]$Evidence=$proofs,[string]$Scan=$scanId,[string]$Library=$libId,[bool]$WithAllApprovals=$true,[bool]$Probe=$false,[bool]$Save=$false,[string]$TempRedirect='') {
    # Function mocking is ONLY in the synthetic harness subprocess, not in the production script.
    # It enables deterministic testing of the volume/disk witness on hosted VMs.
    $volumeScript=@'
function Get-CimInstance { param($ClassName,$Filter) [pscustomobject]@{DeviceID='CI:';VolumeSerialNumber='CI-UNIT-1234';VolumeName='CI-FIXTURE';FileSystem='NTFS';DriveType=3;Size=10000000} }
function Get-Partition { param($DriveLetter) [pscustomobject]@{DiskNumber=1} }
function Get-Disk { param($Number) [pscustomobject]@{Number=1;FriendlyName='CI fixture';SerialNumber='CI-SYNTHETIC-DISK';BusType='Virtual';UniqueId='CI-ONLY'} }
'@
    $params=' -GitRoot '+(PsLiteral $gitRoot)+' -Root '+(PsLiteral $root)+' -StateRoot '+(PsLiteral $state)+' -SnapshotRoot '+(PsLiteral $snapshot)+' -ProofOutputDirectory '+(PsLiteral $Evidence)+' -ExpectedDriveLetter '+(PsLiteral $drive)
    if($WithAllApprovals){$params+=' -ExpectedVolumeSerial '+(PsLiteral $serial)+' -ExpectedDiskSerial '+(PsLiteral $diskSerial)+' -ExpectedSnapshotScanId '+(PsLiteral $Scan)+' -ExpectedLibraryId '+(PsLiteral $Library)}
    if($Probe){$params+=' -ProbeProofWrite'}
    if($Save){$params+=' -SaveReports'}
    $cmd='$ErrorActionPreference=''Stop''; $env:LOCALAPPDATA='+(PsLiteral $localAppData)+'; '+$volumeScript+"`n"
    if($TempRedirect){$cmd+='$env:TEMP='+(PsLiteral $TempRedirect)+'; $env:TMP='+(PsLiteral $TempRedirect)+"`n"}
    $cmd+='& '+(PsLiteral $preflight)+$params+'; exit $LASTEXITCODE'
    # -EncodedCommand maps script exit 3 to a generic host status on some PS5.1 versions.
    # -File is required to test the real 0/2/3/4 process exit-code contract.
    $wrapper=Join-Path $logs ($Scenario+'.wrapper.ps1')
    [IO.File]::WriteAllText($wrapper,$cmd,$encoding)
    $out=Join-Path $logs ($Scenario+'.out.txt')
    $err=Join-Path $logs ($Scenario+'.err.txt')
    $process=Start-Process -FilePath $ps51 -ArgumentList @('-NoLogo','-NoProfile','-NonInteractive','-File',('"'+$wrapper+'"')) -Wait -PassThru -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden
    $content=[IO.File]::ReadAllText($out)+"`n"+[IO.File]::ReadAllText($err)
    return [pscustomobject]@{Exit=$process.ExitCode;Text=$content}
}
function CheckCase([string]$Name,[int]$ExpectedExit,[scriptblock]$Act,[string]$ExpectedText='') {
    $before=HashInputs
    $case=& $Act
    Require ($case.Exit -eq $ExpectedExit) ($Name+' exit='+$case.Exit+' instead of '+$ExpectedExit+' : '+$case.Text)
    if($ExpectedText) {Require ($case.Text -match [regex]::Escape($ExpectedText)) ($Name+' missing marker '+$ExpectedText+': '+$case.Text)}
    Assert-InputsUnchanged $before
    $script:passed++
    Write-Host ('PREFLIGHT_SYNTHETIC_PASS '+$Name+' exit='+$case.Exit)
}
function NewJunction([string]$Link,[string]$Target) {
    $null=New-Item -ItemType Junction -Path $Link -Target $Target -ErrorAction Stop
    Require (((Get-Item -LiteralPath $Link -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) 'JUNCTION_NOT_REAL'
}
function RemoveRedirect([string]$Link) {
    if (Test-Path -LiteralPath $Link) {
        $it=Get-Item -LiteralPath $Link -Force
        Require (($it.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) 'LINK_WAS_REPLACED'
        [IO.Directory]::Delete($Link,$false)
    }
}
$gitWorktreeCreated=$false
try {
    foreach($d in @($workspace,$root,$localAppData,$state,$snapshot,$proofs,$logs)) { [IO.Directory]::CreateDirectory($d) | Out-Null }
    # Git source fixture = an exact CLEAN checkout of the approved source commit, no repo mutations in PROD.
    & git -C $repo worktree add --detach $gitRoot $approved | Out-Null
    Require ($LASTEXITCODE -eq 0) 'GIT_WORKTREE_CREATION_FAILED'
    $gitWorktreeCreated=$true
    $decisions=@(1..545 | ForEach-Object {[ordered]@{decisionId=('ci-'+$_);state='active'}})
    $transactions=@(1..7 | ForEach-Object {[ordered]@{transactionId=('ci-txn-'+$_);status='COMMITTED'}})
    $assoc=[ordered]@{libraryId=$libId;decisions=$decisions;transactions=$transactions}
    $registry=[ordered]@{libraryId=$libId;games=@();variants=@();romUnits=@();metadataEntries=@()}
    $catProd=[ordered]@{libraryId=$libId;scan=[ordered]@{scanId=$persistScanId;rootObserved=$root;status='complete';hashMode='none'};statistics=[ordered]@{metadataResolved=1038;metadataUnresolved=4;metadataGameEntries=1042}}
    $catIso=[ordered]@{libraryId=$libId;scan=[ordered]@{scanId=$scanId;rootObserved=$root;status='complete';hashMode='none'};statistics=[ordered]@{metadataResolved=1041;metadataUnresolved=1;metadataGameEntries=1042}}
    foreach($d in @($state,$snapshot)) {
        Json (Join-Path $d 'associations.json') $assoc
        Json (Join-Path $d 'identity_registry.json') $registry
    }
    Json (Join-Path $state 'catalogue.json') $catProd
    Json (Join-Path $snapshot 'catalogue.json') $catIso

    # Default no-write run: all read checks PASS, only authorized write probe is HOLD -> exit 3.
    CheckCase 'default_hold_no_writes' 3 {Invoke-Preflight 'default_hold_no_writes'} 'PROOF_DIRECTORY_CREATE_DELETE'
    Assert-NoEvidence
    CheckCase 'approved_synthetic_probe' 0 {Invoke-Preflight 'approved_synthetic_probe' -Probe $true} 'readOnlyChecks=PASS'
    Assert-NoEvidence
    CheckCase 'no_ids_hold' 3 {Invoke-Preflight 'no_ids_hold' -WithAllApprovals $false}
    Assert-NoEvidence
    CheckCase 'wrong_expected_library' 2 {Invoke-Preflight 'wrong_expected_library' -Library ([guid]::NewGuid().ToString())}
    CheckCase 'wrong_expected_scan' 2 {Invoke-Preflight 'wrong_expected_scan' -Scan ([guid]::NewGuid().ToString())}
    $catIso.statistics.metadataResolved=1038;$catIso.statistics.metadataUnresolved=4
    Json (Join-Path $snapshot 'catalogue.json') $catIso
    CheckCase 'snapshot_confused_with_prod' 2 {Invoke-Preflight 'snapshot_confused_with_prod'} 'ISOLATED_METADATA_HISTORICAL_1041_1'
    $catIso.statistics.metadataResolved=1041;$catIso.statistics.metadataUnresolved=1
    Json (Join-Path $snapshot 'catalogue.json') $catIso
    $stateCatalog=Join-Path $state 'catalogue.json'
    $catProd.libraryId=[guid]::NewGuid().ToString()
    Json $stateCatalog $catProd
    CheckCase 'persistent_catalogue_library_id_changed' 2 {Invoke-Preflight 'persistent_catalogue_library_id_changed'} 'LIBRARY_ID_ALL_SIX'
    $catProd.libraryId=$libId; Json $stateCatalog $catProd
    $snapshotAssoc=Join-Path $snapshot 'associations.json'
    $assoc.decisions[0].state='revoked'; Json $snapshotAssoc $assoc
    CheckCase 'snapshot_state_mismatch' 2 {Invoke-Preflight 'snapshot_state_mismatch'} 'ASSOCIATIONS_BYTES_IDENTICAL'
    $assoc.decisions[0].state='active'; Json $snapshotAssoc $assoc
    # Restore byte identity (same ordered data -> same JSON serialization)
    Require ((Get-FileHash $snapshotAssoc -Algorithm SHA256).Hash -ceq ((Get-FileHash (Join-Path $state 'associations.json') -Algorithm SHA256).Hash)) 'ASSOC_RESTORE_FAILURE'
    # Repo wrong clean commit (creating commit ONLY in disposable git worktree).
    & git -C $gitRoot -c user.name='CI Fixture' -c user.email='fixture@example.invalid' commit --allow-empty -m 'CI only synthetic wrong HEAD' | Out-Null
    Require ($LASTEXITCODE -eq 0) 'EMPTY_TEST_COMMIT_FAILED'
    CheckCase 'wrong_commit' 2 {Invoke-Preflight 'wrong_commit'} 'GIT_COMMIT_LOCKED'
    & git -C $gitRoot checkout --detach $approved | Out-Null
    Require ($LASTEXITCODE -eq 0) 'RESTORE_APPROVED_COMMIT_FAILED'
    # Untracked file in temp checkout -> exit 2.
    $marker=Join-Path $gitRoot 'synthetic-dirty.marker'
    [IO.File]::WriteAllText($marker,'ci-only',$encoding)
    CheckCase 'dirty_checkout' 2 {Invoke-Preflight 'dirty_checkout'} 'GIT_CLEAN'
    Remove-Item -LiteralPath $marker -Force
    # Changed source file in checkout -> hash mismatch (exit 2), then restore.
    $moduleFile=Join-Path $gitRoot 'Modules\Transactions.psm1'
    $originalBytes=[IO.File]::ReadAllBytes($moduleFile)
    try {
        [IO.File]::AppendAllText($moduleFile,"`n# CI mutation")
        CheckCase 'changed_git_source' 2 {Invoke-Preflight 'changed_git_source'} 'CHECKOUT_21_SHA'
    } finally { [IO.File]::WriteAllBytes($moduleFile,$originalBytes) }
    $missing=Join-Path $snapshot 'identity_registry.json';$saved=[IO.File]::ReadAllBytes($missing)
    try {
        Remove-Item -LiteralPath $missing -Force
        # Cannot call HashInputs on a file intentionally absent; assert the preflight itself returns 4.
        $case=Invoke-Preflight 'missing_json'
        Require ($case.Exit -eq 4 -and $case.Text -match 'E1_PREFLIGHT_STOP') 'MISSING_JSON_WAS_NOT_REJECTED'
        $script:passed++
        Write-Host 'PREFLIGHT_SYNTHETIC_PASS missing_json exit=4'
    } finally { [IO.File]::WriteAllBytes($missing,$saved) }
    Require ((Get-FileHash -LiteralPath $missing -Algorithm SHA256).Hash -eq (([BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash($saved))).Replace('-',''))) 'MISSING_JSON_RESTORE_FAILED'
    CheckCase 'protected_output' 4 {Invoke-Preflight 'protected_output' -Evidence $root} 'E1_PREFLIGHT_STOP'
    CheckCase 'redirected_temp' 4 {Invoke-Preflight 'redirected_temp' -TempRedirect $root} 'E1_PREFLIGHT_STOP'
    NewJunction $junction $root
    try {CheckCase 'junction_output' 4 {Invoke-Preflight 'junction_output' -Evidence $junction} 'E1_PREFLIGHT_STOP'}
    finally {RemoveRedirect $junction}
    # Symlink on Windows may require SeCreateSymbolicLinkPrivilege: do not mark PASS without real reparse creation. Failed creation is a CI environment BLOCKER, not a skipped case.
    $null=New-Item -ItemType SymbolicLink -Path $symlink -Target $root -ErrorAction Stop
    Require (((Get-Item -LiteralPath $symlink -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) 'SYMLINK_NOT_REAL'
    try {CheckCase 'symlink_output' 4 {Invoke-Preflight 'symlink_output' -Evidence $symlink} 'E1_PREFLIGHT_STOP'}
    finally {RemoveRedirect $symlink}
    # JSON file symlink should be rejected specifically, including while the containing directory remains regular.
    $fileToLink=Join-Path $snapshot 'identity_registry.json'
    $spare=Join-Path $logs 'registry-backup.json'
    [IO.File]::Move($fileToLink,$spare)
    try {
        $null=New-Item -ItemType SymbolicLink -Path $fileToLink -Target $spare -ErrorAction Stop
        CheckCase 'symlink_json' 4 {Invoke-Preflight 'symlink_json'} 'E1_PREFLIGHT_STOP'
    } finally {
        if(Test-Path -LiteralPath $fileToLink) {Remove-Item -LiteralPath $fileToLink -Force}
        [IO.File]::Move($spare,$fileToLink)
    }
    # Explicit synthetic SaveReports permission should create reports exclusively in evidence.
    CheckCase 'approved_synthetic_reports' 0 {Invoke-Preflight 'approved_synthetic_reports' -Probe $true -Save $true} 'LOCAL_REPORTS_CREATED'
    $evidenceFiles=@(Get-ChildItem -LiteralPath $proofs -File)
    Require ($evidenceFiles.Count -eq 2) 'EXPECTED_TWO_EVIDENCE_FILES'
    Require (@($evidenceFiles | Where-Object{$_.Name -match 'PRIVATE_'}).Count -eq 1) 'PRIVATE_REPORT_MISSING'
    Require (@($evidenceFiles | Where-Object{$_.Name -match 'REDACTED_'}).Count -eq 1) 'REDACTED_REPORT_MISSING'
    $safe=Get-Content -LiteralPath (@($evidenceFiles|Where-Object{$_.Name -match 'REDACTED_'})[0].FullName) -Raw -Encoding UTF8
    Require ($safe -notmatch [regex]::Escape($workspace)) 'REDACTED_REPORT_LEAKED_SYNTHETIC_PATH'
    Write-Host ('PREFLIGHT_SYNTHETIC_ALL_PASS scenarios='+$script:passed+' exits=0,2,3,4 noDefaultWrites=true JSON_SHA256=UNCHANGED')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString());exit 1
} finally {
    # Only dispose synthetic worktree, never production paths.
    if(Test-Path -LiteralPath $symlink){RemoveRedirect $symlink}
    if(Test-Path -LiteralPath $junction){RemoveRedirect $junction}
    if($gitWorktreeCreated){ & git -C $repo worktree remove --force $gitRoot 2>$null | Out-Null }
    if(Test-Path -LiteralPath $workspace){Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue}
}
