# E1 fixtures are synthetic. No ROM, ES-DE, catalogue or transaction files are accessed.
$exportModule = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'Export\ExportE1.psm1'
Import-Module -Name $exportModule -Force

function New-E1SyntheticCatalogue {
    $g1=[pscustomobject]@{sourceEntry=[pscustomobject]@{metadataEntryId='game-1';entryKind='game'};classification='EXACT'}
    $g2=[pscustomobject]@{sourceEntry=[pscustomobject]@{metadataEntryId='game-2';entryKind='game'};classification='DUPLICATE_METADATA'}
    $missing=[pscustomobject]@{sourceEntry=[pscustomobject]@{metadataEntryId='game-missing';entryKind='game'};classification='MISSING_ROM';status='unresolved';candidates=@()}
    $ambiguous=[pscustomobject]@{sourceEntry=[pscustomobject]@{metadataEntryId='game-ambiguous';entryKind='game'};classification='AMBIGUOUS';status='unresolved';candidates=@([pscustomobject]@{romUnitId='unit-a'},[pscustomobject]@{romUnitId='unit-b'})}
    return [pscustomobject]@{
        statistics=[pscustomobject]@{metadataResolved=2;metadataUnresolved=2;metadataGameEntries=4}
        manualMetadataResolvedIds=@()
        metadataIndex=@([pscustomobject]@{metadataEntryId='game-1';entryKind='game'},[pscustomobject]@{metadataEntryId='game-2';entryKind='game'},[pscustomobject]@{metadataEntryId='folder-infamous';entryKind='folder'},[pscustomobject]@{metadataEntryId='folder-infamous2';entryKind='folder'},[pscustomobject]@{metadataEntryId='game-missing';entryKind='game'},[pscustomobject]@{metadataEntryId='game-ambiguous';entryKind='game'})
        metadataReconciliation=[pscustomobject]@{resolved=@($g1,$g2);unresolved=@($missing,$ambiguous);folders=@()}
    }
}
function New-E1SyntheticLinks {
    return @(
        [pscustomobject]@{metadataEntryId='game-1';scope='unit';romUnitId='unit-a'},
        [pscustomobject]@{metadataEntryId='game-2';scope='unit';romUnitId='unit-a'},
        [pscustomobject]@{metadataEntryId='folder-infamous';scope='unit';romUnitId='unit-a'},
        [pscustomobject]@{metadataEntryId='folder-infamous2';scope='unit';romUnitId='unit-b'},
        [pscustomobject]@{metadataEntryId='game-missing';scope='unit';romUnitId='unit-c'}
    )
}

