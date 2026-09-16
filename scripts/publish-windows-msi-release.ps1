$ErrorActionPreference = 'Stop'

$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:PATH += ";C:\Program Files\GitHub CLI"

if ([Environment]::OSVersion.Platform -ne 2) {
    throw 'This task must run on Windows because the MSI packaging step targets Windows.'
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required. Install it and run gh auth login before publishing a release.'
}

[xml]$versionProps = Get-Content (Join-Path $workspaceRoot 'Version.props')
$version = $versionProps.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Unable to read the application version from Version.props.'
}

$tag = "v$version"
$msiPath = Join-Path $workspaceRoot "Installer/bin/Release/ClippyAI-$version-windows-x64.msi"

if ($args.Contains("--check-only")) {
    if (Test-Path $msiPath) {
        Write-Host "MSI artifact for version $version already exists at $msiPath. Skipping build."
        exit 0
    }
    Write-Host "MSI artifact for version $version not found. Proceeding with build."
    exit 1
}

if (-not (Test-Path $msiPath)) {
    throw "MSI package not found at '$msiPath'. Run the publish-windows-msi task first."
}

& gh auth status --hostname github.com | Out-Null

try {
    $existingRelease = & gh release view $tag --json tagName --jq .tagName 2>$null
    if ($LASTEXITCODE -eq 0 -and $null -ne $existingRelease -and $existingRelease -eq $tag) {
        Write-Host "GitHub release '$tag' already exists. Adding asset to existing release..."
        & gh release upload $tag $msiPath --clobber
        exit 0
    }
} catch {
    # Ignore errors from gh release view (e.g. 404)
}

Write-Host "Creating new GitHub release '$tag'..."
& gh release create $tag $msiPath --title "ClippyAI $version" --generate-notes