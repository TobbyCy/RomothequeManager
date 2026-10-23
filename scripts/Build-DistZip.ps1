# DIST-ZIP-01 — build and verify a quarantined, synthetic Windows x64 POC.
# This script NEVER uploads; GitHub Actions owns the separately gated publication.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[0-9a-f]{40}$')]
  [string]$SourceSha
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
$noticeSource = Join-Path $repo 'docs/legal/THIRD-PARTY-NOTICES.txt'
$inventorySource = Join-Path $repo 'docs/legal/THIRD-PARTY-INVENTORY.md'
foreach ($requiredSource in @($noticeSource, $inventorySource, (Join-Path $repo 'LICENSE'), (Join-Path $repo 'LEGAL.md'))) {
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

$stage = Join-Path $env:RUNNER_TEMP ('dist-zip-01-' + $SourceSha.Substring(0, 12))
$publish = Join-Path $stage 'app'
$extract = Join-Path $stage 'verify-extracted'
New-Item -ItemType Directory -Force -Path $publish | Out-Null
$app = 'src/RomothequeManager.V2/RomothequeManager.V2.csproj'
dotnet restore $app -r win-x64 --locked-mode -p:SelfContained=true -p:NuGetAudit=true -p:NuGetAuditMode=all -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw 'DIST_LOCKED_RESTORE_FAILED' }
dotnet publish $app -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -p:PublishTrimmed=false -p:ContinuousIntegrationBuild=true -p:TreatWarningsAsErrors=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'DIST_PUBLISH_FAILED' }

Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $publish 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $repo 'LEGAL.md') -Destination (Join-Path $publish 'LEGAL.md') -Force
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
}
[IO.File]::WriteAllText((Join-Path $publish 'POC-MANIFEST.json'), (($manifest | ConvertTo-Json -Depth 4) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))

foreach ($required in @('RomothequeManager.V2.exe', 'coreclr.dll', 'hostfxr.dll', 'LICENSE', 'LEGAL.md', 'POC-NOTICE.txt', 'POC-MANIFEST.json', 'THIRD-PARTY-NOTICES.txt', 'THIRD-PARTY-INVENTORY.md')) {
  if (-not (Test-Path -LiteralPath (Join-Path $publish $required) -PathType Leaf)) {
    throw ('DIST_PUBLISH_FILE_MISSING:' + $required)
  }
}
$published = @(Get-ChildItem -LiteralPath $publish -Recurse -File | Sort-Object FullName)
if ($published.Count -lt 10) { throw 'DIST_PUBLISH_EMPTY' }
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
if ($checkedManifest.sourceCommit -cne $SourceSha -or $checkedManifest.runtimeIdentifier -cne 'win-x64' -or -not $checkedManifest.selfContained -or -not $checkedManifest.syntheticOnly -or $checkedManifest.gameFixtures -ne 6 -or $checkedManifest.libraryFixtures -ne 3 -or ($checkedManifest.views -join ',') -cne 'Dashboard,Collection,GameDetail,Libraries' -or $checkedManifest.distribution -cne 'POC_TEST_ONLY_OWNER_CONTROLLED') {
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

Write-Host ('DIST_ZIP_VALIDATED_PASS sha256=' + $digest + ' sizeBytes=' + (Get-Item $zip).Length + ' files=' + $extracted.Count + ' source=' + $SourceSha)
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('zip=' + $zip + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('checksum=' + $checksum + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('sha256=' + $digest + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('zipName=' + [IO.Path]::GetFileName($zip) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
[IO.File]::AppendAllText($env:GITHUB_OUTPUT, ('checksumName=' + [IO.Path]::GetFileName($checksum) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
# Deliberately no upload or publication in this script.
