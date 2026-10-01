#!/bin/sh
# Starts the Auth Emulator, with the Emulator UI when the build could download it (storage.googleapis.com may be
# out of reach, e.g. in a cloud session). The accounts in /home/node/data are imported and exported on stop.
set -eu
config=firebase.json
if ! ls "$HOME"/.cache/firebase/emulators/ui-v*/server/server.mjs >/dev/null 2>&1; then
  echo "firebase: the Emulator UI is not installed; starting the Auth Emulator alone"
  config=firebase.no-ui.json
fi
exec firebase emulators:start --config "$config" --only auth --project demo-zengear \
  --import /home/node/data --export-on-exit
