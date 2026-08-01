#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$VpsHost = '103.12.77.73',
    [string]$VpsUser = 'root',
    [string]$SshKey = '',
    [string]$Platform = 'linux/amd64',
    [string]$ApiBaseUrl = 'https://moto.luandinh.com/api/v1',
    [string]$AppSettingsPath = '',
    [switch]$EnableHttps
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = $PSScriptRoot
$deployDirectory = Join-Path $repositoryRoot 'deploy\production'
$defaultAppSettings = Join-Path $repositoryRoot 'src\MotoCare.Api\appsettings.Production.json'
$artifactRoot = Join-Path $repositoryRoot 'deploy\.artifacts'
$releaseId = Get-Date -Format 'yyyyMMddHHmmss'
$releaseDirectory = Join-Path $artifactRoot $releaseId
$bundleDirectory = Join-Path $releaseDirectory 'bundle'
$imageArchive = Join-Path $bundleDirectory 'motocare-images.tar'
$bundleArchive = Join-Path $releaseDirectory "motocare-compose-${releaseId}.tar.gz"
$sshTarget = "${VpsUser}@${VpsHost}"
$remoteStage = "/tmp/motocare-${releaseId}"
$sshArguments = @('-o', 'BatchMode=yes')

if ([string]::IsNullOrWhiteSpace($AppSettingsPath)) {
    $AppSettingsPath = $defaultAppSettings
}
$AppSettingsPath = [System.IO.Path]::GetFullPath($AppSettingsPath)
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

foreach ($command in @('docker.exe', 'tar.exe', 'ssh.exe', 'scp.exe')) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $command"
    }
}
if (-not (Test-Path -LiteralPath $AppSettingsPath -PathType Leaf)) {
    throw "Production appsettings file not found: $AppSettingsPath"
}
$productionSettings = Get-Content -Raw -LiteralPath $AppSettingsPath | ConvertFrom-Json
$mongoConnectionString = [string]$productionSettings.Mongo.ConnectionString
if ([string]::IsNullOrWhiteSpace($mongoConnectionString) -or
    -not $mongoConnectionString.StartsWith('mongodb', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Mongo:ConnectionString must contain a MongoDB Cloud URI.'
}
if ($mongoConnectionString -match '^mongodb://(?:localhost|127\.0\.0\.1)') {
    throw 'Production MongoDB must not point to localhost; MongoDB is not part of the VPS stack.'
}
if ($mongoConnectionString.StartsWith('mongodb+srv://', [System.StringComparison]::OrdinalIgnoreCase) -and
    $mongoConnectionString -match '(?i)(?:[?&])directConnection=true(?:&|$)') {
    throw 'A mongodb+srv URI cannot use directConnection=true. Remove that query option.'
}

[System.IO.Directory]::CreateDirectory($bundleDirectory) | Out-Null

Write-Host "==> Building API image for $Platform"
Invoke-Checked -Command 'docker.exe' -Arguments @(
    'build', '--platform', $Platform,
    '--file', (Join-Path $repositoryRoot 'src\MotoCare.Api\Dockerfile'),
    '--tag', 'motocare-api:local', $repositoryRoot
)

Write-Host "==> Building frontend image for $Platform"
Invoke-Checked -Command 'docker.exe' -Arguments @(
    'build', '--platform', $Platform,
    '--build-arg', "NUXT_PUBLIC_API_BASE=${ApiBaseUrl}",
    '--file', (Join-Path $repositoryRoot 'src\MotoCare.FE\Dockerfile'),
    '--tag', 'motocare-web:local', $repositoryRoot
)

Write-Host '==> Exporting Docker images'
Invoke-Checked -Command 'docker.exe' -Arguments @(
    'save', '--output', $imageArchive,
    'motocare-api:local', 'motocare-web:local'
)
Copy-Item -LiteralPath (Join-Path $deployDirectory 'docker-compose.yml') `
    -Destination (Join-Path $bundleDirectory 'docker-compose.yml')
Copy-Item -LiteralPath $AppSettingsPath `
    -Destination (Join-Path $bundleDirectory 'appsettings.Production.json')
Copy-Item -LiteralPath (Join-Path $deployDirectory 'nginx\host-http.conf') `
    -Destination (Join-Path $bundleDirectory 'host-http.conf')
Copy-Item -LiteralPath (Join-Path $deployDirectory 'nginx\host-https.conf') `
    -Destination (Join-Path $bundleDirectory 'host-https.conf')

Write-Host '==> Creating deployment bundle'
Invoke-Checked -Command 'tar.exe' -Arguments @(
    '-czf', $bundleArchive, '-C', $bundleDirectory, '.'
)

Write-Host "==> Preparing ${sshTarget}:${remoteStage}"
Invoke-Checked -Command 'ssh.exe' -Arguments (
    $sshArguments + @($sshTarget, "mkdir -p '${remoteStage}'")
)

Write-Host '==> Uploading Docker bundle and installer'
foreach ($file in @($bundleArchive, (Join-Path $deployDirectory 'install-compose.sh'))) {
    Invoke-Checked -Command 'scp.exe' -Arguments (
        $sshArguments + @($file, "${sshTarget}:${remoteStage}/")
    )
}

$httpsFlag = if ($EnableHttps) { 'true' } else { 'false' }
Write-Host '==> Loading images and starting Docker Compose on the VPS'
Invoke-Checked -Command 'ssh.exe' -Arguments (
    $sshArguments + @(
        $sshTarget,
        "chmod 0755 '${remoteStage}/install-compose.sh' && '${remoteStage}/install-compose.sh' '${releaseId}' '${httpsFlag}'"
    )
)

Write-Host ''
Write-Host 'Deployment completed: https://moto.luandinh.com'
Write-Host 'API health: https://moto.luandinh.com/api/health'
if (-not $EnableHttps) {
    Write-Host 'HTTPS certificate creation was not requested. Use -EnableHttps after DNS is ready.'
}
