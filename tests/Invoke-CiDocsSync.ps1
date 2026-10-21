#Requires -Version 5.1
# Docs-as-code gate. Reads Git/event data only; never imports product modules.
[CmdletBinding()]
param(
    [AllowEmptyString()][string]$ChangedFilesCsv,
    [string]$SyntheticRepositoryRoot
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0

function Invoke-GitRead {
    param([string[]]$Arguments, [switch]$AllowFailure)
    # Arguments are constants, validated SHA values or fixed document paths.
    foreach($argument in $Arguments) {
        if([string]::IsNullOrEmpty($argument) -or $argument -match '[\s"]') {
            throw 'DOC_GIT_ARGUMENT_INVALID'
        }
    }
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$script:gitPath
    $start.WorkingDirectory=$script:root
    $start.Arguments='--no-optional-locks --no-replace-objects '+($Arguments -join ' ')
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $start.StandardOutputEncoding=New-Object Text.UTF8Encoding($false)
    $process=[Diagnostics.Process]::Start($start)
    try {
        $errorTask=$process.StandardError.ReadToEndAsync()
        $output=$process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        $errorText=$errorTask.Result
        $code=$process.ExitCode
        if($code -ne 0 -and !$AllowFailure) {
            throw ('DOC_GIT_FAILED: '+($Arguments -join ' ')+' | exit='+$code+' | '+$errorText.Trim())
        }
        return [pscustomobject]@{Output=$output;Code=$code}
    } finally {$process.Dispose()}
}
function Get-EventField {
    param($Object, [string]$Path)
    $value=$Object
    foreach($part in $Path.Split('.')) {
        if($null -eq $value -or $null -eq $value.PSObject.Properties[$part]) {
            throw ('DOC_EVENT_FIELD_MISSING: '+$Path)
        }
        $value=$value.PSObject.Properties[$part].Value
    }
    if($null -eq $value) {throw ('DOC_EVENT_FIELD_MISSING: '+$Path)}
    return $value
}
function Resolve-Commit {
    param([string]$Value, [string]$Label)
    if($Value -cnotmatch '^[0-9a-fA-F]{40}$' -or $Value -match '^0{40}$') {
        throw ('DOC_SHA_INVALID: '+$Label)
    }
    try {$resolved=(Invoke-GitRead @('rev-parse','--verify',($Value+'^{commit}'))).Output.Trim()}
    catch {throw ('DOC_COMMIT_UNAVAILABLE: '+$Label)}
    if($resolved -cne $Value.ToLowerInvariant()) {throw ('DOC_COMMIT_INVALID: '+$Label)}
    return $resolved
}
function Assert-RequiredTree {
    param([string]$Commit)
    $tree=(Invoke-GitRead (@('ls-tree','-z',$Commit,'--')+$script:requiredDocs)).Output
    $present=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach($entry in $tree.Split([char]0)) {
        if($entry -eq '') {continue}
        if($entry -cnotmatch '^(100644|100755) blob [0-9a-f]{40}\t(.+)$') {
            throw 'DOC_REQUIRED_NOT_REGULAR_BLOB'
        }
        $null=$present.Add($matches[2])
    }
    foreach($name in $script:requiredDocs) {
        if(!$present.Contains($name)) {throw ('DOC_REQUIRED_MISSING_IN_COMMIT: '+$name)}
    }
}

try {
    $productionRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    $root=$productionRoot
    $csvMode=$PSBoundParameters.ContainsKey('ChangedFilesCsv')
    if($PSBoundParameters.ContainsKey('SyntheticRepositoryRoot')) {
        if($csvMode) {throw 'DOC_SYNTHETIC_REQUIRES_EVENT_MODE'}
        $root=(Resolve-Path -LiteralPath $SyntheticRepositoryRoot).Path
        $marker=Join-Path $root '.git/docs-sync-synthetic'
        if([string]::Equals($root,$productionRoot,[StringComparison]::OrdinalIgnoreCase) -or
           !(Test-Path -LiteralPath (Join-Path $root '.git') -PathType Container) -or
           !(Test-Path -LiteralPath $marker -PathType Leaf) -or
           [IO.File]::ReadAllText($marker) -cne 'docs-sync-synthetic-v1') {
            throw 'DOC_SYNTHETIC_REPOSITORY_NOT_MARKED'
        }
    }
    $requiredDocs=@('README.md','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md','docs/product-status.md','docs/roadmap.md','docs/developer/workflow.md')
    foreach($name in $requiredDocs) {
        if(!(Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) {
            throw ('DOC_REQUIRED_MISSING: '+$name)
        }
    }
    $gitPath=(Get-Command git.exe -ErrorAction Stop).Source
    $repoRoot=(Invoke-GitRead @('rev-parse','--show-toplevel')).Output.Trim()
    if(![string]::Equals([IO.Path]::GetFullPath($repoRoot),[IO.Path]::GetFullPath($root),[StringComparison]::OrdinalIgnoreCase)) {
        throw 'DOC_REPOSITORY_ROOT_MISMATCH'
    }
    $changed=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $updated=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $mode='explicit-path-list'
    if($csvMode) {
        # Legacy canaries only: this mode is not a Git range verification.
        foreach($path in $ChangedFilesCsv.Split(';')) {
            if($path -ne '') {$null=$changed.Add($path);$null=$updated.Add($path)}
        }
    } else {
        $eventName=[string]$env:GITHUB_EVENT_NAME
        if($eventName -eq 'workflow_dispatch') {
            Write-Host 'DOCS_SYNC_NOT_APPLICABLE | event=workflow_dispatch | requiredDocs=present | changeSet=not_checked'
            exit 0
        }
        if($eventName -notin @('pull_request','push')) {throw ('DOC_EVENT_UNSUPPORTED: '+$eventName)}
        if([string]::IsNullOrWhiteSpace($env:GITHUB_EVENT_PATH) -or
           !(Test-Path -LiteralPath $env:GITHUB_EVENT_PATH -PathType Leaf)) {
            throw 'DOC_EVENT_PAYLOAD_MISSING'
        }
        $event=Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw -Encoding UTF8 | ConvertFrom-Json
        $checkout=Resolve-Commit ([string]$env:GITHUB_SHA) 'GITHUB_SHA'
        $actualHead=(Invoke-GitRead @('rev-parse','--verify','HEAD^{commit}')).Output.Trim()
        if($checkout -cne $actualHead) {throw 'DOC_CHECKOUT_SHA_MISMATCH'}
        if($eventName -eq 'pull_request') {
            $mode='pull_request'
            if([string](Get-EventField $event 'pull_request.base.ref') -cne 'main' -or $env:GITHUB_BASE_REF -cne 'main') {
                throw 'DOC_UNEXPECTED_PR_BASE'
            }
            $number=[string](Get-EventField $event 'number')
            if($number -cnotmatch '^[1-9][0-9]*$') {throw 'DOC_PR_NUMBER_INVALID'}
            $base=Resolve-Commit ([string](Get-EventField $event 'pull_request.base.sha')) 'PR_BASE'
            $head=Resolve-Commit ([string](Get-EventField $event 'pull_request.head.sha')) 'PR_HEAD'
            if($checkout -ceq $head) {
                if($env:GITHUB_REF -cne ('refs/pull/'+$number+'/head')) {throw 'DOC_PR_CHECKOUT_REF_MISMATCH'}
            } else {
                if($env:GITHUB_REF -cne ('refs/pull/'+$number+'/merge')) {throw 'DOC_PR_CHECKOUT_REF_MISMATCH'}
                $parents=(Invoke-GitRead @('show','-s','--format=%P',$checkout)).Output.Trim().Split(' ')
                if($parents.Count -ne 2 -or $parents[0] -cne $base -or $parents[1] -cne $head) {
                    throw 'DOC_PR_MERGE_PARENTS_MISMATCH'
                }
            }
            $bases=(Invoke-GitRead @('merge-base','--all',$base,$head)).Output.Trim()
            if($bases -cnotmatch '^[0-9a-f]{40}$') {throw 'DOC_PR_MERGE_BASE_AMBIGUOUS'}
            $from=$bases
            $to=$head
            Write-Host ('DOCS_SYNC_RANGE | event=PR | base='+$base+' | source='+$head+' | checkout='+$checkout+' | mergeBase='+$from)
        } else {
            $mode='push-main'
            if([string](Get-EventField $event 'ref') -cne 'refs/heads/main' -or $env:GITHUB_REF -cne 'refs/heads/main') {
                throw 'DOC_UNEXPECTED_PUSH_REF'
            }
            $before=[string](Get-EventField $event 'before')
            $to=Resolve-Commit ([string](Get-EventField $event 'after')) 'PUSH_AFTER'
            if($to -cne $checkout) {throw 'DOC_PUSH_AFTER_CHECKOUT_MISMATCH'}
            if($null -ne $event.PSObject.Properties['forced'] -and $event.forced -ne $false) {
                throw 'DOC_PUSH_FORCED_NOT_SUPPORTED'
            }
            $rootPush=$false
            if($before -ceq ('0'*40)) {
                $parents=(Invoke-GitRead @('show','-s','--format=%P',$to)).Output.Trim()
                if($parents -ne '') {throw 'DOC_ROOT_PUSH_NOT_ROOT_COMMIT'}
                $rootPush=$true
                Write-Host ('DOCS_SYNC_RANGE | event=root-push | after='+$to+' | checkout='+$checkout)
            } else {
                $from=Resolve-Commit $before 'PUSH_BEFORE'
                if($from -ceq $to) {throw 'DOC_PUSH_NO_ADVANCE'}
                $ancestry=Invoke-GitRead @('merge-base','--is-ancestor',$from,$to) -AllowFailure
                if($ancestry.Code -ne 0) {throw 'DOC_PUSH_NON_FAST_FORWARD'}
                Write-Host ('DOCS_SYNC_RANGE | event=push | before='+$from+' | after='+$to+' | checkout='+$checkout)
            }
        }
        Assert-RequiredTree $to
        # NUL records preserve spaces, accents, tabs and newlines in Git paths.
        # --no-renames exposes BOTH sides of moves as deletion + addition.
        if($eventName -eq 'push' -and $rootPush) {
            $diff=(Invoke-GitRead @('diff-tree','--root','--no-commit-id','-r','--no-renames','--name-status','-z',$to,'--')).Output
        } else {
            $diff=(Invoke-GitRead @('diff','--no-ext-diff','--no-textconv','--no-renames','--name-status','-z','--diff-filter=ACMDT',$from,$to,'--')).Output
        }
        $tokens=$diff.Split([char]0)
        $limit=$tokens.Length
        if($limit -gt 0 -and $tokens[$limit-1] -eq '') {$limit--}
        if($limit % 2 -ne 0) {throw 'DOC_DIFF_RECORD_INVALID'}
        for($i=0;$i -lt $limit;$i+=2) {
            $status=$tokens[$i];$path=$tokens[$i+1]
            if($status -cnotmatch '^[AMDT]$' -or $path -eq '') {throw 'DOC_DIFF_RECORD_INVALID'}
            $null=$changed.Add($path)
            if($status -cin @('A','M')) {$null=$updated.Add($path)}
        }
    }
    $hasStatus=$updated.Contains('docs/product-status.md')
    $hasChangelog=$updated.Contains('CHANGELOG.md')
    $code=@($changed|Where-Object{$_ -cmatch '^(Initialize\.ps1|Modules/.+\.psm1|Export/.+\.(ps1|psm1)|Preflight/[^/]+\.ps1)$'})
    $testInfra=@($changed|Where-Object{$_ -cmatch '^(tests/|\.github/workflows/)'})
    if($code.Count -gt 0) {
        if(!$hasStatus) {throw ('DOCS_SYNC_FAIL: code changed ('+$code.Count+' paths) but docs/product-status.md not added/modified.')}
        if(!$hasChangelog) {throw ('DOCS_SYNC_FAIL: code changed ('+$code.Count+' paths) but CHANGELOG.md not added/modified.')}
    } elseif($testInfra.Count -gt 0 -and !$hasStatus) {
        throw ('DOCS_SYNC_FAIL: CI/tests changed ('+$testInfra.Count+' paths) but docs/product-status.md not added/modified.')
    }
    Write-Host ('DOCS_SYNC_PASS | mode='+$mode+' | changed='+$changed.Count+' | code='+$code.Count+' | testsAndCI='+$testInfra.Count+' | status='+$hasStatus+' | changelog='+$hasChangelog)
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
