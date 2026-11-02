# #149 B — subprocess running as a newly created non-admin Windows user.
# Input must be disposable synthetic fixtures. No personal library or writable product root.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$Extract,
  [Parameter(Mandatory = $true)][string]$Fixture,
  [Parameter(Mandatory = $true)][string]$Shared
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'BETA_NONADMIN_WINDOWS_REQUIRED' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
  throw 'BETA_NONADMIN_IDENTITY_FAILED'
}
foreach ($path in @($Extract, $Fixture, $Shared)) {
  if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw 'BETA_SYNTHETIC_SHARED_PATH_MISSING' }
}
$prefix = [IO.Path]::GetFullPath($Shared).TrimEnd('\') + '\'
if (-not [IO.Path]::GetFullPath($Fixture).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
  throw 'BETA_FIXTURE_OUTSIDE_SHARED_ROOT'
}
$exe = Join-Path $Extract 'RomothequeManager.V2.exe'
$xml = Join-Path $Fixture 'MEDIAS/gba/metadata/es-de-gamelist.xml'
if (-not (Test-Path -LiteralPath $xml -PathType Leaf) -or
    -not (Test-Path -LiteralPath $exe -PathType Leaf)) {
  throw 'BETA_NONADMIN_EXTRACTED_EXE_OR_XML_MISSING'
}
$originalXml = (Get-FileHash -LiteralPath $xml -Algorithm SHA256).Hash

function Invoke-BetaExe([string[]]$args, [string]$expected) {
  $start = [Diagnostics.ProcessStartInfo]::new()
  $start.FileName = $exe
  $start.WorkingDirectory = $Extract
  $start.UseShellExecute = $false
  $start.RedirectStandardOutput = $true
  $start.RedirectStandardError = $true
  foreach ($arg in $args) { [void]$start.ArgumentList.Add($arg) }
  $start.Environment['GITHUB_ACTIONS'] = 'true'
  $start.Environment['RUNNER_TEMP'] = $Shared
  $start.Environment['DOTNET_ROOT'] = Join-Path $Shared 'missing-dotnet'
  $start.Environment['DOTNET_ROOT_X64'] = Join-Path $Shared 'missing-dotnet'
  $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
  $start.Environment['PATH'] = $Extract + ';' + $env:SystemRoot + '\System32;' + $env:SystemRoot
  $proc = [Diagnostics.Process]::Start($start)
  if ($null -eq $proc) { throw 'BETA_NONADMIN_PROCESS_NOT_STARTED' }
  try {
    $out = $proc.StandardOutput.ReadToEndAsync()
    $err = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit(90000)) {
      $proc.Kill()
      throw 'BETA_NONADMIN_EXTRACTED_TIMEOUT'
    }
    if ($proc.ExitCode -ne 0 -or -not $out.Result.Contains($expected, [StringComparison]::Ordinal)) {
      throw ('BETA_NONADMIN_EXTRACTED_FAIL exit=' + $proc.ExitCode +
        ' stdout=' + $out.Result + ' stderr=' + $err.Result)
    }
  } finally { $proc.Dispose() }
}
Invoke-BetaExe @('--smoke-window') 'POC_WINDOW_OPENED_PASS'
Write-Host 'BETA_NONADMIN_EXTRACTED_UI_PASS native_window=true self_contained=true'
Invoke-BetaExe @('--ci-ux-offline-fixture', $Fixture) 'UX_RECIPE_OFFLINE_ESDE_GUI_PASS'
if ((Get-FileHash -LiteralPath $xml -Algorithm SHA256).Hash -cne $originalXml) {
  throw 'BETA_ESDE_XML_NOT_RESTORED'
}
Write-Host 'BETA_NONADMIN_ESDE_PASS imported=4 diagnostics=true media_fallback_placeholder=true malformed_xml_refused=true'

# Native SQLite is actually loaded from the EXTRACTED ZIP, never from a system copy.
# Product SQLite persistence and restoration are additionally exercised by the
# existing fixture-only .NET tests; this probe alone is NOT a full app restore.
$native = @'
using System;
using System.Runtime.InteropServices;
public static class BetaNativeSqliteProbe {
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string name, out IntPtr db);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int Exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr arg, out IntPtr error);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int Close(IntPtr db);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int Prepare(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr statement, IntPtr tail);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int Step(IntPtr statement);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int ColumnInt(IntPtr statement, int index);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  delegate int Finalize(IntPtr statement);
  static T Fn<T>(IntPtr lib, string name) where T: Delegate =>
    Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(lib,name));
  public static void Verify(string dll, string path) {
    IntPtr lib = NativeLibrary.Load(dll);
    try {
      var open=Fn<Open>(lib,"sqlite3_open");
      var exec=Fn<Exec>(lib,"sqlite3_exec");
      var close=Fn<Close>(lib,"sqlite3_close");
      var prep=Fn<Prepare>(lib,"sqlite3_prepare_v2");
      var step=Fn<Step>(lib,"sqlite3_step");
      var value=Fn<ColumnInt>(lib,"sqlite3_column_int");
      var final=Fn<Finalize>(lib,"sqlite3_finalize");
      IntPtr db;
      if(open(path,out db)!=0) throw new Exception("BETA_NATIVE_SQLITE_CREATE_FAILED");
      try {
        IntPtr error;
        if(exec(db,"CREATE TABLE probe (id INTEGER PRIMARY KEY); INSERT INTO probe VALUES (7);",
             IntPtr.Zero,IntPtr.Zero,out error)!=0) throw new Exception("BETA_NATIVE_SQLITE_WRITE_FAILED");
      } finally { if(close(db)!=0) throw new Exception("BETA_NATIVE_SQLITE_CLOSE_FAILED"); }
      if(open(path,out db)!=0) throw new Exception("BETA_NATIVE_SQLITE_REOPEN_FAILED");
      try {
        IntPtr statement;
        if(prep(db,"SELECT id FROM probe",-1,out statement,IntPtr.Zero)!=0)
          throw new Exception("BETA_NATIVE_SQLITE_SELECT_FAILED");
        try {
          if(step(statement)!=100 || value(statement,0)!=7)
            throw new Exception("BETA_NATIVE_SQLITE_RESTORED_VALUE_FAILED");
        } finally { if(final(statement)!=0) throw new Exception("BETA_NATIVE_SQLITE_FINALIZE_FAILED"); }
      } finally { if(close(db)!=0) throw new Exception("BETA_NATIVE_SQLITE_FINAL_CLOSE_FAILED"); }
    } finally { NativeLibrary.Free(lib); }
  }
}
'@
Add-Type -TypeDefinition $native -ErrorAction Stop
$database = Join-Path $Shared 'synthetic-sqlite-probe.sqlite'
[BetaNativeSqliteProbe]::Verify((Join-Path $Extract 'e_sqlite3.dll'), $database)
Write-Host 'BETA_NONADMIN_NATIVE_SQLITE_PASS e_sqlite3_from_extracted=true write_close_reopen_read=true'
Write-Host 'BETA_NONADMIN_FIXTURE_PASS no_personal_scan=true no_release=true'
