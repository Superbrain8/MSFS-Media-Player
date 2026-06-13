# MSFS Media Player

In-sim media controller for **Microsoft Flight Simulator 2024**: control playback of
either **local running media** (Spotify/browser/etc. via Windows system controls) and/or
a selection of **internet radio streams**, surfaced through the EFB tablet.

> Sibling project to the MSFS Rentability Calculator. Reuses the same EFB build/deploy
> knowledge but is architecturally bigger because audio + OS control can't live in the EFB.

## The hard constraint (learned from the calculator project)

The EFB runs in the **Coherent GT** engine — a sandboxed UI renderer:
- **No `globalThis`** (use `window`/`self`).
- **No reliable HTML5 audio / codecs** → cannot play streams in-EFB.
- **No external network** guarantees (CORS/sandbox).
- **No OS access / WinRT / shell** → cannot touch Windows media controls.

Market check: the closest existing addon uses an **external SimConnect app to stream audio
into the aircraft ADF** — i.e. audio happens *outside* the EFB. This confirms the only viable
architecture.

## Architecture

```
┌─────────────────────────┐        SimConnect          ┌────────────────────────────┐
│  EFB app (control UI)    │  LVARs / client data /     │  Companion app (.exe, C#)  │
│  - station list / presets│  custom events  ───────▶   │  - reads commands           │
│  - play/pause/next       │  ◀── status (now playing)  │  - controls local media via │
│  - volume                │                            │    Windows SMTC (WinRT)     │
└─────────────────────────┘                            │  - plays radio stream →     │
        (sandboxed)                                      │    audio out / ADF route    │
                                                         └────────────────────────────┘
                                                              (full OS + network access)
```

- **EFB app** = thin control surface only (buttons, lists, now-playing text). Same stack as
  the calculator: TypeScript + TSX + `@efb/efb-api` + esbuild, packaged to `efb_apps/`.
- **Companion app** = does the real work outside the sandbox:
  - **Local media:** Windows System Media Transport Controls (`GlobalSystemMediaTransportControlsSessionManager`) for play/pause/next/track info of whatever app is playing.
  - **Radio:** fetch + decode + play stream (own audio device, or route into the sim's ADF audio like the existing addon).
  - Bridges to the EFB over **SimConnect** (LVARs or client data areas for commands + status).
- **The catch:** the user must run the companion `.exe` alongside the sim. That's the cost of doing audio/OS work with MSFS.

## Open decisions (resolve before coding — see docs/DECISIONS.md)

1. **Scope:** radio streams, local-media control, or both? (local-media is the unserved niche; radio may already exist in marketplace.)
2. **Companion tech:** C# (.NET) with managed SimConnect + WinRT (easiest for SMTC) — confirm.
3. **Audio route for radio:** own audio output vs ADF-injection (matches sim immersion but more complex).
4. **EFB↔app channel:** LVARs (simple, limited) vs SimConnect client data area (structured, more setup).
5. **EFB UI needed at all for v1**, or start companion-app-only with a tray UI?

## Layout (planned)

```
efb-app/        EFB control-surface app (TSX, mirrors calculator structure)
companion-app/  Windows companion (.NET) — SimConnect bridge + media/audio
docs/           ARCHITECTURE.md, DECISIONS.md
```

## Status

Scaffold only. No code yet — pending the scope/architecture decisions above.
Reusable EFB knowledge carried from the calculator project (Coherent limits, package/deploy
flow, reload-without-cache) is captured in docs/ and that project's memory.
