# Architecture notes

## Why two components

The EFB cannot do media work (sandboxed Coherent GT: no audio, no network, no OS). So the
system splits:

| Concern | Lives in | Why |
|---------|----------|-----|
| UI (buttons, lists, now-playing) | EFB app | It's a UI engine; this it can do |
| Audio playback (radio) | Companion `.exe` | Needs codecs + audio device + network |
| Local media control (Spotify etc.) | Companion `.exe` | Needs Windows SMTC / WinRT |
| Bridge between them | SimConnect | The only channel the EFB can reach |

## EFB ↔ Companion bridge options

- **LVARs (`L:` local variables):** EFB sets/reads via `SimVar.SetSimVarValue("L:MEDIA_CMD", ...)`;
  companion reads/writes via SimConnect. Simple; numeric only; fine for command codes + a few
  status flags. Strings (track titles) are the pain point.
- **SimConnect Client Data Area:** structured byte buffers both sides map. Handles strings/structs.
  More setup. Better for "now playing: <title>".
- Likely: LVARs for commands (play=1, pause=2, next=3, vol=n), client data for text status.

## Local media control (Windows SMTC)

- API: `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` (WinRT).
- Reachable from .NET (C#) via the Windows SDK projections (`Microsoft.Windows.SDK.Contracts`
  or modern `net8.0-windows10.x` TFM).
- Gives: current session, play/pause/next/previous, and media properties (title/artist).

## Radio playback

- Companion fetches the stream and plays it. Options:
  - **Own audio output** (e.g. NAudio + a stream reader): simplest; plays through default device,
    independent of sim audio.
  - **ADF injection** (like the existing marketplace addon): route audio so it comes through the
    aircraft's ADF receiver — better immersion, volume tied to radio stack, but significantly more
    complex (audio routing into the sim).
- v1 likely: own audio output; ADF route is a stretch goal.

## SimConnect from .NET

- Managed SimConnect DLL: `Microsoft.FlightSimulator.SimConnect.dll`
  (from `C:\MSFS 2024 SDK\SimConnect SDK\lib\managed\`).
- Connect, define LVAR/client-data, run a message loop, respond to EFB commands.

## EFB app (control surface)

- Mirror the calculator: TS + TSX + `@efb/efb-api` + esbuild; package to
  `html_ui/efb_ui/efb_apps/MediaPlayer/`.
- Reuse the deploy flow + **reload-without-cache** iteration trick.
- Coherent caveats apply: no `globalThis`; use `DataStore` for any persisted prefs.
