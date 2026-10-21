#!/usr/bin/env bash
set -euo pipefail
DOCMON_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
role="${1:?Specify tui or server}"
shift
framework=""
if [[ "${1:-}" == "--framework" || "${1:-}" == "-f" ]]; then
  shift
  framework="${1:?Specify net8.0 or net10.0}"
  shift
elif [[ "${1:-}" == net* ]]; then
  framework="$1"
  shift
fi
case "$framework" in
  net8|net8.0) framework=net8.0 ;;
  net10|net10.0) framework=net10.0 ;;
  "") if dotnet --list-sdks | awk '/^10\./ {found=1} END {exit !found}'; then framework=net10.0; else framework=net8.0; fi ;;
  *) echo "Unsupported framework: $framework (use net8.0 or net10.0)" >&2; exit 1 ;;
esac
case "$role" in
  tui) project=Docmon.App ;;
  server) project=Docmon.Server; export DOCMON_BEARER_TOKEN="${DOCMON_BEARER_TOKEN:-docmonadmin}" ;;
  *) echo "Unknown application: $role" >&2; exit 1 ;;
esac
cd "$DOCMON_ROOT"
exec dotnet run --project "$DOCMON_ROOT/src/$project/$project.csproj" --framework "$framework" -- "$@"
