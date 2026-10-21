#!/usr/bin/env bash
# Usage: ./build-all.sh [tag] [namespace]
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

tag="${1:-v0.1.0}"
namespace="${2:-jchristn77}"

./build-server.sh "$tag" "$namespace"
./build-dashboard.sh "$tag" "$namespace"
