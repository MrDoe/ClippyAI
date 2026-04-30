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

mapfile -t assets < <(
  find "$workspace_root/ClippyAI/bin/Release" -maxdepth 3 -type f \
    \( -name "ClippyAI.$version.deb" -o -name "ClippyAI.$version.rpm" -o -name "ClippyAI.$version.tar.gz" \) \
    | sort
)

if [[ ${#assets[@]} -eq 0 ]]; then
  echo "No Linux release assets were found for version $version. Run the Linux packaging tasks first." >&2
  exit 1
fi

gh auth status --hostname github.com >/dev/null

if ! gh release view "$tag" --json tagName >/dev/null 2>&1; then
  echo "GitHub release '$tag' does not exist. Create it first, then rerun publish-linux." >&2
  exit 1
fi

gh release upload "$tag" "${assets[@]}" --clobber