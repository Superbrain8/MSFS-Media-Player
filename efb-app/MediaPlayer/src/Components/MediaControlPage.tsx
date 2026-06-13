import { GamepadUiView, RequiredProps, TTButton, TVNode, UiViewProps } from "@efb/efb-api";
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

const VOLUME_STEP = 10;

/**
 * The whole control surface: local-media transport, radio station list + stop, volume, and live
 * status read from the companion's LVARs. Thin — all real work happens in the companion.
 */
export class MediaControlPage extends GamepadUiView<HTMLDivElement, MediaControlPageProps> {
  public readonly tabName = MediaControlPage.name;

  private readonly statusText = Subject.create("Connecting…");
  private readonly gateText = Subject.create("");
  private readonly volumeText = Subject.create("60%");

  private volume = 60;
  private pollHandle = 0;

  public onAfterRender(node: VNode): void {
    super.onAfterRender(node);
    setRadioVolume(this.volume);
    this.pollHandle = window.setInterval(() => this.poll(), 300);
  }

  public destroy(): void {
    if (this.pollHandle) window.clearInterval(this.pollHandle);
    super.destroy();
  }

  private poll(): void {
    const s = readStatus();
    if (s.radioPlaying) {
      const name = Stations[s.radioIdx] ?? "playing";
      this.statusText.set(`Radio — ${name}`);
    } else if (s.localPlaying) {
      this.statusText.set("Local media playing");
    } else {
      this.statusText.set("Idle");
    }
    this.gateText.set(s.gateOpen ? "Avionics ON" : "Avionics OFF — radio muted");
  }

  private changeVolume(delta: number): void {
    this.volume = Math.max(0, Math.min(100, this.volume + delta));
    this.volumeText.set(`${this.volume}%`);
    setRadioVolume(this.volume);
  }

  public render(): TVNode<HTMLDivElement> {
    return (
      <div ref={this.gamepadUiViewRef} class="media-control-page">
        <div class="status-bar">
          <span class="now-playing">{this.statusText}</span>
          <span class="gate">{this.gateText}</span>
        </div>

        <section class="block">
          <h3>Local media</h3>
          <div class="row">
            <TTButton key="◄◄" type="secondary" callback={localPrev} />
            <TTButton key="▶ / ⏸" callback={localPlayPause} />
            <TTButton key="►►" type="secondary" callback={localNext} />
          </div>
        </section>

        <section class="block">
          <h3>Radio</h3>
          <div class="stations">
            {Stations.map((name, i) => (
              <TTButton key={name} type="secondary" callback={(): void => radioPlay(i)} />
            ))}
          </div>
          <div class="row">
            <TTButton key="Stop radio" type="secondary" callback={radioStop} />
          </div>
        </section>

        <section class="block">
          <h3>Radio volume</h3>
          <div class="row">
            <TTButton key="–" type="secondary" callback={(): void => this.changeVolume(-VOLUME_STEP)} />
            <span class="volume">{this.volumeText}</span>
            <TTButton key="+" type="secondary" callback={(): void => this.changeVolume(VOLUME_STEP)} />
          </div>
        </section>
      </div>
    );
  }
}
