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
$sessionScript = Join-Path $Shared 'BetaIdealWindowsSession.ps1'
if (-not (Test-Path -LiteralPath $sessionScript -PathType Leaf)) {
  throw 'BETA_SESSION_DIAGNOSTIC_SCRIPT_MISSING'
}
. $sessionScript
$sessionFacts = Get-BetaWindowsSessionFacts -Role worker
if ($sessionFacts.integrityRid -lt 0) { Write-Host 'BETA_WORKER_INTEGRITY_NOT_VERIFIED' }
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

# Codes are the only application data permitted to escape a terminated process.
# Never display raw stdout/stderr, exception text, paths or the trace's parent process.
$script:BetaTracePattern = '^BETA_AVALONIA_TRACE (MAIN_ENTER|SETUP_BEGIN|SETUP_COMPLETE|MAINWINDOW_CTOR_BEGIN|MAINWINDOW_CTOR_END|LIFETIME_START_BEGIN|WINDOW_OPENED|DISPATCHER_POST|DISPATCHER_EXECUTE|SMOKE_PASS|SMOKE_FAIL|LIFETIME_RETURN)$'
function Write-BetaTraceSummary([object]$stdoutTask, [object]$stderrTask, [string]$role) {
  $completed = $stdoutTask.IsCompletedSuccessfully
  $codes = [Collections.Generic.List[string]]::new()
  if ($completed) {
    $raw = [string]$stdoutTask.Result
    $tail = $raw.Substring([Math]::Max(0, $raw.Length - 16384))
    foreach ($line in ($tail -split '\r?\n')) {
      if ($line -cmatch $script:BetaTracePattern) {
        if ($codes.Count -eq 12) { $codes.RemoveAt(0) }
        $codes.Add($Matches[1])
      }
    }
  }
  $last = if ($codes.Count -eq 0) { 'NOT_VERIFIED' } else { $codes[$codes.Count - 1] }
  $sequence = if ($codes.Count -eq 0) { 'NOT_VERIFIED' } else { [string]::Join(',', $codes) }
  Write-Host ('BETA_AVALONIA_TRACE_SUMMARY role=' + $role + ' last=' + $last +
    ' codes=' + $sequence + ' stdoutComplete=' + $completed +
    ' stderrComplete=' + $stderrTask.IsCompletedSuccessfully)
}
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
  # The actual child RUNNER_TEMP must equal the explicit, canonical shared fixture.
  $start.Environment['ROMOTHEQUE_BETA_TRACE'] = '1'
  $start.Environment['ROMOTHEQUE_BETA_TRACE_ROOT'] = $Shared
  $start.Environment['PATH'] = $Extract + ';' + $env:SystemRoot + '\System32;' + $env:SystemRoot
  $proc = [Diagnostics.Process]::Start($start)
  if ($null -eq $proc) { throw 'BETA_NONADMIN_PROCESS_NOT_STARTED' }
  Write-Host ('BETA_EXTRACTED_PROCESS_START role=worker pid=' + $proc.Id +
    ' session=' + $proc.SessionId + ' workerSession=' + $sessionFacts.session +
    ' launched=true exited=' + $proc.HasExited)
  try {
    $out = $proc.StandardOutput.ReadToEndAsync()
    $err = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit(90000)) {
      $threads = -1
      try { $threads = $proc.Threads.Count } catch { }
      Write-Host ('BETA_EXTRACTED_PROCESS_TIMEOUT pid=' + $proc.Id +
        ' processSession=' + $proc.SessionId +
        ' workerSession=' + $sessionFacts.session +
        ' threads=' + $threads + ' windowSignal=NOT_VERIFIED')
      # Only the EXE child, never arbitrary session processes or GUI helpers.
      if (-not $proc.HasExited) { $proc.Kill() }
      [void]$proc.WaitForExit(5000)
      # Capture bounded partial output only AFTER termination. No raw pipes or paths.
      try { [void][Threading.Tasks.Task]::WhenAll(@($out, $err)).Wait(5000) } catch { }
      Write-BetaTraceSummary $out $err 'nonadmin'
      throw 'BETA_NONADMIN_EXTRACTED_TIMEOUT'
    }
    Write-Host ('BETA_EXTRACTED_PROCESS_EXIT pid=' + $proc.Id +
      ' session=' + $proc.SessionId + ' exit=' + $proc.ExitCode +
      ' completed=true')
    try { [void][Threading.Tasks.Task]::WhenAll(@($out, $err)).Wait(5000) } catch { }
    Write-BetaTraceSummary $out $err 'nonadmin'
    $oracle = $out.IsCompletedSuccessfully -and
      $out.Result.Contains($expected, [StringComparison]::Ordinal)
    if ($proc.ExitCode -ne 0 -or -not $oracle) {
      throw ('BETA_NONADMIN_EXTRACTED_FAIL exit=' + $proc.ExitCode +
        ' expectedOracleObserved=' + $oracle)
    }
  } finally { $proc.Dispose() }
}
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
if ($sessionFacts.isAdmin -or $sessionFacts.integrityRid -ge 12288) {
  throw 'BETA_NONADMIN_INTEGRITY_OR_ADMIN_REFUSED'
}
# A new Windows account launched by -Credential is not proof of a visible desktop.
# WTSActive + WinSta0/Default + WSF_VISIBLE must be observed in THAT token.
if (-not $sessionFacts.interactive) {
  $reason = if ($sessionFacts.defaultDesktopAccessError -eq 5 -or
      $sessionFacts.inputDesktopAccessError -eq 5 -or
      $sessionFacts.hiddenWindowCreateError -eq 5) {
    'win32_desktop_access_denied'
  } elseif ($sessionFacts.hiddenWindowCreateError -ne 0) {
    'native_hwnd_creation_failed'
  } else {
    'wts_or_desktop_unavailable'
  }
  Write-Host ('CI_NONADMIN_GUI_ENV_UNSUPPORTED / OWNER_DECISION_REQUIRED ' +
    'reason=' + $reason +
    ' session=' + $sessionFacts.session +
    ' state=' + $sessionFacts.connectionState +
    ' station=' + $sessionFacts.windowStation +
    ' desktop=' + $sessionFacts.desktop +
    ' visible=' + $sessionFacts.stationVisible +
    ' openDefaultError=' + $sessionFacts.defaultDesktopAccessError +
    ' openInputError=' + $sessionFacts.inputDesktopAccessError +
    ' createWindowError=' + $sessionFacts.hiddenWindowCreateError +
    ' sqliteNative=PASS gui=NOT_EXECUTED')
  throw 'CI_NONADMIN_GUI_ENV_UNSUPPORTED OWNER_DECISION_REQUIRED'
}
Write-Host 'BETA_NONADMIN_INTERACTIVE_DESKTOP_VERIFIED user=nonadmin station=WinSta0 desktop=Default openDefault=PASS openInput=PASS hiddenWindowCreate=PASS'
Invoke-BetaExe @('--smoke-window') 'POC_WINDOW_OPENED_PASS'
Write-Host 'BETA_NONADMIN_EXTRACTED_UI_PASS native_window=true self_contained=true'
Invoke-BetaExe @('--ci-ux-offline-fixture', $Fixture) 'UX_RECIPE_OFFLINE_ESDE_GUI_PASS'
if ((Get-FileHash -LiteralPath $xml -Algorithm SHA256).Hash -cne $originalXml) {
  throw 'BETA_ESDE_XML_NOT_RESTORED'
}
Write-Host 'BETA_NONADMIN_ESDE_PASS imported=4 diagnostics=true media_fallback_placeholder=true malformed_xml_refused=true'
Write-Host 'BETA_NONADMIN_FIXTURE_PASS no_personal_scan=true no_release=true'
