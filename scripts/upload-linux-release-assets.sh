#!/usr/bin/env bash

set -euo pipefail

workspace_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI (gh) is required. Install it and run gh auth login before uploading release assets." >&2
  exit 1
fi

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$workspace_root/Version.props" | head -n 1)"

if [[ -z "$version" ]]; then
  echo "Unable to read the application version from Version.props." >&2
  exit 1
fi

tag="v$version"

expected_assets=(
  "ClippyAI.$version.deb"
  "ClippyAI.$version.rpm"
  "ClippyAI.$version.tar.gz"
)

assets=()
missing_assets=()

for asset_name in "${expected_assets[@]}"; do
  asset_path="$(find "$workspace_root/ClippyAI/bin/Release" -maxdepth 3 -type f -name "$asset_name" | head -n 1)"

  if [[ -n "$asset_path" ]]; then
    assets+=("$asset_path")
  else
    missing_assets+=("$asset_name")
  fi
done

if [[ ${#missing_assets[@]} -gt 0 ]]; then
  printf 'Missing Linux release assets for version %s:\n' "$version" >&2
  printf '  %s\n' "${missing_assets[@]}" >&2
  echo "Run the Linux packaging tasks first and rerun publish-linux." >&2
  exit 1
fi

gh auth status --hostname github.com >/dev/null

if ! gh release view "$tag" --json tagName >/dev/null 2>&1; then
  echo "GitHub release '$tag' does not exist. Create it first, then rerun publish-linux." >&2
  exit 1
fi

gh release upload "$tag" "${assets[@]}" --clobber