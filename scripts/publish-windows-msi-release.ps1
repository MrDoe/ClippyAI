$ErrorActionPreference = 'Stop'

$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $IsWindows) {
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

if (-not (Test-Path $msiPath)) {
    throw "MSI package not found at '$msiPath'. Run the publish-windows-msi task first."
}

& gh auth status --hostname github.com | Out-Null

& gh release view $tag --json tagName --jq .tagName 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw "GitHub release '$tag' already exists. Update the version before creating a new release."
}

& gh release create $tag $msiPath --title "ClippyAI $version" --generate-notes