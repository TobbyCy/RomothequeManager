# Shared bootstrap for CLI and tests. Source saved with UTF-8 BOM for Windows PowerShell 5.1.
$moduleOrder=@('Utils','Canonical','Configuration','Inventory','Metadata','RomResolver','DecisionSchema','StateCodec','Identity','PersistentIdentity','MediaMatcher','MediaModel','Reconciliation','Proposals','Projection','Transactions','Catalogue','Associations')
foreach ($moduleName in $moduleOrder) {
    Import-Module (Join-Path $PSScriptRoot ('Modules\'+$moduleName+'.psm1')) -Force -Global -DisableNameChecking
}
