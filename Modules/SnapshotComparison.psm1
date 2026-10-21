# Pure in-memory snapshot comparison; no scanner, file IO, StateRoot or mutations.
# Independent of the historical PowerShell engine modules (Issue #9).
Set-StrictMode -Version 2.0

function Get-RmSnapshotField {
    param($Record, [string]$Name, [string]$Context)
    if ($null -eq $Record) { throw ('SNAPSHOT_NULL_RECORD:' + $Context) }
    $property = $Record.PSObject.Properties[$Name]
    if ($null -eq $property) { throw ('SNAPSHOT_FIELD_MISSING:' + $Context + '.' + $Name) }
    return $property.Value
}

function Get-RmSnapshotText {
    param($Record, [string]$Name, [string]$Context)
    $value = Get-RmSnapshotField $Record $Name $Context
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        throw ('SNAPSHOT_FIELD_INVALID:' + $Context + '.' + $Name)
    }
    return $value
}

function Assert-RmSnapshotChoice {
    param([string]$Value, [string[]]$Choices, [string]$Field)
    if ($Choices -cnotcontains $Value) { throw ('SNAPSHOT_CHOICE_INVALID:' + $Field) }
}

function Get-RmSnapshotDate {
    param([string]$Value)
    if ($Value -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$') {
        throw 'SNAPSHOT_UTC_DATE_REQUIRED'
    }
    $stamp = [DateTimeOffset]::MinValue
    $valid = [DateTimeOffset]::TryParse($Value,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$stamp)
    if (-not $valid -or $stamp.Offset -ne [TimeSpan]::Zero) {
        throw 'SNAPSHOT_UTC_DATE_INVALID'
    }
    return $stamp
}

function ConvertTo-RmComparisonSnapshot {
    param($InputSnapshot)
    $version = Get-RmSnapshotText $InputSnapshot 'schemaVersion' 'snapshot'
    if ($version -cne '1.0.0') { throw 'SNAPSHOT_SCHEMA_UNSUPPORTED' }
    $libraryId = Get-RmSnapshotText $InputSnapshot 'libraryId' 'snapshot'
    $generationId = Get-RmSnapshotText $InputSnapshot 'generationId' 'snapshot'
    $capturedAtUtc = Get-RmSnapshotText $InputSnapshot 'capturedAtUtc' 'snapshot'
    $date = Get-RmSnapshotDate $capturedAtUtc
    $scanStatus = Get-RmSnapshotText $InputSnapshot 'scanStatus' 'snapshot'
    $availability = Get-RmSnapshotText $InputSnapshot 'sourceAvailability' 'snapshot'
    $freshness = Get-RmSnapshotText $InputSnapshot 'freshness' 'snapshot'
    Assert-RmSnapshotChoice $scanStatus @('complete','partial','failed') 'scanStatus'
    Assert-RmSnapshotChoice $availability @('ONLINE','OFFLINE','INACCESSIBLE','UNKNOWN') 'sourceAvailability'
    Assert-RmSnapshotChoice $freshness @('FRESH','STALE','UNKNOWN') 'freshness'

    $rawCoverage = Get-RmSnapshotField $InputSnapshot 'coverage' 'snapshot'
    $rawUnits = Get-RmSnapshotField $InputSnapshot 'units' 'snapshot'
    if ($rawCoverage -isnot [array] -or $rawUnits -isnot [array]) {
        throw 'SNAPSHOT_ARRAY_REQUIRED'
    }
    $coverage = @{}
    foreach ($scope in $rawCoverage) {
        $console = Get-RmSnapshotText $scope 'console' 'coverage'
        $status = Get-RmSnapshotText $scope 'status' 'coverage'
        if ($console -cnotmatch '^[A-Z][A-Z0-9_-]*$') { throw 'SNAPSHOT_CONSOLE_INVALID' }
        Assert-RmSnapshotChoice $status @('complete','partial','unobserved') 'coverage.status'
        if ($coverage.ContainsKey($console)) { throw 'SNAPSHOT_DUPLICATE_COVERAGE' }
        $coverage[$console] = $status
    }

    $units = @{}
    foreach ($record in $rawUnits) {
        $id = Get-RmSnapshotText $record 'romUnitId' 'unit'
        $gameId = Get-RmSnapshotText $record 'gameId' 'unit'
        $variantId = Get-RmSnapshotText $record 'variantId' 'unit'
        $console = Get-RmSnapshotText $record 'console' 'unit'
        if ($null -ne $record.PSObject.Properties['fileId']) { throw 'SNAPSHOT_FILEID_NOT_PERSISTENT' }
        if ($units.ContainsKey($id)) { throw 'SNAPSHOT_DUPLICATE_UNIT_ID' }
        if (-not $coverage.ContainsKey($console)) { throw 'SNAPSHOT_COVERAGE_SCOPE_MISSING' }
        if ($coverage[$console] -ceq 'unobserved') { throw 'SNAPSHOT_UNIT_IN_UNOBSERVED_SCOPE' }
        $digest = $null
        $digestProperty = $record.PSObject.Properties['observationDigest']
        if ($null -ne $digestProperty -and $null -ne $digestProperty.Value) {
            if ($digestProperty.Value -isnot [string] -or
                $digestProperty.Value -cnotmatch '^[0-9a-fA-F]{64}$') {
                throw 'SNAPSHOT_DIGEST_INVALID'
            }
            $digest = $digestProperty.Value.ToLowerInvariant()
        }
        $units[$id] = [pscustomobject]@{
            romUnitId = $id
            gameId = $gameId
            variantId = $variantId
            console = $console
            observationDigest = $digest
        }
    }
    return [pscustomobject]@{
        libraryId = $libraryId
        generationId = $generationId
        capturedAtUtc = $capturedAtUtc
        date = $date
        scanStatus = $scanStatus
        sourceAvailability = $availability
        freshness = $freshness
        coverage = $coverage
        units = $units
    }
}

