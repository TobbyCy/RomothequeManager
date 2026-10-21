# #8C-B: prove actual execution of disposable Pester suites (NOT code coverage).
# Standalone PS5.1 process. Revalidates signed Pester before import.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-DisposableSuite {
    param(
        [Parameter(Mandatory=$true)][string]$Directory,
        [Parameter(Mandatory=$true)][string]$Name
    )
    if ($Name -cnotin @('Alpha','Beta','Gamma')) { throw 'DYNAMIC_SUITE_NAME_NOT_ALLOWED' }
    $template = @'
Describe '__NAME__' {
    It 'runs __NAME__' {
        [IO.File]::WriteAllText((Join-Path $env:RM_CI_PESTER_MARKER_DIR '__NAME__.marker'), 'executed')
        1 | Should -Be 1
    }
}
'@
    $path = Join-Path $Directory ($Name + '.Tests.ps1')
    [IO.File]::WriteAllText($path, $template.Replace('__NAME__', $Name))
    return $path
}

function Assert-FixtureRun {
    param(
        [Parameter(Mandatory=$true)]$Result,
        [Parameter(Mandatory=$true)][string[]]$Expected,
        [Parameter(Mandatory=$true)][string]$MarkerRoot
    )
    if ($null -eq $Result -or [int]$Result.FailedCount -ne 0 -or
        [int]$Result.PassedCount -ne $Expected.Count -or
        [int]$Result.SkippedCount -ne 0 -or [int]$Result.PendingCount -ne 0) {
        throw ('PESTER_DYNAMIC_COUNTS_MISMATCH: expected=' + $Expected.Count +
            ' passed=' + [string]$Result.PassedCount +
            ' failed=' + [string]$Result.FailedCount)
    }
    $actualMarkers = @(Get-ChildItem -LiteralPath $MarkerRoot -Filter '*.marker' -File)
    if ($actualMarkers.Count -ne $Expected.Count) { throw 'PESTER_DYNAMIC_MARKER_COUNT_MISMATCH' }
    foreach ($name in $Expected) {
        $marker = Join-Path $MarkerRoot ($name + '.marker')
        if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
            [IO.File]::ReadAllText($marker) -cne 'executed') {
            throw ('PESTER_DYNAMIC_EXECUTION_MISSING: ' + $name)
        }
    }
}


function Clear-DisposableMarkers {
    param([Parameter(Mandatory=$true)][string]$MarkerRoot)
    @(Get-ChildItem -LiteralPath $MarkerRoot -Filter '*.marker' -File) |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop }
    if (@(Get-ChildItem -LiteralPath $MarkerRoot -Filter '*.marker' -File).Count -ne 0) {
        throw 'PESTER_DYNAMIC_MARKER_RESET_FAILED'
    }
}

function Assert-ExpectedRejection {
    param(
        [Parameter(Mandatory=$true)]$Result,
        [Parameter(Mandatory=$true)][string[]]$Expected,
        [Parameter(Mandatory=$true)][string]$MarkerRoot
    )
    $rejected = $false
    try {
        Assert-FixtureRun -Result $Result -Expected $Expected -MarkerRoot $MarkerRoot
    } catch {
        if ($_.Exception.Message -like 'PESTER_DYNAMIC_COUNTS_MISMATCH:*' -or
            $_.Exception.Message -like 'PESTER_DYNAMIC_EXECUTION_MISSING:*' -or
            $_.Exception.Message -eq 'PESTER_DYNAMIC_MARKER_COUNT_MISMATCH') {
            $rejected = $true
        } else { throw }
    }
    if (-not $rejected) { throw 'PESTER_DYNAMIC_NEGATIVE_NOT_REJECTED' }
}

