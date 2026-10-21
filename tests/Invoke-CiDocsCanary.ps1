#Requires -Version 5.1
# Exercise the production gate on invented, disposable Git repositories.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$script:caseCount=0
$script:workspace=$null
$script:fixture=$null

function Invoke-FixtureGit {
    param([string[]]$Arguments)
    $oldPreference=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $output=@(& $script:git -C $script:fixture -c core.autocrlf=false -c commit.gpgsign=false -c ('core.hooksPath='+$script:emptyHooks) -c 'user.name=CI Docs Canary' -c 'user.email=ci-docs@example.invalid' @Arguments 2>&1)
        $code=$LASTEXITCODE
    } finally {$ErrorActionPreference=$oldPreference}
    if($code -ne 0) {throw ('DOCS_CANARY_GIT_FAILED: '+($Arguments -join ' ')+' | exit='+$code+' | '+($output -join ' '))}
    return ($output -join [Environment]::NewLine)
}
function Write-FixtureFile {
    param([string]$Relative, [string]$Text='synthetic change')
    $path=[IO.Path]::GetFullPath((Join-Path $script:fixture $Relative))
    $prefix=[IO.Path]::GetFullPath($script:fixture).TrimEnd('\')+'\'
    if(!$path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) {throw 'DOCS_CANARY_WRITE_OUTSIDE_FIXTURE'}
    $null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path,($Text+[char]10),(New-Object Text.UTF8Encoding($false)))
}
function Commit-Fixture {
    param([string]$Message='Synthetic change')
    $null=Invoke-FixtureGit @('add','--all','--','.')
    $null=Invoke-FixtureGit @('commit','--quiet','--allow-empty','-m',$Message)
    return (Invoke-FixtureGit @('rev-parse','HEAD')).Trim()
}
function Merge-Fixture {
    param([string]$Base, [string]$Head)
    $null=Invoke-FixtureGit @('checkout','--quiet','--detach',$Base)
    $null=Invoke-FixtureGit @('merge','--quiet','--no-ff','-m','Synthetic merge',$Head)
    return (Invoke-FixtureGit @('rev-parse','HEAD')).Trim()
}
function Remove-Fixture {
    param([string]$Path)
    # Only delete a freshly created direct child of our own unique workspace.
    $workspaceFull=[IO.Path]::GetFullPath($script:workspace).TrimEnd('\')
    $targetFull=[IO.Path]::GetFullPath($Path).TrimEnd('\')
    if(![string]::Equals([IO.Path]::GetDirectoryName($targetFull),$workspaceFull,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'DOCS_CANARY_CLEANUP_OUTSIDE_WORKSPACE'
    }
    if(Test-Path -LiteralPath $targetFull) {
        $resolved=(Resolve-Path -LiteralPath $targetFull).Path.TrimEnd('\')
        if(![string]::Equals($resolved,$targetFull,[StringComparison]::OrdinalIgnoreCase) -or
           ((Get-Item -LiteralPath $targetFull -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'DOCS_CANARY_CLEANUP_REDIRECTED'
        }
        Remove-Item -LiteralPath $targetFull -Recurse -Force -ErrorAction Stop
    }
}
function Assert-Gate {
    param([string]$Name,[int]$Expected,[string]$ExpectedText,[hashtable]$Environment,[string]$Csv)
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$script:ps
    $start.WorkingDirectory=$script:productionRoot
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $start.StandardOutputEncoding=New-Object Text.UTF8Encoding($false)
    $start.StandardErrorEncoding=New-Object Text.UTF8Encoding($false)
    if($PSBoundParameters.ContainsKey('Csv')) {
        $start.Arguments='-NoLogo -NoProfile -NonInteractive -File "'+$script:gate+'" -ChangedFilesCsv "'+$Csv+'"'
    } else {
        $start.Arguments='-NoLogo -NoProfile -NonInteractive -File "'+$script:gate+'" -SyntheticRepositoryRoot "'+$script:fixture+'"'
    }
    foreach($key in $Environment.Keys) {$start.EnvironmentVariables[$key]=[string]$Environment[$key]}
    $process=[Diagnostics.Process]::Start($start)
    try {
        $errorTask=$process.StandardError.ReadToEndAsync()
        $output=$process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        $errorText=$errorTask.Result
        $code=$process.ExitCode
    } finally {$process.Dispose()}
    $combined=$output+$errorText
    if($code -ne $Expected -or !$combined.Contains($ExpectedText)) {
        throw ('DOCS_CANARY_VERDICT_MISMATCH: '+$Name+' | expected='+$Expected+' | actual='+$code+' | required='+$ExpectedText+[Environment]::NewLine+$combined)
    }
    if($ExpectedText -eq 'DOCS_SYNC_NOT_APPLICABLE' -and $combined.Contains('DOCS_SYNC_PASS')) {
        throw 'DOCS_CANARY_DISPATCH_FALSE_FULL_PASS'
    }
    $script:caseCount++
    Write-Host ('DOCS_CANARY_CASE_PASS | name='+$Name+' | expected='+$Expected+' | actual='+$code)
}
function Invoke-Scenario {
    param([hashtable]$Case)
    $script:fixture=Join-Path $script:workspace ($Case.Name+'-'+[guid]::NewGuid().ToString('N'))
    if(Test-Path -LiteralPath $script:fixture) {throw 'DOCS_CANARY_FIXTURE_COLLISION'}
    $null=[IO.Directory]::CreateDirectory($script:fixture)
    try {
        $null=Invoke-FixtureGit @('init','--quiet','--initial-branch=main')
        [IO.File]::WriteAllText((Join-Path $script:fixture '.git/docs-sync-synthetic'),'docs-sync-synthetic-v1')
        foreach($path in @('README.md','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md','docs/product-status.md','docs/roadmap.md','docs/developer/workflow.md','Modules/Inventory.psm1','Modules/Removed.psm1','Export/Example.ps1','Preflight/Check.ps1','tests/Unit/Fake.Tests.ps1','.github/workflows/fake.yml','notes/Guide.md')) {
            Write-FixtureFile $path ('# synthetic baseline: '+$path)
        }
        $base=Commit-Fixture 'Synthetic baseline'
        & $Case.Change | Out-Null
        $head=Commit-Fixture
        $checkout=$head
        if($Case.ContainsKey('RootPush')) {
            $null=Invoke-FixtureGit @('checkout','--quiet','--detach',$base)
            $head=$base
            $checkout=$head
        }
        if($Case.ContainsKey('DivergedBase')) {
            $null=Invoke-FixtureGit @('checkout','--quiet','--detach',$base)
            Write-FixtureFile 'Modules/Inventory.psm1' 'base-only change'
            $base=Commit-Fixture 'Synthetic base advance'
            $checkout=Merge-Fixture $base $head
        } elseif($Case.ContainsKey('Merge')) {
            $checkout=Merge-Fixture $base $head
        }
        if($Case.ContainsKey('NonAncestorBefore')) {
            $null=Invoke-FixtureGit @('checkout','--quiet','--detach',$base)
            Write-FixtureFile 'notes/Guide.md' 'unrelated branch change'
            $base=Commit-Fixture 'Synthetic sibling'
            $null=Invoke-FixtureGit @('checkout','--quiet','--detach',$head)
        }
        if($Case.ContainsKey('Rename')) {
            $renameProof=Invoke-FixtureGit @('diff','--name-status','--find-renames',$base,$head,'--')
            if($renameProof -cnotmatch '(?m)^R[0-9]+\s') {throw ('DOCS_CANARY_RENAME_NOT_REAL: '+$Case.Name)}
        }
        if($Case.ContainsKey('NewerOrigin')) {
            $null=Invoke-FixtureGit @('update-ref','refs/remotes/origin/main',$head)
        }
        if($Case.ContainsKey('PostCheckout')) {& $Case.PostCheckout | Out-Null}
        $payloadPath=Join-Path $script:fixture '.git/event.json'
        $environment=@{
            GITHUB_EVENT_NAME=$Case.Event;GITHUB_EVENT_PATH=$payloadPath;
            GITHUB_SHA=$checkout;GITHUB_REF='refs/heads/main';GITHUB_BASE_REF=''
        }
        if($Case.Event -eq 'pull_request') {
            $payload=@{number=37;pull_request=@{base=@{ref='main';sha=$base};head=@{ref='synthetic-source';sha=$head}}}
            $environment.GITHUB_BASE_REF='main'
            $environment.GITHUB_REF='refs/pull/37/head'
            if($checkout -cne $head) {$environment.GITHUB_REF='refs/pull/37/merge'}
        } else {
            $payload=@{ref='refs/heads/main';before=$base;after=$head;forced=$false}
        }
        if($Case.ContainsKey('Adjust')) {& $Case.Adjust | Out-Null}
        [IO.File]::WriteAllText($payloadPath,($payload|ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
        Assert-Gate -Name $Case.Name -Expected $Case.Exit -ExpectedText $Case.Text -Environment $environment
    } finally {Remove-Fixture $script:fixture}
}

try {
    if($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1) {throw 'DOCS_CANARY_WINDOWS_PS51_REQUIRED'}
    $script:ps=Join-Path $PSHOME 'powershell.exe'
    $script:gate=Join-Path $PSScriptRoot 'Invoke-CiDocsSync.ps1'
    $script:productionRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $script:git=(Get-Command git.exe -ErrorAction Stop).Source
    $fixtureBase=if([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)){[IO.Path]::GetTempPath()}else{$env:RUNNER_TEMP}
    $fixtureBase=(Resolve-Path -LiteralPath $fixtureBase).Path
    $script:workspace=Join-Path $fixtureBase ('rm-docs-sync-canary-'+[guid]::NewGuid().ToString('N'))
    if(Test-Path -LiteralPath $script:workspace) {throw 'DOCS_CANARY_WORKSPACE_COLLISION'}
    $null=[IO.Directory]::CreateDirectory($script:workspace)
    $script:emptyHooks=Join-Path $script:workspace 'empty-hooks'
    $null=[IO.Directory]::CreateDirectory($script:emptyHooks)
    $legacy=@(
        @{Name='legacy_code_missing_docs';Paths='Modules/Inventory.psm1';Exit=1;Text='DOCS_SYNC_FAIL'},
        @{Name='legacy_code_documented';Paths='Modules/Inventory.psm1;docs/product-status.md;CHANGELOG.md';Exit=0;Text='mode=explicit-path-list'},
        @{Name='legacy_tests_missing_status';Paths='tests/Unit/Engine.Tests.ps1';Exit=1;Text='DOCS_SYNC_FAIL'},
        @{Name='legacy_tests_documented';Paths='tests/Unit/Engine.Tests.ps1;docs/product-status.md';Exit=0;Text='mode=explicit-path-list'},
        @{Name='legacy_preflight_missing_docs';Paths='Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1';Exit=1;Text='DOCS_SYNC_FAIL'},
        @{Name='legacy_preflight_missing_changelog';Paths='Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1;docs/product-status.md';Exit=1;Text='DOCS_SYNC_FAIL'},
        @{Name='legacy_preflight_missing_status';Paths='Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1;CHANGELOG.md';Exit=1;Text='DOCS_SYNC_FAIL'},
        @{Name='legacy_preflight_documented';Paths='Preflight/Invoke-E1RealLibraryPreflightReadOnly.ps1;docs/product-status.md;CHANGELOG.md';Exit=0;Text='mode=explicit-path-list'}
    )
    foreach($case in $legacy) {Assert-Gate -Name $case.Name -Expected $case.Exit -ExpectedText $case.Text -Environment @{} -Csv $case.Paths}
    $cases=@(
        @{Name='pr_code_no_docs';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory.psm1'}},
        @{Name='pr_code_status_only';Event='pull_request';Exit=1;Text='CHANGELOG.md not added/modified';Change={Write-FixtureFile 'Modules/Inventory.psm1';Write-FixtureFile 'docs/product-status.md'}},
        @{Name='pr_code_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={Write-FixtureFile 'Modules/Inventory.psm1';Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='pr_tests_no_status';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'tests/Unit/Fake.Tests.ps1'}},
        @{Name='pr_tests_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={Write-FixtureFile 'tests/Unit/Fake.Tests.ps1';Write-FixtureFile 'docs/product-status.md'}},
        @{Name='pr_workflow_no_status';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile '.github/workflows/fake.yml'}},
        @{Name='pr_workflow_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={Write-FixtureFile '.github/workflows/fake.yml';Write-FixtureFile 'docs/product-status.md'}},
        @{Name='rename_code_to_notes';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','Modules/Inventory.psm1','notes/Moved.md')}},
        @{Name='rename_notes_to_code';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','notes/Guide.md','Modules/Imported.psm1')}},
        @{Name='rename_tests_to_notes';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','tests/Unit/Fake.Tests.ps1','notes/Moved.md')}},
        @{Name='rename_workflow_to_notes';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','.github/workflows/fake.yml','notes/Moved.md')}},
        @{Name='rename_code_to_tests_status_only';Event='pull_request';Exit=1;Text='CHANGELOG.md not added/modified';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','Modules/Inventory.psm1','tests/Moved.ps1');Write-FixtureFile 'docs/product-status.md'}},
        @{Name='rename_code_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','Modules/Inventory.psm1','notes/Moved.md');Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='delete_module_no_docs';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={$null=Invoke-FixtureGit @('rm','Modules/Removed.psm1')}},
        @{Name='delete_module_status_only';Event='pull_request';Exit=1;Text='CHANGELOG.md not added/modified';Change={$null=Invoke-FixtureGit @('rm','Modules/Removed.psm1');Write-FixtureFile 'docs/product-status.md'}},
        @{Name='delete_module_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={$null=Invoke-FixtureGit @('rm','Modules/Removed.psm1');Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='delete_tests_no_status';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={$null=Invoke-FixtureGit @('rm','tests/Unit/Fake.Tests.ps1')}},
        @{Name='delete_tests_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={$null=Invoke-FixtureGit @('rm','tests/Unit/Fake.Tests.ps1');Write-FixtureFile 'docs/product-status.md'}},
        @{Name='delete_required_status';Event='pull_request';Exit=1;Text='DOC_REQUIRED_MISSING';Change={$null=Invoke-FixtureGit @('rm','docs/product-status.md')}},
        @{Name='delete_required_changelog';Event='pull_request';Exit=1;Text='DOC_REQUIRED_MISSING';Change={Write-FixtureFile 'Modules/Inventory.psm1';Write-FixtureFile 'docs/product-status.md';$null=Invoke-FixtureGit @('rm','CHANGELOG.md')}},
        @{Name='move_required_status';Event='pull_request';Exit=1;Text='DOC_REQUIRED_MISSING';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','docs/product-status.md','notes/Status.md');Write-FixtureFile 'Modules/Inventory.psm1';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='uncommitted_docs_do_not_satisfy';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory.psm1'};PostCheckout={Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='deleted_doc_restored_only_in_worktree';Event='pull_request';Exit=1;Text='DOC_REQUIRED_MISSING_IN_COMMIT';Change={$null=Invoke-FixtureGit @('rm','docs/product-status.md')};PostCheckout={Write-FixtureFile 'docs/product-status.md'}},
        @{Name='pr_preflight_no_docs';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Preflight/Check.ps1'}},
        @{Name='pr_preflight_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={Write-FixtureFile 'Preflight/Check.ps1';Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='pr_add_export_no_docs';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Export/Added.psm1'}},
        @{Name='pr_add_export_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={Write-FixtureFile 'Export/Added.psm1';Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='pr_merge_checkout_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Merge=$true;Change={Write-FixtureFile 'Modules/Inventory.psm1';Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='pr_merge_checkout_no_docs';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Merge=$true;Change={Write-FixtureFile 'Modules/Inventory.psm1'}},
        @{Name='pr_excludes_base_only_changes';Event='pull_request';Exit=0;Text='mode=pull_request';DivergedBase=$true;Change={Write-FixtureFile 'notes/Guide.md'}},
        @{Name='pr_ignores_newer_origin_main';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';NewerOrigin=$true;Change={Write-FixtureFile 'Modules/Inventory.psm1'}},
        @{Name='pr_cumulative_not_last_commit';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory.psm1';$null=Commit-Fixture 'Synthetic earlier code';Write-FixtureFile 'notes/Guide.md'}},
        @{Name='pr_base_absent';Event='pull_request';Exit=1;Text='DOC_COMMIT_UNAVAILABLE: PR_BASE';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.pull_request.base.sha=('1'*40)}},
        @{Name='pr_head_absent';Event='pull_request';Exit=1;Text='DOC_COMMIT_UNAVAILABLE: PR_HEAD';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.pull_request.head.sha=('1'*40)}},
        @{Name='pr_base_invalid';Event='pull_request';Exit=1;Text='DOC_SHA_INVALID: PR_BASE';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.pull_request.base.sha='main'}},
        @{Name='pr_base_wrong_target';Event='pull_request';Exit=1;Text='DOC_UNEXPECTED_PR_BASE';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.pull_request.base.ref='other'}},
        @{Name='pr_merge_wrong_parents';Event='pull_request';Exit=1;Text='DOC_PR_MERGE_PARENTS_MISMATCH';Merge=$true;Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.pull_request.base.sha=$head}},
        @{Name='push_code_no_docs';Event='push';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory.psm1'}},
        @{Name='push_code_multicommit_documented';Event='push';Exit=0;Text='mode=push-main';Change={Write-FixtureFile 'Modules/Inventory.psm1';$null=Commit-Fixture 'Synthetic earlier code';Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='push_code_hidden_in_earlier_commit';Event='push';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory.psm1';$null=Commit-Fixture 'Synthetic earlier code';Write-FixtureFile 'notes/Guide.md'}},
        @{Name='push_tests_no_status';Event='push';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'tests/Unit/Fake.Tests.ps1'}},
        @{Name='push_tests_documented';Event='push';Exit=0;Text='mode=push-main';Change={Write-FixtureFile 'tests/Unit/Fake.Tests.ps1';Write-FixtureFile 'docs/product-status.md'}},
        @{Name='push_rename_code_to_notes';Event='push';Exit=1;Text='DOCS_SYNC_FAIL';Rename=$true;Change={$null=Invoke-FixtureGit @('mv','Modules/Inventory.psm1','notes/Moved.md')}},
        @{Name='push_before_missing_field';Event='push';Exit=1;Text='DOC_EVENT_FIELD_MISSING: before';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.Remove('before')}},
        @{Name='push_before_invalid';Event='push';Exit=1;Text='DOC_SHA_INVALID: PUSH_BEFORE';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.before='not-a-sha'}},
        @{Name='push_root_genuine';Event='push';Exit=0;Text='mode=push-main';RootPush=$true;Change={};Adjust={$payload.before=('0'*40)}},
        @{Name='push_before_zero';Event='push';Exit=1;Text='DOC_ROOT_PUSH_NOT_ROOT_COMMIT';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.before=('0'*40)}},
        @{Name='push_before_object_absent';Event='push';Exit=1;Text='DOC_COMMIT_UNAVAILABLE: PUSH_BEFORE';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.before=('1'*40)}},
        @{Name='push_after_object_absent';Event='push';Exit=1;Text='DOC_COMMIT_UNAVAILABLE: PUSH_AFTER';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.after=('1'*40)}},
        @{Name='push_after_mismatch';Event='push';Exit=1;Text='DOC_PUSH_AFTER_CHECKOUT_MISMATCH';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.after=$base}},
        @{Name='push_after_zero';Event='push';Exit=1;Text='DOC_SHA_INVALID: PUSH_AFTER';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.after=('0'*40)}},
        @{Name='checkout_sha_mismatch';Event='push';Exit=1;Text='DOC_CHECKOUT_SHA_MISMATCH';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$environment.GITHUB_SHA=$base}},
        @{Name='push_wrong_branch';Event='push';Exit=1;Text='DOC_UNEXPECTED_PUSH_REF';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.ref='refs/heads/other'}},
        @{Name='push_before_not_ancestor';Event='push';Exit=1;Text='DOC_PUSH_NON_FAST_FORWARD';NonAncestorBefore=$true;Change={Write-FixtureFile 'Modules/Inventory.psm1'}},
        @{Name='push_forced_flag';Event='push';Exit=1;Text='DOC_PUSH_FORCED_NOT_SUPPORTED';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.forced=$true}},
        @{Name='event_payload_absent';Event='push';Exit=1;Text='DOC_EVENT_PAYLOAD_MISSING';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$environment.GITHUB_EVENT_PATH=Join-Path $script:fixture '.git/missing.json'}},
        @{Name='synthetic_override_unmarked';Event='push';Exit=1;Text='DOC_SYNTHETIC_REPOSITORY_NOT_MARKED';Change={Write-FixtureFile 'notes/Guide.md'};PostCheckout={[IO.File]::Delete((Join-Path $script:fixture '.git/docs-sync-synthetic'))}},
        @{Name='workflow_dispatch_explicit_skip';Event='workflow_dispatch';Exit=0;Text='DOCS_SYNC_NOT_APPLICABLE';Change={Write-FixtureFile 'notes/Guide.md'}},
        @{Name='unknown_event_rejected';Event='schedule';Exit=1;Text='DOC_EVENT_UNSUPPORTED';Change={Write-FixtureFile 'notes/Guide.md'}},
        @{Name='path_accents_no_docs';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile ('Modules/Inventaire-'+[char]0x00E9+'.psm1')}},
        @{Name='path_accents_documented';Event='pull_request';Exit=0;Text='mode=pull_request';Change={Write-FixtureFile ('Modules/Inventaire-'+[char]0x00E9+'.psm1');Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md'}},
        @{Name='push_before_blob_not_commit';Event='push';Exit=1;Text='DOC_COMMIT_UNAVAILABLE: PUSH_BEFORE';Change={Write-FixtureFile 'notes/Guide.md'};Adjust={$payload.before=(Invoke-FixtureGit @('rev-parse',($base+':notes/Guide.md'))).Trim()}},
        @{Name='push_reverted_status_not_update';Event='push';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory.psm1';Write-FixtureFile 'docs/product-status.md';Write-FixtureFile 'CHANGELOG.md';$null=Commit-Fixture 'Synthetic documented stage';Write-FixtureFile 'docs/product-status.md' '# synthetic baseline: docs/product-status.md'}},
        @{Name='path_spaces';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory with spaces.psm1'}},
        @{Name='path_semicolon';Event='pull_request';Exit=1;Text='DOCS_SYNC_FAIL';Change={Write-FixtureFile 'Modules/Inventory;extra.psm1'}}
    )
    foreach($case in $cases) {Invoke-Scenario $case}
    Write-Host 'DOCS_DELETION_CANARY_PASS: production gate checked real Git deletions.'
    Write-Host 'DOCS_NEGATIVE_CANARY_PASS: historical and Git-range policy checks passed.'
    Write-Host ('DOCS_GIT_CANARY_ALL_PASS | cases='+$script:caseCount+' | legacy='+$legacy.Count+' | git='+$cases.Count)
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
} finally {
    if($null -ne $script:workspace -and (Test-Path -LiteralPath $script:workspace)) {
        # Verify the final target is our unique direct child, never the temp root.
        $resolved=(Resolve-Path -LiteralPath $script:workspace).Path.TrimEnd('\')
        $expected=[IO.Path]::GetFullPath($script:workspace).TrimEnd('\')
        $parent=[IO.Path]::GetDirectoryName($expected)
        if($resolved -cne $expected -or
           ![string]::Equals($parent,$fixtureBase.TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase) -or
           [IO.Path]::GetFileName($expected) -cnotmatch '^rm-docs-sync-canary-[0-9a-f]{32}$' -or
           ((Get-Item -LiteralPath $expected -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'DOCS_CANARY_FINAL_CLEANUP_UNSAFE'
        }
        Remove-Item -LiteralPath $expected -Recurse -Force -ErrorAction Stop
    }
}
