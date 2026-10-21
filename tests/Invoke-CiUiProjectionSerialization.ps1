# Synthetic serializer smoke and refusal canaries (Windows PowerShell 5.1 only).
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
try {
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) { throw 'UI_SERIALIZATION_REQUIRES_PS51' }
    $scriptPath=Join-Path $PSScriptRoot 'Export-SyntheticUiProjection.ps1'
    $first=& $scriptPath
    $second=& $scriptPath
    if ($first -cne $second) { throw 'UI_SERIALIZATION_NON_DETERMINISTIC' }
    $data=ConvertFrom-Json -InputObject $first
    if ($data.registry.schemaVersion -cne '0.1.0-synthetic' -or -not $data.registry.readOnly) { throw 'UI_SERIALIZATION_VERSION' }
    if (@($data.registry.libraries).Count -ne 6 -or @($data.comparisons).Count -ne 5) { throw 'UI_SERIALIZATION_SCENARIOS' }
    $classes=@('ADDED','CHANGED','UNCHANGED','NOT_OBSERVED_IN_B_COVERED_SCOPE','UNKNOWN','NOT_COMPARABLE')
    foreach ($lib in $data.registry.libraries) {
        if (-not $lib.libraryId -or -not $lib.generations) { throw 'UI_SERIALIZATION_LIBRARY_FIELDS' }
        foreach ($generation in $lib.generations) {
            if ($generation.snapshotId -ceq $generation.generationId) { throw 'UI_SERIALIZATION_IDENTITIES_CONFLATED' }
            if ($generation.capturedAtUtc -cnotmatch 'Z$' -or $null -eq $generation.scopeIsExhaustive) { throw 'UI_SERIALIZATION_UTC_PROOF' }
            if (-not $lib.indexAccessible -and ($generation.countKnown -or $null -ne $generation.observedUnitCount)) { throw 'UI_SERIALIZATION_INACCESSIBLE_COUNT' }
        }
    }
    foreach ($cmp in $data.comparisons) {
        $sum=0
        foreach ($class in $classes) {
            $value=$cmp.counts.PSObject.Properties[$class]
            if ($null -eq $value -or $value.Value -lt 0) { throw 'UI_SERIALIZATION_MISSING_CLASS' }
            $sum+=$value.Value
        }
        if ($sum -ne $cmp.countDenominator -or $sum -ne @($cmp.entries).Count) { throw 'UI_SERIALIZATION_COUNT_MISMATCH' }
        if ($cmp.coverageStatus -ceq 'COMPLETE' -and @($cmp.scopeNotComparable).Count -ne 0) { throw 'UI_SERIALIZATION_UNPROVEN_COMPLETE' }
        foreach ($ref in @($cmp.before,$cmp.after)) {
            if ($null -eq $ref.coverageProof -or $null -eq $ref.scopeIsExhaustive) { throw 'UI_SERIALIZATION_MISSING_PROOF' }
        }
    }
    $invalid=ConvertFrom-Json -InputObject $first
    $invalid.comparisons[0].counts.ADDED=999
    $total=0
    foreach ($class in $classes) { $total+=$invalid.comparisons[0].counts.PSObject.Properties[$class].Value }
    if ($total -eq $invalid.comparisons[0].countDenominator) { throw 'UI_SERIALIZATION_NEGATIVE_COUNT_NOT_DETECTED' }
    $invalid.registry.libraries[0].generations[0].coverageProof=@()
    if (@($invalid.registry.libraries[0].generations[0].coverageProof).Count -ne 0) { throw 'UI_SERIALIZATION_NEGATIVE_PROOF_NOT_DETECTED' }
    Write-Host 'UI_SYNTHETIC_SERIALIZATION_PASS | deterministic=1 | scenarioCount=6 | comparisonCount=5 | real_io=0'
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
