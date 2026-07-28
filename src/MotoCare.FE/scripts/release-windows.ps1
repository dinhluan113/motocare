[CmdletBinding()]
param(
    [string]$Version,
    [string]$Notes,
    [switch]$InitKey
)

$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$releaseConfigPath = Join-Path $projectDirectory 'release.config.json'
$tauriConfigPath = Join-Path $projectDirectory 'src-tauri\tauri.conf.json'

function Save-Json {
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string]$Path,
        [int]$Depth = 20
    )

    $json = $Value | ConvertTo-Json -Depth $Depth
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Read-ReleaseConfig {
    if (-not (Test-Path -LiteralPath $releaseConfigPath)) {
        throw "Release config not found: $releaseConfigPath"
    }

    return Get-Content -LiteralPath $releaseConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Update-TauriConfig {
    param([Parameter(Mandatory)]$ReleaseConfig)

    $tauri = Get-Content -LiteralPath $tauriConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $tauri.version = $ReleaseConfig.version
    $tauri.plugins.updater.pubkey = $ReleaseConfig.updaterPublicKey
    $tauri.plugins.updater.endpoints = @("$($ReleaseConfig.updateBaseUrl.TrimEnd('/'))/latest.json")
    Save-Json -Value $tauri -Path $tauriConfigPath
}

function Initialize-UpdaterKey {
    $config = Read-ReleaseConfig
    $privateKeyPath = [Environment]::ExpandEnvironmentVariables([string]$config.privateKeyPath)
    $privateKeyDirectory = Split-Path -Parent $privateKeyPath

    if ((Test-Path -LiteralPath $privateKeyPath) -or (Test-Path -LiteralPath "$privateKeyPath.pub")) {
        throw "Updater key already exists at $privateKeyPath. It will not be overwritten."
    }

    [System.IO.Directory]::CreateDirectory($privateKeyDirectory) | Out-Null
    Push-Location $projectDirectory
    try {
        & pnpm.cmd tauri signer generate -w $privateKeyPath
        if ($LASTEXITCODE -ne 0) {
            throw "Could not generate updater key."
        }
    }
    finally {
        Pop-Location
    }

    $publicKeyPath = "$privateKeyPath.pub"
    if (-not (Test-Path -LiteralPath $publicKeyPath)) {
        throw "Public key not found: $publicKeyPath"
    }

    $config.updaterPublicKey = (Get-Content -LiteralPath $publicKeyPath -Raw -Encoding UTF8).Trim()
    Save-Json -Value $config -Path $releaseConfigPath
    Update-TauriConfig -ReleaseConfig $config

    Write-Host "Updater key generated:"
    Write-Host "  Private: $privateKeyPath"
    Write-Host "  Public : $publicKeyPath"
    Write-Host 'Back up the private key and its password. Existing installs cannot update if this key is lost.'
}

function Copy-ReleaseFiles {
    param(
        [Parameter(Mandatory)][string[]]$Files,
        [Parameter(Mandatory)]$ReleaseConfig
    )

    $uploadMethod = [string]$ReleaseConfig.upload.method
    switch ($uploadMethod.ToLowerInvariant()) {
        'local' {
            $destination = Join-Path $projectDirectory ([string]$ReleaseConfig.upload.localDirectory)
            [System.IO.Directory]::CreateDirectory($destination) | Out-Null
            foreach ($file in $Files) {
                Copy-Item -LiteralPath $file -Destination $destination -Force
            }
            Write-Host "Published locally: $destination"
        }
        'scp' {
            $destination = [string]$ReleaseConfig.upload.scpDestination
            if ([string]::IsNullOrWhiteSpace($destination) -or $destination -like '*example.com*') {
                throw 'Configure upload.scpDestination in release.config.json.'
            }

            # Upload latest.json last so clients never see an incomplete release.
            $latestJson = $Files | Where-Object { (Split-Path -Leaf $_) -eq 'latest.json' }
            $artifacts = $Files | Where-Object { (Split-Path -Leaf $_) -ne 'latest.json' }
            foreach ($file in @($artifacts) + @($latestJson)) {
                & scp $file $destination
                if ($LASTEXITCODE -ne 0) {
                    throw "Upload failed: $file"
                }
            }
            Write-Host "Release uploaded to: $destination"
        }
        default {
            throw "Unsupported upload.method '$uploadMethod'. Use 'local' or 'scp'."
        }
    }
}

if ($InitKey) {
    Initialize-UpdaterKey
    exit 0
}

$releaseConfig = Read-ReleaseConfig
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    if ($Version -notmatch '^\d+\.\d+\.\d+([\-+][0-9A-Za-z.-]+)?$') {
        throw "Version '$Version' is not valid SemVer (example: 1.2.3)."
    }
    $releaseConfig.version = $Version
}
if (-not [string]::IsNullOrWhiteSpace($Notes)) {
    $releaseConfig.notes = $Notes
}

