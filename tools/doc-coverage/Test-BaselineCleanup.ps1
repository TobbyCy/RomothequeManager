# Windows-only native canary for the actual cleanup block in ci.yml, not a copy.
[CmdletBinding()]
param(
    [string] $WorkflowPath = (Join-Path $PSScriptRoot '../../.github/workflows/ci.yml'),
    [Parameter(Mandatory)] [string] $EvidenceRoot,
    [switch] $TransientLock
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if (-not $IsWindows) { throw 'DOC_CLEANUP_CANARY_WINDOWS_REQUIRED' }
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $evidence) { throw 'DOC_CLEANUP_CANARY_FRESH_ROOT_REQUIRED' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$workflow = Get-Content -LiteralPath $WorkflowPath -Raw
$start = $workflow.IndexOf('            $PSNativeCommandUseErrorActionPreference = $false', [StringComparison]::Ordinal)
$endMarker = "            Write-BaselineCleanup 'PASS physical_tree_absent=true git_registration_absent=true'"
$end = $workflow.IndexOf($endMarker, [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'DOC_CLEANUP_CANARY_BLOCK_NOT_FOUND' }
$block = $workflow.Substring($start, $end + $endMarker.Length - $start)
$cleanup = [scriptblock]::Create(($block -split '\r?\n' | ForEach-Object {
    if (-not $_.StartsWith('            ', [StringComparison]::Ordinal)) {
        throw 'DOC_CLEANUP_CANARY_BLOCK_INDENT'
    }
    $_.Substring(12)
}) -join "`n")
$repo = Join-Path $evidence 'fixture-repository'
$tempRoot = Join-Path $evidence 'runner-temp'
$report = Join-Path $evidence 'report'
New-Item -ItemType Directory -Path $repo, $tempRoot, $report | Out-Null
git -C $repo init --quiet
if ($LASTEXITCODE -ne 0) { throw 'DOC_CLEANUP_CANARY_GIT_INIT' }
$fixtureDirectory = Join-Path $repo 'src/Fixture'
New-Item -ItemType Directory -Path $fixtureDirectory | Out-Null
[IO.File]::WriteAllText((Join-Path $fixtureDirectory 'held.fixture'), 'synthetic lock fixture only')
git -C $repo add -- src/Fixture/held.fixture
git -C $repo -c user.name=CleanupFixture -c user.email=fixture@example.invalid commit --quiet -m 'Synthetic lock fixture'
if ($LASTEXITCODE -ne 0) { throw 'DOC_CLEANUP_CANARY_GIT_COMMIT' }
$base = (git -C $repo rev-parse HEAD).Trim()
$baselineRoot = Join-Path $tempRoot ('rm-docfx-baseline-' + $base.Substring(0,12))
$oldRunnerTemp = $env:RUNNER_TEMP
$env:RUNNER_TEMP = $tempRoot
$owner = $null
$workerPath = Join-Path $evidence 'lock-owner.ps1'
@'
param($Target, $Ready, $Release, [int] $ReleaseAfterSeconds)
$ErrorActionPreference = 'Stop'
$stream = [IO.File]::Open($Target, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
try {
    [IO.File]::WriteAllText($Ready, [string]$PID)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not [IO.File]::Exists($Release) -and
        ($ReleaseAfterSeconds -eq 0 -or $timer.Elapsed.TotalSeconds -lt $ReleaseAfterSeconds)) {
        Start-Sleep -Milliseconds 50
    }
} finally { $stream.Dispose() }
'@ | Set-Content -LiteralPath $workerPath -Encoding utf8

function Start-LockOwner([int] $releaseAfterSeconds) {
    $script:ready = Join-Path $evidence ('ready-' + [guid]::NewGuid().ToString('N'))
    $script:release = Join-Path $evidence ('release-' + [guid]::NewGuid().ToString('N'))
    $startInfo = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WorkingDirectory = $evidence
    foreach ($arg in @('-NoProfile','-NonInteractive','-File',$workerPath,
        '-Target',(Join-Path $baselineRoot 'src/Fixture/held.fixture'),
        '-Ready',$script:ready,'-Release',$script:release,
        '-ReleaseAfterSeconds',[string]$releaseAfterSeconds)) { $startInfo.ArgumentList.Add($arg) }
    $script:owner = [Diagnostics.Process]::Start($startInfo)
    $wait = [Diagnostics.Stopwatch]::StartNew()
    while (-not [IO.File]::Exists($script:ready)) {
        if ($script:owner.HasExited -or $wait.Elapsed.TotalSeconds -gt 20) {
            throw 'DOC_CLEANUP_CANARY_LOCK_OWNER_NOT_READY'
        }
        Start-Sleep -Milliseconds 50
    }
    if ((Get-Content -LiteralPath $script:ready -Raw) -ne [string]$script:owner.Id) {
        throw 'DOC_CLEANUP_CANARY_OWNER_PID_MISMATCH'
    }
    Write-Host ('DOC_CLEANUP_CANARY LOCK_OWNER pid=' + $script:owner.Id +
        ' mode=FileShare.ReadWrite_without_Delete releaseAfterSeconds=' + $releaseAfterSeconds)
}

function Stop-LockOwner {
    if ($null -ne $script:owner) {
        [IO.File]::WriteAllText($script:release, 'release this fixture handle only')
        if (-not $script:owner.WaitForExit(10000)) {
            # Only the process created by this canary; never search/kill other processes.
            $script:owner.Kill($true)
            throw 'DOC_CLEANUP_CANARY_LOCK_OWNER_RELEASE_TIMEOUT'
        }
        if ($script:owner.ExitCode -ne 0) { throw 'DOC_CLEANUP_CANARY_LOCK_OWNER_FAILED' }
        $script:owner.Dispose()
        $script:owner = $null
    }
}

Push-Location $repo
try {
    git worktree add --quiet --detach -- $baselineRoot $base
    if ($LASTEXITCODE -ne 0) { throw 'DOC_CLEANUP_CANARY_WORKTREE_ADD' }
    Start-LockOwner 0
    $fatal = $null
    try { & $cleanup } catch { $fatal = $_.Exception.Message }
    Write-Host ('DOC_CLEANUP_CANARY EXPECTED_FAILURE=' + $fatal)
    $log = Get-Content -LiteralPath (Join-Path $report 'baseline-cleanup.log') -Raw
    if ($fatal -ne 'DOC_BASELINE_WORKTREE_CLEANUP_FAILED' -or
        -not (Test-Path -LiteralPath $baselineRoot) -or $owner.HasExited -or
        $log -notmatch 'VERIFY attempt=8 pathExists=True' -or
        $log -match 'PASS physical_tree_absent=true') {
        throw 'DOC_CLEANUP_CANARY_HELD_LOCK_DID_NOT_FAIL_CLOSED'
    }
    Copy-Item -LiteralPath (Join-Path $report 'baseline-cleanup.log') -Destination (Join-Path $report 'held-lock-red.log')
    Write-Host 'DOC_CLEANUP_CANARY RED held_lock=true fatal=true attempts=8 physical_tree_present=true'
    Stop-LockOwner
    & $cleanup
    $trees = @(git worktree list --porcelain)
    if ($LASTEXITCODE -ne 0 -or (Test-Path -LiteralPath $baselineRoot) -or
        @($trees | Where-Object { $_.Replace('\','/').TrimEnd('/') -ieq
            ('worktree ' + $baselineRoot.Replace('\','/').TrimEnd('/')) }).Count -ne 0) {
        throw 'DOC_CLEANUP_CANARY_RELEASED_LOCK_NOT_CLEAN'
    }
    Write-Host 'DOC_CLEANUP_CANARY GREEN released_lock=true physical_tree_absent=true git_registration_absent=true'
    if ($TransientLock) {
        $report = Join-Path $evidence 'transient-report'
        New-Item -ItemType Directory -Path $report | Out-Null
        git worktree add --quiet --detach -- $baselineRoot $base
        if ($LASTEXITCODE -ne 0) { throw 'DOC_CLEANUP_CANARY_TRANSIENT_WORKTREE_ADD' }
        Start-LockOwner 22
        & $cleanup
        Stop-LockOwner
        $log = Get-Content -LiteralPath (Join-Path $report 'baseline-cleanup.log') -Raw
        if ($log -notmatch 'FILESYSTEM_REMOVE attempt=1 failed=' -or
            $log -notmatch 'PASS physical_tree_absent=true git_registration_absent=true') {
            throw 'DOC_CLEANUP_CANARY_TRANSIENT_NOT_EXERCISED'
        }
        Write-Host 'DOC_CLEANUP_CANARY GREEN transient_lock_22s=true retry_recovered=true'
    }
} finally {
    try { Stop-LockOwner } finally {
        Pop-Location
        $env:RUNNER_TEMP = $oldRunnerTemp
    }
}
Write-Host 'DOC_CLEANUP_CANARY PASS windows_native=true no_personal_data=true'
