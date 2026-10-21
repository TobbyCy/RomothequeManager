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
    if (@($data.registry.libraries).Count -ne 7 -or @($data.comparisons).Count -ne 6 -or @($data.referenceVectors).Count -ne 7) { throw 'UI_SERIALIZATION_SCENARIOS' }
    $classes=@('ADDED','CHANGED','UNCHANGED','NOT_OBSERVED_IN_B_COVERED_SCOPE','UNKNOWN','NOT_COMPARABLE')
    $libraryIds=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($lib in $data.registry.libraries) {
        if (-not $libraryIds.Add($lib.libraryId)) { throw 'UI_SERIALIZATION_DUPLICATE_LIBRARY' }
        if (-not $lib.libraryId -or -not $lib.generations) { throw 'UI_SERIALIZATION_LIBRARY_FIELDS' }
        foreach ($generation in $lib.generations) {
            if ($generation.snapshotId -ceq $generation.generationId) { throw 'UI_SERIALIZATION_IDENTITIES_CONFLATED' }
            if ($generation.capturedAtUtc -cnotmatch 'Z$' -or $null -eq $generation.scopeIsExhaustive) { throw 'UI_SERIALIZATION_UTC_PROOF' }
            if (-not $lib.indexAccessible -and ($generation.countKnown -or $null -ne $generation.observedUnitCount)) { throw 'UI_SERIALIZATION_INACCESSIBLE_COUNT' }
        }
    }
    foreach ($vector in $data.referenceVectors) {
        $matches=@($data.registry.libraries | Where-Object { $_.libraryId -ceq $vector.libraryId })
        if ($matches.Count -ne 1) { throw 'UI_SERIALIZATION_VECTOR_LIBRARY_NOT_FOUND' }
        $comparisons=@($data.comparisons | Where-Object { $_.library.libraryId -ceq $vector.libraryId })
        if ($vector.comparisonAvailable -and $comparisons.Count -ne 1) { throw 'UI_SERIALIZATION_VECTOR_COMPARISON_MISSING' }
        if (-not $vector.comparisonAvailable -and $comparisons.Count -ne 0) { throw 'UI_SERIALIZATION_INACCESSIBLE_COMPARISON_LEAK' }
        if ($vector.comparisonAvailable -and ($comparisons[0].before.snapshotId -cne $vector.beforeSnapshotId -or
            $comparisons[0].after.snapshotId -cne $vector.afterSnapshotId -or
            $comparisons[0].coverageStatus -cne $vector.expectedCoverageStatus)) {
            throw 'UI_SERIALIZATION_VECTOR_REFERENCE_MISMATCH'
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
    function Assert-UiDto {
        param([object]$Dto)
        if (@($Dto.registry.libraries).Count -ne 7 -or @($Dto.comparisons).Count -ne 6) { throw 'DTO_CARDINALITY' }
        foreach ($library in $Dto.registry.libraries) {
            foreach ($g in $library.generations) {
                if ($null -eq $g.coverageProof -or $null -eq $g.scopeIsExhaustive) { throw 'DTO_PROOF_MISSING' }
                if ($g.scanStatus -ceq 'complete' -and $g.scopeIsExhaustive -and
                    @($g.coverage | Where-Object { $_.status -ceq 'complete' }).Count -gt 0 -and
                    @($g.coverageProof | Where-Object { $_.status -ceq 'complete' }).Count -eq 0) { throw 'DTO_PROOF_MISSING' }
            }
        }
        foreach ($cmp in $Dto.comparisons) {
            $total=0
            foreach ($class in $classes) {
                $v=$cmp.counts.PSObject.Properties[$class]
                if ($null -eq $v -or $v.Value -isnot [int] -or $v.Value -lt 0) { throw 'DTO_COUNT_INVALID' }
                $total+=$v.Value
            }
            if ($total -ne $cmp.countDenominator -or $total -ne @($cmp.entries).Count) { throw 'DTO_COUNT_MISMATCH' }
        }
    }
    function Assert-DtoRejected {
        param([object]$Dto,[string]$Expected)
        $actual=''
        try { Assert-UiDto -Dto $Dto } catch { $actual=$_.Exception.Message }
        if (-not $actual.Contains($Expected)) { throw ('DTO_NEGATIVE_NOT_REJECTED:'+$Expected+':'+$actual) }
    }
    Assert-UiDto -Dto $data
    $invalid=ConvertFrom-Json -InputObject $first
    $invalid.comparisons[0].counts.ADDED=999
    Assert-DtoRejected -Dto $invalid -Expected 'DTO_COUNT_MISMATCH'
    $invalid=ConvertFrom-Json -InputObject $first
    $invalid.registry.libraries[0].generations[0].coverageProof=@()
    Assert-DtoRejected -Dto $invalid -Expected 'DTO_PROOF_MISSING'
    Write-Host 'UI_SYNTHETIC_SERIALIZATION_PASS | deterministic=1 | scenarioCount=7 | comparisonCount=6 | real_io=0'
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
