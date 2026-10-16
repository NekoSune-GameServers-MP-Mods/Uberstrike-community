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

## Patch catalogue integration requirement

The intended central patch catalogue URL is:

`https://paradise-directory.nekosunevr.co.uk/patch-file.xml`

The goal is for UniversalUnityPatcher and compatible Paradise/community patcher clients to refresh this central catalogue so newly published community and personal patch definitions become available without users manually replacing XML files.

**This URL is a requested target, not a verified live endpoint.** The current directory API and this browser PR do not implement or publish it. A complete implementation requires coordinated changes:

1. Publish and maintain the XML catalogue from the Paradise directory service, including only reviewed, compatible patch definitions or trusted references to provider manifests.
2. Update UniversalUnityPatcher (or its supported extension mechanism) to download and refresh the remote catalogue; an XML patch definition cannot fetch or update itself.
3. Add a documented provider/manifest contract and ownership/opt-in rules so community providers can publish updates safely.
4. Validate XML and compatibility metadata before applying patches, use HTTPS, retain the last known-good catalogue if an update fails, and do not silently execute untrusted patch payloads.
5. Keep server discovery separate from patch trust: an opted-in public server listing must not automatically make its provider or patch trusted.

This browser PR currently covers public server discovery, not the UniversalUnityPatcher implementation or deployment of the catalogue endpoint. The central catalogue must not be described as live until the directory service actually serves and validates it.

## Build

Requirements: Windows and the .NET 8 SDK.

```powershell
dotnet build .\\network-browser\\ParadiseNetworkBrowser.csproj -c Release
dotnet run --project .\\network-browser\\ParadiseNetworkBrowser.csproj
```

## Current limitations

- The app only displays records returned by the public API. The directory currently filters out private, disabled, offline, stale, and unverified records. A server heartbeat alone is not proof that its realtime game endpoint is reachable.
- Copying an endpoint does not automatically join the game; a supported client connection command or game integration must be implemented and tested separately.
- Provider discovery is not implemented by the current directory API. The proposed `/api/community/providers` endpoint and provider manifests must be added to the service before this app can display provider catalogues or download provider patch definitions.
- The app does not download, execute, or apply arbitrary patches. Patch provider trust, manifest signing, compatibility checks, and a safe installation workflow should be implemented before adding patch installation.

The default directory URL can be changed in the text field for development or alternate compatible deployments.
