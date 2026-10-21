# #8C-C report validation. No business code is executed by these helpers.
Set-StrictMode -Version 2.0

function Get-RmCoverageKey {
    param($Point)
    return ('{0}|{1}|{2}' -f ([IO.Path]::GetFullPath([string]$Point.File).ToLowerInvariant()), $Point.StartLine, $Point.StartColumn)
}

function Get-RmCoverageExpectedPoints {
    param([string[]]$Paths, $PesterModule)
    if (@($Paths).Count -eq 0) { throw 'COVERAGE_SCOPE_EMPTY' }
    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'COVERAGE_TARGET_MISSING' }
        $tokens = $null; $parseErrors = $null
        $null = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
        if (@($parseErrors).Count -ne 0) { throw 'COVERAGE_TARGET_PARSE_INVALID' }
    }
    # Use this exact Pester release's eligibility predicate, separately from
    # its breakpoint creation. This makes omitted instrumentation detectable.
    $points = @(& $PesterModule {
        param($Files)
        foreach ($file in $Files) {
            foreach ($command in Get-CommandsInFile -Path $file) {
                if (-not (IsIgnoredCommand -Command $command)) {
                    [pscustomobject]@{
                        File = $command.Extent.File
                        StartLine = $command.Extent.StartLineNumber
                        StartColumn = $command.Extent.StartColumnNumber
                    }
                }
            }
        }
    } $Paths)
    if ($points.Count -eq 0) { throw 'COVERAGE_SCOPE_EMPTY' }
    return $points
}

function Assert-RmCoverageResult {
    param($Result, [object[]]$ExpectedPoints, [int]$ExpectedPassed)
    if ($null -eq $Result) { throw 'COVERAGE_RESULT_NULL' }
    foreach ($name in @('TotalCount','PassedCount','FailedCount','SkippedCount','PendingCount','InconclusiveCount')) {
        if ($null -eq $Result.PSObject.Properties[$name] -or $Result.$name -isnot [int]) {
            throw 'COVERAGE_TEST_COUNTS_INVALID'
        }
    }
    if ($Result.TotalCount -ne $ExpectedPassed -or $Result.PassedCount -ne $ExpectedPassed -or
        $Result.FailedCount -ne 0 -or $Result.SkippedCount -ne 0 -or
        $Result.PendingCount -ne 0 -or $Result.InconclusiveCount -ne 0) {
        throw 'COVERAGE_TEST_RUN_REJECTED'
    }
    if ($null -eq $Result.PSObject.Properties['CodeCoverage'] -or $null -eq $Result.CodeCoverage) {
        throw 'COVERAGE_REPORT_NULL'
    }
    $report = $Result.CodeCoverage
    foreach ($name in @('NumberOfCommandsAnalyzed','NumberOfFilesAnalyzed','NumberOfCommandsExecuted','NumberOfCommandsMissed')) {
        if ($null -eq $report.PSObject.Properties[$name] -or $report.$name -isnot [int] -or $report.$name -lt 0) {
            throw 'COVERAGE_COUNTS_INVALID'
        }
    }
    foreach ($name in @('HitCommands','MissedCommands','AnalyzedFiles')) {
        if ($null -eq $report.PSObject.Properties[$name]) { throw 'COVERAGE_COLLECTION_MISSING' }
    }
    $hit = @($report.HitCommands); $missed = @($report.MissedCommands)
    if (@($ExpectedPoints).Count -eq 0 -or $report.NumberOfCommandsAnalyzed -eq 0 -or
        $report.NumberOfFilesAnalyzed -eq 0) { throw 'COVERAGE_SCOPE_EMPTY' }
    if ($report.NumberOfCommandsExecuted -ne $hit.Count -or
        $report.NumberOfCommandsMissed -ne $missed.Count -or
        $report.NumberOfCommandsAnalyzed -ne ($hit.Count + $missed.Count)) {
        throw 'COVERAGE_COUNTS_INCONSISTENT'
    }
    $expected = @{}
    foreach ($point in $ExpectedPoints) {
        $key = Get-RmCoverageKey $point
        if ($expected.ContainsKey($key)) { throw 'COVERAGE_EXPECTED_DUPLICATE' }
        $expected[$key] = $true
    }
    $seen = @{}
    foreach ($point in @($hit) + @($missed)) {
        foreach ($name in @('File','Function','StartLine','StartColumn','HitCount')) {
            if ($null -eq $point.PSObject.Properties[$name]) { throw 'COVERAGE_POINT_INVALID' }
        }
        if ($point.HitCount -isnot [int] -or $point.HitCount -lt 0 -or
            $point.StartLine -isnot [int] -or $point.StartLine -lt 1 -or
            $point.StartColumn -isnot [int] -or $point.StartColumn -lt 1) { throw 'COVERAGE_POINT_INVALID' }
        $key = Get-RmCoverageKey $point
        if ($seen.ContainsKey($key) -or -not $expected.ContainsKey($key)) { throw 'COVERAGE_POINT_SET_MISMATCH' }
        $seen[$key] = $true
    }
    if ($seen.Count -ne $expected.Count) { throw 'COVERAGE_POINT_SET_MISMATCH' }
    if (@($hit | Where-Object { $_.HitCount -le 0 }).Count -ne 0 -or
        @($missed | Where-Object { $_.HitCount -ne 0 }).Count -ne 0) { throw 'COVERAGE_HIT_CLASSIFICATION_INVALID' }
    $expectedFiles = @($ExpectedPoints | ForEach-Object { [IO.Path]::GetFullPath($_.File).ToLowerInvariant() } | Sort-Object -Unique)
    $actualFiles = @($report.AnalyzedFiles | ForEach-Object { [IO.Path]::GetFullPath($_).ToLowerInvariant() } | Sort-Object -Unique)
    if ($report.NumberOfFilesAnalyzed -ne $actualFiles.Count -or
        ($expectedFiles -join '|') -cne ($actualFiles -join '|')) { throw 'COVERAGE_FILE_SET_MISMATCH' }
    return $report
}

