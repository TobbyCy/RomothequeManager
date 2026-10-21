Set-StrictMode -Version 2.0
function Get-RmInventory {
    param($Context)
    $files = New-Object 'System.Collections.Generic.List[object]'
    $dirs = New-Object 'System.Collections.Generic.List[string]'
    $byPath = @{}
    $complete = $true
    foreach ($block in @('ROMS','MEDIAS')) {
        $stack = New-Object 'System.Collections.Generic.Stack[string]'
        $stack.Push($block)
        while ($stack.Count -gt 0) {
            $relative = $stack.Pop()
            $native = Get-RmNativePath ($Context.Root+'\'+$relative.Replace('/','\'))
            try {
                $dir = Get-RmFileStat ($Context.Root+'\'+$relative.Replace('/','\'))
                if (($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    Add-RmAnomaly $Context 'REPARSE_POINT_SKIPPED' $relative 'Directory link/junction was not followed.'
                    $complete=$false; continue
                }
                $dirs.Add($relative)
                foreach ($entry in @(Get-RmDirectoryEntries ($Context.Root+'\'+$relative.Replace('/','\')))) {
                    $rp = $relative+'/'+$entry.Name
                    if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                        Add-RmAnomaly $Context 'REPARSE_POINT_SKIPPED' $rp 'File/directory link was not followed.'
                        $complete=$false; continue
                    }
                    if (($entry.Attributes -band [IO.FileAttributes]::Directory) -ne 0) { $stack.Push($rp); continue }
                    try {
                        $ext=$entry.Extension.ToLowerInvariant()
                        $segments=$rp.Split('/')
                        $type='unknown'; $roles=@('unassigned'); $mediaType=$null; $mediaStem=$null
                        if ($block -eq 'MEDIAS') {
                            if ($segments -contains 'legacy') { $type='legacy'; $roles=@('legacy') }
                            elseif ($segments.Length -ge 4 -and $segments[2] -eq 'metadata') { $type='metadata'; $roles=@('metadata') }
                            elseif ($segments.Length -ge 4 -and $Context.Config.mediaTypes -contains $segments[2]) {
                                $type='media'; $roles=@('media'); $mediaType=$segments[2]
                                $tail=($segments[3..($segments.Length-1)] -join '/')
                                $mediaStem=$tail.Substring(0,$tail.Length-$ext.Length)
                            }
                        }
                        if ($block -eq 'ROMS' -and $ext -in @('.txt','.md','.log','.ps1','.psm1','.cmd','.bat','.deltaskin','.exe','.dll')) {
                            $type='auxiliary';$roles=@('auxiliary')
                        }
                        $record=[pscustomobject]@{fileId=('file-'+($files.Count+1)); relativePath=$rp;
                            sizeBytes=[long]$entry.Length; modifiedAtUtc=$entry.LastWriteTimeUtc.ToString('o');
                            extension=$ext; type=$type; roles=$roles; hash=$null;
                            console=$segments[1]; mediaType=$mediaType; mediaStem=$mediaStem}
                        $files.Add($record); $byPath[$rp]=$record
                    } catch {
                        Add-RmAnomaly $Context 'FILE_STAT_ERROR' $rp $_.Exception.Message 'Error'
                        $complete=$false
                    }
                }
            } catch {
                Add-RmAnomaly $Context 'DIRECTORY_READ_ERROR' $relative $_.Exception.Message 'Error'
                $complete=$false
            }
        }
    }
    return [pscustomobject]@{files=$files.ToArray(); directories=$dirs.ToArray(); byPath=$byPath; complete=$complete}
}
Export-ModuleMember -Function Get-RmInventory
