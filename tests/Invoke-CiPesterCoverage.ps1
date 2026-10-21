# #8C-C disposable coverage experiment, standalone Windows PowerShell 5.1.
param([switch]$CanaryOnly, [switch]$BusinessWorker, [string]$ReportPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
. (Join-Path $PSScriptRoot 'Invoke-CiPesterCoverageSupport.ps1')
$workspace = $null
$exitCode = 0
$previousLocal = $env:LOCALAPPDATA
$previousRoaming = $env:APPDATA
$initialBreakpoints = @(Get-PSBreakpoint | Select-Object -ExpandProperty Id)
try {
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) { throw 'COVERAGE_PS51_REQUIRED' }
    $found = @(Get-Module -ListAvailable Pester | Where-Object { $_.Version.ToString() -eq '4.10.1' })
    if ($found.Count -ne 1) { throw 'COVERAGE_PESTER_CANDIDATE_COUNT' }
    $manifest = Join-Path $found[0].ModuleBase 'Pester.psd1'
    $signedHashes = @{}
    foreach ($file in @($manifest, (Join-Path $found[0].ModuleBase 'Pester.psm1'))) {
        $signedHashes[$file] = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        $signature = Get-AuthenticodeSignature -LiteralPath $file -ErrorAction Stop
        if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne '7B9157664392D633EDA2C0248605C1C868EBDE43') {
            throw 'COVERAGE_PESTER_SIGNATURE_REJECTED'
        }
    }
    $pesterModule = Import-Module $manifest -Force -PassThru -ErrorAction Stop
    if ($pesterModule.Version.ToString() -ne '4.10.1' -or $pesterModule.ModuleBase -ine $found[0].ModuleBase) {
        throw 'COVERAGE_PESTER_IDENTITY_INVALID'
    }
    Write-Host 'COVERAGE_SIGNATURE_PASS | version=4.10.1 | signedFiles=2'
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $workspace = Join-Path $tempRoot ('rm-ci-coverage-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($workspace) | Out-Null
    $env:LOCALAPPDATA = Join-Path $workspace 'LocalAppData'
    $env:APPDATA = Join-Path $workspace 'AppData'
    [IO.Directory]::CreateDirectory($env:LOCALAPPDATA) | Out-Null
    [IO.Directory]::CreateDirectory($env:APPDATA) | Out-Null
    if (-not $BusinessWorker) {
    $fixture = Join-Path $workspace 'CoverageFixture.psm1'
    $suite = Join-Path $workspace 'CoverageFixture.Tests.ps1'
    $utf8 = New-Object Text.UTF8Encoding($true)
    [IO.File]::WriteAllText($fixture, @'
function Get-CoverageFixture {
    param([bool]$TakeLeft)
    if ($TakeLeft) {
        Write-Output 'reached'
    } else {
        Write-Output 'missed'
    }
}
function Get-CoverageNever {
    Write-Output 'never'
}
function Get-CoverageInline { param([bool]$Left) if ($Left) { Write-Output 'inline-reached' } else { Write-Output 'inline-missed' } }
Export-ModuleMember -Function Get-CoverageFixture,Get-CoverageNever,Get-CoverageInline
'@, $utf8)
    [IO.File]::WriteAllText($suite, @'
Import-Module (Join-Path $PSScriptRoot 'CoverageFixture.psm1') -Force
Describe 'Coverage disposable module' {
    It 'takes only the left arm' {
        Get-CoverageFixture -TakeLeft $true | Should -Be 'reached'
    }
    It 'distinguishes same-line commands by column' {
        Get-CoverageInline -Left $true | Should -Be 'inline-reached'
    }
}
'@, $utf8)
    $expected = @(Get-RmCoverageExpectedPoints -Paths @($fixture) -PesterModule $pesterModule)
    $positive = Invoke-Pester -Script $suite -CodeCoverage $fixture -PassThru -Show None -ErrorAction Stop
    $report = Assert-RmCoverageResult $positive $expected 2
    if (@($report.HitCommands | Where-Object { $_.StartLine -eq 4 -and $_.Function -eq 'Get-CoverageFixture' }).Count -ne 1 -or
        @($report.MissedCommands | Where-Object { $_.StartLine -eq 6 -and $_.Function -eq 'Get-CoverageFixture' }).Count -ne 1 -or
        @($report.MissedCommands | Where-Object { $_.StartLine -eq 10 -and $_.Function -eq 'Get-CoverageNever' }).Count -ne 1) {
        throw 'COVERAGE_CANARY_CLASSIFICATION_FAILED'
    }
    if (@($report.HitCommands | Where-Object { $_.Command -ceq "Write-Output 'inline-reached'" }).Count -ne 1 -or
        @($report.MissedCommands | Where-Object { $_.Command -ceq "Write-Output 'inline-missed'" }).Count -ne 1) { throw 'COVERAGE_SAME_LINE_CLASSIFICATION_FAILED' }
    Write-Host 'COVERAGE_SAME_LINE_PASS | reached=true | missed=false | columnBreakpoints=true'
    Write-Host ('COVERAGE_POSITIVE_PASS | hit=' + $report.NumberOfCommandsExecuted +
        ' | analyzed=' + $report.NumberOfCommandsAnalyzed + ' | missed=' + $report.NumberOfCommandsMissed +
        ' | reachedLine=4 | missedLine=6 | neverLine=10')
    Assert-RmCoverageRejection { Assert-RmCoverageResult $null $expected 2 } 'COVERAGE_RESULT_NULL' 'null-result'
    $clone = $positive | Select-Object TotalCount,PassedCount,FailedCount,SkippedCount,PendingCount,InconclusiveCount,CodeCoverage | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $clone.CodeCoverage.NumberOfCommandsAnalyzed++
    Assert-RmCoverageRejection { Assert-RmCoverageResult $clone $expected 2 } 'COVERAGE_COUNTS_INCONSISTENT' 'inconsistent-counts'
    $missing = Join-Path $workspace 'absent.psm1'
    Assert-RmCoverageRejection { Get-RmCoverageExpectedPoints @($missing) $pesterModule } 'COVERAGE_TARGET_MISSING' 'missing-target'
    $broken = Join-Path $workspace 'broken.psm1'
    [IO.File]::WriteAllText($broken, 'function Broken {', $utf8)
    Assert-RmCoverageRejection { Get-RmCoverageExpectedPoints @($broken) $pesterModule } 'COVERAGE_TARGET_PARSE_INVALID' 'invalid-ast'
    $emptyScope = Invoke-Pester -Script $suite -CodeCoverage @{Path=$fixture;Function='FunctionThatDoesNotExist'} -PassThru -Show None -ErrorAction Stop
    Assert-RmCoverageRejection { Assert-RmCoverageResult $emptyScope $expected 2 } 'COVERAGE_SCOPE_EMPTY' 'absent-function-scope'
    $clone = $positive | Select-Object TotalCount,PassedCount,FailedCount,SkippedCount,PendingCount,InconclusiveCount,CodeCoverage | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $clone.CodeCoverage.HitCommands[0].HitCount = 0
    Assert-RmCoverageRejection { Assert-RmCoverageResult $clone $expected 2 } 'COVERAGE_HIT_CLASSIFICATION_INVALID' 'invalid-hit'
    # A nonempty report for only one of two requested files must also fail.
    $extra = Join-Path $workspace 'OmittedTarget.psm1'
    [IO.File]::WriteAllText($extra, "function Get-OmittedTarget { Write-Output 'never' }", $utf8)
    $expanded = @(Get-RmCoverageExpectedPoints -Paths @($fixture,$extra) -PesterModule $pesterModule)
    $narrowed = Invoke-Pester -Script $suite -CodeCoverage $fixture -PassThru -Show None -ErrorAction Stop
    Assert-RmCoverageRejection { Assert-RmCoverageResult $narrowed $expanded 2 } 'COVERAGE_POINT_SET_MISMATCH' 'omitted-target'
    # Inject a real breakpoint-creation failure in Pester's in-memory lookup;
    # restore it in finally. No signed package file is modified.
    $injectionRejected = & $pesterModule {
        param($SuitePath, $TargetPath)
        $saved = $SafeCommands['Set-PSBreakpoint']
        try {
            $SafeCommands['Set-PSBreakpoint'] = { throw 'COVERAGE_INJECTED_BREAKPOINT_FAILURE' }
            $caught = $false
            try { $null = Invoke-Pester -Script $SuitePath -CodeCoverage $TargetPath -PassThru -Show None -ErrorAction Stop }
            catch {
                if ($_.Exception.Message -notlike '*COVERAGE_INJECTED_BREAKPOINT_FAILURE*') { throw }
                $caught = $true
            }
            return $caught
        } finally { $SafeCommands['Set-PSBreakpoint'] = $saved }
    } $suite $fixture
    if (-not $injectionRejected) { throw 'COVERAGE_INSTRUMENTATION_FAILURE_ACCEPTED' }
    Write-Host 'COVERAGE_NEGATIVE_PASS | name=breakpoint-failure | diagnostic=COVERAGE_INJECTED_BREAKPOINT_FAILURE'
    # Prove lookup restoration by performing another real instrumented run.
    $repeat = Invoke-Pester -Script $suite -CodeCoverage $fixture -PassThru -Show None -ErrorAction Stop
    $null = Assert-RmCoverageResult $repeat $expected 2
    Write-Host 'COVERAGE_CANARIES_ALL_PASS | positives=2 | fixtureTestsPerRun=2 | negatives=8'
    }

    if ($BusinessWorker) {
        $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
        $destination = Assert-RmCoverageReportDestination $ReportPath $tempRoot
        $business = Invoke-RmBusinessCoverage $root $pesterModule
        [IO.File]::WriteAllText($destination, ($business | ConvertTo-Json -Depth 12 -Compress), (New-Object Text.UTF8Encoding($false)))
        Write-Host ('COVERAGE_WORKER_PASS | passed=42 | hit=' + $business.hit + ' | analyzed=' + $business.analyzed + ' | missed=' + $business.missed)
    } elseif (-not $CanaryOnly) {
        [IO.File]::WriteAllText((Join-Path $workspace '.coverage-parent'),'synthetic')
        $paths = @()
        foreach ($attempt in 1..2) {
            $destination = Join-Path $workspace ('business-' + $attempt + '.json')
            & (Join-Path $PSHOME 'powershell.exe') -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -BusinessWorker -ReportPath $destination
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $destination -PathType Leaf)) { throw 'COVERAGE_WORKER_FAILED' }
            $paths += $destination
        }
        $first = Get-Content -LiteralPath $paths[0] -Raw -Encoding UTF8 | ConvertFrom-Json
        $second = Get-Content -LiteralPath $paths[1] -Raw -Encoding UTF8 | ConvertFrom-Json
        # Compare every classified location and source hash, not only a percentage.
        foreach ($property in @('sourceSha','baseSha','checkoutSha','checkoutTree','pester','powershell','hit','analyzed','missed','tests','files','functions','points','exclusions')) {
            $left = ConvertTo-Json -InputObject $first.$property -Depth 12 -Compress
            $right = ConvertTo-Json -InputObject $second.$property -Depth 12 -Compress
            if ($left -cne $right) { throw ('COVERAGE_REPEAT_UNSTABLE: ' + $property) }
        }
        if ($first.status -cne 'MEASURED' -or $first.analyzed -le 0 -or @($first.files).Count -ne 19) { throw 'COVERAGE_REPEAT_INVALID' }
        Write-Host 'COVERAGE_REPRODUCIBLE_PASS | freshProcesses=2 | classifiedPointSets=IDENTICAL | sourceHashes=UNCHANGED'
        Write-Host ('COVERAGE_METADATA ' + (ConvertTo-Json -InputObject ($first | Select-Object schema,status,metric,sourceSha,baseSha,checkoutSha,checkoutTree,pester,powershell,tests,hit,analyzed,missed,exclusions) -Depth 8 -Compress))
        foreach ($row in $first.files) { Write-Host ('COVERAGE_FILE ' + ($row | ConvertTo-Json -Compress)) }
        foreach ($row in $first.functions) { Write-Host ('COVERAGE_FUNCTION ' + ($row | ConvertTo-Json -Compress)) }
        # Durable compact report in CI logs; coordinates resolve against exact
        # source SHA. No source text, private fixtures or absolute paths emitted.
        foreach ($row in $first.points) { Write-Host ('COVERAGE_POINT ' + ($row | ConvertTo-Json -Compress)) }
        if ($env:GITHUB_STEP_SUMMARY) {
            $summary = @('## Pester command coverage (synthetic)', '',
                ('Source: ' + $first.sourceSha), ('Checkout: ' + $first.checkoutSha),
                ('42 PASS / 0 FAIL / 0 SKIP; commands hit/analyzed/missed: ' + $first.hit + '/' + $first.analyzed + '/' + $first.missed),
                'Two fresh PS5.1 processes produced identical classified point sets. C# bodies, branches and assertion quality are not measured.',
                '', '| File | Hit | Analyzed | Missed |', '| --- | ---: | ---: | ---: |')
            foreach ($file in $first.files) { $summary += ('| ' + $file.file + ' | ' + $file.hit + ' | ' + $file.analyzed + ' | ' + $file.missed + ' |') }
            [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, (($summary -join [Environment]::NewLine) + [Environment]::NewLine))
        }
        Write-Host ('COVERAGE_MEASUREMENT_PASS | businessPassed=42 | hit=' + $first.hit + ' | analyzed=' + $first.analyzed + ' | missed=' + $first.missed + ' | files=19 | threshold=NONE')
    }

    foreach ($file in $signedHashes.Keys) {
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -cne $signedHashes[$file]) { throw 'COVERAGE_SIGNED_PACKAGE_CHANGED' }
    }
    Write-Host 'COVERAGE_SIGNED_FILES_UNCHANGED'
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    $exitCode = 1
} finally {
    $env:LOCALAPPDATA = $previousLocal
    $env:APPDATA = $previousRoaming
    Remove-Module CoverageFixture -Force -ErrorAction SilentlyContinue
    $leaked = @(Get-PSBreakpoint | Where-Object { $_.Id -notin $initialBreakpoints })
    if ($leaked.Count -gt 0) {
        $leaked | Remove-PSBreakpoint
        [Console]::Error.WriteLine('COVERAGE_BREAKPOINT_LEAK')
        $exitCode = 1
    }
    if ($null -ne $workspace -and (Test-Path -LiteralPath $workspace)) {
        try {
            $resolved = [IO.Path]::GetFullPath($workspace)
            if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
                [IO.Path]::GetFileName($resolved) -notmatch '^rm-ci-coverage-[a-f0-9]{32}$') {
                throw 'COVERAGE_CLEANUP_PATH_REJECTED'
            }
            Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop
            if (Test-Path -LiteralPath $resolved) { throw 'COVERAGE_CLEANUP_FAILED' }
            Write-Host 'COVERAGE_CLEANUP_PASS'
        } catch { [Console]::Error.WriteLine($_.Exception.ToString()); $exitCode = 1 }
    }
}
exit $exitCode
