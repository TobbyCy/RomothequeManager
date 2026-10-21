# CI-0: file audit and parse-only validation. Never imports or executes engine code.
[CmdletBinding()]
param([string]$AdditionalSyntaxPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$failures = New-Object 'System.Collections.Generic.List[string]'
function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { $script:failures.Add($Message) }
}
function Get-CommittedSha256([string]$RelativePath) {
    # Windows checkout can convert LF to CRLF; audit ORIGINAL Git blob bytes, not checkout bytes.
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $git.Source
    $start.Arguments = 'cat-file blob "HEAD:' + $RelativePath + '"'
    $start.WorkingDirectory = $root
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $proc = [Diagnostics.Process]::Start($start)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash($proc.StandardOutput.BaseStream)
        $errorText = $proc.StandardError.ReadToEnd()
        $proc.WaitForExit()
        if ($proc.ExitCode -ne 0) { throw ('GIT_BLOB_READ_FAILED: ' + $RelativePath + ' : ' + $errorText) }
        return ([BitConverter]::ToString($digest)).Replace('-','').ToLowerInvariant()
    } finally { $sha.Dispose(); $proc.Dispose() }
}
try {
    Set-Location -LiteralPath $root
    $git = Get-Command git.exe -ErrorAction Stop
    $paths = @(& $git.Source ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed' }
    Require ($paths.Count -gt 0) 'No tracked files'
    $allowed = @(
        '^README\.md$', '^CHANGELOG\.md$', '^CONTRIBUTING\.md$', '^SECURITY\.md$', '^SUPPORT\.md$', '^\.gitignore$', '^\.gitattributes$', '^Initialize\.ps1$',
        '^Modules/[A-Za-z][A-Za-z0-9]*\.psm1$',
        '^Export/(ExportE1\.psm1|Invoke-ExportE1InventoryReadOnly\.ps1|README\.md)$',
        '^Preflight/Invoke-E1RealLibraryPreflightReadOnly\.ps1$',
        '^docs/(product-status|roadmap)\.md$',
        '^docs/(user|developer|architecture|decisions|ux)/[A-Za-z0-9_.-]+\.md$',
        '^\.github/workflows/ci\.yml$', '^\.github/pull_request_template\.md$',
        '^\.github/ISSUE_TEMPLATE/(bug|config|docs|feature|question)\.yml$',
        '^tests/(ci_policy|test_ci_policy)\.py$',
        '^(src|app)/RomothequeManager\.V2/([A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(cs|csproj|sln|axaml|resx|json|png|svg|ico)$',
        '^tests/dotnet/RomothequeManager\.V2\.Tests/([A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(cs|csproj|json)$',
        '^(contracts|schemas)/([A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.json$',
        '^adapters/shared/([A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.(cs|ps1|psm1|json)$',
        '^tests/Invoke-Ci[A-Za-z]+\.ps1$', '^tests/Invoke-CiE1NegativeCanary\.ps1$', '^tests/Invoke-CiE1Integration\.ps1$',
        '^tests/Unit/[A-Za-z0-9_.-]+\.Tests\.ps1$', '^tests/fixtures/(SyntheticLibrarySnapshots|UiProjectionVectors)\.ps1, '^tests/(ci-export-sources|ci-preflight-sources)\.json$'
    )
    $collisions = @{}
    foreach ($path in $paths) {
        $pass = $false
        foreach ($rule in $allowed) { if ($path -cmatch $rule) { $pass = $true; break } }
        Require $pass ('TRACKED_PATH_NOT_ALLOWED: ' + $path)
        $key = $path.Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
        Require (-not $collisions.ContainsKey($key)) ('CASE_UNICODE_COLLISION: ' + $path)
        $collisions[$key] = $true
        $full = Join-Path $root $path
        Require (Test-Path -LiteralPath $full -PathType Leaf) ('TRACKED_FILE_MISSING: ' + $path)
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $file = Get-Item -LiteralPath $full -Force
            Require (-not (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) ('REPARSE_FILE: ' + $path)
            Require ($file.Length -lt 2000000) ('SUSPICIOUS_SIZE: ' + $path)
        }
    }
    $expectedModules = @('Utils','Canonical','Configuration','Inventory','Metadata','RomResolver','DecisionSchema','StateCodec','Identity','PersistentIdentity','MediaMatcher','MediaModel','Reconciliation','Proposals','Projection','Transactions','Catalogue','Associations')
    $modules = @(Get-ChildItem -LiteralPath (Join-Path $root 'Modules') -File -Filter '*.psm1')
    Require ($modules.Count -eq 20) ('EXPECTED_20_MODULES actual=' + $modules.Count)
    $extensionModules = @('SnapshotComparison','LibrarySnapshotProjection')
    foreach ($name in $extensionModules) {
        Require (Test-Path (Join-Path $root ('Modules\' + $name + '.psm1')) -PathType Leaf) ('MISSING_APPROVED_EXTENSION_MODULE: ' + $name)
    }
    $actualModuleNames = @($modules | ForEach-Object { $_.BaseName } | Sort-Object)
    $approvedModuleNames = @(($expectedModules + $extensionModules) | Sort-Object)
    Require (($actualModuleNames -join '|') -ceq ($approvedModuleNames -join '|')) 'UNEXPECTED_EXTENSION_MODULE'
    $init = [IO.File]::ReadAllText((Join-Path $root 'Initialize.ps1'))
    $order = [regex]::Match($init,'(?s)\$moduleOrder\s*=\s*@\((.*?)\)')
    Require $order.Success 'BOOTSTRAP_ORDER_NOT_FOUND'
    if ($order.Success) {
        $names = @([regex]::Matches($order.Groups[1].Value,"'([^']+)'") | ForEach-Object { $_.Groups[1].Value })
        Require (($names -join '|') -ceq ($expectedModules -join '|')) 'BOOTSTRAP_ORDER_CHANGED'
        foreach ($name in $expectedModules) {
            Require (Test-Path (Join-Path $root ('Modules\' + $name + '.psm1')) -PathType Leaf) ('MISSING_BOOTSTRAP_MODULE: ' + $name)
        }
    }
    # These hashes preserve the separately audited engine baseline; the CI files live in a different commit.
    $baseline = @'
178c318fc270c3afc090c8edd644dbb47656e61c19c30b3894ca07780af430c6  Initialize.ps1
f16335429ccb2252210aadb242a051ed626a850d3729e5879c8a390d358780d2  Modules/Associations.psm1
bae923a4585c40b4a4d20d8b1caa9e16bedf54be31a9ea8952f5b523842b2864  Modules/Canonical.psm1
3631c5adad951628cb8312311c9af9d88b4af172dfcf81aaad0b5a3df75076ae  Modules/Catalogue.psm1
2a90ecd75fe137a4f5c02ae542fe132af98079664842c9df9819c54660424424  Modules/Configuration.psm1
0a42e17d5f06a7c8cc3cb64ee3dab6402df805fd4d8e00be95a85105aa25e793  Modules/DecisionSchema.psm1
b9ba591f35f46b0cafa1b62595a21146db9e98415f3a0af2db2426c54c80eb28  Modules/Identity.psm1
3511e9857f186ca39eb10b4b6a0adc728792cf3f59942ac4c4fcd4c6b15edc83  Modules/Inventory.psm1
6be444fe3db82c92d15b4414ce9aab3a39e9870267235fb9e0051d407322a5a4  Modules/MediaMatcher.psm1
b3bcfbe222886f6b78a104b6d17c553c12fbf4ad556e315edd9969672894a423  Modules/MediaModel.psm1
328f73b259c435887ee17d122d2fa83d72bc76c1174dc1fcc41bedbfad8e9b77  Modules/Metadata.psm1
58e1d8203c71bc36ad00d294602e12a4310718a53a572dcfdf45014a576020b2  Modules/PersistentIdentity.psm1
1b915efacc23f2fb3f79fca7b1b18cb2eb3e46bee359376edbf4d0a0fb5b81df  Modules/Projection.psm1
42a6c951fc87f8658e2f3b5113af50b4c79ceb21b02412e051c3da4edb851201  Modules/Proposals.psm1
08ba836d5f70cffc1caf1795176f5e405633116d63ceee6870424bdf80355405  Modules/Reconciliation.psm1
a01c347d20fabfae58b34065adf9a5fa0e172780bca5e92ec98a3528d4cff240  Modules/RomResolver.psm1
fb6a46bc6e9e5786e3800c67e82abe6937343d8f19952b861f053ade6786e1d4  Modules/StateCodec.psm1
827bf312b10cef21059d3921ce5f0d47e4b475e64014a68df03ede47c8b063af  Modules/Transactions.psm1
28318a751b276250d4b3224ba89008261714dfa4f680b859023fb31400a7c827  Modules/Utils.psm1
'@
    foreach ($row in @($baseline -split "`n")) {
        if ($row -notmatch '^([0-9a-f]{64})  ([^\r\n]+)') { continue }
        $expectedHash = $matches[1];$name = $matches[2]
        $target = Join-Path $root ($name.Replace('/', '\'))
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            $actual = Get-CommittedSha256 $name
            Require ($actual -ceq $expectedHash) ('BASELINE_HASH_CHANGED: ' + $name)
        } else { Require $false ('BASELINE_FILE_MISSING: ' + $name) }
    }
    # E1 sources are separately hash-locked through a versioned manifest.
    # Do not alter the historic 19-SHA baseline above or silently admit other Export files.
    $manifestRelative = 'tests/ci-export-sources.json'
    $manifestFile = Join-Path $root 'tests\ci-export-sources.json'
    $exportManifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($exportManifest.schemaVersion -ceq '1.0.0') 'EXPORT_MANIFEST_SCHEMA_INVALID'
    $exportAllowed = @('Export/ExportE1.psm1','Export/Invoke-ExportE1InventoryReadOnly.ps1')
    $declared = @($exportManifest.sources)
    Require ($declared.Count -eq $exportAllowed.Count) ('EXPORT_MANIFEST_COUNT_INVALID: ' + $declared.Count)
    $seenNew=@{}
    foreach ($entry in $declared) {
        $name=[string]$entry.path
        Require ($name -cin $exportAllowed) ('EXPORT_MANIFEST_PATH_NOT_APPROVED: ' + $name)
        Require (-not $seenNew.ContainsKey($name)) ('EXPORT_MANIFEST_DUPLICATE: ' + $name)
        $seenNew[$name]=$true
        Require ($name -cin $paths) ('EXPORT_SOURCE_NOT_TRACKED: ' + $name)
        $expectedHash=[string]$entry.sha256
        Require ($expectedHash -cmatch '^[0-9a-f]{64}$') ('EXPORT_MANIFEST_SHA_INVALID: ' + $name)
        if ($name -cin $paths -and $expectedHash -cmatch '^[0-9a-f]{64}$') {
            Require ((Get-CommittedSha256 $name) -ceq $expectedHash) ('EXPORT_SOURCE_SHA_CHANGED: ' + $name)
        }
    }
    foreach ($name in $exportAllowed) {Require $seenNew.ContainsKey($name) ('EXPORT_SOURCE_UNDECLARED: ' + $name)}
    foreach ($name in @($paths | Where-Object {$_ -match '^Export/.*\.(ps1|psm1)$'})) {Require ($name -cin $exportAllowed) ('EXPORT_SOURCE_NOT_IN_MANIFEST: ' + $name)}
    # This new preflight has an independent SHA lock; the 19 historical and two E1 hashes remain unchanged.
    $preflightPath = 'Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1'
    $preflightManifest = Get-Content -LiteralPath (Join-Path $root 'tests\ci-preflight-sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($preflightManifest.schemaVersion -ceq '1.0.0') 'PREFLIGHT_MANIFEST_SCHEMA_INVALID'
    Require (@($preflightManifest.sources).Count -eq 1) 'PREFLIGHT_MANIFEST_COUNT_INVALID'
    $pfEntry = @($preflightManifest.sources)[0]
    Require ([string]$pfEntry.path -ceq $preflightPath) 'PREFLIGHT_MANIFEST_PATH_INVALID'
    Require ([string]$pfEntry.sha256 -cmatch '^[0-9a-f]{64}$') 'PREFLIGHT_MANIFEST_SHA_INVALID'
    if ([string]$pfEntry.path -ceq $preflightPath -and [string]$pfEntry.sha256 -cmatch '^[0-9a-f]{64}$') {
        Require ((Get-CommittedSha256 $preflightPath) -ceq [string]$pfEntry.sha256) 'PREFLIGHT_SOURCE_SHA_CHANGED'
    }
    $sourceNames = @('Initialize.ps1') + @($modules | ForEach-Object { 'Modules/' + $_.Name }) + $exportAllowed + @($preflightPath)
    foreach ($name in $sourceNames) {
        $file = Join-Path $root $name
        $data = [IO.File]::ReadAllText($file)
        # Detect embedded absolute personal library roots, credential-like literals, and common token prefixes.
        Require (-not ($data -match '(?i)[A-Z]:\\Users\\[^\s\x22\x27]+')) ('PERSONAL_PATH: ' + $name)
        Require (-not ($data -match '(?i)[A-Z]:\\ROM[oô]th[eè]que')) ('LIBRARY_PATH: ' + $name)
        Require (-not ($data -match '(?i)(github_pat_|gh[pousr]_|AKIA)[A-Za-z0-9_]{16,}')) ('TOKEN_PATTERN: ' + $name)
        Require (-not ($data -match '(?i)(password|secret|api[_-]?key)\s*[:=]\s*[\x22\x27][^\x22\x27]{4,}')) ('SUSPECT_SECRET_ASSIGNMENT: ' + $name)
    }
    $parse = @($paths | Where-Object { $_ -match '\.(ps1|psm1)$' } | ForEach-Object { Join-Path $root $_ })
    if ($AdditionalSyntaxPath) { $parse += @($AdditionalSyntaxPath) }
    foreach ($file in $parse) {
        $tokens = $null; $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
        foreach ($e in @($errors)) { Require $false ('PARSE_ERROR: ' + $file + ' : ' + $e.Message) }
    }
    if ($failures.Count -gt 0) { throw ("CI-0 FAIL ({0}):`n{1}" -f $failures.Count,($failures -join "`n")) }
    Write-Host ('CI-0 PASS | tracked=' + $paths.Count + ' | modules=' + $modules.Count + ' | parsed=' + $parse.Count + ' | baseline=19/19 | export=2/2')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
, '^tests/Export-SyntheticUiProjection\.ps1, '^tests/(ci-export-sources|ci-preflight-sources)\.json$'
    )
    $collisions = @{}
    foreach ($path in $paths) {
        $pass = $false
        foreach ($rule in $allowed) { if ($path -cmatch $rule) { $pass = $true; break } }
        Require $pass ('TRACKED_PATH_NOT_ALLOWED: ' + $path)
        $key = $path.Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
        Require (-not $collisions.ContainsKey($key)) ('CASE_UNICODE_COLLISION: ' + $path)
        $collisions[$key] = $true
        $full = Join-Path $root $path
        Require (Test-Path -LiteralPath $full -PathType Leaf) ('TRACKED_FILE_MISSING: ' + $path)
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $file = Get-Item -LiteralPath $full -Force
            Require (-not (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) ('REPARSE_FILE: ' + $path)
            Require ($file.Length -lt 2000000) ('SUSPICIOUS_SIZE: ' + $path)
        }
    }
    $expectedModules = @('Utils','Canonical','Configuration','Inventory','Metadata','RomResolver','DecisionSchema','StateCodec','Identity','PersistentIdentity','MediaMatcher','MediaModel','Reconciliation','Proposals','Projection','Transactions','Catalogue','Associations')
    $modules = @(Get-ChildItem -LiteralPath (Join-Path $root 'Modules') -File -Filter '*.psm1')
    Require ($modules.Count -eq 20) ('EXPECTED_20_MODULES actual=' + $modules.Count)
    $extensionModules = @('SnapshotComparison','LibrarySnapshotProjection')
    foreach ($name in $extensionModules) {
        Require (Test-Path (Join-Path $root ('Modules\' + $name + '.psm1')) -PathType Leaf) ('MISSING_APPROVED_EXTENSION_MODULE: ' + $name)
    }
    $actualModuleNames = @($modules | ForEach-Object { $_.BaseName } | Sort-Object)
    $approvedModuleNames = @(($expectedModules + $extensionModules) | Sort-Object)
    Require (($actualModuleNames -join '|') -ceq ($approvedModuleNames -join '|')) 'UNEXPECTED_EXTENSION_MODULE'
    $init = [IO.File]::ReadAllText((Join-Path $root 'Initialize.ps1'))
    $order = [regex]::Match($init,'(?s)\$moduleOrder\s*=\s*@\((.*?)\)')
    Require $order.Success 'BOOTSTRAP_ORDER_NOT_FOUND'
    if ($order.Success) {
        $names = @([regex]::Matches($order.Groups[1].Value,"'([^']+)'") | ForEach-Object { $_.Groups[1].Value })
        Require (($names -join '|') -ceq ($expectedModules -join '|')) 'BOOTSTRAP_ORDER_CHANGED'
        foreach ($name in $expectedModules) {
            Require (Test-Path (Join-Path $root ('Modules\' + $name + '.psm1')) -PathType Leaf) ('MISSING_BOOTSTRAP_MODULE: ' + $name)
        }
    }
    # These hashes preserve the separately audited engine baseline; the CI files live in a different commit.
    $baseline = @'
178c318fc270c3afc090c8edd644dbb47656e61c19c30b3894ca07780af430c6  Initialize.ps1
f16335429ccb2252210aadb242a051ed626a850d3729e5879c8a390d358780d2  Modules/Associations.psm1
bae923a4585c40b4a4d20d8b1caa9e16bedf54be31a9ea8952f5b523842b2864  Modules/Canonical.psm1
3631c5adad951628cb8312311c9af9d88b4af172dfcf81aaad0b5a3df75076ae  Modules/Catalogue.psm1
2a90ecd75fe137a4f5c02ae542fe132af98079664842c9df9819c54660424424  Modules/Configuration.psm1
0a42e17d5f06a7c8cc3cb64ee3dab6402df805fd4d8e00be95a85105aa25e793  Modules/DecisionSchema.psm1
b9ba591f35f46b0cafa1b62595a21146db9e98415f3a0af2db2426c54c80eb28  Modules/Identity.psm1
3511e9857f186ca39eb10b4b6a0adc728792cf3f59942ac4c4fcd4c6b15edc83  Modules/Inventory.psm1
6be444fe3db82c92d15b4414ce9aab3a39e9870267235fb9e0051d407322a5a4  Modules/MediaMatcher.psm1
b3bcfbe222886f6b78a104b6d17c553c12fbf4ad556e315edd9969672894a423  Modules/MediaModel.psm1
328f73b259c435887ee17d122d2fa83d72bc76c1174dc1fcc41bedbfad8e9b77  Modules/Metadata.psm1
58e1d8203c71bc36ad00d294602e12a4310718a53a572dcfdf45014a576020b2  Modules/PersistentIdentity.psm1
1b915efacc23f2fb3f79fca7b1b18cb2eb3e46bee359376edbf4d0a0fb5b81df  Modules/Projection.psm1
42a6c951fc87f8658e2f3b5113af50b4c79ceb21b02412e051c3da4edb851201  Modules/Proposals.psm1
08ba836d5f70cffc1caf1795176f5e405633116d63ceee6870424bdf80355405  Modules/Reconciliation.psm1
a01c347d20fabfae58b34065adf9a5fa0e172780bca5e92ec98a3528d4cff240  Modules/RomResolver.psm1
fb6a46bc6e9e5786e3800c67e82abe6937343d8f19952b861f053ade6786e1d4  Modules/StateCodec.psm1
827bf312b10cef21059d3921ce5f0d47e4b475e64014a68df03ede47c8b063af  Modules/Transactions.psm1
28318a751b276250d4b3224ba89008261714dfa4f680b859023fb31400a7c827  Modules/Utils.psm1
'@
    foreach ($row in @($baseline -split "`n")) {
        if ($row -notmatch '^([0-9a-f]{64})  ([^\r\n]+)') { continue }
        $expectedHash = $matches[1];$name = $matches[2]
        $target = Join-Path $root ($name.Replace('/', '\'))
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            $actual = Get-CommittedSha256 $name
            Require ($actual -ceq $expectedHash) ('BASELINE_HASH_CHANGED: ' + $name)
        } else { Require $false ('BASELINE_FILE_MISSING: ' + $name) }
    }
    # E1 sources are separately hash-locked through a versioned manifest.
    # Do not alter the historic 19-SHA baseline above or silently admit other Export files.
    $manifestRelative = 'tests/ci-export-sources.json'
    $manifestFile = Join-Path $root 'tests\ci-export-sources.json'
    $exportManifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($exportManifest.schemaVersion -ceq '1.0.0') 'EXPORT_MANIFEST_SCHEMA_INVALID'
    $exportAllowed = @('Export/ExportE1.psm1','Export/Invoke-ExportE1InventoryReadOnly.ps1')
    $declared = @($exportManifest.sources)
    Require ($declared.Count -eq $exportAllowed.Count) ('EXPORT_MANIFEST_COUNT_INVALID: ' + $declared.Count)
    $seenNew=@{}
    foreach ($entry in $declared) {
        $name=[string]$entry.path
        Require ($name -cin $exportAllowed) ('EXPORT_MANIFEST_PATH_NOT_APPROVED: ' + $name)
        Require (-not $seenNew.ContainsKey($name)) ('EXPORT_MANIFEST_DUPLICATE: ' + $name)
        $seenNew[$name]=$true
        Require ($name -cin $paths) ('EXPORT_SOURCE_NOT_TRACKED: ' + $name)
        $expectedHash=[string]$entry.sha256
        Require ($expectedHash -cmatch '^[0-9a-f]{64}$') ('EXPORT_MANIFEST_SHA_INVALID: ' + $name)
        if ($name -cin $paths -and $expectedHash -cmatch '^[0-9a-f]{64}$') {
            Require ((Get-CommittedSha256 $name) -ceq $expectedHash) ('EXPORT_SOURCE_SHA_CHANGED: ' + $name)
        }
    }
    foreach ($name in $exportAllowed) {Require $seenNew.ContainsKey($name) ('EXPORT_SOURCE_UNDECLARED: ' + $name)}
    foreach ($name in @($paths | Where-Object {$_ -match '^Export/.*\.(ps1|psm1)$'})) {Require ($name -cin $exportAllowed) ('EXPORT_SOURCE_NOT_IN_MANIFEST: ' + $name)}
    # This new preflight has an independent SHA lock; the 19 historical and two E1 hashes remain unchanged.
    $preflightPath = 'Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1'
    $preflightManifest = Get-Content -LiteralPath (Join-Path $root 'tests\ci-preflight-sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($preflightManifest.schemaVersion -ceq '1.0.0') 'PREFLIGHT_MANIFEST_SCHEMA_INVALID'
    Require (@($preflightManifest.sources).Count -eq 1) 'PREFLIGHT_MANIFEST_COUNT_INVALID'
    $pfEntry = @($preflightManifest.sources)[0]
    Require ([string]$pfEntry.path -ceq $preflightPath) 'PREFLIGHT_MANIFEST_PATH_INVALID'
    Require ([string]$pfEntry.sha256 -cmatch '^[0-9a-f]{64}$') 'PREFLIGHT_MANIFEST_SHA_INVALID'
    if ([string]$pfEntry.path -ceq $preflightPath -and [string]$pfEntry.sha256 -cmatch '^[0-9a-f]{64}$') {
        Require ((Get-CommittedSha256 $preflightPath) -ceq [string]$pfEntry.sha256) 'PREFLIGHT_SOURCE_SHA_CHANGED'
    }
    $sourceNames = @('Initialize.ps1') + @($modules | ForEach-Object { 'Modules/' + $_.Name }) + $exportAllowed + @($preflightPath)
    foreach ($name in $sourceNames) {
        $file = Join-Path $root $name
        $data = [IO.File]::ReadAllText($file)
        # Detect embedded absolute personal library roots, credential-like literals, and common token prefixes.
        Require (-not ($data -match '(?i)[A-Z]:\\Users\\[^\s\x22\x27]+')) ('PERSONAL_PATH: ' + $name)
        Require (-not ($data -match '(?i)[A-Z]:\\ROM[oô]th[eè]que')) ('LIBRARY_PATH: ' + $name)
        Require (-not ($data -match '(?i)(github_pat_|gh[pousr]_|AKIA)[A-Za-z0-9_]{16,}')) ('TOKEN_PATTERN: ' + $name)
        Require (-not ($data -match '(?i)(password|secret|api[_-]?key)\s*[:=]\s*[\x22\x27][^\x22\x27]{4,}')) ('SUSPECT_SECRET_ASSIGNMENT: ' + $name)
    }
    $parse = @($paths | Where-Object { $_ -match '\.(ps1|psm1)$' } | ForEach-Object { Join-Path $root $_ })
    if ($AdditionalSyntaxPath) { $parse += @($AdditionalSyntaxPath) }
    foreach ($file in $parse) {
        $tokens = $null; $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
        foreach ($e in @($errors)) { Require $false ('PARSE_ERROR: ' + $file + ' : ' + $e.Message) }
    }
    if ($failures.Count -gt 0) { throw ("CI-0 FAIL ({0}):`n{1}" -f $failures.Count,($failures -join "`n")) }
    Write-Host ('CI-0 PASS | tracked=' + $paths.Count + ' | modules=' + $modules.Count + ' | parsed=' + $parse.Count + ' | baseline=19/19 | export=2/2')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
, '^tests/(ci-export-sources|ci-preflight-sources)\.json$'
    )
    $collisions = @{}
    foreach ($path in $paths) {
        $pass = $false
        foreach ($rule in $allowed) { if ($path -cmatch $rule) { $pass = $true; break } }
        Require $pass ('TRACKED_PATH_NOT_ALLOWED: ' + $path)
        $key = $path.Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
        Require (-not $collisions.ContainsKey($key)) ('CASE_UNICODE_COLLISION: ' + $path)
        $collisions[$key] = $true
        $full = Join-Path $root $path
        Require (Test-Path -LiteralPath $full -PathType Leaf) ('TRACKED_FILE_MISSING: ' + $path)
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $file = Get-Item -LiteralPath $full -Force
            Require (-not (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) ('REPARSE_FILE: ' + $path)
            Require ($file.Length -lt 2000000) ('SUSPICIOUS_SIZE: ' + $path)
        }
    }
    $expectedModules = @('Utils','Canonical','Configuration','Inventory','Metadata','RomResolver','DecisionSchema','StateCodec','Identity','PersistentIdentity','MediaMatcher','MediaModel','Reconciliation','Proposals','Projection','Transactions','Catalogue','Associations')
    $modules = @(Get-ChildItem -LiteralPath (Join-Path $root 'Modules') -File -Filter '*.psm1')
    Require ($modules.Count -eq 20) ('EXPECTED_20_MODULES actual=' + $modules.Count)
    $extensionModules = @('SnapshotComparison','LibrarySnapshotProjection')
    foreach ($name in $extensionModules) {
        Require (Test-Path (Join-Path $root ('Modules\' + $name + '.psm1')) -PathType Leaf) ('MISSING_APPROVED_EXTENSION_MODULE: ' + $name)
    }
    $actualModuleNames = @($modules | ForEach-Object { $_.BaseName } | Sort-Object)
    $approvedModuleNames = @(($expectedModules + $extensionModules) | Sort-Object)
    Require (($actualModuleNames -join '|') -ceq ($approvedModuleNames -join '|')) 'UNEXPECTED_EXTENSION_MODULE'
    $init = [IO.File]::ReadAllText((Join-Path $root 'Initialize.ps1'))
    $order = [regex]::Match($init,'(?s)\$moduleOrder\s*=\s*@\((.*?)\)')
    Require $order.Success 'BOOTSTRAP_ORDER_NOT_FOUND'
    if ($order.Success) {
        $names = @([regex]::Matches($order.Groups[1].Value,"'([^']+)'") | ForEach-Object { $_.Groups[1].Value })
        Require (($names -join '|') -ceq ($expectedModules -join '|')) 'BOOTSTRAP_ORDER_CHANGED'
        foreach ($name in $expectedModules) {
            Require (Test-Path (Join-Path $root ('Modules\' + $name + '.psm1')) -PathType Leaf) ('MISSING_BOOTSTRAP_MODULE: ' + $name)
        }
    }
    # These hashes preserve the separately audited engine baseline; the CI files live in a different commit.
    $baseline = @'
178c318fc270c3afc090c8edd644dbb47656e61c19c30b3894ca07780af430c6  Initialize.ps1
f16335429ccb2252210aadb242a051ed626a850d3729e5879c8a390d358780d2  Modules/Associations.psm1
bae923a4585c40b4a4d20d8b1caa9e16bedf54be31a9ea8952f5b523842b2864  Modules/Canonical.psm1
3631c5adad951628cb8312311c9af9d88b4af172dfcf81aaad0b5a3df75076ae  Modules/Catalogue.psm1
2a90ecd75fe137a4f5c02ae542fe132af98079664842c9df9819c54660424424  Modules/Configuration.psm1
0a42e17d5f06a7c8cc3cb64ee3dab6402df805fd4d8e00be95a85105aa25e793  Modules/DecisionSchema.psm1
b9ba591f35f46b0cafa1b62595a21146db9e98415f3a0af2db2426c54c80eb28  Modules/Identity.psm1
3511e9857f186ca39eb10b4b6a0adc728792cf3f59942ac4c4fcd4c6b15edc83  Modules/Inventory.psm1
6be444fe3db82c92d15b4414ce9aab3a39e9870267235fb9e0051d407322a5a4  Modules/MediaMatcher.psm1
b3bcfbe222886f6b78a104b6d17c553c12fbf4ad556e315edd9969672894a423  Modules/MediaModel.psm1
328f73b259c435887ee17d122d2fa83d72bc76c1174dc1fcc41bedbfad8e9b77  Modules/Metadata.psm1
58e1d8203c71bc36ad00d294602e12a4310718a53a572dcfdf45014a576020b2  Modules/PersistentIdentity.psm1
1b915efacc23f2fb3f79fca7b1b18cb2eb3e46bee359376edbf4d0a0fb5b81df  Modules/Projection.psm1
42a6c951fc87f8658e2f3b5113af50b4c79ceb21b02412e051c3da4edb851201  Modules/Proposals.psm1
08ba836d5f70cffc1caf1795176f5e405633116d63ceee6870424bdf80355405  Modules/Reconciliation.psm1
a01c347d20fabfae58b34065adf9a5fa0e172780bca5e92ec98a3528d4cff240  Modules/RomResolver.psm1
fb6a46bc6e9e5786e3800c67e82abe6937343d8f19952b861f053ade6786e1d4  Modules/StateCodec.psm1
827bf312b10cef21059d3921ce5f0d47e4b475e64014a68df03ede47c8b063af  Modules/Transactions.psm1
28318a751b276250d4b3224ba89008261714dfa4f680b859023fb31400a7c827  Modules/Utils.psm1
'@
    foreach ($row in @($baseline -split "`n")) {
        if ($row -notmatch '^([0-9a-f]{64})  ([^\r\n]+)') { continue }
        $expectedHash = $matches[1];$name = $matches[2]
        $target = Join-Path $root ($name.Replace('/', '\'))
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            $actual = Get-CommittedSha256 $name
            Require ($actual -ceq $expectedHash) ('BASELINE_HASH_CHANGED: ' + $name)
        } else { Require $false ('BASELINE_FILE_MISSING: ' + $name) }
    }
    # E1 sources are separately hash-locked through a versioned manifest.
    # Do not alter the historic 19-SHA baseline above or silently admit other Export files.
    $manifestRelative = 'tests/ci-export-sources.json'
    $manifestFile = Join-Path $root 'tests\ci-export-sources.json'
    $exportManifest = Get-Content -LiteralPath $manifestFile -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($exportManifest.schemaVersion -ceq '1.0.0') 'EXPORT_MANIFEST_SCHEMA_INVALID'
    $exportAllowed = @('Export/ExportE1.psm1','Export/Invoke-ExportE1InventoryReadOnly.ps1')
    $declared = @($exportManifest.sources)
    Require ($declared.Count -eq $exportAllowed.Count) ('EXPORT_MANIFEST_COUNT_INVALID: ' + $declared.Count)
    $seenNew=@{}
    foreach ($entry in $declared) {
        $name=[string]$entry.path
        Require ($name -cin $exportAllowed) ('EXPORT_MANIFEST_PATH_NOT_APPROVED: ' + $name)
        Require (-not $seenNew.ContainsKey($name)) ('EXPORT_MANIFEST_DUPLICATE: ' + $name)
        $seenNew[$name]=$true
        Require ($name -cin $paths) ('EXPORT_SOURCE_NOT_TRACKED: ' + $name)
        $expectedHash=[string]$entry.sha256
        Require ($expectedHash -cmatch '^[0-9a-f]{64}$') ('EXPORT_MANIFEST_SHA_INVALID: ' + $name)
        if ($name -cin $paths -and $expectedHash -cmatch '^[0-9a-f]{64}$') {
            Require ((Get-CommittedSha256 $name) -ceq $expectedHash) ('EXPORT_SOURCE_SHA_CHANGED: ' + $name)
        }
    }
    foreach ($name in $exportAllowed) {Require $seenNew.ContainsKey($name) ('EXPORT_SOURCE_UNDECLARED: ' + $name)}
    foreach ($name in @($paths | Where-Object {$_ -match '^Export/.*\.(ps1|psm1)$'})) {Require ($name -cin $exportAllowed) ('EXPORT_SOURCE_NOT_IN_MANIFEST: ' + $name)}
    # This new preflight has an independent SHA lock; the 19 historical and two E1 hashes remain unchanged.
    $preflightPath = 'Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1'
    $preflightManifest = Get-Content -LiteralPath (Join-Path $root 'tests\ci-preflight-sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Require ($preflightManifest.schemaVersion -ceq '1.0.0') 'PREFLIGHT_MANIFEST_SCHEMA_INVALID'
    Require (@($preflightManifest.sources).Count -eq 1) 'PREFLIGHT_MANIFEST_COUNT_INVALID'
    $pfEntry = @($preflightManifest.sources)[0]
    Require ([string]$pfEntry.path -ceq $preflightPath) 'PREFLIGHT_MANIFEST_PATH_INVALID'
    Require ([string]$pfEntry.sha256 -cmatch '^[0-9a-f]{64}$') 'PREFLIGHT_MANIFEST_SHA_INVALID'
    if ([string]$pfEntry.path -ceq $preflightPath -and [string]$pfEntry.sha256 -cmatch '^[0-9a-f]{64}$') {
        Require ((Get-CommittedSha256 $preflightPath) -ceq [string]$pfEntry.sha256) 'PREFLIGHT_SOURCE_SHA_CHANGED'
    }
    $sourceNames = @('Initialize.ps1') + @($modules | ForEach-Object { 'Modules/' + $_.Name }) + $exportAllowed + @($preflightPath)
    foreach ($name in $sourceNames) {
        $file = Join-Path $root $name
        $data = [IO.File]::ReadAllText($file)
        # Detect embedded absolute personal library roots, credential-like literals, and common token prefixes.
        Require (-not ($data -match '(?i)[A-Z]:\\Users\\[^\s\x22\x27]+')) ('PERSONAL_PATH: ' + $name)
        Require (-not ($data -match '(?i)[A-Z]:\\ROM[oô]th[eè]que')) ('LIBRARY_PATH: ' + $name)
        Require (-not ($data -match '(?i)(github_pat_|gh[pousr]_|AKIA)[A-Za-z0-9_]{16,}')) ('TOKEN_PATTERN: ' + $name)
        Require (-not ($data -match '(?i)(password|secret|api[_-]?key)\s*[:=]\s*[\x22\x27][^\x22\x27]{4,}')) ('SUSPECT_SECRET_ASSIGNMENT: ' + $name)
    }
    $parse = @($paths | Where-Object { $_ -match '\.(ps1|psm1)$' } | ForEach-Object { Join-Path $root $_ })
    if ($AdditionalSyntaxPath) { $parse += @($AdditionalSyntaxPath) }
    foreach ($file in $parse) {
        $tokens = $null; $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($file, [ref]$tokens, [ref]$errors)
        foreach ($e in @($errors)) { Require $false ('PARSE_ERROR: ' + $file + ' : ' + $e.Message) }
    }
    if ($failures.Count -gt 0) { throw ("CI-0 FAIL ({0}):`n{1}" -f $failures.Count,($failures -join "`n")) }
    Write-Host ('CI-0 PASS | tracked=' + $paths.Count + ' | modules=' + $modules.Count + ' | parsed=' + $parse.Count + ' | baseline=19/19 | export=2/2')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
