# Companion app

Windows companion `.exe` for MSFS Media Player. Does the work the EFB sandbox can't:
local-media control (SMTC), radio playback (NAudio), and the SimConnect bridge to the EFB.

See [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) and [../docs/DECISIONS.md](../docs/DECISIONS.md).

## Status

**P4 — EFB bridge.** Reads EFB commands (`L:MEDIAPLAYER_CMD`) + volume (`L:MEDIAPLAYER_RADIO_VOL`)
over SimConnect and dispatches to media/radio; writes status LVARs (`_RADIO_PLAYING`, `_RADIO_IDX`,
`_GATE`, `_LOCAL_PLAYING`) back for the EFB to poll. Gate on `CIRCUIT AVIONICS ON` (P3). Plus P1
(SMTC + media-key fallback) and P2 (radio). The EFB control surface lives in `../efb-app`.
Remaining: now-playing TEXT to the EFB (needs a SimConnect client-data area).

Stations config: `%LocalAppData%\MsfsMediaPlayer\stations.json` — edit + restart to change the list.

## Requirements

- **.NET 8 SDK** (`winget install Microsoft.DotNet.SDK.8`). Runtime alone is not enough to build.
- Windows 10 build 19041+ (for the WinRT SMTC projections used from P1 on).

## Build & run

```pwsh
cd companion-app
dotnet build
dotnet run
```

A tray icon appears with a "Companion running" balloon. Right-click → Exit to quit,
or "Open log folder" for logs.

## Logs

`%LocalAppData%\MsfsMediaPlayer\logs\companion-<timestamp>.log` (one file per run).

## SimConnect (from P3)

Managed wrapper: `C:\MSFS 2024 SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`.
The native `SimConnect.dll` (`...\lib\SimConnect.dll`) must sit next to the built `.exe` at runtime.
