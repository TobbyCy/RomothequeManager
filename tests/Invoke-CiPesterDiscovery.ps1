# #8C-A: fail closed when a Pester suite is omitted from the invoked set.
# Parse the only supported runner shape with the Windows PowerShell 5.1 AST.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-CiPesterSuiteSet {
    param([string[]]$Discovered, [string[]]$Referenced)
    if ($Discovered.Count -eq 0) { throw 'PESTER_DISCOVERY_EMPTY' }
    if (@($Discovered | Group-Object | Where-Object { $_.Count -gt 1 }).Count -gt 0 -or
        @($Referenced | Group-Object | Where-Object { $_.Count -gt 1 }).Count -gt 0) {
        throw 'PESTER_DISCOVERY_DUPLICATE'
    }
    $omitted = @($Discovered | Where-Object { $Referenced -notcontains $_ })
    $phantom = @($Referenced | Where-Object { $Discovered -notcontains $_ })
    if ($omitted.Count -gt 0 -or $phantom.Count -gt 0) {
        throw ('PESTER_SUITE_SET_MISMATCH: omitted=' + ($omitted -join ',') +
            ' missingOnDisk=' + ($phantom -join ','))
    }
}

function Get-CiPesterExecutedSuiteSet {
    param([Parameter(Mandatory=$true)][string]$Source)
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput(
        $Source, [ref]$tokens, [ref]$parseErrors)
    if (@($parseErrors).Count -ne 0) { throw 'PESTER_RUNNER_PARSE_FAILED' }

    # Exactly one assignment to $testScripts; only its syntax tree counts.
    $assignments = @($ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $node.Left.VariablePath.UserPath -ieq 'testScripts'
    }, $true))
    if ($assignments.Count -ne 1) { throw 'PESTER_RUNNER_ASSIGNMENT_UNSUPPORTED' }
    $assignment = $assignments[0]
    $rhs = $assignment.Right.Extent.Text.Trim()

    # Deliberately support only @((Join-Path $root 'tests\Unit\X.Tests.ps1'), ...).
    # Dynamic path building, splatting, pipelines and other forms FAIL CLOSED.
    $entry = '\(Join-Path[ \t]+\$root[ \t]+''tests\\Unit\\[A-Za-z0-9_.-]+\.Tests\.ps1''\)'
    $shape = '^@\(\s*' + $entry + '(\s*,\s*' + $entry + ')*\s*\)$'
    if ($rhs -cnotmatch $shape) { throw 'PESTER_RUNNER_ASSIGNMENT_SHAPE_UNSUPPORTED' }

    # Exactly one actual Invoke-Pester command with -Script $testScripts.
    $commands = @($ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.CommandAst] -and
        $node.GetCommandName() -ieq 'Invoke-Pester'
    }, $true))
    if ($commands.Count -ne 1) { throw 'PESTER_RUNNER_INVOKE_UNSUPPORTED' }
    $elements = @($commands[0].CommandElements)
    $indices = @(for ($i = 0; $i -lt $elements.Count; $i++) {
        if ($elements[$i] -is [System.Management.Automation.Language.CommandParameterAst] -and
            $elements[$i].ParameterName -ieq 'Script') { $i }
    })
    if ($indices.Count -ne 1 -or $indices[0] -ge ($elements.Count - 1)) {
        throw 'PESTER_RUNNER_SCRIPT_ARGUMENT_UNSUPPORTED'
    }
    $argument = $elements[$indices[0] + 1]
    if ($argument -isnot [System.Management.Automation.Language.VariableExpressionAst] -or
        $argument.VariablePath.UserPath -ine 'testScripts') {
        throw 'PESTER_RUNNER_SCRIPT_ARGUMENT_UNSUPPORTED'
    }
    # Forbid any other reference/read/write to the variable (no later mutation).
    $uses = @($ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.VariableExpressionAst] -and
        $node.VariablePath.UserPath -ieq 'testScripts'
    }, $true))
    if ($uses.Count -ne 2) { throw 'PESTER_RUNNER_VARIABLE_FLOW_UNSUPPORTED' }

    $refs = @([regex]::Matches($rhs,
        'tests\\Unit\\([A-Za-z0-9_.-]+\.Tests\.ps1)') |
        ForEach-Object { $_.Groups[1].Value })
    if ($refs.Count -eq 0) { throw 'PESTER_RUNNER_NO_SUITE_REFERENCES' }
    return [pscustomobject]@{
        Names = $refs
        AssignmentStart = $assignment.Right.Extent.StartOffset
        AssignmentEnd = $assignment.Right.Extent.EndOffset
        AssignmentText = $assignment.Right.Extent.Text
    }
}

