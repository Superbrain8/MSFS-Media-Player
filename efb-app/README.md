# EFB app — MSFS Media Player control surface

Thin EFB control surface (TS + TSX + `@efb/efb-api` + esbuild) for the companion app. Mirrors the
MSFS 2024 SDK EFB template. All real work (audio, SMTC, SimConnect) lives in `../companion-app`.

See [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) and [../docs/DECISIONS.md](../docs/DECISIONS.md).

## Layout

```
efb_api/        EFB SDK API package (from MSFS 2024 SDK; prebuilt dist is vendored)
vendor/         msfs-sdk tarball the above depends on
MediaPlayer/    the app — src/, build.js (esbuild), builds to MediaPlayer/dist/
PackageDefinitions/  + MediaPlayerProject.xml — MSFS package built in-sim via DevMode
```

## Build

Requires Node ≥18 (tested on 24) + npm.

```bash
cd efb_api && npm install          # once
cd ../MediaPlayer && npm install   # once
npm run build                      # → MediaPlayer/dist/
npm run watch                      # rebuild on change
```

## Deploy to the sim

The app's JS/CSS build to `MediaPlayer/dist/`. The MSFS package is built **in-sim via DevMode**
(bare `fspackagetool` hangs — known from the calculator project):

1. In MSFS DevMode → open `efb-app/MediaPlayerProject.xml` as a project.
2. Build package → copies `MediaPlayer/dist/` to `html_ui/efb_ui/efb_apps/MediaPlayer/`.
3. Iterate with **reload-without-cache** in the EFB debugger after each `npm run build`.

## Bridge contract

`src/bridge/MediaBridge.ts` defines the LVAR names + command codes the companion mirrors
(`companion-app/SimConnectBridge.cs`). EFB writes `L:MEDIAPLAYER_CMD` / `_RADIO_VOL`; companion
writes status LVARs (`_RADIO_PLAYING`, `_RADIO_IDX`, `_GATE`, `_LOCAL_PLAYING`) back. Numeric only
for now — now-playing text needs a client-data area (later).
