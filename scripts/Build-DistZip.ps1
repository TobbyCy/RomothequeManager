# DIST-ZIP-01 — build and verify a quarantined, synthetic Windows x64 POC.
# This script NEVER uploads; GitHub Actions owns the separately gated publication.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{40}$')]
  [string]$SourceSha,
  [ValidateSet('validate', 'publish')]
  [string]$Phase = 'validate'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_REF -cne 'refs/heads/main' -or $env:GITHUB_SHA -cne $SourceSha) {
  throw 'DIST_SOURCE_NOT_FROZEN_MAIN'
}
if ($env:GITHUB_ACTOR -cne 'TobbyCy') { throw 'DIST_OWNER_DISPATCH_ONLY' }
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP) -or [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
  throw 'DIST_RUNNER_CONTEXT_MISSING'
}

$repo = (Resolve-Path -LiteralPath '.').Path
$noticeSource = Join-Path $repo 'packaging/THIRD-PARTY-NOTICES.txt'
$inventorySource = Join-Path $repo 'docs/developer/lic-zip-01-inventory.md'
foreach ($requiredSource in @($noticeSource, $inventorySource, (Join-Path $repo 'LICENSE'), (Join-Path $repo 'LEGAL.md'), (Join-Path $repo 'BRANDING.md'))) {
  if (-not (Test-Path -LiteralPath $requiredSource -PathType Leaf)) {
    throw ('DIST_REQUIRED_NOTICE_OR_LEGAL_FILE_MISSING:' + $requiredSource)
  }
}
$notices = [IO.File]::ReadAllText($noticeSource)
$inventory = [IO.File]::ReadAllText($inventorySource)
if ($notices.Length -lt 800 -or $inventory.Length -lt 400) { throw 'DIST_NOTICES_INCOMPLETE' }
foreach ($name in @('Avalonia', 'SkiaSharp', 'HarfBuzz', 'Microsoft', 'MIT')) {
  if (-not $notices.Contains($name)) { throw ('DIST_NOTICE_COMPONENT_MISSING:' + $name) }
}
if ($notices -match '(?i)\[(?:TODO|PENDING|TBD)\]' -or $inventory -match '(?i)\[(?:TODO|PENDING|TBD)\]') {
  throw 'DIST_NOTICES_UNRESOLVED_PLACEHOLDER'
}
$legalApproved = $inventory -cmatch '(?m)^LIC_ZIP_01_BINARY_DISTRIBUTION_APPROVED\r?$'
if ($Phase -ceq 'publish' -and -not $legalApproved) {
  throw 'DIST_LICENCE_VALIDATION_HOLD'
}
$legalStatus = if ($legalApproved) { 'APPROVED_SOURCE_MARKER_PRESENT' } else { 'LEGAL_HOLD' }
Write-Host ('DIST_LEGAL_STATUS phase=' + $Phase + ' status=' + $legalStatus)
if ($Phase -ceq 'validate' -and -not $legalApproved) {
  Write-Warning 'DIST_VALIDATE_LEGAL_HOLD — technical candidate only; no distribution authorization or upload.'
}

$stage = Join-Path $env:RUNNER_TEMP ('dist-zip-01-' + $SourceSha.Substring(0, 12))
$publish = Join-Path $stage 'app'
$extract = Join-Path $stage 'verify-extracted'
New-Item -ItemType Directory -Force -Path $publish | Out-Null
$app = 'src/RomothequeManager.V2/RomothequeManager.V2.csproj'
dotnet restore $app -r win-x64 --locked-mode -p:SelfContained=true -p:NuGetAudit=true -p:NuGetAuditMode=all -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw 'DIST_LOCKED_RESTORE_FAILED' }
dotnet publish $app -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -p:ContinuousIntegrationBuild=true -p:TreatWarningsAsErrors=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'DIST_PUBLISH_FAILED' }

