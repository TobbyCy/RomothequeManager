# TEXTUAL ONLY: cross-branch token inventory, not semantic JS validation. No browser, ROM or scan.
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
    if ($data -match 'RM_PROJECTION_DATA\\s*=\\s*null') {
        Write-Host 'UI_ADAPTER_TEXTUAL_ONLY | data=null | semantic_projection=NOT_EXECUTED'
    } else {
        Write-Host 'UI_ADAPTER_TEXTUAL_ONLY | data=non-null-text | semantic_projection=NOT_EXECUTED'
    }
    if ($adapter.Contains('Compare-RmInventorySnapshots') -or
        $adapter.Contains('SNAPSHOT_DIGEST_DIFFERENT')) { throw 'UI_CONTRACT_COMPARISON_ENGINE_DUPLICATED' }
    Write-Host ('UI_ADAPTER_TEXTUAL_TOKENS_PRESENT | tokens='+$must.Count+' | semantic_compatibility=NOT_EXECUTED | source=external')
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
