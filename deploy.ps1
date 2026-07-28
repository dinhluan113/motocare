#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$VpsHost = '103.12.77.73',
    [string]$VpsUser = 'root',
    [string]$SshKey = '',
    [switch]$EnableHttps
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = $PSScriptRoot
$apiProject = Join-Path $repositoryRoot 'src\MotoCare.Api\MotoCare.Api.csproj'
$frontendDirectory = Join-Path $repositoryRoot 'src\MotoCare.FE'
$deployDirectory = Join-Path $repositoryRoot 'deploy\production'
$artifactRoot = Join-Path $repositoryRoot 'deploy\.artifacts'
$releaseId = Get-Date -Format 'yyyyMMddHHmmss'
$releaseDirectory = Join-Path $artifactRoot $releaseId
$apiPublishDirectory = Join-Path $releaseDirectory 'api'
$apiArchive = Join-Path $releaseDirectory 'api.tar.gz'
$webArchive = Join-Path $releaseDirectory 'web.tar.gz'
$sshTarget = "${VpsUser}@${VpsHost}"
$remoteStage = "/tmp/motocare-${releaseId}"
$sshArguments = @('-o', 'BatchMode=yes')

if (-not [string]::IsNullOrWhiteSpace($SshKey)) {
    $sshArguments += @('-i', $SshKey, '-o', 'IdentitiesOnly=yes')
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE"
    }
}

foreach ($command in @('dotnet', 'pnpm.cmd', 'tar.exe', 'ssh.exe', 'scp.exe')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $command"
    }
}

[System.IO.Directory]::CreateDirectory($apiPublishDirectory) | Out-Null

Write-Host "==> Publishing API for linux-x64 ($releaseId)"
Invoke-Checked -Command 'dotnet' -Arguments @(
    'publish',
    $apiProject,
    '--configuration', 'Release',
    '--runtime', 'linux-x64',
    '--self-contained', 'true',
    '--output', $apiPublishDirectory,
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Write-Host '==> Generating Nuxt production site'
Push-Location $frontendDirectory
try {
    $env:CI = 'true'
    $env:NUXT_PUBLIC_API_BASE = 'https://moto.luandinh.com/api/v1'
    Invoke-Checked -Command 'pnpm.cmd' -Arguments @('install', '--frozen-lockfile')
    Invoke-Checked -Command 'pnpm.cmd' -Arguments @('generate')
}
finally {
    Remove-Item Env:\NUXT_PUBLIC_API_BASE -ErrorAction SilentlyContinue
    Pop-Location
}

Write-Host '==> Creating deployment archives'
Invoke-Checked -Command 'tar.exe' -Arguments @('-czf', $apiArchive, '-C', $apiPublishDirectory, '.')
Invoke-Checked -Command 'tar.exe' -Arguments @(
    '-czf', $webArchive,
    '-C', (Join-Path $frontendDirectory '.output\public'),
    '.'
)

Write-Host "==> Preparing ${sshTarget}:${remoteStage}"
Invoke-Checked -Command 'ssh.exe' -Arguments (
    $sshArguments + @($sshTarget, "mkdir -p '${remoteStage}'")
)

Write-Host '==> Uploading API, web, systemd and Nginx files'
$files = @(
    $apiArchive,
    $webArchive,
    (Join-Path $deployDirectory 'motocare.service'),
    (Join-Path $deployDirectory 'moto.luandinh.com.conf'),
    (Join-Path $deployDirectory 'install-release.sh')
)
foreach ($file in $files) {
    Invoke-Checked -Command 'scp.exe' -Arguments (
        $sshArguments + @($file, "${sshTarget}:${remoteStage}/")
    )
}

$httpsFlag = if ($EnableHttps) { 'true' } else { 'false' }
Write-Host '==> Installing release and validating health'
Invoke-Checked -Command 'ssh.exe' -Arguments (
    $sshArguments + @(
        $sshTarget,
        "chmod 0755 '${remoteStage}/install-release.sh' && '${remoteStage}/install-release.sh' '${releaseId}' '${httpsFlag}'"
    )
)

Write-Host ''
Write-Host "Deployment completed: http://moto.luandinh.com"
Write-Host "API health: http://moto.luandinh.com/api/health"
if (-not $EnableHttps) {
    Write-Host 'HTTPS was not requested. Run again with -EnableHttps after DNS is configured.'
}
