# Third-party notices

MSFS Media Player (the original code in this repository) is licensed under the
**GNU AGPL-3.0** (see [LICENSE](LICENSE)). It bundles or builds against the
following third-party components, under their own licenses.

## Bundled in this repository

- **@microsoft/msfs-sdk** — © Working Title Simulations — MIT License.
  (`efb-app/vendor/microsoft-msfs-sdk-*.tgz`)
- **@efb/efb-api** — © Asobo Studio — MIT License.
  (`efb-app/efb_api/`)

The EFB app structure/build config (`efb-app/MediaPlayer/build.js`, `tsconfig.json`,
etc.) is adapted from the MSFS 2024 SDK EFB sample template (Asobo Studio, MIT).

## NuGet packages (restored at build, not committed)

- **NAudio** — © Mark Heath & contributors — MIT License.

## SimConnect (Microsoft, proprietary)

- `Microsoft.FlightSimulator.SimConnect.dll` (managed wrapper) and `SimConnect.dll` (native) are
  **proprietary Microsoft components** from the MSFS 2024 SDK, **not** covered by this project's
  AGPL license.
- They are **not committed to this repository** — the build references them from your local SDK via
  the `MSFS2024_SDK` environment variable, so contributors install the MSFS 2024 SDK themselves.
- Binary **releases bundle the SimConnect client DLLs** so the app runs without the SDK, as is
  standard for MSFS add-ons (SimConnect is the redistributable client interface). Their use/
  redistribution is governed by the Microsoft Flight Simulator SDK EULA. Use
  `package-release.ps1 -ExcludeSimConnect` to build a release without them.
