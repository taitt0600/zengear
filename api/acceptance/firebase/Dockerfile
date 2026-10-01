# Firebase Auth Emulator and its UI for the acceptance environment (docs/ACCEPTANCE.md). The Auth Emulator runs on
# Node.js alone; firebase-tools is pinned so that every build runs the same emulator.
FROM node:22-bookworm-slim
ARG FIREBASE_TOOLS_VERSION=15.32.1
RUN npm install --global --no-audit --no-fund "firebase-tools@${FIREBASE_TOOLS_VERSION}"

USER node
WORKDIR /home/node/firebase
COPY --chown=node:node firebase.json firebase.no-ui.json ./
COPY --chown=node:node --chmod=0755 start.sh ./
# The UI is downloaded once here; without it (no access to storage.googleapis.com) only the Auth Emulator starts.
# /home/node/data keeps the accounts between restarts.
RUN (firebase setup:emulators:ui || echo "Emulator UI not downloaded") && mkdir -p /home/node/data
EXPOSE 9099 4000
CMD ["./start.sh"]
