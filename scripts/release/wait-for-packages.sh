#!/usr/bin/env bash
# Waits until each package id at <version> is downloadable from nuget.org's flat container, the
# same endpoint the Dnui library downloads runtimes from (spec 003 R7). Polls every 30 s, for at
# most 30 minutes.
set -euo pipefail
version="${1:?usage: wait-for-packages.sh <version> <id>...}"
shift
[ $# -gt 0 ] || { echo "usage: wait-for-packages.sh <version> <id>..."; exit 2; }
lower_version=$(echo "$version" | tr '[:upper:]' '[:lower:]')
pending=("$@")
deadline=$(( $(date +%s) + 30 * 60 ))
while :; do
  still=()
  for id in "${pending[@]}"; do
    lower_id=$(echo "$id" | tr '[:upper:]' '[:lower:]')
    url="https://api.nuget.org/v3-flatcontainer/$lower_id/$lower_version/$lower_id.$lower_version.nupkg"
    code=$(curl -s -o /dev/null -I -w '%{http_code}' "$url" || echo 000)
    if [ "$code" = "200" ]; then
      echo "available: $id $version"
    else
      still+=("$id")
    fi
  done
  if [ ${#still[@]} -eq 0 ]; then
    echo "All ${#@} packages are available."
    exit 0
  fi
  if [ "$(date +%s)" -ge "$deadline" ]; then
    echo "::error::Still not available after 30 minutes: ${still[*]}"
    exit 1
  fi
  echo "waiting for: ${still[*]}"
  pending=("${still[@]}")
  sleep 30
done
