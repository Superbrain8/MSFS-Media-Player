import { GamepadUiView, Marquee, RequiredProps, Slider, TTButton, TVNode, UiViewProps } from "@efb/efb-api";
import { FSComponent, Subject, VNode } from "@microsoft/msfs-sdk";
import {
  localNext,
  localPlayPause,
  localPrev,
  radioPlay,
  radioStop,
  readStatus,
  setRadioVolume,
  Stations,
} from "../bridge/MediaBridge";
import "./MediaControlPage.scss";

type MediaControlPageProps = RequiredProps<UiViewProps, "appViewService">;

const INITIAL_VOLUME = 60;

/**
 * The whole control surface: local-media transport, a vertical radio station list (the playing
 * station is highlighted), a volume slider, and live status polled from the companion's LVARs.
 * Thin — all real work happens in the companion.
 */
export class MediaControlPage extends GamepadUiView<HTMLDivElement, MediaControlPageProps> {
  public readonly tabName = MediaControlPage.name;

  private readonly nowPlaying = Subject.create("Connecting…");
  private readonly gateOpen = Subject.create(true);
  /** Index of the currently playing station, -1 if none. Drives the row highlight. */
  private readonly playingIdx = Subject.create(-1);
  private readonly volume = Subject.create(INITIAL_VOLUME);

  private pollHandle = 0;

  public onAfterRender(node: VNode): void {
    super.onAfterRender(node);
    setRadioVolume(this.volume.get());
    this.pollHandle = window.setInterval(() => this.poll(), 300);
  }

  public destroy(): void {
    if (this.pollHandle) window.clearInterval(this.pollHandle);
    super.destroy();
  }

  private poll(): void {
    const s = readStatus();
    this.gateOpen.set(s.gateOpen);
    this.playingIdx.set(s.radioPlaying ? s.radioIdx : -1);
    if (s.radioPlaying) {
      this.nowPlaying.set(`Radio — ${Stations[s.radioIdx] ?? "playing"}`);
    } else if (s.localPlaying) {
      this.nowPlaying.set("Local media playing");
    } else {
      this.nowPlaying.set("Idle");
    }
  }

  private onVolume(value: number): void {
    this.volume.set(value);
    setRadioVolume(value);
  }

  public render(): TVNode<HTMLDivElement> {
    return (
      <div ref={this.gamepadUiViewRef} class="media-control-page">
        <div class="status-bar">
          <Marquee class="now-playing">{this.nowPlaying}</Marquee>
          <span class={this.gateOpen.map((o) => (o ? "gate ok" : "gate muted"))}>
            {this.gateOpen.map((o) => (o ? "Avionics ON" : "Avionics OFF — muted"))}
          </span>
        </div>

        <section class="block">
          <h3>Local media</h3>
          <div class="transport">
            <TTButton key="◄◄" type="secondary" callback={localPrev} />
            <TTButton key="▶ / ⏸" callback={localPlayPause} />
            <TTButton key="►►" type="secondary" callback={localNext} />
          </div>
        </section>

        <section class="block radio">
          <h3>Radio</h3>
          <div class="station-list">
            {Stations.map((name, i) => (
              <div class={this.playingIdx.map((p) => (p === i ? "station selected" : "station"))}>
                <span class="marker">{this.playingIdx.map((p) => (p === i ? "▶" : ""))}</span>
                <TTButton key={name} type="secondary" callback={(): void => radioPlay(i)} />
              </div>
            ))}
          </div>
          <div class="radio-actions">
            <TTButton key="Stop" type="secondary" callback={radioStop} />
          </div>
        </section>

        <section class="block">
          <h3>Volume</h3>
          <div class="volume-row">
            <Slider value={this.volume} min={0} max={100} step={5} onValueChange={(v): void => this.onVolume(v)} />
            <span class="volume-pct">{this.volume.map((v) => `${Math.round(v)}%`)}</span>
          </div>
        </section>
      </div>
    );
  }
}