try {
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {
        throw 'PESTER_DISCOVERY_REQUIRES_PS51'
    }
    $root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $unitDir = Join-Path $root 'tests\Unit'
    $runner = Join-Path $root 'tests\Invoke-CiPester.ps1'
    if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) { throw 'PESTER_RUNNER_MISSING' }
    if (-not (Test-Path -LiteralPath $unitDir -PathType Container)) { throw 'PESTER_TEST_DIRECTORY_MISSING' }
    $files = @(Get-ChildItem -LiteralPath $unitDir -Filter '*.Tests.ps1' -File -Recurse)
    $names = @($files | ForEach-Object {
        if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'PESTER_TEST_REPARSE_POINT'
        }
        if ($_.DirectoryName -ine $unitDir) { throw 'PESTER_TEST_NESTED_UNSUPPORTED' }
        if ($_.Name -cnotmatch '^[A-Za-z0-9_.-]+\.Tests\.ps1$') {
            throw 'PESTER_TEST_FILENAME_INVALID'
        }
        $_.Name
    } | Sort-Object)
    $source = [IO.File]::ReadAllText($runner)
    $execution = Get-CiPesterExecutedSuiteSet -Source $source
    $refs = @($execution.Names)
    Assert-CiPesterSuiteSet -Discovered $names -Referenced $refs

    # Canari 1: simulated third suite omitted from the active list must FAIL.
    $thirdRejected = $false
    try {
        Assert-CiPesterSuiteSet -Discovered @('Alpha.Tests.ps1','NewSuite.Tests.ps1') -Referenced @('Alpha.Tests.ps1')
    } catch {
        if ($_.Exception.Message -like 'PESTER_SUITE_SET_MISMATCH:*') {
            $thirdRejected = $true
        } else { throw }
    }
    if (-not $thirdRejected) { throw 'PESTER_THIRD_SUITE_CANARY_NOT_DETECTED' }

    # Canari 2: remove the LAST executed suite from the REAL runner's AST
    # assignment, retain its name in a COMMENT, then rerun the actual parser.
    if ($refs.Count -lt 2) { throw 'PESTER_COMMENT_CANARY_NEEDS_TWO_SUITES' }
    $removedSuite = $refs[$refs.Count - 1]
    $oneLess = [regex]::Replace($execution.AssignmentText,
        ',\s*\(Join-Path\s+\$root\s+''tests\\Unit\\' +
        [regex]::Escape($removedSuite) + '''\)\s*(?=\)$)', '')
    if ($oneLess -ceq $execution.AssignmentText) { throw 'PESTER_COMMENT_CANARY_SETUP_FAILED' }
    $mutated = $source.Substring(0, $execution.AssignmentStart) + $oneLess +
        $source.Substring($execution.AssignmentEnd) +
        ("`r`n# tests/Unit/" + $removedSuite)
    $commentRejected = $false
    try {
        $mutatedRefs = @( (Get-CiPesterExecutedSuiteSet -Source $mutated).Names )
        Assert-CiPesterSuiteSet -Discovered $names -Referenced $mutatedRefs
    } catch {
        if ($_.Exception.Message -like 'PESTER_SUITE_SET_MISMATCH:*') {
            $commentRejected = $true
        } else { throw }
    }
    if (-not $commentRejected) { throw 'PESTER_COMMENT_CANARY_NOT_DETECTED' }

    Write-Host ('PESTER_DISCOVERY_PASS | suites=' + $names.Count + ' | refs=' + $refs.Count +
        ' | thirdSuiteCanary=PASS | commentCanary=PASS')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