$exitCode = 0
$workspace = $null
$previousMarkerDir = [Environment]::GetEnvironmentVariable('RM_CI_PESTER_MARKER_DIR')
$previousLocal = $env:LOCALAPPDATA
$previousRoaming = $env:APPDATA
try {
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {
        throw 'PESTER_DYNAMIC_REQUIRES_PS51'
    }
    $version = '4.10.1'
    $expectedSigner = '7B9157664392D633EDA2C0248605C1C868EBDE43'
    $found = @(Get-Module -ListAvailable -Name Pester |
        Where-Object { $_.Version.ToString() -eq $version })
    if ($found.Count -ne 1) { throw ('PESTER_DYNAMIC_PIN_NOT_FOUND: ' + $found.Count) }
    $moduleRoot = $found[0].ModuleBase
    $manifestPath = Join-Path $moduleRoot 'Pester.psd1'
    foreach ($file in @($manifestPath, (Join-Path $moduleRoot 'Pester.psm1'))) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw 'PESTER_DYNAMIC_SIGNED_FILE_MISSING'
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $file -ErrorAction Stop
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne $expectedSigner) {
            throw ('PESTER_DYNAMIC_SIGNATURE_REJECTED: ' + [IO.Path]::GetFileName($file))
        }
    }
    $loaded = Import-Module -Name $manifestPath -Force -PassThru -ErrorAction Stop
    if ($null -eq $loaded -or $loaded.Version.ToString() -ne $version -or
        [string]$loaded.ModuleBase -ine [string]$moduleRoot) {
        throw 'PESTER_DYNAMIC_MODULE_IDENTITY_INVALID'
    }

    $workspace = Join-Path ([IO.Path]::GetTempPath()) ('rm-ci-pester-exec-' + [guid]::NewGuid().ToString('N'))
    $suites = Join-Path $workspace 'suites'
    $markers = Join-Path $workspace 'markers'
    $env:LOCALAPPDATA = Join-Path $workspace 'LocalAppData'
    $env:APPDATA = Join-Path $workspace 'AppData'
    $env:RM_CI_PESTER_MARKER_DIR = $markers
    foreach ($dir in @($suites,$markers,$env:LOCALAPPDATA,$env:APPDATA)) {
        [IO.Directory]::CreateDirectory($dir) | Out-Null
    }
    foreach ($name in @('Alpha','Beta','Gamma')) {
        $null = New-DisposableSuite -Directory $suites -Name $name
    }
    # This is the positive dynamic inclusion proof: enumerate actual temporary
    # suites; no static allowlist or hard-coded list is passed to Pester.
    $selected = @(Get-ChildItem -LiteralPath $suites -Filter '*.Tests.ps1' -File |
        Sort-Object Name | ForEach-Object { $_.FullName })
    if ($selected.Count -ne 3) { throw 'PESTER_DYNAMIC_DISCOVERY_WRONG_COUNT' }
    $result = Invoke-Pester -Script $selected -PassThru -Show None -ErrorAction Stop
    Assert-FixtureRun -Result $result -Expected @('Alpha','Beta','Gamma') -MarkerRoot $markers
    Write-Host 'PESTER_DYNAMIC_POSITIVE_PASS | discovered=3 | executed=3 | passed=3 | failed=0'

    # N1: a third suite exists on disk but is OMITTED from the executed list.
    Clear-DisposableMarkers -MarkerRoot $markers
    $alpha = Join-Path $suites 'Alpha.Tests.ps1'
    $beta = Join-Path $suites 'Beta.Tests.ps1'
    $omitted = Invoke-Pester -Script @($alpha, $beta) -PassThru -Show None -ErrorAction Stop
    if ([int]$omitted.PassedCount -ne 2 -or [int]$omitted.FailedCount -ne 0 -or
        (Test-Path -LiteralPath (Join-Path $markers 'Gamma.marker'))) {
        throw 'PESTER_DYNAMIC_OMISSION_SCENARIO_INVALID'
    }
    Assert-ExpectedRejection -Result $omitted -Expected @('Alpha','Beta','Gamma') -MarkerRoot $markers
    Write-Host 'PESTER_DYNAMIC_OMISSION_PASS | expected=3 | executed=2 | rejected=true'

    # N2: Ghost exists and is mentioned in both a comment and a string;
    # neither is a Pester invocation. No Ghost.It marker may appear.
    Clear-DisposableMarkers -MarkerRoot $markers
    $ghost = @'
