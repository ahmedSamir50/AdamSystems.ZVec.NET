#!/usr/bin/env bash
# Verify a release tag matches the Pack run that produced its nupkg (no secrets).
# Usage:
#   bash build/ci/verify-release-provenance.sh v1.0.0-beta.3.1 [pack_run_id]
# If pack_run_id is omitted, looks up the newest successful pack.yml run for the tag SHA.
set -euo pipefail

TAG="${1:?tag e.g. v1.0.0-beta.3.1}"
PACK_RUN_ID="${2:-}"
REPO="${GITHUB_REPOSITORY:-ahmedSamir50/AdamSystems.ZVec.NET}"

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

TAG_SHA="$(git rev-list -n1 "$TAG")"
echo "tag=$TAG sha=$TAG_SHA"

if [[ -z "$PACK_RUN_ID" ]]; then
  PACK_RUN_ID="$(gh api -H 'Accept: application/vnd.github+json' \
    "/repos/${REPO}/actions/workflows/pack.yml/runs?per_page=30" \
    --jq ".workflow_runs[] | select(.head_sha==\"${TAG_SHA}\" and .conclusion==\"success\") | .id" \
    | head -n1 || true)"
fi

if [[ -z "${PACK_RUN_ID:-}" ]]; then
  echo "ERROR: no successful Pack run found for ${TAG_SHA}" >&2
  exit 1
fi

META="$(gh api -H 'Accept: application/vnd.github+json' "/repos/${REPO}/actions/runs/${PACK_RUN_ID}")"
HEAD="$(echo "$META" | jq -r '.head_sha')"
CONCLUSION="$(echo "$META" | jq -r '.conclusion')"
echo "pack_run_id=$PACK_RUN_ID head_sha=$HEAD conclusion=$CONCLUSION"

if [[ "$HEAD" != "$TAG_SHA" ]]; then
  echo "ERROR: Pack head_sha != tag SHA" >&2
  exit 1
fi
if [[ "$CONCLUSION" != "success" ]]; then
  echo "ERROR: Pack conclusion is not success" >&2
  exit 1
fi

echo "PROVENANCE_OK tag=$TAG pack_run=$PACK_RUN_ID"
exit 0
