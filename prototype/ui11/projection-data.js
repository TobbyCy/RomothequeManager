/* Optional OFFLINE synthetic PowerShell DTO payload, injected by a separate trusted build step.
   Keep null for demo mode. To exercise projection mode, replace null with
   { registry: <New-RmLibraryProjection JSON>, comparisons: [<Get-RmLibraryComparisonView JSON>] }.
   Do not use fetch() or read real ROMs from a browser. */
window.RM_PROJECTION_DATA=null;