# Canonical LIC-ZIP-01 source files come from DEV 2, not memory or placeholders.
$licensesSource = Join-Path $repo 'packaging/licenses'
$requiredLicenseNames = @(
  'ANGLE-LICENSE.txt', 'ANGLE-xxHash-LICENSE.txt', 'Avalonia-LICENSE.txt', 'Avalonia-NOTICE.txt',
  'HarfBuzz-COPYING.txt', 'MicroCom-LICENSE.txt', 'Skia-LICENSE.txt',
  'SkiaSharp-LICENSE.txt', 'SkiaSharp-THIRD-PARTY-NOTICES.txt', 'Tmds.DBus-COPYING.txt'
)
foreach ($name in $requiredLicenseNames) {
  $source = Join-Path $licensesSource $name
  if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or (Get-Item -LiteralPath $source).Length -lt 100) {
    throw ('DIST_REQUIRED_FULL_LICENSE_MISSING:' + $name)
  }
}
$licenseDestination = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Force -Path $licenseDestination | Out-Null
foreach ($name in $requiredLicenseNames) {
  Copy-Item -LiteralPath (Join-Path $licensesSource $name) -Destination (Join-Path $licenseDestination $name) -Force
}
# The Windows .NET product runtime has its OWN binary license, not merely MIT.
$runtimePackId = 'Microsoft.NETCore.App.Runtime.win-x64'
$runtimePackVersion = '10.0.12'
$fxrVersion = (Get-Item -LiteralPath (Join-Path $publish 'hostfxr.dll')).VersionInfo.ProductVersion
if (-not $fxrVersion.StartsWith($runtimePackVersion)) { throw ('DIST_RUNTIME_PACK_VERSION_MISMATCH:' + $fxrVersion) }
$nugetRoot = if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
  $env:NUGET_PACKAGES
} else {
  Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
}
$runtimeCandidates = @((Join-Path $nugetRoot ('microsoft.netcore.app.runtime.win-x64/' + $runtimePackVersion)))
if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT)) {
  $runtimeCandidates += (Join-Path $env:DOTNET_ROOT ('packs/Microsoft.NETCore.App.Runtime.win-x64/' + $runtimePackVersion))
}
$runtimeLicense = $null
$runtimeNotices = $null
$runtimePackSource = $null
foreach ($candidate in $runtimeCandidates) {
  if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { continue }
  $lic = @(Get-ChildItem -LiteralPath $candidate -Recurse -File | Where-Object { $_.Name -ceq 'LICENSE.TXT' })
  $not = @(Get-ChildItem -LiteralPath $candidate -Recurse -File | Where-Object { $_.Name -ceq 'THIRD-PARTY-NOTICES.TXT' })
  if ($lic.Count -eq 1 -and $not.Count -eq 1) {
    $runtimeLicense = $lic[0]
    $runtimeNotices = $not[0]
    $runtimePackSource = $candidate
    break
  }
}
if ($null -eq $runtimeLicense -or $null -eq $runtimeNotices) {
  throw 'DIST_EXACT_WINDOWS_RUNTIME_LEGAL_ASSETS_MISSING'
}
if ($runtimeLicense.Length -lt 500 -or $runtimeNotices.Length -lt 500) {
  throw 'DIST_RUNTIME_LEGAL_ASSETS_INCOMPLETE'
}
$runtimeLegalDestination = Join-Path $licenseDestination 'dotnet-runtime'
New-Item -ItemType Directory -Force -Path $runtimeLegalDestination | Out-Null
Copy-Item -LiteralPath $runtimeLicense.FullName -Destination (Join-Path $runtimeLegalDestination 'LICENSE.TXT') -Force
Copy-Item -LiteralPath $runtimeNotices.FullName -Destination (Join-Path $runtimeLegalDestination 'THIRD-PARTY-NOTICES.TXT') -Force
$runtimeLicenseSha = (Get-FileHash -LiteralPath $runtimeLicense.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeNoticesSha = (Get-FileHash -LiteralPath $runtimeNotices.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeProvenance = [ordered]@{
  package = $runtimePackId
  version = $runtimePackVersion
  hostfxrProductVersion = $fxrVersion
  licenseSha256 = $runtimeLicenseSha
  thirdPartyNoticesSha256 = $runtimeNoticesSha
}
[IO.File]::WriteAllText((Join-Path $runtimeLegalDestination 'PROVENANCE.json'), (($runtimeProvenance | ConvertTo-Json -Depth 4) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
Write-Host ('DIST_RUNTIME_LEGAL_ASSETS_VERIFIED package=' + $runtimePackId + ' version=' + $runtimePackVersion + ' licenseSha256=' + $runtimeLicenseSha + ' noticesSha256=' + $runtimeNoticesSha + ' source=' + $runtimePackSource)

Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $publish 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $repo 'LEGAL.md') -Destination (Join-Path $publish 'LEGAL.md') -Force
Copy-Item -LiteralPath (Join-Path $repo 'BRANDING.md') -Destination (Join-Path $publish 'BRANDING.md') -Force
Copy-Item -LiteralPath $noticeSource -Destination (Join-Path $publish 'THIRD-PARTY-NOTICES.txt') -Force
Copy-Item -LiteralPath $inventorySource -Destination (Join-Path $publish 'THIRD-PARTY-INVENTORY.md') -Force
$notice = @(
  'ROMothèque Manager — POC SYNTHÉTIQUE / TEST UNIQUEMENT'
  'Windows x64 autonome ; six jeux fictifs, trois bibliothèques fictives, quatre vues.'
  'Aucune ROM, BIOS, bibliothèque réelle, sauvegarde, collecte ni export inclus.'
  'Application non signée : Windows SmartScreen peut afficher un avertissement.'
  'Code original MIT ; marque/bandeau réservés ; licences tierces dans THIRD-PARTY-NOTICES.txt.'
  'Pré-release de test uniquement après autorisation distincte. Pas une version stable.'
) -join [Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $publish 'POC-NOTICE.txt'), ($notice + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
$manifest = [ordered]@{
  schemaVersion = 1
  package = 'RomothequeManager-V2-01C'
  sourceCommit = $SourceSha
  runtimeIdentifier = 'win-x64'
  selfContained = $true
  syntheticOnly = $true
  gameFixtures = 6
  libraryFixtures = 3
  views = @('Dashboard', 'Collection', 'GameDetail', 'Libraries')
  distribution = 'POC_TEST_ONLY_OWNER_CONTROLLED'
  runtimePack = 'Microsoft.NETCore.App.Runtime.win-x64/10.0.12'
}
[IO.File]::WriteAllText((Join-Path $publish 'POC-MANIFEST.json'), (($manifest | ConvertTo-Json -Depth 4) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))

foreach ($required in @('RomothequeManager.V2.exe', 'coreclr.dll', 'hostfxr.dll', 'LICENSE', 'LEGAL.md', 'BRANDING.md', 'POC-NOTICE.txt', 'POC-MANIFEST.json', 'THIRD-PARTY-NOTICES.txt', 'THIRD-PARTY-INVENTORY.md', 'licenses/dotnet-runtime/LICENSE.TXT', 'licenses/dotnet-runtime/THIRD-PARTY-NOTICES.TXT', 'licenses/dotnet-runtime/PROVENANCE.json')) {
  if (-not (Test-Path -LiteralPath (Join-Path $publish $required) -PathType Leaf)) {
    throw ('DIST_PUBLISH_FILE_MISSING:' + $required)
  }
}
$published = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Sort-Object FullName)
if ($published.Count -lt 10) { throw 'DIST_PUBLISH_EMPTY' }
Write-Host ('DIST_PUBLISH_FILE_MANIFEST_BEGIN source=' + $SourceSha + ' count=' + $published.Count)
foreach ($file in $published) {
  $relative = $file.FullName.Substring($publish.Length + 1)
  $version = if ($file.Extension -in @('.dll', '.exe')) { $file.VersionInfo.FileVersion } else { '' }
  $productVersion = if ($file.Extension -in @('.dll', '.exe')) { $file.VersionInfo.ProductVersion } else { '' }
  $record = [ordered]@{
    path = $relative
    sizeBytes = $file.Length
    sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    fileVersion = $version
    productVersion = $productVersion
  }
  Write-Host ('DIST_PUBLISH_FILE_JSON ' + ($record | ConvertTo-Json -Compress))
}
Write-Host ('DIST_PUBLISH_FILE_MANIFEST_END source=' + $SourceSha + ' count=' + $published.Count)

$forbiddenExtensions = @('.rom', '.chd', '.iso', '.cue', '.gdi', '.cdi', '.nes', '.sfc', '.smc', '.gba', '.gbc', '.n64', '.z64', '.nsp', '.xci', '.wad', '.sav', '.srm', '.pfx', '.pem', '.p12', '.snk', '.key')
$forbiddenNames = @('.env', 'id_rsa', 'id_ed25519', 'credentials.json', 'secrets.json')
foreach ($file in $published) {
  if ($forbiddenExtensions -ccontains $file.Extension.ToLowerInvariant() -or $forbiddenNames -ccontains $file.Name.ToLowerInvariant()) {
    throw ('DIST_FORBIDDEN_CONTENT:' + $file.FullName)
  }
}
function New-DeterministicZip([string]$SourceDirectory, [string]$Destination) {
  Add-Type -AssemblyName System.IO.Compression
  $files = @(Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File | Sort-Object { $_.FullName.Substring($SourceDirectory.Length + 1) })
  $output = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
  try {
    $archive = [IO.Compression.ZipArchive]::new($output, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
      foreach ($file in $files) {
        $relative = $file.FullName.Substring($SourceDirectory.Length + 1).Replace([IO.Path]::DirectorySeparatorChar, '/')
        $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
        $sourceStream = [IO.File]::OpenRead($file.FullName)
        $destinationStream = $entry.Open()
        try { $sourceStream.CopyTo($destinationStream) } finally { $destinationStream.Dispose(); $sourceStream.Dispose() }
      }
    } finally { $archive.Dispose() }
  } finally { $output.Dispose() }
}
$baseName = 'RomothequeManager-V2-01C-win-x64-' + $SourceSha.Substring(0, 12)
$zip = Join-Path $stage ($baseName + '.zip')
$repro = Join-Path $stage 'repeat-archive-check.zip'
New-DeterministicZip $publish $zip
New-DeterministicZip $publish $repro
$digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-FileHash -LiteralPath $repro -Algorithm SHA256).Hash.ToLowerInvariant() -cne $digest) {
  throw 'DIST_ARCHIVE_REPRODUCIBILITY_FAILED'
}
$checksum = Join-Path $stage ($baseName + '.zip.sha256')
$checksumLine = $digest + '  ' + [IO.Path]::GetFileName($zip)
[IO.File]::WriteAllText($checksum, ($checksumLine + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
if ((Get-Content -LiteralPath $checksum -Raw).Trim() -cne $checksumLine) { throw 'DIST_CHECKSUM_FILE_INVALID' }
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -cne $digest) { throw 'DIST_ZIP_SHA256_MISMATCH' }

Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
$extracted = @(Get-ChildItem -LiteralPath $extract -Recurse -File)
$publishRelative = @($published | ForEach-Object { $_.FullName.Substring($publish.Length + 1) })
$extractRelative = @($extracted | ForEach-Object { $_.FullName.Substring($extract.Length + 1) })
if (@(Compare-Object -ReferenceObject $publishRelative -DifferenceObject $extractRelative -CaseSensitive).Count -ne 0) {
  throw 'DIST_EXTRACTED_FILE_SET_MISMATCH'
}
foreach ($file in $published) {
  $relative = $file.FullName.Substring($publish.Length + 1)
  $copy = Join-Path $extract $relative
  if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) {
    throw ('DIST_EXTRACTED_FILE_SHA256_MISMATCH:' + $relative)
  }
}
$checkedManifest = Get-Content -LiteralPath (Join-Path $extract 'POC-MANIFEST.json') -Raw | ConvertFrom-Json
if ($checkedManifest.sourceCommit -cne $SourceSha -or $checkedManifest.runtimeIdentifier -cne 'win-x64' -or -not $checkedManifest.selfContained -or -not $checkedManifest.syntheticOnly -or $checkedManifest.gameFixtures -ne 6 -or $checkedManifest.libraryFixtures -ne 3 -or ($checkedManifest.views -join ',') -cne 'Dashboard,Collection,GameDetail,Libraries' -or $checkedManifest.distribution -cne 'POC_TEST_ONLY_OWNER_CONTROLLED' -or $checkedManifest.runtimePack -cne 'Microsoft.NETCore.App.Runtime.win-x64/10.0.12') {
  throw 'DIST_MANIFEST_INVALID'
}
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = Join-Path $extract 'RomothequeManager.V2.exe'
$start.Arguments = '--smoke-window'
$start.WorkingDirectory = $extract
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.Environment['DOTNET_ROOT'] = Join-Path $stage 'intentionally-unavailable-dotnet'
$start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$proc = [Diagnostics.Process]::Start($start)
try {
  $stdout = $proc.StandardOutput.ReadToEndAsync()
  $stderr = $proc.StandardError.ReadToEndAsync()
  if (-not $proc.WaitForExit(30000)) { $proc.Kill(); throw 'DIST_EXTRACTED_SMOKE_TIMEOUT' }
  $expected = 'POC_WINDOW_OPENED_PASS views=Dashboard,Collection,GameDetail,Libraries fixtures=6 synthetic=1'
  if ($proc.ExitCode -ne 0 -or -not $stdout.Result.Contains($expected)) {
    throw ('DIST_EXTRACTED_SMOKE_FAILED exit=' + $proc.ExitCode + ';stdout=' + $stdout.Result + ';stderr=' + $stderr.Result)
  }
  Write-Host $stdout.Result
} finally { $proc.Dispose() }

Write-Host ('DIST_ZIP_VALIDATED_PASS sha256=' + $digest + ' sizeBytes=' + (Get-Item $zip).Length + ' files=' + $extracted.Count + ' source=' + $SourceSha + ' legalStatus=' + $legalStatus + ' phase=' + $Phase)
if ($Phase -ceq 'validate' -and -not $legalApproved) {
  Write-Host ('DIST_CANDIDATE_VERIFIED_LEGAL_HOLD sha256=' + $digest + ' source=' + $SourceSha + ' — NO_UPLOAD')
}
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('zip=' + $zip + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('checksum=' + $checksum + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('sha256=' + $digest + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('zipName=' + [IO.Path]::GetFileName($zip) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('checksumName=' + [IO.Path]::GetFileName($checksum) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('legalStatus=' + $legalStatus + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
# Deliberately no upload or publication in this script.
