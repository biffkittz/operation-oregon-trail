#!/usr/bin/env bash
# Release the web app (Vue SPA + ASP.NET backend) to the "ca-oregon-web" Container App.
# See docs/build-and-deploy.md. Run with --help for options.
set -euo pipefail
# shellcheck source=scripts/lib/release.sh
source "$(dirname "$0")/lib/release.sh"

# Migrations and the least-privilege database role are applied by the migrate job, as the admin, with the image
# that is about to go live; the web app itself can't run DDL.
before_rollout() { "$(dirname "$0")/migrate-db.sh" --image "$1"; }

release_image oregon-web app/Dockerfile "${WEB_APP_NAME:-ca-oregon-web}" "$@"