function Assert-RmCoverageRejection {
    param([scriptblock]$Action, [string]$Diagnostic, [string]$Name)
    $rejected = $false
    try { $null = & $Action } catch {
        if ($_.Exception.Message -notlike ('*' + $Diagnostic + '*')) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw ('COVERAGE_NEGATIVE_ACCEPTED: ' + $Name) }
    Write-Host ('COVERAGE_NEGATIVE_PASS | name=' + $Name + ' | diagnostic=' + $Diagnostic)
}

function Invoke-RmBusinessCoverage {
    param([string]$Root, $PesterModule)
    $targets = @(Get-ChildItem -LiteralPath (Join-Path $Root 'Modules') -Filter '*.psm1' -File |
        Sort-Object Name | ForEach-Object { $_.FullName })
    if ($targets.Count -ne 18) { throw 'COVERAGE_MODULE_SCOPE_CHANGED' }
    $targets += Join-Path $Root 'Export\ExportE1.psm1'
    $suites = @((Join-Path $Root 'tests\Unit\Engine.Tests.ps1'), (Join-Path $Root 'tests\Unit\ExportE1.Tests.ps1'))
    $protected = @($targets) + @($suites) + @((Join-Path $Root 'Initialize.ps1'))
    $before = @{}
    foreach ($path in $protected) { $before[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    $expected = @(Get-RmCoverageExpectedPoints $targets $PesterModule)
    $result = Invoke-Pester -Script $suites -CodeCoverage $targets -PassThru -Show None -ErrorAction Stop
    $report = Assert-RmCoverageResult $result $expected 42
    foreach ($path in $protected) {
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $before[$path]) { throw 'COVERAGE_SOURCE_CHANGED' }
    }
    foreach ($local in @($env:LOCALAPPDATA,$env:APPDATA)) {
        if (Test-Path -LiteralPath (Join-Path $local 'RomothequeManager')) { throw 'COVERAGE_STATE_WRITE_DETECTED' }
    }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $fileRows = @()
    $functionRows = @()
    $pointRows = @()
    foreach ($path in $targets) {
        $relative = $path.Substring($prefix.Length).Replace('\','/')
        $points = @(@($report.HitCommands) + @($report.MissedCommands) | Where-Object { $_.File -ieq $path })
        $hit = @($points | Where-Object { $_.HitCount -gt 0 }).Count
        $fileRows += [pscustomobject]@{file=$relative; hit=$hit; analyzed=$points.Count; missed=($points.Count-$hit); sha256=$before[$path]}
        $tokens = $null; $parseErrors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$parseErrors)
        if (@($parseErrors).Count -ne 0) { throw 'COVERAGE_TARGET_PARSE_INVALID' }
        $definitions = @($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$true))
        $groups = @{}
        $groups['<module>|0|0'] = [pscustomobject]@{file=$relative; function='<module>'; definitionLine=0; definitionColumn=0; hit=0; analyzed=0; missed=0; status='NOT_MEASURED'}
        foreach ($definition in $definitions) {
            $key = '{0}|{1}|{2}' -f $definition.Name,$definition.Extent.StartLineNumber,$definition.Extent.StartColumnNumber
            $groups[$key] = [pscustomobject]@{file=$relative; function=$definition.Name; definitionLine=$definition.Extent.StartLineNumber; definitionColumn=$definition.Extent.StartColumnNumber; hit=0; analyzed=0; missed=0; status='NOT_MEASURED'}
        }
        foreach ($point in $points) {
            $owners = @($definitions | Where-Object {
                ($_.Extent.StartLineNumber -lt $point.StartLine -or ($_.Extent.StartLineNumber -eq $point.StartLine -and $_.Extent.StartColumnNumber -le $point.StartColumn)) -and
                ($_.Extent.EndLineNumber -gt $point.StartLine -or ($_.Extent.EndLineNumber -eq $point.StartLine -and $_.Extent.EndColumnNumber -gt $point.StartColumn))
            } | Sort-Object { $_.Extent.StartOffset } -Descending)
            $ownerKey = '<module>|0|0'
            if ($owners.Count -gt 0) {
                $owner = $owners[0]
                $ownerKey = '{0}|{1}|{2}' -f $owner.Name,$owner.Extent.StartLineNumber,$owner.Extent.StartColumnNumber
            }
            $row = $groups[$ownerKey]
            $row.analyzed++
            if ($point.HitCount -gt 0) { $row.hit++ } else { $row.missed++ }
            $row.status = 'MEASURED'
            $pointRows += [pscustomobject]@{file=$relative; function=$row.function; definitionLine=$row.definitionLine; line=$point.StartLine; column=$point.StartColumn; endLine=$point.EndLine; endColumn=$point.EndColumn; hit=($point.HitCount -gt 0)}
        }
        $functionRows += @($groups.Values | Sort-Object definitionLine,definitionColumn)
    }
    $checkout = (& git -C $Root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $checkout -notmatch '^[0-9a-f]{40}$') { throw 'COVERAGE_GIT_HEAD_INVALID' }
    $tree = (& git -C $Root rev-parse 'HEAD^{tree}').Trim()
    if ($LASTEXITCODE -ne 0 -or $tree -notmatch '^[0-9a-f]{40}$') { throw 'COVERAGE_GIT_TREE_INVALID' }
    $source = $checkout; $base = $null
    if ($env:GITHUB_EVENT_NAME -eq 'pull_request') {
        $event = Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
        $source = $event.pull_request.head.sha
        $base = $event.pull_request.base.sha
    }
    return [pscustomobject]@{
        schema='rm-pester-command-coverage-v1'; status='MEASURED'
        metric='Pester 4.10.1 eligible PowerShell command breakpoint locations'
        sourceSha=$source; baseSha=$base; checkoutSha=$checkout; checkoutTree=$tree
        pester='4.10.1'; powershell=$PSVersionTable.PSVersion.ToString()
        tests=[pscustomobject]@{passed=$result.PassedCount; failed=$result.FailedCount; skipped=$result.SkippedCount; pending=$result.PendingCount; inconclusive=$result.InconclusiveCount}
        hit=$report.NumberOfCommandsExecuted; analyzed=$report.NumberOfCommandsAnalyzed; missed=$report.NumberOfCommandsMissed
        files=$fileRows; functions=$functionRows; points=@($pointRows | Sort-Object file,line,column)
        exclusions=@('Initialize.ps1','tests/**','Export/*.ps1','Preflight/**','other scripts and V2','embedded C# bodies','cmdlet/.NET implementation internals')
    }
}

function Assert-RmCoverageReportDestination {
    param([string]$Path, [string]$TempRoot)
    $full = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Path]::GetDirectoryName($full)
    if (-not $full.StartsWith($TempRoot,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($parent) -notmatch '^rm-ci-coverage-[a-f0-9]{32}$' -or
        [IO.Path]::GetFileName($full) -notmatch '^business-[12]\.json$' -or
        -not (Test-Path -LiteralPath (Join-Path $parent '.coverage-parent') -PathType Leaf)) {
        throw 'COVERAGE_REPORT_DESTINATION_REJECTED'
    }
    return $full
}
