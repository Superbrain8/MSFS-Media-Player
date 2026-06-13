import {
  App,
  AppBootMode,
  AppInstallProps,
  AppSuspendMode,
  AppView,
  AppViewProps,
  Efb,
  RequiredProps,
  TVNode,
} from "@efb/efb-api";
import { FSComponent, VNode } from "@microsoft/msfs-sdk";
import { MediaControlPage } from "./Components/MediaControlPage";

import "./MediaPlayer.scss";

/** Defined in build.js — points at this app's dist folder (for asset/css URLs). */
declare const BASE_URL: string;

class MediaPlayerView extends AppView<RequiredProps<AppViewProps, "bus">> {
  protected defaultView = "MediaControlPage";

  protected registerViews(): void {
    this.appViewService.registerPage("MediaControlPage", () => (
      <MediaControlPage appViewService={this.appViewService} />
    ));
  }

  public render(): VNode {
    return <div class="media-player">{super.render()}</div>;
  }
}

class MediaPlayer extends App {
  public get name(): string {
    return "Media Player";
  }

  public get icon(): string {
    return `${BASE_URL}/Assets/app-icon.svg`;
  }

  public BootMode = AppBootMode.COLD;
  public SuspendMode = AppSuspendMode.SLEEP;

  public async install(_props: AppInstallProps): Promise<void> {
    // Cache-bust: Coherent caches coui:// stylesheets, so a plain reload keeps serving the old CSS
    // even after the JS updates. A per-launch query forces a fresh fetch.
    Efb.loadCss(`${BASE_URL}/MediaPlayer.css?v=${Date.now()}`);
    return Promise.resolve();
  }

  public get compatibleAircraftModels(): string[] | undefined {
    return undefined;
  }

  public render(): TVNode<MediaPlayerView> {
    return <MediaPlayerView bus={this.bus} />;
  }
}

Efb.use(MediaPlayer);
