# #149 Lot B — native Windows non-admin fixture proof against the REAL extracted ZIP.
# Temporary account, outbound firewall block, disposable synthetic bytes only.
[CmdletBinding()]
param([Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceSha)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_SHA -cne $SourceSha) {
  throw 'BETA_WINDOWS_CI_EXACT_HEAD_REQUIRED'
}
. .\tests\BetaIdealWindowsSession.ps1
$runnerFacts = Get-BetaWindowsSessionFacts -Role runner
Write-Host ('BETA_WINDOWS_RUNNER_CONTEXT session=' + $runnerFacts.session +
  ' interactive=' + $runnerFacts.interactive +
  ' integrityRid=' + $runnerFacts.integrityRid +
  ' no_personal_paths=true')
$stage = Join-Path $env:RUNNER_TEMP ('rm-beta-ideal-' + $SourceSha.Substring(0, 12))
$zip = Join-Path $stage ('build-1/RomothequeManager-v0.5.0-beta.1-win-x64-' + $SourceSha.Substring(0, 12) + '.zip')
$checksum = $zip + '.sha256'
if (-not (Test-Path -LiteralPath $zip -PathType Leaf) -or
    -not (Test-Path -LiteralPath $checksum -PathType Leaf)) {
  throw 'BETA_REPRODUCIBLE_VALIDATED_ZIP_MISSING'
}
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content -LiteralPath $checksum -Raw).Trim() -cne ($hash + '  ' + [IO.Path]::GetFileName($zip))) {
  throw 'BETA_ZIP_SHA256_SIDECAR_INVALID'
}
$common = [Environment]::GetFolderPath('CommonDocuments')
if ([string]::IsNullOrWhiteSpace($common) -or -not (Test-Path $common -PathType Container)) {
  throw 'BETA_COMMON_DOCUMENTS_MISSING'
}
$id = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$shared = Join-Path $common ('rm-beta-ci-' + $id)
$extract = Join-Path $shared 'unpacked'
$fixture = Join-Path $shared 'fictional-ideal'
$worker = Join-Path $shared 'Invoke-BetaIdealNonAdminWorker.ps1'
$username = 'rmv' + $id
$accountCreated = $false
$firewallAdded = $false
$ruleName = 'rm-beta-validate-' + $id
New-Item -ItemType Directory -Path $shared | Out-Null
try {
  # Extract once more from the ZIP (not from a build/publish folder).
  Expand-Archive -LiteralPath $zip -DestinationPath $extract
  python -B tests/ideal_beta_zip_gate.py --root $extract --source $SourceSha
  if ($LASTEXITCODE -ne 0) { throw 'BETA_SHARED_EXTRACT_MANIFEST_INVALID' }
  Copy-Item -LiteralPath 'tests/Invoke-BetaIdealNonAdminWorker.ps1' -Destination $worker
  Copy-Item -LiteralPath 'tests/BetaIdealWindowsSession.ps1' -Destination (Join-Path $shared 'BetaIdealWindowsSession.ps1')

  $rom = Join-Path $fixture 'ROMS/gba'
  $media = Join-Path $fixture 'MEDIAS/gba'
  foreach ($path in @($rom, (Join-Path $media 'metadata'), (Join-Path $media 'covers'))) {
    New-Item -ItemType Directory -Force -Path $path | Out-Null
  }
  $utf8 = [Text.UTF8Encoding]::new($false)
  foreach ($file in @('A.fixture','B.fixture','C.fixture')) {
    [IO.File]::WriteAllText((Join-Path $rom $file), 'SYNTHETIC TEXT; NOT A ROM', $utf8)
  }
  # Known, invented, decodable PNGs from the existing UX64 fixture contract.
  $pngA = 'iVBORw0KGgoAAAANSUhEUgAAABgAAAAgCAIAAACHPC9vAAAAUElEQVR42mOUkFJgoAZgYqASoJpBLHDWbDY+MvSn/vo0WL02jA1iwR8XWAHW+KWxi3DZjMelo9E/ahCtEyT+jEJfF5FR7I5G/1A0iHH4NiIAH+gM7ezTEpoAAAAASUVORK5CYII='
  $pngB = 'iVBORw0KGgoAAAANSUhEUgAAABgAAAAgCAIAAACHPC9vAAAAUElEQVR42mOUkFJgoAZgYqASoJpBLHCWdGU3GfqftpcOVq8NY4NY8McFVoA1fmnsIlw243HpaPSPGkTrBIk/o9DXRWQUu6PRPxQNYhy+jQgA9PkM7aOIpH8AAAAASUVORK5CYII='
  [IO.File]::WriteAllBytes((Join-Path $media 'covers/cover.png'), [Convert]::FromBase64String($pngA))
  [IO.File]::WriteAllBytes((Join-Path $media 'covers/thumb.png'), [Convert]::FromBase64String($pngB))
  [IO.File]::WriteAllText((Join-Path $media 'covers/corrupt.png'), 'INVALID FICTITIOUS PNG', $utf8)
  $xml = @'
<gameList>
  <game><path>./A.fixture</path><name>Même titre</name><image>../covers/absent.png</image><thumbnail>../covers/thumb.png</thumbnail></game>
  <game><path>./B.fixture</path><name>Même titre</name><image>../covers/cover.png</image></game>
  <game><path>./C.fixture</path><name>Jaquette illisible</name><image>../covers/corrupt.png</image></game>
  <game><path>./Missing.fixture</path><name>ROM indisponible</name></game>
</gameList>
'@
  [IO.File]::WriteAllText((Join-Path $media 'metadata/es-de-gamelist.xml'), $xml, $utf8)
  # Exact marker inside the ephemeral CommonDocuments/rm-beta-ci-* fixture.
  # Child RUNNER_TEMP must equal this root; production/default users cannot activate trace.
  [IO.File]::WriteAllText((Join-Path $shared 'BETA-TRACE-FIXTURE.marker'),
    'BETA_SYNTHETIC_FIXTURE_ONLY', $utf8)
  if (-not (Test-Path -LiteralPath (Join-Path $fixture 'ROMS/gba/A.fixture') -PathType Leaf) -or
      -not (Test-Path -LiteralPath (Join-Path $fixture 'MEDIAS/gba/metadata/es-de-gamelist.xml') -PathType Leaf)) {
    throw 'BETA_TRACE_FIXTURE_SCOPE_MISSING'
  }

  # Comparative diagnostic ONLY: same extracted ZIP under runner token. Its PASS
  # cannot replace the mandatory worker non-admin GUI oracle.
  $runnerExe = Join-Path $extract 'RomothequeManager.V2.exe'
  $runnerStart = [Diagnostics.ProcessStartInfo]::new()
  $runnerStart.FileName = $runnerExe
  $runnerStart.WorkingDirectory = $extract
  $runnerStart.UseShellExecute = $false
  $runnerStart.RedirectStandardOutput = $true
  $runnerStart.RedirectStandardError = $true
  [void]$runnerStart.ArgumentList.Add('--smoke-window')
  $runnerStart.Environment['GITHUB_ACTIONS'] = 'true'
  $runnerStart.Environment['RUNNER_TEMP'] = $shared
  $runnerStart.Environment['ROMOTHEQUE_BETA_TRACE'] = '1'
  $runnerStart.Environment['ROMOTHEQUE_BETA_TRACE_ROOT'] = $shared
  $runnerStart.Environment['DOTNET_ROOT'] = Join-Path $shared 'missing-dotnet'
  $runnerStart.Environment['DOTNET_ROOT_X64'] = Join-Path $shared 'missing-dotnet'
  $runnerStart.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
  $runnerStart.Environment['PATH'] = $extract + ';' + $env:SystemRoot + '\System32;' + $env:SystemRoot
  $runnerProc = [Diagnostics.Process]::Start($runnerStart)
  if ($null -eq $runnerProc) { throw 'BETA_RUNNER_TRACE_PROCESS_NOT_STARTED' }
  try {
    $runnerOut = $runnerProc.StandardOutput.ReadToEndAsync()
    $runnerErr = $runnerProc.StandardError.ReadToEndAsync()
    $runnerTimedOut = -not $runnerProc.WaitForExit(90000)
    if ($runnerTimedOut -and -not $runnerProc.HasExited) {
      $runnerProc.Kill()
      [void]$runnerProc.WaitForExit(5000)
    }
    try { [void][Threading.Tasks.Task]::WhenAll(@($runnerOut,$runnerErr)).Wait(5000) } catch { }
    $runnerCodes = [Collections.Generic.List[string]]::new()
    if ($runnerOut.IsCompletedSuccessfully) {
      $data = [string]$runnerOut.Result
      $tail = $data.Substring([Math]::Max(0, $data.Length - 16384))
      foreach ($line in ($tail -split '\r?\n')) {
        if ($line -cmatch '^BETA_AVALONIA_TRACE (MAIN_ENTER|SETUP_BEGIN|SETUP_COMPLETE|MAINWINDOW_CTOR_BEGIN|MAINWINDOW_CTOR_END|LIFETIME_START_BEGIN|WINDOW_OPENED|DISPATCHER_POST|DISPATCHER_EXECUTE|SMOKE_PASS|SMOKE_FAIL|LIFETIME_RETURN)$') {
          if ($runnerCodes.Count -eq 12) { $runnerCodes.RemoveAt(0) }
          $runnerCodes.Add($Matches[1])
        }
      }
    }
    $lastRunnerCode = if ($runnerCodes.Count) { $runnerCodes[$runnerCodes.Count-1] } else { 'NOT_VERIFIED' }
    $oracleRunner = $runnerOut.IsCompletedSuccessfully -and
      $runnerOut.Result.Contains('POC_WINDOW_OPENED_PASS', [StringComparison]::Ordinal)
    $runnerExit = if ($runnerTimedOut) { 'TIMEOUT' } else { [string]$runnerProc.ExitCode }
    Write-Host ('BETA_AVALONIA_TRACE_SUMMARY role=runner last=' + $lastRunnerCode +
      ' codes=' + [string]::Join(',', $runnerCodes) +
      ' exit=' + $runnerExit + ' timedOut=' + $runnerTimedOut +
      ' oracleObserved=' + $oracleRunner + ' compareOnly=true')
    if ($runnerTimedOut -or $runnerProc.ExitCode -ne 0 -or -not $oracleRunner) {
      Write-Host 'BETA_RUNNER_TRACE_OBSERVATION_FAILED nonadmin_gate_unchanged=true'
    }
  } finally { $runnerProc.Dispose() }

  # One ephemeral local user, never an administrator. SecureString is never logged.
  if (-not (Get-Command New-LocalUser -ErrorAction SilentlyContinue)) {
    throw 'BETA_NONADMIN_ACCOUNT_CREATION_UNAVAILABLE'
  }
  $rawPassword = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) + 'aA1!'
  $secret = ConvertTo-SecureString $rawPassword -AsPlainText -Force
  $rawPassword = $null
  $local = New-LocalUser -Name $username -Password $secret -Description 'Disposable beta ZIP CI fixture account' -ErrorAction Stop
  $accountCreated = $true
  $credential = [Management.Automation.PSCredential]::new($username, $secret)
  $sid = $local.SID.Value
  & icacls.exe $shared /grant ('*' + $sid + ':(OI)(CI)M') /T | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'BETA_NONADMIN_FIXTURE_ACL_FAILED' }

  $exe = Join-Path $extract 'RomothequeManager.V2.exe'
  New-NetFirewallRule -DisplayName $ruleName -Direction Outbound -Program $exe -Action Block -Profile Any -Enabled True -ErrorAction Stop | Out-Null
  $firewallAdded = $true
  $actualRule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction Stop
  if ($actualRule.Enabled -ne 'True' -or $actualRule.Action -ne 'Block') {
    throw 'BETA_OFFLINE_FIREWALL_NOT_ENFORCED'
  }
  $filter = Get-NetFirewallApplicationFilter -AssociatedNetFirewallRule $actualRule -ErrorAction Stop
  if ($filter.Program -cne $exe) { throw 'BETA_FIREWALL_EXE_PATH_MISMATCH' }

  $output = Join-Path $shared 'stdout.txt'
  $errors = Join-Path $shared 'stderr.txt'
  $pwsh = (Get-Command pwsh.exe -ErrorAction Stop).Source
  $args = @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', ('"' + $worker + '"'),
    '-Extract', ('"' + $extract + '"'), '-Fixture', ('"' + $fixture + '"'),
    '-Shared', ('"' + $shared + '"'))
  $child = Start-Process -FilePath $pwsh -ArgumentList $args -WorkingDirectory $shared -Credential $credential -PassThru -RedirectStandardOutput $output -RedirectStandardError $errors
  if ($null -eq $child) { throw 'BETA_NONADMIN_WORKER_NOT_STARTED' }
  Write-Host ('BETA_WINDOWS_WORKER_PROCESS_START pid=' + $child.Id +
    ' session=' + $child.SessionId +
    ' runnerSession=' + $runnerFacts.session +
    ' accountIsTemporary=true')
  try {
    if (-not $child.WaitForExit(180000)) {
      $child.Kill()
      throw 'BETA_NONADMIN_WORKER_TIMEOUT'
    }
    $outText = Get-Content -LiteralPath $output -Raw
    # Raw stderr and stdout can contain PowerShell script paths or arbitrary text.
    # Only the bounded fixed-format diagnostics from the actual worker are printed.
    $traceReported = $false
    foreach ($line in ($outText -split '\r?\n')) {
      if ($line.Length -gt 500) { continue }
      if ($line -cmatch '^BETA_AVALONIA_TRACE_SUMMARY role=nonadmin last=(?:[A-Z_]+) codes=(?:[A-Z_,]+|NOT_VERIFIED) stdoutComplete=(?:True|False) stderrComplete=(?:True|False)$') {
        Write-Host $line
        $traceReported = $true
      } elseif ($line -ceq 'BETA_NONADMIN_NATIVE_SQLITE_PASS e_sqlite3_from_extracted=true write_close_reopen_read=true' -or
          $line -ceq 'BETA_NONADMIN_FIXTURE_PASS no_personal_scan=true no_release=true' -or
          $line -ceq 'BETA_NONADMIN_EXTRACTED_UI_PASS native_window=true self_contained=true' -or
          $line -ceq 'BETA_NONADMIN_ESDE_PASS imported=4 diagnostics=true media_fallback_placeholder=true malformed_xml_refused=true') {
        Write-Host $line
      } elseif ($line -cmatch '^BETA_EXTRACTED_PROCESS_(START|TIMEOUT|EXIT) .*') {
        # Print only numeric fields and the fixed gate code, never the whole line.
        $kind = $Matches[1]
        Write-Host ('BETA_EXTRACTED_PROCESS_' + $kind + ' observed=true')
      } elseif ($line.StartsWith('CI_NONADMIN_GUI_ENV_UNSUPPORTED', [StringComparison]::Ordinal)) {
        Write-Host 'CI_NONADMIN_GUI_ENV_UNSUPPORTED / OWNER_DECISION_REQUIRED gate=FAIL_CLOSED'
      } elseif ($line -cmatch '^BETA_WINDOWS_SESSION_DIAG \{') {
        try {
          $facts = ($line.Substring('BETA_WINDOWS_SESSION_DIAG '.Length) | ConvertFrom-Json)
          if ($facts.role -ceq 'worker') {
            Write-Host ('BETA_WORKER_SESSION_SANITIZED session=' + [int]$facts.session +
              ' integrityRid=' + [int]$facts.integrityRid +
              ' isAdmin=' + [bool]$facts.isAdmin +
              ' desktopAccessible=' + [bool]$facts.interactive)
          }
        } catch { Write-Host 'BETA_WORKER_SESSION_UNREADABLE' }
      }
    }
    if (-not $traceReported) { Write-Host 'BETA_AVALONIA_TRACE_SUMMARY role=nonadmin last=NOT_VERIFIED' }
    if ($child.ExitCode -ne 0 -or
        -not $outText.Contains('BETA_NONADMIN_FIXTURE_PASS', [StringComparison]::Ordinal)) {
      throw ('BETA_NONADMIN_WORKER_FAILED exit=' + $child.ExitCode +
        ' traceCaptured=' + $traceReported + ' stderrNotDisplayed=true')
    }
    Write-Host ('BETA_IDEAL_WINDOWS_NONADMIN_PASS sha256=' + $hash + ' offline_firewall=true no_global_dotnet=true fixture_only=true')
  } finally { $child.Dispose() }
} finally {
  if ($firewallAdded) { Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction Continue }
  if ($accountCreated) { Remove-LocalUser -Name $username -ErrorAction Continue }
  if (Test-Path -LiteralPath $shared -PathType Container) {
    Remove-Item -LiteralPath $shared -Recurse -Force -ErrorAction Continue
  }
}
