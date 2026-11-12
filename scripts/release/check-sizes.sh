#!/usr/bin/env bash
# Prints every .nupkg size in a folder and fails if any is over nuget.org's 250 MB limit (spec 003 R9).
set -euo pipefail
dir="${1:?usage: check-sizes.sh <folder>}"
limit=262144000
status=0
shopt -s nullglob
packages=("$dir"/*.nupkg)
if [ ${#packages[@]} -eq 0 ]; then
  echo "::error::No .nupkg files in $dir"
  exit 1
fi
for pkg in "${packages[@]}"; do
  size=$(wc -c < "$pkg" | tr -d ' ')
  printf '%12d  %s\n' "$size" "$(basename "$pkg")"
  if [ "$size" -gt "$limit" ]; then
    echo "::error::$(basename "$pkg") is $size bytes, over nuget.org's $limit-byte limit."
    status=1
  fi
done
exit $status
