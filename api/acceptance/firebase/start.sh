#!/bin/sh
# Starts the Auth Emulator, with the Emulator UI when the build could download it (storage.googleapis.com may be
# out of reach, e.g. in a cloud session). The accounts are imported from /home/node/data/export when it starts
# and exported there when it stops.
set -eu
config=/home/node/firebase/firebase.json
if ! ls "$HOME"/.cache/firebase/emulators/ui-v*/server/server.mjs >/dev/null 2>&1; then
  echo "firebase: the Emulator UI is not installed; starting the Auth Emulator alone"
  config=/home/node/firebase/firebase.no-ui.json
fi

# An export replaces its directory: firebase-tools removes it, then moves in a temporary directory created in the
# working directory. The volume is mounted on /home/node/data and a mount point cannot be removed (EBUSY), so the
# export goes to a subdirectory of it. The working directory is the home of the node user, which it can write to;
# a temporary directory left there by an export that was cut short is removed first.
data=/home/node/data
mkdir -p "$data/export"
cd "$HOME"
rm -rf firebase-export-*
exec firebase emulators:start --config "$config" --only auth --project demo-zengear \
  --import "$data/export" --export-on-exit
