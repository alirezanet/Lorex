param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$owner = 'alirezanet'
$repo = 'lorex'
$installDir = Join-Path $HOME '.lorex\bin'
$binaryName = 'lorex.exe'

function Write-Info([string]$Message) { Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Ok([string]$Message) { Write-Host "[ OK ] $Message" -ForegroundColor Green }
function Write-Warn([string]$Message) { Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Err([string]$Message) { Write-Host "[FAIL] $Message" -ForegroundColor Red }

function Get-ReleaseTag {
    param([string]$RequestedVersion)

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        if ($RequestedVersion.StartsWith('v')) { return $RequestedVersion }
        return "v$RequestedVersion"
    }

    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/latest" -Headers @{ 'User-Agent' = 'lorex-installer' }
    if (-not $release.tag_name) {
        throw "Could not determine the latest Lorex release tag."
    }

    return [string]$release.tag_name
}

function Get-ArchitectureAssetName {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture

    switch ($arch) {
        'X64' { return 'lorex-win-x64.exe' }
        'Arm64' { return 'lorex-win-arm64.exe' }
        default { throw "Unsupported Windows architecture '$arch'. Lorex currently publishes win-x64 and win-arm64 assets." }
    }
}

function Ensure-PathContainsInstallDir {
    param([string]$TargetDir)

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $parts = @()
    if (-not [string]::IsNullOrWhiteSpace($userPath)) {
        $parts = $userPath.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
    }

    if ($parts -contains $TargetDir) {
        return $false
    }

    $updatedPath = if ($parts.Count -eq 0) { $TargetDir } else { ($parts + $TargetDir) -join ';' }
    [Environment]::SetEnvironmentVariable('Path', $updatedPath, 'User')
    $env:Path = "$env:Path;$TargetDir"
    return $true
}

try {
    $tag = Get-ReleaseTag -RequestedVersion $Version
    $assetName = Get-ArchitectureAssetName
    $downloadUrl = "https://github.com/$owner/$repo/releases/download/$tag/$assetName"

    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("lorex-" + [guid]::NewGuid().ToString('N') + '.exe')

    Write-Info "Downloading $assetName from $tag..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile -Headers @{ 'User-Agent' = 'lorex-installer' }

    $targetPath = Join-Path $installDir $binaryName
    Move-Item -Force -Path $tempFile -Destination $targetPath
    Write-Ok "Installed Lorex to $targetPath"

    if (Ensure-PathContainsInstallDir -TargetDir $installDir) {
        Write-Ok "Added $installDir to your user PATH."
        Write-Warn "Restart your terminal if the 'lorex' command is not available yet."
    }
    else {
        Write-Info "$installDir is already on PATH."
    }

    Write-Host ''
    Write-Host 'Next steps:'
    Write-Host '  lorex --version'
    Write-Host '  lorex init'
}
catch {
    Write-Err $_.Exception.Message
    exit 1
}
