Set-StrictMode -Version 2.0


# Windows PowerShell 5.1 runs on .NET Framework and can still enforce MAX_PATH
# even when paths use the extended \\?\ syntax. Use the Win32 Unicode APIs only
# for extended paths; ordinary paths keep the normal System.IO behavior.
if (-not ('Rm032.LongPathIO' -as [type])) {
Add-Type -ReferencedAssemblies 'System.dll','System.Core.dll' -TypeDefinition @'
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Rm032 {
 public sealed class RmFsEntry {
  public string Name { get; set; }
  public FileAttributes Attributes { get; set; }
  public long Length { get; set; }
  public DateTime LastWriteTimeUtc { get; set; }
  public string Extension { get; set; }
 }
 public static class LongPathIO {
  const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;
  const uint GENERIC_READ = 0x80000000;
  const uint GENERIC_WRITE = 0x40000000;
  const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
  const uint MOVEFILE_REPLACE_EXISTING = 0x00000001;
  const uint MOVEFILE_WRITE_THROUGH = 0x00000008;
  static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

  [StructLayout(LayoutKind.Sequential)]
  struct WIN32_FILE_ATTRIBUTE_DATA {
   public FileAttributes dwFileAttributes;
   public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
   public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
   public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
   public uint nFileSizeHigh;
   public uint nFileSizeLow;
  }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
  struct WIN32_FIND_DATA {
   public FileAttributes dwFileAttributes;
   public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
   public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
   public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
   public uint nFileSizeHigh;
   public uint nFileSizeLow;
   public uint dwReserved0;
   public uint dwReserved1;
   [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string cFileName;
   [MarshalAs(UnmanagedType.ByValTStr, SizeConst=14)] public string cAlternateFileName;
  }

  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern uint GetFileAttributesW(string lpFileName);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool GetFileAttributesExW(string lpFileName, int fInfoLevelId, out WIN32_FILE_ATTRIBUTE_DATA data);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool CreateDirectoryW(string lpPathName, IntPtr lpSecurityAttributes);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool RemoveDirectoryW(string lpPathName);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool DeleteFileW(string lpFileName);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool CopyFileW(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, uint dwFlags);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool ReplaceFileW(string lpReplacedFileName, string lpReplacementFileName, string lpBackupFileName, uint dwReplaceFlags, IntPtr lpExclude, IntPtr lpReserved);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATA lpFindFileData);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);
  [DllImport("kernel32.dll", SetLastError=true)]
  static extern bool FindClose(IntPtr hFindFile);

  static void ThrowLast(string operation, string path) {
   int error = Marshal.GetLastWin32Error();
   throw new IOException(operation + " failed for " + path + ": " + new Win32Exception(error).Message, new Win32Exception(error));
  }
  static long FileSize(uint high, uint low) { return ((long)high << 32) | (long)low; }
  static DateTime FileTime(System.Runtime.InteropServices.ComTypes.FILETIME value) {
   long ticks = ((long)(uint)value.dwHighDateTime << 32) | (uint)value.dwLowDateTime;
   return DateTime.FromFileTimeUtc(ticks);
  }
  public static bool FileExists(string path) {
   uint a=GetFileAttributesW(path); return a!=INVALID_FILE_ATTRIBUTES && (a & (uint)FileAttributes.Directory)==0;
  }
  public static bool DirectoryExists(string path) {
   uint a=GetFileAttributesW(path); return a!=INVALID_FILE_ATTRIBUTES && (a & (uint)FileAttributes.Directory)!=0;
  }
  public static void CreateDirectorySingle(string path) {
   if (DirectoryExists(path)) return;
   if (!CreateDirectoryW(path,IntPtr.Zero)) { int e=Marshal.GetLastWin32Error(); if(e!=183) ThrowLast("CreateDirectoryW",path); }
  }
  public static void RemoveDirectorySingle(string path) { if(DirectoryExists(path) && !RemoveDirectoryW(path)) ThrowLast("RemoveDirectoryW",path); }
  public static void DeleteFile(string path) { if(FileExists(path) && !DeleteFileW(path)) ThrowLast("DeleteFileW",path); }
  public static void CopyFile(string source,string destination,bool overwrite) { if(!CopyFileW(source,destination,!overwrite)) ThrowLast("CopyFileW",destination); }
  public static void MoveFile(string source,string destination,bool replace) {
   uint flags=MOVEFILE_WRITE_THROUGH | (replace?MOVEFILE_REPLACE_EXISTING:0u);
   if(!MoveFileExW(source,destination,flags)) ThrowLast("MoveFileExW",destination);
  }
  public static void ReplaceFile(string target,string replacement,string backup) {
   if(!ReplaceFileW(target,replacement,backup,0,IntPtr.Zero,IntPtr.Zero)) ThrowLast("ReplaceFileW",target);
  }
  public static FileStream OpenFile(string path,int modeValue,int accessValue,int shareValue,bool writeThrough) {
   FileMode mode=(FileMode)modeValue; FileAccess access=(FileAccess)accessValue;
   uint desired=0; if((access & FileAccess.Read)!=0) desired|=GENERIC_READ; if((access & FileAccess.Write)!=0) desired|=GENERIC_WRITE;
   uint disposition;
   switch(mode) {
    case FileMode.CreateNew: disposition=1; break;
    case FileMode.Create: disposition=2; break;
    case FileMode.Open: disposition=3; break;
    case FileMode.OpenOrCreate: disposition=4; break;
    case FileMode.Truncate: disposition=5; break;
    case FileMode.Append: disposition=4; break;
    default: throw new ArgumentOutOfRangeException("modeValue");
   }
   uint flags=0x00000080u | (writeThrough?FILE_FLAG_WRITE_THROUGH:0u);
   SafeFileHandle h=CreateFileW(path,desired,(uint)shareValue,IntPtr.Zero,disposition,flags,IntPtr.Zero);
   if(h==null || h.IsInvalid){ if(h!=null) h.Dispose(); ThrowLast("CreateFileW",path); }
   FileStream stream=new FileStream(h,access,65536,false);
   if(mode==FileMode.Append) stream.Seek(0,SeekOrigin.End);
   return stream;
  }
  public static RmFsEntry Stat(string path) {
   WIN32_FILE_ATTRIBUTE_DATA d; if(!GetFileAttributesExW(path,0,out d)) ThrowLast("GetFileAttributesExW",path);
   string trimmed=path.TrimEnd('\\'); int slash=trimmed.LastIndexOf('\\'); string name=slash>=0?trimmed.Substring(slash+1):trimmed; int dot=name.LastIndexOf('.'); string ext=(dot>=0?name.Substring(dot):String.Empty); return new RmFsEntry { Name=name, Attributes=d.dwFileAttributes, Length=FileSize(d.nFileSizeHigh,d.nFileSizeLow), LastWriteTimeUtc=FileTime(d.ftLastWriteTime), Extension=ext };
  }
  public static RmFsEntry[] Enumerate(string directory) {
   string pattern=directory.TrimEnd('\\')+"\\*"; WIN32_FIND_DATA d; IntPtr h=FindFirstFileW(pattern,out d);
   if(h==INVALID_HANDLE_VALUE){ int e=Marshal.GetLastWin32Error(); if(e==2) return new RmFsEntry[0]; ThrowLast("FindFirstFileW",directory); }
   var list=new List<RmFsEntry>();
   try {
    do {
     if(d.cFileName=="." || d.cFileName=="..") continue;
     list.Add(new RmFsEntry { Name=d.cFileName, Attributes=d.dwFileAttributes, Length=FileSize(d.nFileSizeHigh,d.nFileSizeLow), LastWriteTimeUtc=FileTime(d.ftLastWriteTime), Extension=Path.GetExtension(d.cFileName) });
    } while(FindNextFileW(h,out d));
    int e=Marshal.GetLastWin32Error(); if(e!=18) throw new IOException("FindNextFileW failed: "+new Win32Exception(e).Message);
   } finally { FindClose(h); }
   return list.ToArray();
  }
 }
}
'@
}

