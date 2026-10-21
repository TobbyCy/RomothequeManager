Set-StrictMode -Version 2.0
function Get-RmConfiguration {
    return [pscustomobject]@{
        applicationVersion='0.3.2'; schemaVersion='2.2.0'; associationsReadOnly=$true
        mediaTypes=@('3dboxes','backcovers','covers','fanart','manuals','marquees','miximages','physicalmedia','screenshots','titlescreens','videos')
        extensions=@{
            GB=@('.gb','.zip','.7z'); GBC=@('.gbc','.gb','.zip','.7z'); GBA=@('.gba','.zip','.7z')
            GameCube=@('.iso','.gcm','.rvz','.ciso'); N64=@('.z64','.n64','.v64','.zip','.7z')
            PS1=@('.cue','.chd','.m3u','.pbp','.iso'); PS2=@('.iso','.chd','.cso','.zso','.bin','.m3u')
            PS3=@('.iso'); PSP=@('.iso','.cso','.chd','.pbp'); SNES=@('.sfc','.smc','.zip','.7z'); Xbox=@('.iso','.xiso')
        }
    }
}
Export-ModuleMember -Function Get-RmConfiguration
