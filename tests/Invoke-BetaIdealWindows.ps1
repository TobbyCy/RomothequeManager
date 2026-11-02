# #149 Lot B — native Windows non-admin fixture proof against the REAL extracted ZIP.
# Temporary account, outbound firewall block, disposable synthetic bytes only.
[CmdletBinding()]
param([Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceSha)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_SHA -cne $SourceSha) {
  throw 'BETA_WINDOWS_CI_EXACT_HEAD_REQUIRED'
}
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
  try {
    if (-not $child.WaitForExit(180000)) {
      $child.Kill()
      throw 'BETA_NONADMIN_WORKER_TIMEOUT'
    }
    $outText = Get-Content -LiteralPath $output -Raw
    $errorText = Get-Content -LiteralPath $errors -Raw
    if ($child.ExitCode -ne 0 -or
        -not $outText.Contains('BETA_NONADMIN_FIXTURE_PASS', [StringComparison]::Ordinal)) {
      throw ('BETA_NONADMIN_WORKER_FAILED exit=' + $child.ExitCode + ' stdout=' + $outText + ' stderr=' + $errorText)
    }
    Write-Host $outText.Trim()
    Write-Host ('BETA_IDEAL_WINDOWS_NONADMIN_PASS sha256=' + $hash + ' offline_firewall=true no_global_dotnet=true fixture_only=true')
  } finally { $child.Dispose() }
} finally {
  if ($firewallAdded) { Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction Continue }
  if ($accountCreated) { Remove-LocalUser -Name $username -ErrorAction Continue }
  if (Test-Path -LiteralPath $shared -PathType Container) {
    Remove-Item -LiteralPath $shared -Recurse -Force -ErrorAction Continue
  }
}
