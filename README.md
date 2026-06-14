# MSFS Media Player

In-sim media controller for **Microsoft Flight Simulator 2024**. Control your **local media**
(Spotify / YouTube / any app via Windows media controls) and play **internet radio** — all from
the EFB tablet, with radio volume gated by the aircraft's avionics power for immersion.

> Licensed under **AGPL-3.0**. Builds against the MSFS 2024 SDK (SimConnect) — those proprietary
> DLLs are **not** included; you supply them from your own SDK install. See
> [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Features

- **Local media control** — play/pause, next, previous, and now-playing title for any app that
  exposes Windows SMTC; a global media-key fallback covers apps that don't (e.g. Qobuz).
- **Internet radio** — station list with play/stop and volume, via NAudio.
- **Avionics gating** — radio audio follows `CIRCUIT AVIONICS ON`, so it mutes when the avionics
  bus is unpowered.
- **Context-aware transport** — the EFB transport buttons drive the radio when a station is
  selected, otherwise local media.
- **Station editor** in the companion tray; the list syncs live to the EFB.
- Tray status icon (red/green by sim connection), optional start-with-Windows, log retention.

## How it works

The EFB runs in the sandboxed **Coherent GT** engine — no audio codecs, no OS access, no reliable
network. So the system is split in two, bridged over SimConnect:

```
┌─────────────────────────┐      SimConnect LVARs       ┌────────────────────────────┐
│  EFB app (control UI)    │  commands / volume  ─────▶  │  Companion app (.exe, C#)   │
│  station list, transport │  ◀─── status + now-playing  │  SMTC + media keys (local)  │
│  volume, now-playing     │       (packed into LVARs)   │  NAudio radio + SimConnect  │
└─────────────────────────┘                             └────────────────────────────┘
        (sandboxed)                                          (full OS + network access)
```

Strings (now-playing, station names) are packed into numeric LVARs, since the EFB sandbox can't read
SimConnect client data. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and
[docs/DECISIONS.md](docs/DECISIONS.md).

## Installing (for users)

No build tools, git, or SDK needed — just download a zip.

1. Open the **[Releases](https://github.com/Superbrain8/MSFS-Media-Player/releases)** page and download the newest `MsfsMediaPlayer-v*.zip`.
2. **Unzip it** somewhere you'll keep it (e.g. `Documents`). Inside are two folders: `Companion` and `Community`.
3. **Install the EFB app:** copy the `msfs-mediaplayer` folder (inside the zip's `Community` folder) into your MSFS 2024 **Community** folder. Common locations — paste the path into File Explorer's address bar:
   - **Microsoft Store / Game Pass:**
     `%LOCALAPPDATA%\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community`
   - **Steam:**
     `%APPDATA%\Microsoft Flight Simulator 2024\Packages\Community`
   - Not there? Open MSFS → **Options → General → Developers** to see your exact Community path, or check the path you set during install.

   When done, you should have `…\Community\msfs-mediaplayer\manifest.json`.
4. **Start the companion:** open the `Companion` folder and double-click `MsfsMediaPlayer.Companion.exe`.
   - If Windows shows a blue **SmartScreen** box, click **More info → Run anyway** (the app is unsigned).
   - It runs in the **system tray** (bottom-right, near the clock). Right-click the icon for controls.
     The icon is **red** when not connected to the sim, **green** when connected.
5. **Launch MSFS 2024**, open the EFB tablet, and tap the **Media Player** app.

### Using it

- **Local media** (Spotify / YouTube / Qobuz / any app with Windows media controls): use the transport buttons on the EFB. Now-playing title scrolls across the top.
- **Internet radio**: tap a station to play it (tap again to stop). The transport buttons control the radio while a station is selected, otherwise local media. Radio volume is gated by avionics power — it mutes when the avionics bus is off.
- **Edit stations**: companion tray → **Edit stations…** (name + URL grid). Saves sync live to the EFB.

> Defender SmartScreen may warn on the unsigned exe — **More info → Run anyway**.

## Repository layout

```
companion-app/  Windows companion (.NET 8) — SMTC, NAudio radio, SimConnect bridge, tray UI
efb-app/        EFB control-surface app (TS + TSX, @efb/efb-api, esbuild) + packaging
scripts/        package-release.ps1
docs/           ARCHITECTURE.md, DECISIONS.md
```

## Building

**Prerequisites**

- [MSFS 2024 SDK](https://docs.flightsimulator.com/) installed; the `MSFS2024_SDK` environment
  variable set (the SDK sets this). Provides SimConnect.
- .NET 8 SDK.
- Node.js ≥ 18.

**Companion**

```pwsh
cd companion-app
dotnet build          # or: dotnet run
```

**EFB app**

```pwsh
cd efb-app\efb_api;     npm install   # once
cd ..\MediaPlayer;      npm install   # once
npm run build                         # → MediaPlayer/dist/
```

Then build the MSFS package in-sim via **DevMode** (open `efb-app/MediaPlayerProject.xml`), which
produces `efb-app/Packages/msfs-mediaplayer/`. Copy/junction that into your MSFS 2024 **Community**
folder. See [efb-app/README.md](efb-app/README.md).

## Releasing

```pwsh
.\scripts\package-release.ps1            # framework-dependent companion
.\scripts\package-release.ps1 -SelfContained
```

Bundles the published companion + the DevMode-built EFB package + an INSTALL.md into
`dist/release/`. By default it ships the **SimConnect** client DLLs (Microsoft, proprietary) so the
app runs out of the box — standard practice for MSFS add-ons. Pass `-ExcludeSimConnect` to omit them
(recipients then copy them from their own MSFS SDK). The DLLs are **not** committed to this repo;
contributors build against their own SDK.

## License

[AGPL-3.0](LICENSE) for this project's code. Bundled MIT components and the proprietary SimConnect
dependency are described in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

This project is not affiliated with or endorsed by Microsoft or Asobo Studio.
