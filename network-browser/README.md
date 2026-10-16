# Paradise Network Browser

A Windows desktop companion for the community-owned UberStrike directory. The app reads the shared public server list from:

`https://paradise-directory.nekosunevr.co.uk/api/community/servers`

## Features

- Refresh the public server list on demand and every 30 seconds.
- Search by server name, address, region, map, and game mode.
- Filter by Paradise, NekoNexus, or other server software.
- Filter game servers and community nodes.
- View public server metadata and copy an endpoint.
- Uses the shared directory API rather than a separate local server catalogue.

## Build

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build .\network-browser\ParadiseNetworkBrowser.csproj -c Release
dotnet run --project .\network-browser\ParadiseNetworkBrowser.csproj
```

## Current limitations

- The app only displays records returned by the public API. The directory currently filters out private, disabled, offline, stale, and unverified records. A server heartbeat alone is not proof that its realtime game endpoint is reachable.
- Copying an endpoint does not automatically join the game; a supported client connection command or game integration must be implemented and tested separately.
- Provider discovery is not implemented by the current directory API. The proposed `/api/community/providers` endpoint and provider manifests must be added to the service before this app can display provider catalogues or download provider patch definitions.
- The app does not download, execute, or apply arbitrary patches. Patch provider trust, manifest signing, compatibility checks, and a safe installation workflow should be implemented before adding patch installation.

The default directory URL can be changed in the text field for development or alternate compatible deployments.