Describe 'Export E1 metadata projection (synthetic)' {
    It 'indexes only resolved game entries' {
        $ids=Get-RmE1GameResolutionIndex (New-E1SyntheticCatalogue)
        $ids.Count | Should -Be 2
        $ids.ContainsKey('game-1') | Should -Be $true
        $ids.ContainsKey('folder-infamous') | Should -Be $false
    }
    It 'keeps folder kinds in source index without making them games' {
        $kinds=Get-RmE1GameMetadataIndex (New-E1SyntheticCatalogue)
        $kinds['folder-infamous'] | Should -Be 'folder'
        $kinds['folder-infamous2'] | Should -Be 'folder'
        $kinds.Count | Should -Be 6
    }
    It 'filters both PS3 folder source records and unresolved game records' {
        $cat=New-E1SyntheticCatalogue
        $selected=Select-RmE1ConfirmedGameLinks (New-E1SyntheticLinks) (Get-RmE1GameResolutionIndex $cat) (Get-RmE1GameMetadataIndex $cat)
        @($selected).Count | Should -Be 2
        (@($selected|ForEach-Object{$_.metadataEntryId}|Sort-Object -Unique) -join ',') | Should -Be 'game-1,game-2'
    }
    It 'preserves two distinct metadata entries mapped to one unit' {
        $cat=New-E1SyntheticCatalogue
        $selected=Select-RmE1ConfirmedGameLinks (New-E1SyntheticLinks) (Get-RmE1GameResolutionIndex $cat) (Get-RmE1GameMetadataIndex $cat)
        $projection=Get-RmE1UnitMetadataProjection -Sources $selected -VariantId 'variant-a' -RomUnitId 'unit-a' -VariantUnitCount 1
        $projection.metadataState | Should -Be 'ASSOCIATED'
        @($projection.confirmedMetadataEntryIds).Count | Should -Be 2
    }
    It 'does not assign a game metadata source to a different unit' {
        $cat=New-E1SyntheticCatalogue
        $selected=Select-RmE1ConfirmedGameLinks (New-E1SyntheticLinks) (Get-RmE1GameResolutionIndex $cat) (Get-RmE1GameMetadataIndex $cat)
        $projection=Get-RmE1UnitMetadataProjection -Sources $selected -VariantId 'variant-b' -RomUnitId 'unit-b' -VariantUnitCount 1
        $projection.metadataState | Should -Be 'ROM_ONLY'
    }
    It 'distinguishes a broad game link from unit-specific metadata' {
        $links=@([pscustomobject]@{metadataEntryId='game-1';scope='game'})
        $projection=Get-RmE1UnitMetadataProjection -Sources $links -VariantId 'variant-a' -RomUnitId 'unit-a' -VariantUnitCount 1
        $projection.metadataState | Should -Be 'BROAD_SCOPE_METADATA'
        @($projection.widerScopeMetadataEntryIds).Count | Should -Be 1
    }
    It 'does not classify a multi-unit variant link as unit-specific' {
        $links=@([pscustomobject]@{metadataEntryId='game-1';scope='variant';variantId='variant-a'})
        $projection=Get-RmE1UnitMetadataProjection -Sources $links -VariantId 'variant-a' -RomUnitId 'unit-a' -VariantUnitCount 2
        $projection.metadataState | Should -Be 'BROAD_SCOPE_METADATA'
    }
    It 'classifies a single-unit variant association as associated' {
        $links=@([pscustomobject]@{metadataEntryId='game-1';scope='variant';variantId='variant-a'})
        $projection=Get-RmE1UnitMetadataProjection -Sources $links -VariantId 'variant-a' -RomUnitId 'unit-a' -VariantUnitCount 1
        $projection.metadataState | Should -Be 'ASSOCIATED'
    }
    It 'derives ESDE_ONLY and AMBIGUOUS dynamically' {
        $exceptions=Get-RmE1ExportExceptions (New-E1SyntheticCatalogue)
        @($exceptions).Count | Should -Be 2
        @($exceptions|Where-Object{$_.exportException -eq 'ESDE_ONLY'}).Count | Should -Be 1
        @($exceptions|Where-Object{$_.exportException -eq 'AMBIGUOUS'}).Count | Should -Be 1
        @($exceptions|Where-Object{$_.exportException -eq 'AMBIGUOUS'})[0].candidateCount | Should -Be 2
    }
    It 'does not merge two candidate units in an ambiguous exception' {
        $exceptions=Get-RmE1ExportExceptions (New-E1SyntheticCatalogue)
        $ambiguous=@($exceptions|Where-Object{$_.exportException -eq 'AMBIGUOUS'})[0]
        @($ambiguous.candidateRomUnitIds).Count | Should -Be 2
        $ambiguous.candidateRomUnitIds | Should -Contain 'unit-a'
        $ambiguous.candidateRomUnitIds | Should -Contain 'unit-b'
    }
    It 'rejects contradictory resolved folder metadata' {
        $cat=New-E1SyntheticCatalogue
        $cat.metadataReconciliation.resolved=@([pscustomobject]@{sourceEntry=[pscustomobject]@{metadataEntryId='folder-infamous';entryKind='folder'}})
        { Get-RmE1GameResolutionIndex $cat } | Should -Throw
    }
    It 'rejects a contradictory source kind in the index' {
        $cat=New-E1SyntheticCatalogue
        $cat.metadataIndex[0].entryKind='folder'
        { Select-RmE1ConfirmedGameLinks (New-E1SyntheticLinks) (Get-RmE1GameResolutionIndex $cat) (Get-RmE1GameMetadataIndex $cat) } | Should -Throw
    }
    It 'rejects duplicate resolved IDs' {
        $cat=New-E1SyntheticCatalogue
        $cat.metadataReconciliation.resolved=@($cat.metadataReconciliation.resolved)+@($cat.metadataReconciliation.resolved[0])
        { Get-RmE1GameResolutionIndex $cat } | Should -Throw
    }
    It 'rejects resolved metadata count drift' {
        $cat=New-E1SyntheticCatalogue
        $cat.statistics.metadataResolved=3
        { Get-RmE1GameResolutionIndex $cat } | Should -Throw
    }
    It 'does not drop one of 442 synthetic duplicate metadata links' {
        $links=@(1..442|ForEach-Object { [pscustomobject]@{metadataEntryId=('dup-{0:D3}' -f $_);scope='unit';romUnitId='u'} })
        $resolved=@{};$kinds=@{}
        foreach ($link in $links) {$resolved[$link.metadataEntryId]=$true;$kinds[$link.metadataEntryId]='game'}
        $selected=Select-RmE1ConfirmedGameLinks $links $resolved $kinds
        $projected=Get-RmE1UnitMetadataProjection -Sources $selected -VariantId 'v' -RomUnitId 'u' -VariantUnitCount 1
        @($projected.confirmedMetadataEntryIds).Count | Should -Be 442
    }
    It 'preserves full coverage of resolved references including wider-scope links' {
        $units=@([pscustomobject]@{confirmedMetadataEntryIds=@('game-1');widerScopeMetadataEntryIds=@('game-2')})
        { Assert-RmE1GameCoverage @{ 'game-1'=$true; 'game-2'=$true } $units } | Should -Not -Throw
    }
    It 'fails coverage on an erroneously included folder ID' {
        $units=@([pscustomobject]@{confirmedMetadataEntryIds=@('game-1','folder-infamous');widerScopeMetadataEntryIds=@()})
        { Assert-RmE1GameCoverage @{ 'game-1'=$true } $units } | Should -Throw
    }
    It 'fails coverage on missing resolved game linkage' {
        $units=@([pscustomobject]@{confirmedMetadataEntryIds=@('game-1');widerScopeMetadataEntryIds=@()})
        { Assert-RmE1GameCoverage @{ 'game-1'=$true; 'game-2'=$true } $units } | Should -Throw
    }
    It 'accepts safe relative source paths' {
        Test-RmE1SafeRelativeSourcePath 'ROMS/GBA/fixture.gba' | Should -Be $true
        Test-RmE1SafeRelativeSourcePath 'ROMS/PS1/CHD/fixture.chd' | Should -Be $true
    }
    It 'rejects traversal and drive escapes' {
        Test-RmE1SafeRelativeSourcePath 'ROMS/../outside.gba' | Should -Be $false
        Test-RmE1SafeRelativeSourcePath 'C:/secret.gba' | Should -Be $false
        Test-RmE1SafeRelativeSourcePath 'ROMS/GBA/./abc.gba' | Should -Be $false
    }
    It 'rejects absolute, backslash and ADS-style paths' {
        Test-RmE1SafeRelativeSourcePath '/ROMS/GBA/abc.gba' | Should -Be $false
        Test-RmE1SafeRelativeSourcePath 'ROMS\GBA\abc.gba' | Should -Be $false
        Test-RmE1SafeRelativeSourcePath 'ROMS/GBA/abc.gba:secret' | Should -Be $false
    }
    It 'rejects case / Unicode path collisions' {
        $files=@([pscustomobject]@{relativePath='ROMS/GBA/CAFE.gba'},[pscustomobject]@{relativePath='ROMS/GBA/cafe.gba'})
        { Assert-RmE1UniquePhysicalPaths $files } | Should -Throw
    }
    It 'rejects stale source snapshot fingerprints' {
        { Assert-RmE1SnapshotFingerprint ('a'*64) ('b'*64) } | Should -Throw
    }
    It 'accepts matching source snapshot fingerprints' {
        { Assert-RmE1SnapshotFingerprint ('a'*64) ('a'*64) } | Should -Not -Throw
    }
    It 'does not create a physical unit for ESDE_ONLY' {
        $cat=New-E1SyntheticCatalogue
        $ids=Get-RmE1GameResolutionIndex $cat
        $selected=Select-RmE1ConfirmedGameLinks @([pscustomobject]@{metadataEntryId='game-missing';scope='unit';romUnitId='phantom'}) $ids (Get-RmE1GameMetadataIndex $cat)
        @($selected).Count | Should -Be 0
    }
}
