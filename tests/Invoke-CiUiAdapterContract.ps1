# Static, read-only cross-branch compatibility check; no browser, ROM or scan.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$UiAdapterPath,
      [Parameter(Mandatory=$true)][string]$UiDataPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
try {
    if (-not (Test-Path -LiteralPath $UiAdapterPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $UiDataPath -PathType Leaf)) { throw 'UI_CONTRACT_SOURCE_MISSING' }
    $adapter=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $UiAdapterPath).Path)
    $data=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $UiDataPath).Path)
    $must=@(
        '0.1.0-synthetic','currentAvailability','indexAccessible','indexAvailable',
        'generations','snapshots','snapshotId','generationId','coverageProof',
        'scopeIsExhaustive','countKnown','observedUnitCount','scopeNotComparable',
        'countDenominator','NOT_OBSERVED_IN_B_COVERED_SCOPE','UNKNOWN','NOT_COMPARABLE',
        'COUNT_MISMATCH_','RM_PROJECTION'
    )
    foreach ($token in $must) {
        if (-not $adapter.Contains($token)) { throw ('UI_CONTRACT_ADAPTER_TOKEN_MISSING:'+$token) }
    }
    if (-not $data.Contains('RM_PROJECTION_DATA')) { throw 'UI_CONTRACT_DATA_INJECTION_MISSING' }
    if ($adapter.Contains('Compare-RmInventorySnapshots') -or
        $adapter.Contains('SNAPSHOT_DIGEST_DIFFERENT')) { throw 'UI_CONTRACT_COMPARISON_ENGINE_DUPLICATED' }
    Write-Host ('UI_ADAPTER_STATIC_CONTRACT_OK | tokens='+$must.Count+' | source=external | no_runtime_claim=1')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