Describe 'Ghost' {
    It 'runs Ghost' {
        [IO.File]::WriteAllText((Join-Path $env:RM_CI_PESTER_MARKER_DIR 'Ghost.marker'), 'executed')
        1 | Should -Be 1
    }
}
'@
    [IO.File]::WriteAllText((Join-Path $suites 'Ghost.Tests.ps1'), $ghost)
    [IO.File]::AppendAllText($alpha, ([Environment]::NewLine +
        '# Ghost.Tests.ps1 is listed but intentionally not executed' +
        [Environment]::NewLine + '$ignoredSuite = "Ghost.Tests.ps1"'))
    $mentioned = Invoke-Pester -Script @($alpha, $beta) -PassThru -Show None -ErrorAction Stop
    if ([int]$mentioned.PassedCount -ne 2 -or [int]$mentioned.FailedCount -ne 0 -or
        (Test-Path -LiteralPath (Join-Path $markers 'Ghost.marker'))) {
        throw 'PESTER_DYNAMIC_COMMENT_SCENARIO_INVALID'
    }
    Assert-ExpectedRejection -Result $mentioned -Expected @('Alpha','Beta','Ghost') -MarkerRoot $markers
    Write-Host 'PESTER_DYNAMIC_COMMENT_PASS | ghostMentioned=true | ghostExecuted=false | rejected=true'

    # N3: a skipped It exists, but its body never writes an execution marker.
    Clear-DisposableMarkers -MarkerRoot $markers
    $skipped = @'
Describe 'Skipped' {
    It 'is skipped' -Skip {
        [IO.File]::WriteAllText((Join-Path $env:RM_CI_PESTER_MARKER_DIR 'Skipped.marker'), 'executed')
        1 | Should -Be 1
    }
}
'@
    $skippedPath = Join-Path $suites 'Skipped.Tests.ps1'
    [IO.File]::WriteAllText($skippedPath, $skipped)
    $skipResult = Invoke-Pester -Script @($skippedPath) -PassThru -Show None -ErrorAction Stop
    $notExecuted = [int]$skipResult.SkippedCount + [int]$skipResult.PendingCount
    if ([int]$skipResult.PassedCount -ne 0 -or [int]$skipResult.FailedCount -ne 0 -or
        $notExecuted -lt 1 -or
        (Test-Path -LiteralPath (Join-Path $markers 'Skipped.marker'))) {
        throw ('PESTER_DYNAMIC_SKIP_SCENARIO_INVALID: skippedOrPending=' + $notExecuted)
    }
    Assert-ExpectedRejection -Result $skipResult -Expected @('Skipped') -MarkerRoot $markers
    Write-Host ('PESTER_DYNAMIC_SKIP_PASS | passed=0 | skippedOrPending=' + $notExecuted + ' | rejected=true')

    # N4: a genuinely failing Pester assertion MUST appear as FailedCount > 0.
    Clear-DisposableMarkers -MarkerRoot $markers
    $failedSuite = @'
Describe 'Failure' {
    It 'must fail' {
        throw 'RM_CI_SYNTHETIC_EXPECTED_FAILURE'
    }
}
'@
    $failedPath = Join-Path $suites 'Failure.Tests.ps1'
    [IO.File]::WriteAllText($failedPath, $failedSuite)
    $failed = Invoke-Pester -Script @($failedPath) -PassThru -Show None -ErrorAction Stop
    if ([int]$failed.FailedCount -lt 1 -or [int]$failed.PassedCount -ne 0) {
        throw 'PESTER_DYNAMIC_FAILURE_NOT_PROPAGATED'
    }
    Assert-ExpectedRejection -Result $failed -Expected @('Failure') -MarkerRoot $markers
    Write-Host ('PESTER_DYNAMIC_FAILURE_PASS | injectedFailures=' + $failed.FailedCount + ' | rejected=true')

    Write-Host 'PESTER_DYNAMIC_CANARIES_ALL_PASS | omission=PASS | comment=PASS | skipped=PASS | failure=PASS'

} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    $exitCode = 1
} finally {
    $env:LOCALAPPDATA = $previousLocal
    $env:APPDATA = $previousRoaming
    [Environment]::SetEnvironmentVariable('RM_CI_PESTER_MARKER_DIR', $previousMarkerDir)
    if ($null -ne $workspace -and (Test-Path -LiteralPath $workspace)) {
        try {
            Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction Stop
            if (Test-Path -LiteralPath $workspace) { throw 'PESTER_DYNAMIC_CLEANUP_FAILED' }
            Write-Host 'PESTER_DYNAMIC_CLEANUP_PASS'
        } catch {
            [Console]::Error.WriteLine($_.Exception.ToString())
            $exitCode = 1
        }
    }
}
exit $exitCode