function ConvertTo-RmAbsoluteWindowsPath {
    param([Parameter(Mandatory=$true)][string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Path is empty.' }

    $p = $Path.Replace('/', '\')
    if ($p.StartsWith('\\?\UNC\')) { $p = '\\' + $p.Substring(8) }
    elseif ($p.StartsWith('\\?\')) { $p = $p.Substring(4) }

    # Resolve relative paths without calling Path.GetFullPath on the final long path.
    if ($p -notmatch '^[A-Za-z]:\\' -and !$p.StartsWith('\\')) {
        if ($p -match '^[A-Za-z]:[^\\]') { throw 'Drive-relative paths are refused.' }
        $location = Get-Location
        if ($null -eq $location.Provider -or $location.Provider.Name -ne 'FileSystem') { throw 'Relative path requires FileSystem provider.' }
        $base = [string]$location.Path
        if ($p.StartsWith('\')) {
            if ($base -notmatch '^[A-Za-z]:\\') { throw 'Root-relative path requires a drive-based current location.' }
            $p = $base.Substring(0,2) + $p
        } else {
            $p = $base.TrimEnd('\') + '\' + $p
        }
    }

    $root = $null
    $rest = ''
    if ($p -match '^[A-Za-z]:\\') {
        $root = $p.Substring(0,3)
        $rest = $p.Substring(3)
    } elseif ($p.StartsWith('\\')) {
        $unc = $p.Substring(2).Split('\')
        if ($unc.Length -lt 2 -or [string]::IsNullOrWhiteSpace($unc[0]) -or [string]::IsNullOrWhiteSpace($unc[1])) {
            throw 'Invalid UNC path.'
        }
        $root = '\\' + $unc[0] + '\' + $unc[1] + '\'
        if ($unc.Length -gt 2) { $rest = ($unc[2..($unc.Length-1)] -join '\') }
    } else {
        throw 'Path is not an absolute Windows filesystem path.'
    }

    $parts = New-Object 'System.Collections.Generic.List[string]'
    foreach ($part in $rest.Split('\')) {
        if (!$part -or $part -eq '.') { continue }
        if ($part -eq '..') {
            if ($parts.Count -eq 0) { throw 'Path escapes filesystem root.' }
            $parts.RemoveAt($parts.Count - 1)
            continue
        }
        if ($part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $part.EndsWith('.') -or $part.EndsWith(' ')) {
            throw 'Invalid path component.'
        }
        $parts.Add($part)
    }

    if ($parts.Count -eq 0) { return $root }
    return $root + ($parts -join '\')
}

function Get-RmNativePath {
    param([Parameter(Mandatory=$true)][string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Path is empty.' }
    if ($Path.StartsWith('\\?\')) { return $Path }

    $full = ConvertTo-RmAbsoluteWindowsPath $Path

    # Keep ordinary paths ordinary. Extended syntax is used only when the
    # conservative Win32 directory limit would otherwise be crossed.
    if ($full.Length -lt 248) { return $full }
    if ($full.StartsWith('\\')) { return '\\?\UNC\' + $full.Substring(2) }
    return '\\?\' + $full
}

function Get-RmFullPath {
    param([Parameter(Mandatory=$true)][string]$Path)
    $full = ConvertTo-RmAbsoluteWindowsPath $Path
    if ($full -match '^[A-Za-z]:\\$') { return $full }
    if ($full -match '^\\\\[^\\]+\\[^\\]+\\$') { return $full.TrimEnd('\') }
    return $full.TrimEnd('\')
}

function Get-RmParentPath {
    param([Parameter(Mandatory=$true)][string]$Path)
    $full = Get-RmFullPath $Path
    if ($full -match '^[A-Za-z]:\\$') { return $null }
    if ($full -match '^\\\\[^\\]+\\[^\\]+$') { return $null }

    $index = $full.LastIndexOf('\')
    if ($index -lt 0) { return $null }
    if ($index -eq 2 -and $full.Length -ge 3 -and $full[1] -eq ':') { return $full.Substring(0,3) }
    if ($index -eq 1 -and $full.StartsWith('\\')) { return $null }
    return $full.Substring(0,$index)
}


function Test-RmFileExists {
    param([Parameter(Mandatory=$true)][string]$Path)
    $native=Get-RmNativePath $Path
    if(!$native.StartsWith('\\?\')){return [IO.File]::Exists($native)}
    return [Rm032.LongPathIO]::FileExists($native)
}
function Test-RmDirectoryExists {
    param([Parameter(Mandatory=$true)][string]$Path)
    $native=Get-RmNativePath $Path
    if(!$native.StartsWith('\\?\')){return [IO.Directory]::Exists($native)}
    return [Rm032.LongPathIO]::DirectoryExists($native)
}
function New-RmDirectory {
    param([Parameter(Mandatory=$true)][string]$Path)
    $full=Get-RmFullPath $Path
    if(Test-RmDirectoryExists $full){return}
    $missing=New-Object 'System.Collections.Generic.List[string]'
    $cursor=$full
    while($cursor -and !(Test-RmDirectoryExists $cursor)){
        $missing.Add($cursor)
        $parent=Get-RmParentPath $cursor
        if(!$parent -or $parent -eq $cursor){break}
        $cursor=$parent
    }
    for($i=$missing.Count-1;$i -ge 0;$i--){
        $candidate=$missing[$i];$native=Get-RmNativePath $candidate
        if($native.StartsWith('\\?\')){[Rm032.LongPathIO]::CreateDirectorySingle($native)}
        else{[IO.Directory]::CreateDirectory($native)|Out-Null}
    }
}
function Open-RmFile {
    param([Parameter(Mandatory=$true)][string]$Path,[IO.FileMode]$Mode=[IO.FileMode]::Open,[IO.FileAccess]$Access=[IO.FileAccess]::Read,[IO.FileShare]$Share=[IO.FileShare]::Read,[switch]$WriteThrough)
    $native=Get-RmNativePath $Path
    if(!$native.StartsWith('\\?\')){
        if($WriteThrough){return New-Object IO.FileStream($native,$Mode,$Access,$Share,65536,[IO.FileOptions]::WriteThrough)}
        return [IO.File]::Open($native,$Mode,$Access,$Share)
    }
    return [Rm032.LongPathIO]::OpenFile($native,[int]$Mode,[int]$Access,[int]$Share,[bool]$WriteThrough)
}
function Read-RmAllText {
    param([Parameter(Mandatory=$true)][string]$Path,[Text.Encoding]$Encoding=[Text.Encoding]::UTF8)
    $s=Open-RmFile $Path ([IO.FileMode]::Open) ([IO.FileAccess]::Read) ([IO.FileShare]::Read)
    try{$r=New-Object IO.StreamReader($s,$Encoding,$true,65536,$true);try{return $r.ReadToEnd()}finally{$r.Dispose()}}finally{$s.Dispose()}
}
function Read-RmAllBytes {
    param([Parameter(Mandatory=$true)][string]$Path)
    $s=Open-RmFile $Path ([IO.FileMode]::Open) ([IO.FileAccess]::Read) ([IO.FileShare]::Read)
    try{$m=New-Object IO.MemoryStream;try{$s.CopyTo($m);return $m.ToArray()}finally{$m.Dispose()}}finally{$s.Dispose()}
}
function Write-RmAllText {
    param([Parameter(Mandatory=$true)][string]$Path,[AllowEmptyString()][string]$Text,[Text.Encoding]$Encoding=(New-Object Text.UTF8Encoding($false)))
    $parent=Get-RmParentPath $Path;if($parent){New-RmDirectory $parent}
    $s=Open-RmFile $Path ([IO.FileMode]::Create) ([IO.FileAccess]::Write) ([IO.FileShare]::None)
    try{$w=New-Object IO.StreamWriter($s,$Encoding,65536,$true);try{$w.Write($Text);$w.Flush()}finally{$w.Dispose()}}finally{$s.Dispose()}
}
function Write-RmAllLines {
    param([Parameter(Mandatory=$true)][string]$Path,[string[]]$Lines,[Text.Encoding]$Encoding=(New-Object Text.UTF8Encoding($false)))
    $parent=Get-RmParentPath $Path;if($parent){New-RmDirectory $parent}
    $s=Open-RmFile $Path ([IO.FileMode]::Create) ([IO.FileAccess]::Write) ([IO.FileShare]::None)
    try{$w=New-Object IO.StreamWriter($s,$Encoding,65536,$true);try{foreach($line in $Lines){$w.WriteLine($line)};$w.Flush()}finally{$w.Dispose()}}finally{$s.Dispose()}
}
function Copy-RmFile {
    param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination,[switch]$Overwrite)
    $parent=Get-RmParentPath $Destination;if($parent){New-RmDirectory $parent}
    $src=Get-RmNativePath $Source;$dst=Get-RmNativePath $Destination
    if(!$src.StartsWith('\\?\') -and !$dst.StartsWith('\\?\')){[IO.File]::Copy($src,$dst,[bool]$Overwrite);return}
    [Rm032.LongPathIO]::CopyFile($src,$dst,[bool]$Overwrite)
}
function Move-RmFile {
    param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination,[switch]$Replace)
    $parent=Get-RmParentPath $Destination;if($parent){New-RmDirectory $parent}
    $src=Get-RmNativePath $Source;$dst=Get-RmNativePath $Destination
    if(!$src.StartsWith('\\?\') -and !$dst.StartsWith('\\?\')){
        if($Replace -and [IO.File]::Exists($dst)){[IO.File]::Delete($dst)}
        [IO.File]::Move($src,$dst);return
    }
    [Rm032.LongPathIO]::MoveFile($src,$dst,[bool]$Replace)
}
function Replace-RmFile {
    param([Parameter(Mandatory=$true)][string]$Target,[Parameter(Mandatory=$true)][string]$Replacement,[Parameter(Mandatory=$true)][string]$Backup)
    $targetNative=Get-RmNativePath $Target;$replacementNative=Get-RmNativePath $Replacement;$backupNative=Get-RmNativePath $Backup
    if(!$targetNative.StartsWith('\\?\') -and !$replacementNative.StartsWith('\\?\') -and !$backupNative.StartsWith('\\?\')){[IO.File]::Replace($replacementNative,$targetNative,$backupNative);return}
    [Rm032.LongPathIO]::ReplaceFile($targetNative,$replacementNative,$backupNative)
}
function Remove-RmFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    $native=Get-RmNativePath $Path
    if(!$native.StartsWith('\\?\')){if([IO.File]::Exists($native)){[IO.File]::Delete($native)};return}
    [Rm032.LongPathIO]::DeleteFile($native)
}
function Get-RmFileStat {
    param([Parameter(Mandatory=$true)][string]$Path)
    return [Rm032.LongPathIO]::Stat((Get-RmNativePath $Path))
}
function Get-RmDirectoryEntries {
    param([Parameter(Mandatory=$true)][string]$Path)
    $native=Get-RmNativePath $Path
    if(!$native.StartsWith('\\?\')){return @(New-Object IO.DirectoryInfo($native)|ForEach-Object{$_.GetFileSystemInfos()})}
    return @([Rm032.LongPathIO]::Enumerate($native))
}
function Remove-RmDirectoryTree {
    param([Parameter(Mandatory=$true)][string]$Path)
    if(!(Test-RmDirectoryExists $Path)){return}
    foreach($entry in @(Get-RmDirectoryEntries $Path)){
        $child=(Get-RmFullPath $Path).TrimEnd('\')+'\'+$entry.Name
        if(($entry.Attributes -band [IO.FileAttributes]::Directory) -ne 0){
            if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
                $n=Get-RmNativePath $child;if($n.StartsWith('\\?\')){[Rm032.LongPathIO]::RemoveDirectorySingle($n)}else{[IO.Directory]::Delete($n,$false)}
            }else{Remove-RmDirectoryTree $child}
        }else{Remove-RmFile $child}
    }
    $native=Get-RmNativePath $Path
    if($native.StartsWith('\\?\')){[Rm032.LongPathIO]::RemoveDirectorySingle($native)}else{[IO.Directory]::Delete($native,$false)}
}

function Test-RmContainedPath {
    param([string]$Root, [string]$Path)
    return ($Path.Equals($Root, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($Root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase))
}

function Resolve-RmRelativePath {
    param([string]$Base, [string]$Reference, [string]$Boundary)
    if ([string]::IsNullOrWhiteSpace($Reference)) { throw 'Empty path reference.' }
    $ref = $Reference.Replace('/', '\')
    if ([IO.Path]::IsPathRooted($ref) -or $ref.Contains(':')) { throw 'Absolute/drive path reference refused.' }
    $parts = New-Object 'System.Collections.Generic.List[string]'
    foreach ($part in $Base.TrimEnd('/').Split('/')) { if ($part) { $parts.Add($part) } }
    foreach ($part in $ref.Split('\')) {
        if (!$part -or $part -eq '.') { continue }
        if ($part -eq '..') {
            if ($parts.Count -eq 0) { throw 'Path escapes root.' }
            $parts.RemoveAt($parts.Count - 1)
        } else {
            if ($part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
                $part.EndsWith('.') -or $part.EndsWith(' ')) { throw 'Invalid path component.' }
            $parts.Add($part)
        }
    }
    $result = $parts -join '/'
    if ($Boundary -and !($result.Equals($Boundary,[StringComparison]::OrdinalIgnoreCase) -or
        $result.StartsWith($Boundary.TrimEnd('/')+'/',[StringComparison]::OrdinalIgnoreCase))) {
        throw 'Path escapes permitted boundary.'
    }
    return $result
}

function Get-RmTechnicalKey {
    param([string]$Console, [string]$RelativePath)
    return ($Console + '|' + $RelativePath.Replace('\','/')).ToLowerInvariant()
}

function New-RmId { return [Guid]::NewGuid().ToString('D') }

function Add-RmAnomaly {
    param($Context, [string]$Code, [string]$Path, [string]$Message,
        [string]$Severity='Warning', [string]$Console='', [string]$VariantId='')
    if (!$Console -and $Path -match '^(?:ROMS|MEDIAS)/([^/]+)(?:/|$)') {$Console=$Matches[1]}
    $a = [pscustomobject]@{ anomalyId = New-RmId; code=$Code; severity=$Severity;
        console=$Console; variantId=$VariantId; relativePath=$Path; message=$Message }
    $Context.Anomalies.Add($a)
}

function Read-RmJson {
    param([string]$Path)
    return ((Read-RmAllText $Path ([Text.Encoding]::UTF8)) | ConvertFrom-Json)
}

function Write-RmJsonAtomic {
    param([string]$Path, $Value, [switch]$KeepPrevious)
    $full = Get-RmFullPath $Path
    $native = Get-RmNativePath $full
    $directory = Get-RmParentPath $full
    New-RmDirectory $directory
    $temp = $native + '.' + (New-RmId) + '.tmp'
    try {
        $json = ConvertTo-Json -InputObject $Value -Depth 60
        $null = $json | ConvertFrom-Json
        Write-RmAllText $temp $json (New-Object Text.UTF8Encoding($false))
        if (Test-RmFileExists $full) {
            # PS5.1 coerces a null string parameter to an empty path. Always supply a valid backup.
            $backup = $temp + '.old'
            if ($KeepPrevious) { $backup = $full + '.previous'; if (Test-RmFileExists $backup) { Remove-RmFile $backup } }
            Replace-RmFile $full $temp $backup
            if (!$KeepPrevious -and (Test-RmFileExists $backup)) { Remove-RmFile $backup }
        } else { Move-RmFile $temp $full }
    } finally { if (Test-RmFileExists $temp) { Remove-RmFile $temp } }
}

function Open-RmStateLock {
    param([string]$OutputRoot)
    New-RmDirectory $OutputRoot
    return Open-RmFile ($OutputRoot+'\state.lock') ([IO.FileMode]::OpenOrCreate) ([IO.FileAccess]::ReadWrite) ([IO.FileShare]::None)
}

function Assert-RmOutputPath {
    param([string]$Root, [string]$OutputRoot)
    foreach ($name in @('ROMS','MEDIAS')) {
        if (Test-RmContainedPath ($Root+'\'+$name) $OutputRoot) { throw 'Output inside ROMS/MEDIAS refused.' }
    }
    # Refuse existing reparse-point ancestors, including junctions in OUTILS.
    $cursor = $OutputRoot
    while ($cursor) {
        $native = Get-RmNativePath $cursor
        if (Test-RmDirectoryExists $cursor) {
            $info = Get-RmFileStat $cursor
            if (($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse-point output ancestor refused.' }
        }
        $parent = Get-RmParentPath $cursor
        if ($parent -eq $cursor) { break }; $cursor = $parent
    }
}

Export-ModuleMember -Function *-Rm*