if ([string]$releaseConfig.updaterPublicKey -eq 'REPLACE_WITH_TAURI_UPDATER_PUBLIC_KEY') {
    throw 'Updater key is missing. Run build-release.bat init-key first.'
}
if ([string]$releaseConfig.updateBaseUrl -like '*example.com*') {
    throw 'Configure the real updateBaseUrl in release.config.json before publishing.'
}

$privateKeyPath = [Environment]::ExpandEnvironmentVariables([string]$releaseConfig.privateKeyPath)
if (-not (Test-Path -LiteralPath $privateKeyPath)) {
    throw "Private key not found: $privateKeyPath"
}

$cargoBin = Join-Path $env:USERPROFILE '.cargo\bin'
if ((Test-Path -LiteralPath $cargoBin) -and ($env:Path -notlike "*$cargoBin*")) {
    $env:Path = "$cargoBin;$env:Path"
}
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    throw 'Cargo is not installed or is not available in PATH. Install Rust with rustup first.'
}

Save-Json -Value $releaseConfig -Path $releaseConfigPath
Update-TauriConfig -ReleaseConfig $releaseConfig

$env:TAURI_SIGNING_PRIVATE_KEY = $privateKeyPath
if (-not $env:TAURI_SIGNING_PRIVATE_KEY_PASSWORD) {
    $securePassword = Read-Host 'Updater private key password' -AsSecureString
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    try {
        $env:TAURI_SIGNING_PRIVATE_KEY_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    }
}

Push-Location $projectDirectory
try {
    & pnpm.cmd install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) {
        throw 'pnpm install failed.'
    }

    & pnpm.cmd tauri build
    if ($LASTEXITCODE -ne 0) {
        throw 'Tauri build failed.'
    }
}
finally {
    Pop-Location
}

$nsisDirectory = Join-Path $projectDirectory 'src-tauri\target\release\bundle\nsis'
$installer = Get-ChildItem -LiteralPath $nsisDirectory -Filter '*-setup.exe' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $installer) {
    throw "NSIS installer not found in $nsisDirectory"
}

$signaturePath = "$($installer.FullName).sig"
if (-not (Test-Path -LiteralPath $signaturePath)) {
    throw "Updater signature not found: $signaturePath"
}

$releaseDirectory = Join-Path $projectDirectory "release\windows\$($releaseConfig.version)"
[System.IO.Directory]::CreateDirectory($releaseDirectory) | Out-Null
$releaseInstallerPath = Join-Path $releaseDirectory $installer.Name
$releaseSignaturePath = Join-Path $releaseDirectory (Split-Path -Leaf $signaturePath)
Copy-Item -LiteralPath $installer.FullName -Destination $releaseInstallerPath -Force
Copy-Item -LiteralPath $signaturePath -Destination $releaseSignaturePath -Force

$encodedInstallerName = [Uri]::EscapeDataString($installer.Name).Replace('%2F', '/')
$downloadUrl = "$($releaseConfig.updateBaseUrl.TrimEnd('/'))/$encodedInstallerName"
$latest = [ordered]@{
    version = [string]$releaseConfig.version
    notes = [string]$releaseConfig.notes
    pub_date = [DateTimeOffset]::UtcNow.ToString('o')
    platforms = [ordered]@{
        'windows-x86_64' = [ordered]@{
            signature = (Get-Content -LiteralPath $signaturePath -Raw -Encoding UTF8).Trim()
            url = $downloadUrl
        }
    }
}
$latestJsonPath = Join-Path $releaseDirectory 'latest.json'
Save-Json -Value $latest -Path $latestJsonPath

Copy-ReleaseFiles -Files @($releaseInstallerPath, $releaseSignaturePath, $latestJsonPath) -ReleaseConfig $releaseConfig

Write-Host "Version : $($releaseConfig.version)"
Write-Host "Installer: $releaseInstallerPath"
Write-Host "Manifest : $latestJsonPath"
