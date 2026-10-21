# CI-1 runs in a dedicated Windows PowerShell 5.1 process, with isolated LOCALAPPDATA.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$originalLocalAppData = $env:LOCALAPPDATA
$originalAppData = $env:APPDATA
try {
    if ($PSVersionTable.PSVersion.ToString() -notmatch '^5\.1\.') {throw 'PowerShell 5.1 is mandatory'}
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $sandbox = Join-Path ([IO.Path]::GetTempPath()) ('rm-ci-smoke-' + [guid]::NewGuid().ToString('N'))
    $env:LOCALAPPDATA = Join-Path $sandbox 'LocalAppData'
    $env:APPDATA = Join-Path $sandbox 'AppData'
    [IO.Directory]::CreateDirectory($env:LOCALAPPDATA) | Out-Null
    [IO.Directory]::CreateDirectory($env:APPDATA) | Out-Null
    $state = Join-Path $env:LOCALAPPDATA 'RomothequeManager\RealCommitLot1'
    if (Test-Path -LiteralPath $state) {throw 'Synthetic StateRoot not empty before import'}
    $bootstrap = Join-Path $root 'Initialize.ps1'
    # Dot-source only loads modules, it must NEVER invoke scan or transaction functions.
    . $bootstrap
    $expected = @('Utils','Canonical','Configuration','Inventory','Metadata','RomResolver','DecisionSchema','StateCodec','Identity','PersistentIdentity','MediaMatcher','MediaModel','Reconciliation','Proposals','Projection','Transactions','Catalogue','Associations')
    foreach ($m in $expected) {
        $module = @(Get-Module -Name $m)
        if ($module.Count -ne 1) {throw ('MODULE_IMPORT_FAIL: ' + $m + ' actual=' + $module.Count)}
    }
    foreach ($t in @('Rm032.LongPathIO','Rm032.Canonical')) {
        if (-not ($t -as [type])) { throw ('CSHARP_TYPE_NOT_LOADED: ' + $t) }
    }
    foreach ($f in @('Get-RmConfiguration','Get-RmNormalizedLocator','Assert-RmUuid','Assert-RmTargetShape','Resolve-RmMergeTarget','Invoke-RmDecisionCommit')) {
        if (-not (Get-Command -Name $f -CommandType Function -ErrorAction SilentlyContinue)) {throw ('FUNCTION_MISSING: ' + $f)}
    }
    $initial = @{}
    foreach ($m in $expected) { $initial[$m] = (Get-Module -Name $m).Version.ToString() }
    # A second load in the same process must reuse Add-Type guards without duplicate assembly exceptions.
    . $bootstrap
    foreach ($m in $expected) {
        if (@(Get-Module -Name $m).Count -ne 1) {throw ('REIMPORT_MODULE_FAIL: ' + $m)}
    }
    if (Test-Path -LiteralPath $state) {throw 'Bootstrap wrote into synthetic StateRoot'}
    if (Test-Path -LiteralPath (Join-Path $sandbox 'RomothequeManager')) {throw 'Unexpected state folder'}
    Write-Host ('CI-1 PASS | PS=' + $PSVersionTable.PSVersion + ' | modules=18 | CSharp=2 | repeated-load=PASS | StateRoot=absent')
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.ToString());exit 1 }
finally { $env:LOCALAPPDATA=$originalLocalAppData; $env:APPDATA=$originalAppData }