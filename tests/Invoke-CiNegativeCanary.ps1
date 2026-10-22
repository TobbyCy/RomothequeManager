# Negative control: a parse error MUST produce exit code 1 in the real CI-0 script.
$ErrorActionPreference = 'Stop'
try {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('rm-ci-negative-' + [guid]::NewGuid().ToString('N') + '.ps1')
    # Temporary file, not tracked by Git, not part of ROM library or source tree.
    [IO.File]::WriteAllText($temp, 'function Broken-Canary { if (', (New-Object Text.UTF8Encoding($false)))
    try {
        $exe = Join-Path $PSHOME 'powershell.exe'
        & $exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Invoke-CiStatic.ps1') -AdditionalSyntaxPath $temp
        $exit = $LASTEXITCODE
        if ($exit -eq 0) { throw 'NEGATIVE_CANARY_FAILED: CI-0 incorrectly accepted a malformed source file.' }
        if ($exit -ne 1) { throw ('NEGATIVE_CANARY_UNEXPECTED_EXIT: ' + $exit) }
        Write-Host 'NEGATIVE_CANARY_PASS: real CI-0 returns exit 1 for malformed .ps1 fixture.'
    } finally { Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue }
    # GH-03B: exercise the REAL path policy, never a duplicate regex in a test.
    # The disposable clone contains Git-tracked code/docs only; no engine import.
    $sourceRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $tempBoundary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $fixtureRoot = [IO.Path]::GetFullPath((Join-Path $tempBoundary ('rm-path-canary-' + [guid]::NewGuid().ToString('N'))))
    if (!$fixtureRoot.StartsWith($tempBoundary,[StringComparison]::OrdinalIgnoreCase)) { throw 'PATH_CANARY_BOUNDARY_INVALID' }
    $gitExe = (Get-Command git.exe -ErrorAction Stop).Source
    function Invoke-StaticInFixture {
        $start = New-Object Diagnostics.ProcessStartInfo
        $start.FileName = $exe
        $start.Arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + (Join-Path $fixtureRoot 'tests\Invoke-CiStatic.ps1') + '"'
        $start.WorkingDirectory = $fixtureRoot
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (!$process.WaitForExit(120000)) { $process.Kill(); throw 'PATH_CANARY_TIMEOUT' }
            return [pscustomobject]@{Code=$process.ExitCode;Text=($stdout.Result + $stderr.Result)}
        } finally { $process.Dispose() }
    }
    try {
        & $gitExe clone --quiet --no-hardlinks --no-local -- $sourceRoot $fixtureRoot
        if ($LASTEXITCODE -ne 0) { throw 'PATH_CANARY_CLONE_FAILED' }
        $positive = Invoke-StaticInFixture
        if ($positive.Code -ne 0 -or !$positive.Text.Contains('CI-0 PASS')) { throw ('PATH_CANARY_POSITIVE_FAILED: ' + $positive.Text) }
        $forbidden = @(
            '.github/ISSUE_TEMPLATE/nested/bug.yml',
            '.github/ISSUE_TEMPLATE/bug.ps1',
            '.github/ISSUE_TEMPLATE/bug.yaml',
            '.github/ISSUE_TEMPLATE/BUG-extra.yml',
            '.github/ISSUE_TEMPLATE/incident-securite.yml',
            '.github/workflows/triage.yml',
            '.github/CODEOWNERS.txt',
            'SECURITY.txt'
        )
        $checked = 0
        foreach ($path in $forbidden) {
            $full = Join-Path $fixtureRoot $path
            $parent = Split-Path -Parent $full
            $null = New-Item -ItemType Directory -Path $parent -Force
            [IO.File]::WriteAllText($full,'# harmless path-policy fixture',(New-Object Text.UTF8Encoding($false)))
            & $gitExe -C $fixtureRoot add -- $path
            if ($LASTEXITCODE -ne 0) { throw 'PATH_CANARY_ADD_FAILED' }
            $negative = Invoke-StaticInFixture
            if ($negative.Code -ne 1 -or !$negative.Text.Contains('TRACKED_PATH_NOT_ALLOWED: ' + $path)) {
                throw ('PATH_CANARY_NEGATIVE_FAILED: ' + $path + ' | ' + $negative.Text)
            }
            & $gitExe -C $fixtureRoot rm --cached --quiet -- $path
            if ($LASTEXITCODE -ne 0) { throw 'PATH_CANARY_UNSTAGE_FAILED' }
            Remove-Item -LiteralPath $full -Force
            $checked++
        }
        Write-Host ('PATH_POLICY_CANARY_PASS | baseline=PASS | rejected=' + $checked + ' | realCi0=true')
    } finally {
        # Verify the exact absolute target before recursive deletion, in this shell.
        if ((Test-Path -LiteralPath $fixtureRoot) -and $fixtureRoot.StartsWith($tempBoundary,[StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetFileName($fixtureRoot) -cmatch '^rm-path-canary-[0-9a-f]{32}$') {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.Message);exit 1 }
