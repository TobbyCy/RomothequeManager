# Windows-only diagnostic probe; never prints names, paths, tokens or credentials.
# Called under the runner and under the disposable non-admin identity.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'BETA_SESSION_PROBE_WINDOWS_REQUIRED' }
if (-not ('RomothequeBetaSessionProbe' -as [type])) {
  $native = @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class RomothequeBetaSessionProbe
{
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] public static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("user32.dll", EntryPoint="GetUserObjectInformationW", SetLastError=true)]
    private static extern bool UserInfo(IntPtr handle, int index, IntPtr data, uint length, out uint needed);
    [DllImport("wtsapi32.dll", EntryPoint="WTSQuerySessionInformationW", SetLastError=true)]
    private static extern bool WtsInfo(IntPtr server, uint session, int kind, out IntPtr data, out uint length);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(IntPtr buffer);
    [DllImport("advapi32.dll", SetLastError=true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError=true)] private static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr buffer, int length, out int required);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    // The labels intentionally allow no personal station or desktop names to escape into CI logs.
    private static string SafeName(IntPtr handle, string expected)
    {
        if (handle == IntPtr.Zero) return "UNAVAILABLE";
        uint needed;
        UserInfo(handle, 2, IntPtr.Zero, 0, out needed); // UOI_NAME
        if (needed == 0 || needed > 512) return "UNAVAILABLE";
        IntPtr buffer = Marshal.AllocHGlobal((int)needed);
        try {
            if (!UserInfo(handle, 2, buffer, needed, out needed)) return "UNAVAILABLE";
            string name = Marshal.PtrToStringUni(buffer) ?? "";
            return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase) ? expected : "OTHER";
        } finally { Marshal.FreeHGlobal(buffer); }
    }
    public static string Station() => SafeName(GetProcessWindowStation(), "WinSta0");
    public static string Desktop() => SafeName(GetThreadDesktop(GetCurrentThreadId()), "Default");
    public static int StationVisible()
    {
        IntPtr h = GetProcessWindowStation();
        if (h == IntPtr.Zero) return -1;
        IntPtr data = Marshal.AllocHGlobal(12);
        try {
            uint needed;
            if (!UserInfo(h, 1, data, 12, out needed)) return -1; // UOI_FLAGS, WSF_VISIBLE = 1
            return (Marshal.ReadInt32(data, 8) & 1) == 1 ? 1 : 0;
        } finally { Marshal.FreeHGlobal(data); }
    }
    // WTSActive = 0; any unknown/error is -1, never silently "interactive".
    public static int SessionState(int session)
    {
        IntPtr data;
        uint length;
        if (!WtsInfo(IntPtr.Zero, (uint)session, 8, out data, out length)) return -1;
        try { return length >= 4 ? Marshal.ReadInt32(data) : -1; }
        finally { if (data != IntPtr.Zero) WTSFreeMemory(data); }
    }
    public static int IntegrityRid()
    {
        IntPtr token;
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008, out token)) return -1;
        try {
            int size;
            GetTokenInformation(token, 25, IntPtr.Zero, 0, out size); // TokenIntegrityLevel
            if (size < IntPtr.Size || size > 8192) return -1;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try {
                if (!GetTokenInformation(token, 25, buffer, size, out size)) return -1;
                IntPtr sid = Marshal.ReadIntPtr(buffer);
                if (sid == IntPtr.Zero) return -1;
                int parts = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                if (parts <= 0) return -1;
                return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(parts - 1)));
            } finally { Marshal.FreeHGlobal(buffer); }
        } finally { CloseHandle(token); }
    }
}
'@
  Add-Type -TypeDefinition $native -ErrorAction Stop
}

function Get-BetaWindowsSessionFacts {
  [CmdletBinding()]
  param([Parameter(Mandatory = $true)][ValidateSet('runner', 'worker')][string]$Role)
  $process = [Diagnostics.Process]::GetCurrentProcess()
  $sid = [int]$process.SessionId
  $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
  $admin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  $integrity = [RomothequeBetaSessionProbe]::IntegrityRid()
  $station = [RomothequeBetaSessionProbe]::Station()
  $desktop = [RomothequeBetaSessionProbe]::Desktop()
  $visible = [RomothequeBetaSessionProbe]::StationVisible()
  $state = [RomothequeBetaSessionProbe]::SessionState($sid)
  $console = [RomothequeBetaSessionProbe]::WTSGetActiveConsoleSessionId()
  $activeConsole = if ($console -eq [uint32]::MaxValue) { -1 } else { [int64]$console }
  $interactive = $state -eq 0 -and $station -ceq 'WinSta0' -and
    $desktop -ceq 'Default' -and $visible -eq 1
  $facts = [pscustomobject]@{
    role = $Role
    pid = [int]$process.Id
    session = $sid
    activeConsole = $activeConsole
    connectionState = $state
    windowStation = $station
    desktop = $desktop
    stationVisible = $visible
    integrityRid = $integrity
    isAdmin = [bool]$admin
    interactive = [bool]$interactive
    tempDefined = -not [string]::IsNullOrWhiteSpace($env:TEMP)
    profileDefined = -not [string]::IsNullOrWhiteSpace($env:USERPROFILE)
  }
  Write-Host ('BETA_WINDOWS_SESSION_DIAG ' + ($facts | ConvertTo-Json -Compress))
  return $facts
}
