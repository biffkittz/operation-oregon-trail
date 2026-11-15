#!/usr/bin/env bash
# Release the web app (Vue SPA + ASP.NET backend) to the "ca-oregon-web" Container App.
# See docs/build-and-deploy.md. Run with --help for options.
set -euo pipefail
# shellcheck source=scripts/lib/release.sh
source "$(dirname "$0")/lib/release.sh"
release_image oregon-web app/Dockerfile "${WEB_APP_NAME:-ca-oregon-web}" "$@"
