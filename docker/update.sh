#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

docker compose pull
docker compose down
docker compose up -d
docker ps -a
