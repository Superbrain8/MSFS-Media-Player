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

## Not redistributed (supplied by the user's MSFS SDK)

- **SimConnect** — `Microsoft.FlightSimulator.SimConnect.dll` (managed wrapper) and
  `SimConnect.dll` (native) are **proprietary Microsoft components** from the MSFS 2024 SDK.
  They are **not** included in this repository or in published binaries. The build references
  them from your local SDK via the `MSFS2024_SDK` environment variable, and copies the native
  DLL next to the built executable for local use only. To run a build, install the
  **MSFS 2024 SDK** yourself. Redistribution of these DLLs is governed by the Microsoft
  Flight Simulator SDK EULA, not by this project's license.
