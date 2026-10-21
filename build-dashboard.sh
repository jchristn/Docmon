#!/usr/bin/env bash
# Usage: ./build-dashboard.sh [tag] [namespace]
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

tag="${1:-v0.1.0}"
namespace="${2:-jchristn77}"
builder="${DOCMON_BUILDER:-cloud-jchristn77-jchristn77}"
image="$namespace/docmon-dashboard"
tags=(--tag "$image:$tag")
if [[ "$tag" != "latest" ]]; then
    tags+=(--tag "$image:latest")
fi

printf 'Building and pushing %s for linux/amd64 and linux/arm64/v8...\n' "$image:$tag"
docker buildx build \
    --builder "$builder" \
    --platform linux/amd64,linux/arm64/v8 \
    --file docker/Dockerfile.dashboard \
    "${tags[@]}" \
    --push \
    .

printf 'Refreshing local images for the Docker daemon architecture...\n'
docker pull "$image:$tag"
if [[ "$tag" != "latest" ]]; then
    docker pull "$image:latest"
fi
