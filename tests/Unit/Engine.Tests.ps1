# Pure synthetic unit tests: no scanner, no physical ROM, no transaction commit.
$bootstrap = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'Initialize.ps1'
. $bootstrap
Describe 'Engine v0.3.2 Rev.3 - synthetic unit validation' {
    Context 'Configuration' {
        It 'reports engine version 0.3.2' { (Get-RmConfiguration).applicationVersion | Should -Be '0.3.2' }
        It 'uses catalogue schema version 2.2.0' { (Get-RmConfiguration).schemaVersion | Should -Be '2.2.0' }
        It 'keeps associations read-only by default' { (Get-RmConfiguration).associationsReadOnly | Should -Be $true }
        It 'accepts GBA in extension map' { (Get-RmConfiguration).extensions.GBA | Should -Contain '.gba' }
        It 'has required PS1 cue extension' { (Get-RmConfiguration).extensions.PS1 | Should -Contain '.cue' }
    }
    Context 'Identity and normalization (no registry access)' {
        It 'normalizes separators and casing' { Get-RmNormalizedLocator 'ROMS\GBA\TEST.GBA' | Should -Be 'roms/gba/test.gba' }
        It 'normalizes Unicode to NFC' { Get-RmNormalizedLocator ("CAF$([char]0x0065)$([char]0x0301).GBA") | Should -Be ("caf$([char]0x00E9).gba") }
        It 'accepts a valid UUID' { { Assert-RmUuid '12345678-1234-1234-1234-123456789abc' } | Should -Not -Throw }
        It 'rejects an invalid UUID' { { Assert-RmUuid 'INVALID' } | Should -Throw }
        It 'rejects an empty UUID' { { Assert-RmUuid '00000000-0000-0000-0000-000000000000' } | Should -Throw }
    }
    Context 'Schema rejection and safe relations' {
        It 'accepts known required fields' { { Assert-RmFields ([pscustomobject]@{one=1}) @('one') @('one') } | Should -Not -Throw }
        It 'rejects unknown fields' { { Assert-RmFields ([pscustomobject]@{one=1;rogue=2}) @('one') @('one') } | Should -Throw }
        It 'rejects missing required fields' { { Assert-RmFields ([pscustomobject]@{one=1}) @('one','two') @('two') } | Should -Throw }
        It 'accepts valid synthetic game target' { { Assert-RmTargetShape ([pscustomobject]@{scope='game';gameId='12345678-1234-1234-1234-123456789abc'}) } | Should -Not -Throw }
        It 'rejects an invalid target scope' { { Assert-RmTargetShape ([pscustomobject]@{scope='garbage';gameId='12345678-1234-1234-1234-123456789abc'}) } | Should -Throw }
        It 'preserves unmerged identity when rules are empty' { Resolve-RmMergeTarget 'game-a' @() 'source' 'target' | Should -Be 'game-a' }
        It 'refuses cyclic relations' {
            $rules = @([pscustomobject]@{source='game-a';target='game-b'},[pscustomobject]@{source='game-b';target='game-a'})
            { Resolve-RmMergeTarget 'game-a' $rules 'source' 'target' } | Should -Throw
        }
    }
}