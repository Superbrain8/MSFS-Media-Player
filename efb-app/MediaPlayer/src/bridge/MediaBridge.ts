/**
 * EFB ↔ companion bridge contract (P4).
 *
 * Channel: SimConnect LVARs (numeric). The EFB writes commands + volume; the companion reads them,
 * acts via SMTC/NAudio/SimConnect, and writes numeric status back for the EFB to poll. Now-playing
 * TEXT (track/station name) needs a client-data area and comes later — for now the EFB shows its own
 * station names and numeric state.
 *
 * The companion mirrors these exact LVAR names and command codes.
 */

declare const SimVar: {
  GetSimVarValue(name: string, unit: string): number;
  SetSimVarValue(name: string, unit: string, value: number): Promise<unknown>;
};

const NUMBER = "number";

/** LVAR names. EFB→companion: CMD, RADIO_VOL. companion→EFB: the status vars. */
export const LVar = {
  /** Command pulse written by the EFB; companion executes then resets to 0. */
  Cmd: "L:MEDIAPLAYER_CMD",
  /** Radio volume 0–100, written by the EFB. */
  RadioVol: "L:MEDIAPLAYER_RADIO_VOL",
  /** Status: radio playing 0/1. */
  RadioPlaying: "L:MEDIAPLAYER_RADIO_PLAYING",
  /** Status: currently playing station index, -1 if none. */
  RadioIdx: "L:MEDIAPLAYER_RADIO_IDX",
  /** Status: avionics power gate 0/1 (radio audible only when 1 in-sim). */
  Gate: "L:MEDIAPLAYER_GATE",
  /** Status: local media (SMTC) playing 0/1. */
  LocalPlaying: "L:MEDIAPLAYER_LOCAL_PLAYING",
} as const;

/** Command codes written to {@link LVar.Cmd}. Station play = Radio + index. */
export const Cmd = {
  None: 0,
  LocalPlayPause: 1,
  LocalNext: 2,
  LocalPrev: 3,
  RadioStop: 10,
  /** Play station N → RadioPlayBase + N. */
  RadioPlayBase: 100,
} as const;

/** Station list shown in the EFB. Mirrors the companion defaults until list-sync (client data) lands. */
export const Stations: readonly string[] = [
  "SomaFM - Groove Salad",
  "SomaFM - Drone Zone",
  "Radio Paradise - Main Mix",
  "Radio Paradise - Mellow Mix",
  "Radio Bob - Harte Saite",
];

export function sendCommand(code: number): void {
  SimVar.SetSimVarValue(LVar.Cmd, NUMBER, code);
}

export const localPlayPause = (): void => sendCommand(Cmd.LocalPlayPause);
export const localNext = (): void => sendCommand(Cmd.LocalNext);
export const localPrev = (): void => sendCommand(Cmd.LocalPrev);
export const radioStop = (): void => sendCommand(Cmd.RadioStop);
export const radioPlay = (index: number): void => sendCommand(Cmd.RadioPlayBase + index);

export function setRadioVolume(volume0to100: number): void {
  const v = Math.max(0, Math.min(100, Math.round(volume0to100)));
  SimVar.SetSimVarValue(LVar.RadioVol, NUMBER, v);
}

export interface MediaStatus {
  radioPlaying: boolean;
  radioIdx: number;
  gateOpen: boolean;
  localPlaying: boolean;
}

export function readStatus(): MediaStatus {
  return {
    radioPlaying: SimVar.GetSimVarValue(LVar.RadioPlaying, NUMBER) !== 0,
    radioIdx: SimVar.GetSimVarValue(LVar.RadioIdx, NUMBER),
    gateOpen: SimVar.GetSimVarValue(LVar.Gate, NUMBER) !== 0,
    localPlaying: SimVar.GetSimVarValue(LVar.LocalPlaying, NUMBER) !== 0,
  };
}
