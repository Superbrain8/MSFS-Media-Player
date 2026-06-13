# Decisions

## 2026-06-13 — project created

- Spun off from the MSFS Rentability Calculator after confirming the calculator caused no
  noticeable perf impact → curiosity whether a media app is viable.
- **Confirmed:** media cannot run inside the EFB (Coherent sandbox: no audio/network/OS).
  Marketplace check found nothing like it; closest is an addon streaming audio to the ADF via
  SimConnect → confirms the **companion-app** architecture.
- Decided: build as **EFB control surface + Windows companion `.exe`**, bridged over SimConnect.

## 2026-06-13 — scope/architecture resolved

- [x] **Scope:** **both** local-media control + radio streams.
- [x] **Companion tech:** confirmed C# **.NET 8 Windows** (`net8.0-windows10.x`) + managed SimConnect + WinRT SMTC.
- [x] **Bridge:** LVARs for commands (play/pause/next/vol), client data area for now-playing text. Confirmed.
- [x] **Radio audio route:** **ADF injection** target — realistically = own audio device (NAudio) **gated/attenuated by ADF radio state** read over SimConnect (ADF power, volume, tuned freq). MSFS exposes no API to push PCM into the sim audio mixer, so literal injection is not possible; the ADF layer is control/attenuation, not a sim-mixer feed.
- [x] **v1 surface:** **companion-first**. Build the `.exe` with a system-tray UI and prove media/audio/SMTC + SimConnect ADF gating *before* building the EFB bridge + UI.

## 2026-06-13 — pre-P0 sub-decisions

- **SMTC scope:** control **whatever app holds the active system media session** (generic — Spotify/browser/any). No per-app targeting.
- **Radio station source:** ship a **JSON config** for v1. Later: edit the list in the **companion** (tray), and make stations **selectable from the EFB**.
- **MSFS 2024 SDK:** installed at `C:\MSFS 2024 SDK`.
  - Managed SimConnect: `C:\MSFS 2024 SDK\SimConnect SDK\lib\managed\Microsoft.FlightSimulator.SimConnect.dll`.
  - Native `SimConnect.dll`: `C:\MSFS 2024 SDK\SimConnect SDK\lib\SimConnect.dll` — must sit next to the companion `.exe` at runtime.

## 2026-06-13 — SMTC limitation + media-key fallback (P1)

- **SMTC is the only API exposing track metadata** (title/artist) + targeted transport. Apps must
  register with it. **Qobuz does NOT register SMTC** → no metadata available for it, ever.
- Qobuz **does** respond to hardware media keys. So: companion is **SMTC-first** (full metadata when
  supported — Spotify/Tidal/browser/Windows), **global media-key injection fallback** when no SMTC
  session exists (transport only, no metadata) — covers Qobuz.
- **Known v1 edge case:** if an SMTC app (e.g. Spotify) holds a session while Qobuz also plays,
  commands target the SMTC session, not Qobuz. To media-key-control Qobuz, no SMTC session must
  exist. Acceptable for now; revisit if it bites.

## 2026-06-13 — SimConnect + ADF gating confirmed (P3)

- Managed SimConnect DLL (`Microsoft.FlightSimulator.SimConnect.dll`) **loads fine on .NET 8**
  (not the feared mixed-mode failure). Referenced via `$(MSFS2024_SDK)`; native `SimConnect.dll`
  copied to output. Connects at sim main menu; app id reports "SunRise v12.2" (MSFS 2024).
- **Confirmed ADF SimVars** (probed live): `AVIONICS MASTER SWITCH` (Bool), `ELECTRICAL MASTER
  BATTERY` (Bool), **`ADF VOLUME:1` (Percent, 0–100)** = the cockpit ADF audio knob,
  `ADF ACTIVE FREQUENCY:1` (KHz), `ADF SIGNAL:1`. **Invalid:** `ADF VOLUME` (no index → 0),
  `ADF RADIO:1` (NAME_UNRECOGNIZED).
- **ADF "injection" gate model (final):** gate on **`CIRCUIT AVIONICS ON`** (Bool) —
  `gate = CIRCUIT_AVIONICS_ON ? 1 : 0`; radio output = `userVolume × gate`, applied only while sim
  connected. On disconnect → gate 1.
  - **Verified live in C172:** `CIRCUIT AVIONICS ON` tracks the avionics-bus power (battery +
    avionics master + breaker). `AVIONICS MASTER SWITCH` does **NOT** track the switch (stayed 1);
    `ELECTRICAL MASTER BATTERY`/`:1` stayed 1; `ELECTRICAL MAIN BUS VOLTAGE` read 0 — all useless.
  - ADF VOLUME:1 knob not wired in many aircraft → not used. **Volume control lives in the EFB
    panel / tray**, not the ADF knob. No real PCM injection into the sim mixer (not possible).

## Build phases (derived from the above)

- **P0** Companion skeleton: `net8.0-windows` tray app, single-instance, logging.
- **P1** Local media via SMTC (the unserved niche): enumerate sessions, play/pause/next/prev, read title/artist. Prove first.
- **P2** Radio playback via NAudio: station list, fetch + decode stream, play through default device, volume.
- **P3** SimConnect connect + read ADF state → gate/attenuate radio (the achievable "ADF injection").
- **P4** EFB↔companion bridge: LVAR command channel + client-data status channel.
- **P5** EFB control UI (TSX, mirrors calculator): station/preset list, play/pause/next, volume, now-playing.

## Notes

- Companion app requires the user to run a separate `.exe` — accepted cost.
- Reuse calculator's EFB knowledge: Coherent has no `globalThis`; `GetStoredData`/`SetStoredData`
  are bare globals (use msfs-sdk `DataStore`); debugger needs **reload-without-cache**; build the
  package via in-sim DevMode (bare fspackagetool hangs).
