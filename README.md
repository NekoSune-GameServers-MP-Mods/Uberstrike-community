# UberStrike Community Server Discovery

This repository is the coordination point for a community-owned UberStrike server directory. The upstream implementations remain in the separate Paradise and NekoNexus repositories; this project records their compatibility boundary and develops the shared directory without copying their game-service implementation.

## Directory API prototype

The repository now contains a dependency-free Python 3 directory service in `directory/`. Start it with:

```powershell
python directory/service.py
```

The default bind is `127.0.0.1:8090`; configure `DIRECTORY_HOST`, `DIRECTORY_PORT`, and `DIRECTORY_DB` for local deployment. Put it behind an HTTPS reverse proxy before exposing it to the internet. The public read endpoint is `GET /api/community/servers`. Owners can submit a one-time record to `POST /api/v1/servers/register`, then send HMAC-SHA256 authenticated, timestamped, nonce-protected heartbeats to `/api/v1/servers/{id}/heartbeat`. Generate a UUID v4 and at least 40 random URL-safe credential characters on the host; store the credential locally and never share it between servers.

The directory stores credentials as SHA-256 hashes, validates public IPv4 endpoints and metadata, rate limits requests, and omits credentials and admin fields from public responses. Records distinguish `nekonexus`, `paradise`, or `other` software and `game` or `comm` endpoint usage. Paradise PR #2 reports connected Paradise nodes to this directory. A heartbeat only refreshes last-seen metadata; a trusted protocol-aware probe must explicitly mark a server online. The public list excludes unverified, private, disabled, offline and stale entries.

Paradise PR #2 wires Paradise registration/heartbeats into startup and adapts verified public directory records into the legacy `AuthenticateApplication` response, which feeds UberStrike's existing in-game browser. The repository still does not include a trusted realtime probe worker or directory moderation/credential-rotation interface. Do not mark servers online without a compatible probe, and do not treat this prototype as production-ready until probe operations, TLS/reverse-proxy routing, credential revocation, and administrative controls are deployed.

Run its current unit coverage with `python -m unittest discover -s directory -v`.

## Docker image

Build and start locally with `docker compose up --build -d`. The container stores SQLite data in the `directory-data` volume and binds to localhost port `8090`; put an HTTPS reverse proxy in front of it for public hosting. On startup, the entrypoint repairs ownership of the mounted database directory/file and then drops privileges to the `directory` service account, which also supports volumes created by older images. The GitHub Actions workflow builds and publishes `ghcr.io/nekosune-gameservers-mp-mods/uberstrike-community-directory` on pushes to `main` and version tags. Pull requests build and test without publishing.

## Windows client installer (early preview)

`patcher/Install-UberStrikeCommunity.ps1` is a shared installer entry point, not a patcher per server. It reuses NekoNexus's existing UniversalUnityPatcher, client bootstrap, and LiteNetLib transport. It requires an UberStrike installation and a local NekoNexus source checkout; it can build the patcher when NuGet and MSBuild are available. By default, clients use `paradise-directory.nekosunevr.co.uk`. Example:

```powershell
.\patcher\Install-UberStrikeCommunity.ps1 `
  -UberStrikePath 'C:\Games\UberStrike' `
  -NekoNexusRepoPath 'D:\DEV\Forked Projects\NekoNexus' `
  # Shared default; override only for local testing or another compatible deployment.
  -WebServiceHost 'paradise-directory.nekosunevr.co.uk'
```

HTTPS is used by default. For a local test host only, pass `-InsecureHttp` and set ports as needed. Close the game before running the installer. The patcher backs up the game assembly and original Photon transport using the upstream installation flow. The existing in-game server browser receives directory entries through Paradise's legacy `AuthenticateApplication` adapter (tracked in Paradise PR #2); clients do not query the JSON directory directly. The shared hostname must route legacy UberStrike SOAP paths to the compatible Paradise web-service host and `/api/community/servers` to the directory service. No game binaries or patcher executable are bundled here.

The `Build UniversalUnityPatcher for Windows` workflow runs on pushes to `main`, pull requests, and manual dispatch. It reads NekoNexus's Windows build steps, builds the generic UniversalUnityPatcher on `windows-latest`, and uploads a ZIP artifact. That artifact is only the generic patching utility and its license/readme; it is not a ready-to-install UberStrike community patch. Game references stay off GitHub runners because they are proprietary, and the community client patch definition/bootstrap still needs to be implemented.

## Current status

The repository investigation and compatibility report are in [docs/architecture-and-compatibility.md](docs/architecture-and-compatibility.md). The shared discovery path is implemented, but deployment still depends on the directory's trusted realtime probe and correct reverse-proxy routing:

- NekoNexus serves the game's legacy `AuthenticateApplication` response, but its server entries are configured statically in `ServerCredentials` and the database.
- Paradise stores server records in its `PhotonServers` database model and PR #2 adapts verified public directory records into the legacy `AuthenticateApplication` response. `GetPhotonServers` remains outside the active browser path.
- Both client integrations require a compatible UberStrike client build. The game consumes serialized `PhotonServer` views, not a modern JSON server list.

## Planned integration

1. Keep the community directory API's authenticated registration, heartbeat, and verified public-list contract.
2. Paradise PR #2 integrates Paradise server reporting and the legacy `AuthenticateApplication` adapter; NekoNexus-side reporting/adapter parity remains follow-up work.
3. Implement and deploy the trusted protocol-aware realtime probe and moderation/credential lifecycle.
4. Verify the shared hostname's reverse-proxy routes, in-game list rendering, and real joins with a compatible client.

The directory must not advertise an entry as online until a protocol-aware probe has validated its realtime listener. A heartbeat by itself is not gameplay verification. The current upstream server code does not yet expose a directory probe handler, so public discovery remains an integration task rather than a claim of completed end-to-end behavior.

## Upstream repositories

- [NekoNexus](https://github.com/NekoSune-GameServers-MP-Mods/NekoNexus)
- [Paradise](https://github.com/festivaldev/Paradise)
- [UberStrike reverse-engineered protocol sources](https://github.com/festivaldev/UberStrike-Reverse-Engineered)

Respect each project's license. Do not distribute proprietary UberStrike assets or reference assemblies.
