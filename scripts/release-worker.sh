#!/usr/bin/env bash
# Release the Temporal game worker to the "ca-oregon-worker" Container App.
# The worker has its own release cadence; see worker/CLAUDE.md before changing workflow code (determinism).
# See docs/build-and-deploy.md. Run with --help for options.
set -euo pipefail
# shellcheck source=scripts/lib/release.sh
source "$(dirname "$0")/lib/release.sh"
release_image oregon-worker worker/Dockerfile "${WORKER_APP_NAME:-ca-oregon-worker}" "$@"
