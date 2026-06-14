import { GamepadUiView, RequiredProps, Slider, TTButton, TVNode, UiViewProps } from "@efb/efb-api";
import { FSComponent, Subject, VNode } from "@microsoft/msfs-sdk";
import {
  localNext,
  localPlayPause,
  localPrev,
  radioPlay,
  radioStop,
  readNowPlayingText,
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
  /** The user-selected station index (persists through pause), -1 if none. Drives the highlight. */
  private readonly selectedSub = Subject.create(-1);
  private readonly volume = Subject.create(INITIAL_VOLUME);

  // "Radio mode" = a station is selected (selectedIdx >= 0); it stays selected while paused, and is
  // only cleared by Stop. Transport drives the radio in this mode, otherwise local media.
  private selectedIdx = -1;
  private radioPlaying = false;

  // Auto-scrolling now-playing marquee (the SDK Marquee only scrolls on hover).
  private readonly npContainer = FSComponent.createRef<HTMLDivElement>();
  private readonly npText = FSComponent.createRef<HTMLSpanElement>();

  private pollHandle = 0;

  public onAfterRender(node: VNode): void {
    super.onAfterRender(node);
    setRadioVolume(this.volume.get());
    this.nowPlaying.sub(() => this.updateMarquee());
    this.pollHandle = window.setInterval(() => this.poll(), 300);
  }

  /** Animate the now-playing text only when it overflows its container; otherwise leave it still. */
  private updateMarquee(): void {
    window.requestAnimationFrame(() => {
      const box = this.npContainer.instance;
      const text = this.npText.instance;
      if (!box || !text) return;
      const overflow = text.scrollWidth - box.clientWidth;
      if (overflow > 2) {
        text.style.setProperty("--np-shift", `${-overflow}px`);
        text.style.setProperty("--np-dur", `${Math.max(4, overflow / 40)}s`); // ~40 px/s
        text.classList.add("np-scroll");
      } else {
        text.classList.remove("np-scroll");
        text.style.removeProperty("--np-shift");
      }
    });
  }

  public destroy(): void {
    if (this.pollHandle) window.clearInterval(this.pollHandle);
    super.destroy();
  }

  private poll(): void {
    const s = readStatus();
    this.radioPlaying = s.radioPlaying;
    this.gateOpen.set(s.gateOpen);

    // Adopt the companion's station if it's already playing when the app opens.
    if (s.radioPlaying && this.selectedIdx < 0) this.select(s.radioIdx);

    if (this.selectedIdx >= 0) {
      const name = Stations[this.selectedIdx] ?? "station";
      this.nowPlaying.set(s.radioPlaying ? `Radio: ${name}` : `Radio (paused): ${name}`);
    } else if (s.localPlaying) {
      this.nowPlaying.set(readNowPlayingText() || "Local media playing");
    } else {
      this.nowPlaying.set("Idle");
    }
  }

  private select(index: number): void {
    this.selectedIdx = index;
    this.selectedSub.set(index);
  }

  /** Tapping a station starts it; tapping the selected one again stops + deselects it. */
  private toggleStation(index: number): void {
    if (index === this.selectedIdx) {
      this.select(-1);
      radioStop();
    } else {
      this.select(index);
      radioPlay(index);
    }
  }

  /** Pick + start a station (used by Next/Prev). */
  private playStation(index: number): void {
    this.select(index);
    radioPlay(index);
  }

  private onVolume(value: number): void {
    this.volume.set(value);
    setRadioVolume(value);
  }

  // Transport drives the radio while a station is selected (Play/Pause toggles the stream but keeps
  // the selection); otherwise it drives local media.
  private onPlayPause(): void {
    if (this.selectedIdx >= 0) {
      if (this.radioPlaying) radioStop(); // pause — selection kept
      else radioPlay(this.selectedIdx); // resume
    } else {
      localPlayPause();
    }
  }

  private onNext(): void {
    if (this.selectedIdx >= 0) this.playStation((this.selectedIdx + 1) % Stations.length);
    else localNext();
  }

  private onPrev(): void {
    if (this.selectedIdx >= 0) this.playStation((this.selectedIdx - 1 + Stations.length) % Stations.length);
    else localPrev();
  }

  public render(): TVNode<HTMLDivElement> {
    return (
      <div ref={this.gamepadUiViewRef} class="media-control-page">
        {/* Clears the EFB shell's top bar. Inline style bypasses the cached coui:// stylesheet. */}
        <div class="top-safe" style="flex: 0 0 auto; height: 96px; width: 100%;" />
        <div class="np-bar">
          <div class="now-playing" ref={this.npContainer}>
            <span class="np-text" ref={this.npText}>
              {this.nowPlaying}
            </span>
          </div>
          <span class={this.gateOpen.map((o) => (o ? "gate ok" : "gate muted"))}>
            {this.gateOpen.map((o) => (o ? "Avionics ON" : "Avionics OFF (muted)"))}
          </span>
        </div>

        <section class="block">
          <h3>Media</h3>
          <div class="transport">
            <TTButton key="Prev" type="secondary" callback={(): void => this.onPrev()} />
            <TTButton key="Play / Pause" callback={(): void => this.onPlayPause()} />
            <TTButton key="Next" type="secondary" callback={(): void => this.onNext()} />
          </div>
        </section>

        <section class="block">
          <h3>Volume</h3>
          <div class="volume-row">
            <Slider value={this.volume} min={0} max={100} step={5} onValueChange={(v): void => this.onVolume(v)} />
            <span class="volume-pct">{this.volume.map((v) => `${Math.round(v)}%`)}</span>
          </div>
        </section>

        <section class="block radio">
          <h3>Radio</h3>
          <div class="station-list">
            {Stations.map((name, i) => (
              <div class={this.selectedSub.map((p) => (p === i ? "station selected" : "station"))}>
                <span class="marker">{this.selectedSub.map((p) => (p === i ? ">" : ""))}</span>
                <TTButton key={name} type="secondary" callback={(): void => this.toggleStation(i)} />
              </div>
            ))}
          </div>
        </section>
      </div>
    );
  }
}
