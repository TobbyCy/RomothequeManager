# A deliberate game/folder leak in a disposable unit must make Pester FAIL.
$ErrorActionPreference = 'Stop'
try {
    Import-Module Pester -RequiredVersion '4.10.1' -Force -ErrorAction Stop
    $root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $module=Join-Path $root 'Export\ExportE1.psm1'
    $testFile=Join-Path ([IO.Path]::GetTempPath()) ('rm-e1-regression-canary-'+[guid]::NewGuid().ToString('N')+'.Tests.ps1')
    $source=@'
Import-Module -Name '__EXPORT_MODULE__' -Force
Describe 'Injected broken Export E1 fixture' {
    It 'failing unit projection with leaked folder cannot pass' {
        $resolved=@{'game-1'=$true}
        $tampered=@([pscustomobject]@{confirmedMetadataEntryIds=@('game-1','folder-bad');widerScopeMetadataEntryIds=@()})
        { Assert-RmE1GameCoverage -ResolvedGameIds $resolved -Units $tampered } | Should -Not -Throw
    }
}
'@
    $source=$source.Replace('__EXPORT_MODULE__',$module.Replace("'","''"))
    [IO.File]::WriteAllText($testFile,$source,(New-Object Text.UTF8Encoding($true)))
    try {
        $negative=Invoke-Pester -Script $testFile -PassThru -Show Failed,Summary -ErrorAction Stop
        if ($negative.FailedCount -ne 1 -or $negative.PassedCount -ne 0) {throw ('E1_CANARY_DID_NOT_FAIL_AS_EXPECTED: passed='+$negative.PassedCount+' failed='+$negative.FailedCount)}
        Write-Host 'E1_NEGATIVE_CANARY_PASS: injected folder leak causes real Pester failure (1 expected FAIL)'
    } finally {Remove-Item -LiteralPath $testFile -Force -ErrorAction SilentlyContinue}
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.ToString());exit 1 }
