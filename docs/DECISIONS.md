# Decisions

## 2026-06-13 — project created

- Spun off from the MSFS Rentability Calculator after confirming the calculator caused no
  noticeable perf impact → curiosity whether a media app is viable.
- **Confirmed:** media cannot run inside the EFB (Coherent sandbox: no audio/network/OS).
  Marketplace check found nothing like it; closest is an addon streaming audio to the ADF via
  SimConnect → confirms the **companion-app** architecture.
- Decided: build as **EFB control surface + Windows companion `.exe`**, bridged over SimConnect.

## To decide (blocking real work)

- [ ] **Scope:** radio / local-media / both. (Lean: local-media control = unserved niche.)
- [ ] **Companion tech:** confirm C# .NET (`net8.0-windows`) + managed SimConnect + WinRT SMTC.
- [ ] **Bridge:** LVARs for commands; client data area for now-playing text — confirm.
- [ ] **Radio audio route:** own audio device (v1) vs ADF injection (stretch).
- [ ] **v1 surface:** EFB UI from the start, or companion-only (system tray) first to de-risk the
      hard part (media/audio) before building the EFB bridge + UI.

## Notes

- Companion app requires the user to run a separate `.exe` — accepted cost.
- Reuse calculator's EFB knowledge: Coherent has no `globalThis`; `GetStoredData`/`SetStoredData`
  are bare globals (use msfs-sdk `DataStore`); debugger needs **reload-without-cache**; build the
  package via in-sim DevMode (bare fspackagetool hangs).
