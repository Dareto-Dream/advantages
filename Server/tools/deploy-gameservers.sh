#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="cf4bdb6c-14e6-4b16-91b3-7881f31e4352"
ENVIRONMENT="production"
BUILD_DIR="$(cd "$(dirname "$0")/../.." && pwd)/Build/Server/linux"

REGIONS=("$@")
if [ ${#REGIONS[@]} -eq 0 ]; then
  REGIONS=(us-east us-west eu-west ap-southeast)
fi

if [ ! -d "$BUILD_DIR" ]; then
  echo "No build at $BUILD_DIR"
  echo "Run Advantage > Build > 5 - Dedicated Server (Linux) in the Unity editor first."
  exit 1
fi

if [ ! -f "$BUILD_DIR/Dockerfile" ]; then
  echo "No Dockerfile in $BUILD_DIR - the build writes one; re-run the build menu item."
  exit 1
fi

cd "$BUILD_DIR"

echo "Linking Railway project..."
railway link --project "$PROJECT_ID" --environment "$ENVIRONMENT" --service gateway >/dev/null

for region in "${REGIONS[@]}"; do
  echo
  echo "deploying game-$region-------------"
  railway up --service "game-$region" --detach
done

echo
echo "Deployed to: ${REGIONS[*]}"
echo "Watch them register:  curl -s https://gateway-production-a3c7.up.railway.app/regions | jq"
