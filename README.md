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

You don't need any coding tools. Just download one file and copy a folder. Follow these steps in order.

### Step 1 — Download

1. Go to the **[Releases page](https://github.com/Superbrain8/MSFS-Media-Player/releases)**.
2. Click the file named **`MsfsMediaPlayer-v0.1.0.zip`** to download it.

### Step 2 — Unzip

1. Find the downloaded `.zip` file (usually in your **Downloads** folder).
2. **Right-click it → "Extract All…" → Extract.**
3. A new folder opens. Inside it are **two** folders: **`Companion`** and **`Community`**.

### Step 3 — Put the tablet app into the game

The game looks for add-ons in a folder called **Community**. You need to copy our folder into it.

1. Press the **Windows key + R** on your keyboard. A small box pops up.
2. Copy **one** of these and paste it into that box, then press **Enter**:
   - If you bought the game on **Steam:**
     ```
     %APPDATA%\Microsoft Flight Simulator 2024\Packages\Community
     ```
   - If you got it from the **Microsoft Store / Xbox Game Pass:**
     ```
     %LOCALAPPDATA%\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community
     ```
   A File Explorer window opens showing your **Community** folder. (If you get an error, see *"Can't find Community?"* below.)
3. Go back to the unzipped files. Open the **`Community`** folder there — inside is a folder called **`msfs-mediaplayer`**.
4. **Copy `msfs-mediaplayer`** (Ctrl+C) and **paste it** (Ctrl+V) into the Community window from step 2.
5. ✅ Done right if you now have a file at `…\Community\msfs-mediaplayer\manifest.json`.

<details>
<summary><b>Can't find Community?</b> (custom install)</summary>

Open the file `UserCfg.opt` in Notepad and look for a line like `InstalledPackagesPath "C:\...\Packages"`. Your Community folder is the `Community` folder inside that path. `UserCfg.opt` is here:
- Steam: `%APPDATA%\Microsoft Flight Simulator 2024\UserCfg.opt`
- Store: `%LOCALAPPDATA%\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\UserCfg.opt`
</details>

### Step 4 — Start the helper app

This little app runs on your PC and does the actual music/radio playing.

1. Go back to the unzipped files and open the **`Companion`** folder.
2. Double-click **`MsfsMediaPlayer.Companion.exe`**.
3. If a blue **"Windows protected your PC"** box appears: click **"More info"**, then **"Run anyway"**. (It's safe — it's just not signed.)
4. Nothing big happens on screen — that's normal. A small **music-note icon** appears near the clock (bottom-right). **Right-click it** to see the controls.
   - The icon is **red** when not connected to the game, **green** when connected.
   - Tip: right-click it → **"Start with Windows"** so it opens automatically next time.

### Step 5 — Play!

1. Start **MSFS 2024**.
2. In the cockpit, open the **EFB tablet** and tap the **Media Player** app.

### How to use it

- **Music** (Spotify / YouTube / Qobuz / any app): use the play / next / previous buttons on the tablet. The song title scrolls along the top.
- **Radio**: tap a station to start it; tap it again to stop. While a station is on, the buttons control the radio; otherwise they control your music.
- Radio sound follows the plane's **avionics power** — turn avionics off and the radio goes quiet.
- **Add or change stations**: right-click the tray icon → **"Edit stations…"**, type a name and a stream URL, Save. The list updates on the tablet right away.

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
