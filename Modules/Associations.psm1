Set-StrictMode -Version 2.0
function Confirm-RmSuggestion {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$OutputRoot,
        [Parameter(Mandatory=$true)][string]$ExpectedScanId,
        [Parameter(Mandatory=$true)][int[]]$Numbers)
    # Deliberately no filesystem IO and no persistent application implementation.
    throw 'LEGACY_APPROVAL_DISABLED: use Invoke-Decisions.ps1 Preview with scanId and a typed request. Real commits forbidden in this delivery.'
}
Export-ModuleMember -Function Confirm-RmSuggestion
