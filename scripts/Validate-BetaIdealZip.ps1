# NEXT-05 #149 / Lot B: IDEAL beta ZIP candidate, VALIDATION ONLY.
# No dispatch, upload, Release, personal scan, migration or grant.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{40}$')]
  [string]$SourceSha,
  [ValidateSet('validate')]
  [string]$Phase = 'validate'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Phase -cne 'validate') { throw 'BETA_PUBLISH_FORBIDDEN' }
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_SHA -cne $SourceSha -or [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
  throw 'BETA_EXACT_CI_SOURCE_REQUIRED'
}
$repo = (Resolve-Path -LiteralPath '.').Path
$app = 'src/RomothequeManager.V2/RomothequeManager.V2.csproj'
$stage = Join-Path $env:RUNNER_TEMP ('rm-beta-ideal-' + $SourceSha.Substring(0, 12))
if (Test-Path -LiteralPath $stage) { throw 'BETA_VALIDATION_STAGE_ALREADY_EXISTS' }
New-Item -ItemType Directory -Path $stage | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$runtimeVersion = '10.0.12'
$runtimeName = 'Microsoft.NETCore.App.Runtime.win-x64'
$nugetRoot = if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
  Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
} else { $env:NUGET_PACKAGES }
$licenseNames = @(
  'ANGLE-LICENSE.txt', 'ANGLE-xxHash-LICENSE.txt', 'Avalonia-LICENSE.txt',
  'Avalonia-NOTICE.txt', 'HarfBuzz-COPYING.txt', 'MicroCom-LICENSE.txt',
  'Skia-LICENSE.txt', 'SkiaSharp-LICENSE.txt',
  'SkiaSharp-THIRD-PARTY-NOTICES.txt', 'Tmds.DBus-COPYING.txt',
  'Microsoft.Data.Sqlite-LICENSE.txt', 'SQLitePCLRaw-LICENSE.txt',
  'SQLitePCLRaw-NOTICE.txt', 'SQLite-PUBLIC-DOMAIN.txt'
)
function Require-Source([string]$path) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
    throw ('BETA_REQUIRED_SOURCE_MISSING:' + (Split-Path $path -Leaf))
  }
}
foreach ($name in @('LICENSE', 'LEGAL.md', 'BRANDING.md',
    'packaging/THIRD-PARTY-NOTICES.txt', 'docs/developer/lic-zip-01-inventory.md')) {
  Require-Source (Join-Path $repo $name)
}
foreach ($name in $licenseNames) { Require-Source (Join-Path $repo ('packaging/licenses/' + $name)) }
$inventory = [IO.File]::ReadAllText((Join-Path $repo 'docs/developer/lic-zip-01-inventory.md'))
if ($inventory -cmatch '(?m)^SQLITE_LEGAL_01_BINARY_DISTRIBUTION_APPROVED\r?$') {
  throw 'BETA_UNEXPECTED_LEGAL_PUBLISH_MARKER'
}
Write-Host 'BETA_LEGAL_HOLD phase=validate legalPublishApproved=false'