function Test-RmComparisonScope {
    param($Snapshot, [string]$Console)
    # Absence is informative only when the entire scan AND this scope were completed.
    return ($Snapshot.scanStatus -ceq 'complete' -and
        $Snapshot.coverage.ContainsKey($Console) -and
        $Snapshot.coverage[$Console] -ceq 'complete')
}

function Compare-RmInventorySnapshots {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]$Before,
        [Parameter(Mandatory=$true)]$After
    )
    $old = ConvertTo-RmComparisonSnapshot $Before
    $new = ConvertTo-RmComparisonSnapshot $After
    if ($old.libraryId -cne $new.libraryId) { throw 'SNAPSHOT_LIBRARY_MISMATCH' }
    if ($old.generationId -ceq $new.generationId) { throw 'SNAPSHOT_GENERATION_REUSED' }
    if ($new.date -lt $old.date) { throw 'SNAPSHOT_DATE_ORDER_INVALID' }

    $all = @{}
    foreach ($id in $old.units.Keys) { $all[$id] = $true }
    foreach ($id in $new.units.Keys) { $all[$id] = $true }
    [string[]]$ids = @($all.Keys)
    [Array]::Sort($ids, [StringComparer]::Ordinal)
    $entries = New-Object 'System.Collections.Generic.List[object]'
    $counts = @{added=0;modified=0;unchanged=0;notObserved=0;notComparable=0}
    foreach ($id in $ids) {
        $prior = $null; $current = $null
        if ($old.units.ContainsKey($id)) { $prior = $old.units[$id] }
        if ($new.units.ContainsKey($id)) { $current = $new.units[$id] }
        $owner = if ($null -ne $current) { $current } else { $prior }
        $category = 'notComparable'; $reason = 'UNKNOWN'
        if ($null -ne $prior -and $null -ne $current) {
            if ($prior.gameId -cne $current.gameId -or
                $prior.variantId -cne $current.variantId -or
                $prior.console -cne $current.console) {
                $reason = 'IDENTITY_OWNER_CONFLICT'
            } elseif ($old.scanStatus -ceq 'failed' -or $new.scanStatus -ceq 'failed') {
                $reason = 'SCAN_FAILED'
            } elseif ($null -eq $prior.observationDigest -or
                      $null -eq $current.observationDigest) {
                $reason = 'OBSERVATION_DIGEST_UNKNOWN'
            } elseif ($prior.observationDigest -ceq $current.observationDigest) {
                $category = 'unchanged'; $reason = 'OBSERVATION_IDENTICAL'
            } else {
                $category = 'modified'; $reason = 'OBSERVATION_CHANGED'
            }
        } elseif ($null -ne $current) {
            if (Test-RmComparisonScope $old $current.console -and
                $new.scanStatus -cne 'failed') {
                $category = 'added'; $reason = 'ABSENT_FROM_VERIFIED_PRIOR_SCOPE'
            } else {
                $reason = 'PRIOR_SCOPE_UNVERIFIED'
            }
        } else {
            if (Test-RmComparisonScope $new $prior.console -and
                $old.scanStatus -cne 'failed') {
                $category = 'notObserved'; $reason = 'ABSENT_FROM_VERIFIED_CURRENT_SCOPE'
            } else {
                $reason = 'CURRENT_SCOPE_UNVERIFIED'
            }
        }
        $counts[$category]++
        $entries.Add([pscustomobject]@{
            romUnitId = $id
            gameId = $owner.gameId
            variantId = $owner.variantId
            console = $owner.console
            classification = $category
            reason = $reason
            beforeDigest = $(if ($null -ne $prior) { $prior.observationDigest } else { $null })
            afterDigest = $(if ($null -ne $current) { $current.observationDigest } else { $null })
        })
    }
    return [pscustomobject]@{
        schemaVersion = '1.0.0'
        libraryId = $old.libraryId
        beforeGenerationId = $old.generationId
        afterGenerationId = $new.generationId
        beforeCapturedAtUtc = $old.capturedAtUtc
        afterCapturedAtUtc = $new.capturedAtUtc
        beforeScanStatus = $old.scanStatus
        afterScanStatus = $new.scanStatus
        beforeAvailability = $old.sourceAvailability
        afterAvailability = $new.sourceAvailability
        beforeFreshness = $old.freshness
        afterFreshness = $new.freshness
        counts = [pscustomobject]@{
            added = $counts.added
            modified = $counts.modified
            unchanged = $counts.unchanged
            notObserved = $counts.notObserved
            notComparable = $counts.notComparable
        }
        entries = $entries.ToArray()
    }
}

Export-ModuleMember -Function Compare-RmInventorySnapshots
