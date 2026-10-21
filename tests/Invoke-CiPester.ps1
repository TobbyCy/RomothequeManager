# Pin exact Pester release compatible with Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
try {
    if ($PSVersionTable.PSVersion.ToString() -notmatch '^5\.1\.') {throw 'Requires Windows PowerShell 5.1'}
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $version = '4.10.1'
    # Upstream signing certificate for Pester 4.10.0 through 5.1.1.
    # Official signer reference: https://github.com/pester/Pester#signing-certificates
    $expectedSigner = '7B9157664392D633EDA2C0248605C1C868EBDE43'
    $found = @(Get-Module -ListAvailable -Name Pester | Where-Object { $_.Version.ToString() -eq $version })
    if ($found.Count -eq 0) {
        # Windows-shipped Pester 3.4 has a different Microsoft publisher.
        # The publisher override is compensated by strict Authenticode checks
        # on both entry-point files below, BEFORE loading the downloaded module.
        $gallery = Get-PSRepository -Name PSGallery -ErrorAction Stop
        if ($null -eq $gallery -or [string]$gallery.SourceLocation -notmatch '^https://www\.powershellgallery\.com/api/v2/?$') {
            throw ('PESTER_REPOSITORY_UNEXPECTED: ' + [string]$gallery.SourceLocation)
        }
        Install-Module -Name Pester -RequiredVersion $version -Repository PSGallery -Scope CurrentUser -Force -SkipPublisherCheck -ErrorAction Stop
        $found = @(Get-Module -ListAvailable -Name Pester | Where-Object { $_.Version.ToString() -eq $version })
    }
    if ($found.Count -ne 1) {throw ('PESTER_CANDIDATE_COUNT_INVALID: ' + $found.Count)}
    $moduleRoot = $found[0].ModuleBase
    $manifestPath = Join-Path $moduleRoot 'Pester.psd1'
    $scriptPath = Join-Path $moduleRoot 'Pester.psm1'
    foreach ($candidate in @($manifestPath, $scriptPath)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw ('PESTER_SIGNED_FILE_MISSING: ' + [IO.Path]::GetFileName($candidate))
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $candidate -ErrorAction Stop
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne $expectedSigner) {
            throw ('PESTER_SIGNATURE_REJECTED: ' + [IO.Path]::GetFileName($candidate) +
                ' status=' + $signature.Status)
        }
    }
    Write-Host ('PESTER_SIGNATURE_PASS | version=' + $version + ' | signedFiles=2 | signer=' + $expectedSigner)
    # Import the exact path whose entry points have just been verified.
    $imported = Import-Module -Name $manifestPath -Force -PassThru -ErrorAction Stop
    if ($null -eq $imported -or $imported.Version.ToString() -ne $version -or
        [string]$imported.ModuleBase -ine [string]$moduleRoot) {
        throw 'PESTER_LOADED_MODULE_MISMATCH'
    }
    $loaded = $imported.Version.ToString()
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $originalLocalAppData = $env:LOCALAPPDATA
    $originalAppData = $env:APPDATA
    $isolated = Join-Path ([IO.Path]::GetTempPath()) ('rm-ci-pester-' + [guid]::NewGuid().ToString('N'))
    $env:LOCALAPPDATA = Join-Path $isolated 'LocalAppData'
    $env:APPDATA = Join-Path $isolated 'AppData'
    try {
        [IO.Directory]::CreateDirectory($env:LOCALAPPDATA) | Out-Null
        [IO.Directory]::CreateDirectory($env:APPDATA) | Out-Null
        $state = Join-Path $env:LOCALAPPDATA 'RomothequeManager\RealCommitLot1'
        $testScripts = @((Join-Path $root 'tests\Unit\Engine.Tests.ps1'),(Join-Path $root 'tests\Unit\ExportE1.Tests.ps1'))
        $result = Invoke-Pester -Script $testScripts -PassThru -Show Failed,Summary -ErrorAction Stop
        if ($null -eq $result -or $result.FailedCount -ne 0 -or $result.PassedCount -lt 30) {throw ('PESTER_FAILED: passed=' + $result.PassedCount + ' failed=' + $result.FailedCount)}
        if (Test-Path -LiteralPath $state) {throw 'PESTER_STATE_WRITE_DETECTED'}
        Write-Host ('CI-2 PASS | Pester=' + $loaded + ' | passed=' + $result.PassedCount + ' | failed=0 | StateRoot=absent')
    } finally { $env:LOCALAPPDATA=$originalLocalAppData; $env:APPDATA=$originalAppData }
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.ToString());exit 1 }