function Get-FileRecords([string]$folder) {
  $names = [Collections.Generic.List[string]]::new()
  foreach ($entry in @(Get-ChildItem -LiteralPath $folder -Recurse -File)) {
    if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'BETA_PUBLISH_REPARSE_FILE_REFUSED' }
    $names.Add($entry.FullName.Substring($folder.Length + 1).Replace('\', '/'))
  }
  $names.Sort([StringComparer]::Ordinal)
  $result = [Collections.Generic.List[object]]::new()
  foreach ($name in $names) {
    $file = Join-Path $folder ($name.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $result.Add([ordered]@{
      path = $name
      sizeBytes = (Get-Item -LiteralPath $file).Length
      sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    })
  }
  return @($result.ToArray())
}
function Copy-LegalFiles([string]$publish) {
  $dest = Join-Path $publish 'licenses'
  New-Item -ItemType Directory -Force -Path $dest | Out-Null
  foreach ($name in $licenseNames) {
    Copy-Item -LiteralPath (Join-Path $repo ('packaging/licenses/' + $name)) -Destination (Join-Path $dest $name)
  }
  foreach ($entry in @(
      @('LICENSE','LICENSE'), @('LEGAL.md','LEGAL.md'),
      @('BRANDING.md','BRANDING.md'),
      @('packaging/THIRD-PARTY-NOTICES.txt','THIRD-PARTY-NOTICES.txt'),
      @('docs/developer/lic-zip-01-inventory.md','THIRD-PARTY-INVENTORY.md'))) {
    Copy-Item -LiteralPath (Join-Path $repo $entry[0]) -Destination (Join-Path $publish $entry[1])
  }
  $hostfxr = Join-Path $publish 'hostfxr.dll'
  Require-Source $hostfxr
  $observedVersion = (Get-Item -LiteralPath $hostfxr).VersionInfo.ProductVersion
  if (-not $observedVersion.StartsWith($runtimeVersion, [StringComparison]::Ordinal)) {
    throw 'BETA_DOTNET_RUNTIME_VERSION_MISMATCH'
  }
  $roots = @((Join-Path $nugetRoot ('microsoft.netcore.app.runtime.win-x64/' + $runtimeVersion)))
  if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT)) {
    $roots += (Join-Path $env:DOTNET_ROOT ('packs/Microsoft.NETCore.App.Runtime.win-x64/' + $runtimeVersion))
  }
  $runtimeLicense = $null
  $runtimeNotice = $null
  foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    $licenses = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object Name -CEQ 'LICENSE.TXT')
    $notices = @(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object Name -CEQ 'THIRD-PARTY-NOTICES.TXT')
    if ($licenses.Count -eq 1 -and $notices.Count -eq 1) {
      $runtimeLicense = $licenses[0].FullName
      $runtimeNotice = $notices[0].FullName
      break
    }
  }
  if ($null -eq $runtimeLicense -or $null -eq $runtimeNotice) { throw 'BETA_RUNTIME_LEGAL_ASSETS_MISSING' }
  $runtimeDest = Join-Path $dest 'dotnet-runtime'
  New-Item -Path $runtimeDest -ItemType Directory -Force | Out-Null
  Copy-Item -LiteralPath $runtimeLicense -Destination (Join-Path $runtimeDest 'LICENSE.TXT')
  Copy-Item -LiteralPath $runtimeNotice -Destination (Join-Path $runtimeDest 'THIRD-PARTY-NOTICES.TXT')
  $provenance = [ordered]@{
    package = $runtimeName
    version = $runtimeVersion
    hostfxrProductVersion = $observedVersion
    licenseSha256 = (Get-FileHash -LiteralPath $runtimeLicense -Algorithm SHA256).Hash.ToLowerInvariant()
    thirdPartyNoticesSha256 = (Get-FileHash -LiteralPath $runtimeNotice -Algorithm SHA256).Hash.ToLowerInvariant()
  }
  [IO.File]::WriteAllText((Join-Path $runtimeDest 'PROVENANCE.json'),
    (($provenance | ConvertTo-Json -Depth 5) + [Environment]::NewLine), $utf8)
}
function Write-IdealManifest([string]$publish) {
  $notice = @(
    'ROMothèque Manager v0.5.0-beta.1 candidate — NOT A RELEASE.'
    'NO REAL ROM or BIOS bundled. No personal scan, copy, or operation.'
    'phase=validate only. LEGAL HOLD and owner publication HOLD.'
    'Application non signée; SmartScreen peut avertir.'
  ) -join [Environment]::NewLine
  [IO.File]::WriteAllText((Join-Path $publish 'BETA-IDEAL-NOTICE.txt'), ($notice + [Environment]::NewLine), $utf8)
  $manifest = [ordered]@{
    schemaVersion = 1
    package = 'RomothequeManager'
    version = 'v0.5.0-beta.1'
    sourceCommit = $SourceSha
    phase = 'validate'
    distribution = 'LEGAL_AND_OWNER_PUBLISH_HOLD'
    runtimeIdentifier = 'win-x64'
    runtimePack = ($runtimeName + '/' + $runtimeVersion)
    selfContained = $true
    fixtureDataBundled = $false
    files = @(Get-FileRecords $publish)
  }
  [IO.File]::WriteAllText((Join-Path $publish 'BETA-IDEAL-MANIFEST.json'),
    (($manifest | ConvertTo-Json -Depth 10) + [Environment]::NewLine), $utf8)
}
function New-FrozenZip([string]$publish, [string]$path) {
  Add-Type -AssemblyName System.IO.Compression
  $names = [Collections.Generic.List[string]]::new()
  foreach ($file in @(Get-ChildItem -LiteralPath $publish -Recurse -File)) {
    $names.Add($file.FullName.Substring($publish.Length + 1).Replace('\', '/'))
  }
  $names.Sort([StringComparer]::Ordinal)
  $stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
  try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
      foreach ($name in $names) {
        $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
        $sourceFile = Join-Path $publish ($name.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $input = [IO.File]::OpenRead($sourceFile)
        $output = $entry.Open()
        try { $input.CopyTo($output) } finally { $input.Dispose(); $output.Dispose() }
      }
    } finally { $archive.Dispose() }
  } finally { $stream.Dispose() }
}
$builds = [Collections.Generic.List[object]]::new()
for ($index = 1; $index -le 2; $index++) {
  $dir = Join-Path $stage ('build-' + $index)
  $publish = Join-Path $dir 'publish'
  New-Item -ItemType Directory -Force -Path $publish | Out-Null
  # Distinct clean/locked restore/compilation for each pass: no reused output.
  dotnet clean $app -c Release -r win-x64 -p:SelfContained=true
  if ($LASTEXITCODE -ne 0) { throw ('BETA_INDEPENDENT_CLEAN_FAILED:' + $index) }
  dotnet restore $app -r win-x64 --locked-mode -p:SelfContained=true -p:NuGetAudit=true -p:NuGetAuditMode=all -p:TreatWarningsAsErrors=true
  if ($LASTEXITCODE -ne 0) { throw ('BETA_LOCKED_RESTORE_FAILED:' + $index) }
  dotnet publish $app -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -p:ContinuousIntegrationBuild=true -p:Deterministic=true -p:TreatWarningsAsErrors=true -o $publish
  if ($LASTEXITCODE -ne 0) { throw ('BETA_PUBLISH_BUILD_FAILED:' + $index) }
  python -B tests/sqlite_legal_gate.py --manifest packaging/sqlite-legal-manifest.json --publish $publish --packages $nugetRoot --lock src/RomothequeManager.V2/packages.lock.json --notices packaging/THIRD-PARTY-NOTICES.txt --licenses packaging/licenses
  if ($LASTEXITCODE -ne 0) { throw ('BETA_SQLITE_NUGET_FAIL_CLOSED:' + $index) }
  Copy-LegalFiles $publish
  Write-IdealManifest $publish
  $zip = Join-Path $dir ('RomothequeManager-v0.5.0-beta.1-win-x64-' + $SourceSha.Substring(0, 12) + '.zip')
  New-FrozenZip $publish $zip
  $extract = Join-Path $dir 'verified-extracted'
  Expand-Archive -LiteralPath $zip -DestinationPath $extract
  python -B tests/ideal_beta_zip_gate.py --root $extract --source $SourceSha
  if ($LASTEXITCODE -ne 0) { throw ('BETA_EXTRACTED_MANIFEST_VALIDATION_FAILED:' + $index) }
  $builds.Add([pscustomobject]@{
    publish=$publish
    zip=$zip
    extract=$extract
    digest=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    files=@(Get-FileRecords $publish)
  })
  Write-Host ('BETA_INDEPENDENT_BUILD_SUCCESS pass=' + $index + ' sha256=' + $builds[$index - 1].digest + ' files=' + $builds[$index - 1].files.Count)
}
$first = $builds[0]
$second = $builds[1]
if ($first.files.Count -ne $second.files.Count) { throw 'BETA_INDEPENDENT_FILE_COUNT_MISMATCH' }
for ($i = 0; $i -lt $first.files.Count; $i++) {
  if ($first.files[$i].path -cne $second.files[$i].path -or $first.files[$i].sha256 -cne $second.files[$i].sha256 -or $first.files[$i].sizeBytes -ne $second.files[$i].sizeBytes) {
    throw ('BETA_INDEPENDENT_FILE_SHA_MISMATCH:' + $first.files[$i].path)
  }
}
if ($first.digest -cne $second.digest) { throw 'BETA_INDEPENDENT_ZIP_SHA_MISMATCH' }
$checksumLine = $first.digest + '  ' + [IO.Path]::GetFileName($first.zip)
$checksumPath = $first.zip + '.sha256'
[IO.File]::WriteAllText($checksumPath, ($checksumLine + [Environment]::NewLine), $utf8)
if ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -cne $checksumLine) { throw 'BETA_CHECKSUM_SIDECAR_MISMATCH' }
# Negative tests run on disposable copies of the REAL extracted file set.
$negative = Join-Path $stage 'negative-extract'
Copy-Item -LiteralPath $first.extract -Destination $negative -Recurse
Remove-Item -LiteralPath (Join-Path $negative 'e_sqlite3.dll')
python -B tests/ideal_beta_zip_gate.py --root $negative --source $SourceSha
if ($LASTEXITCODE -eq 0) { throw 'BETA_MISSING_NATIVE_LIBRARY_NEGATIVE_FALSE_PASS' }
Copy-Item -LiteralPath (Join-Path $first.extract 'e_sqlite3.dll') -Destination (Join-Path $negative 'e_sqlite3.dll')
[IO.File]::AppendAllText((Join-Path $negative 'BETA-IDEAL-NOTICE.txt'), 'TAMPER', $utf8)
python -B tests/ideal_beta_zip_gate.py --root $negative --source $SourceSha
if ($LASTEXITCODE -eq 0) { throw 'BETA_NOTICE_MUTATION_NEGATIVE_FALSE_PASS' }
Write-Host ('BETA_ZIP_TWO_BUILDS_IDENTICAL sha256=' + $first.digest + ' files=' + $first.files.Count + ' phase=validate no_upload=true')
Write-Host 'BETA_NEGATIVES_PASS missing_native=true tampered_notice=true'
Write-Host ('BETA_ZIP_VALIDATED_STAGED source=' + $SourceSha + ' extract=' + $first.extract)
# An expected nonzero negative oracle must not leak into the successful caller.
# Every fatal positive gate and both negative-refusal assertions precede this point.
$global:LASTEXITCODE = 0
