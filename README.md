# UberStrike Community Server Discovery

This repository is the coordination point for a community-owned UberStrike server directory. The upstream implementations remain in the separate Paradise and NekoNexus repositories; this project records their compatibility boundary and develops the shared directory without copying their game-service implementation.

## Directory API prototype

The repository now contains a dependency-free Python 3 directory service in `directory/`. Start it with:

```powershell
python directory/service.py
```

The default bind is `127.0.0.1:8090`; configure `DIRECTORY_HOST`, `DIRECTORY_PORT`, and `DIRECTORY_DB` for local deployment. Put it behind an HTTPS reverse proxy before exposing it to the internet. The public read endpoint is `GET /api/community/servers`. Owners can submit a one-time record to `POST /api/v1/servers/register`, then send HMAC-SHA256 authenticated, timestamped, nonce-protected heartbeats to `/api/v1/servers/{id}/heartbeat`. Generate a UUID v4 and at least 40 random URL-safe credential characters on the host; store the credential locally and never share it between servers.

The prototype stores credentials as SHA-256 hashes, validates public IPv4 endpoints and metadata, rate limits requests, and omits credentials and admin fields from public responses. Records distinguish `nekonexus`, `paradise`, or `other` software and `game` or `comm` endpoint usage. Paradise-shaped records are accepted and returned, but Paradise does not currently report itself to this directory. A heartbeat only sets last-seen metadata. A trusted protocol-aware probe must explicitly mark the server online; the server list excludes unverified, private, disabled, offline and stale entries.

This API is not yet wired to Paradise/NekoNexus startup, and no real realtime probe worker exists yet. Paradise server presence currently requires a Paradise-side registration/heartbeat integration; its existing realtime WebSocket only reports to its own web service. So the in-game browser does not yet use this service. Do not operate this prototype as a production directory until TLS, credential rotation/revocation, administrative controls, probe implementation, and the legacy `AuthenticateApplication` integration are completed.

Run its current unit coverage with `python -m unittest discover -s directory -v`.

## Docker image

Build and start locally with `docker compose up --build -d`. The container stores SQLite data in the `directory-data` volume and binds to localhost port `8090`; put an HTTPS reverse proxy in front of it for public hosting. On startup, the entrypoint repairs ownership of the mounted database directory/file and then drops privileges to the `directory` service account, which also supports volumes created by older images. The GitHub Actions workflow builds and publishes `ghcr.io/nekosune-gameservers-mp-mods/uberstrike-community-directory` on pushes to `main` and version tags. Pull requests build and test without publishing.

## Windows client installer (early preview)

`patcher/Install-UberStrikeCommunity.ps1` is an installer entry point that reuses NekoNexus's existing UniversalUnityPatcher, client bootstrap, and LiteNetLib transport. It requires an UberStrike installation and a local NekoNexus source checkout; it can build the patcher when NuGet and MSBuild are available. Example:

```powershell
.\patcher\Install-UberStrikeCommunity.ps1 `
  -UberStrikePath 'C:\Games\UberStrike' `
  -NekoNexusRepoPath 'D:\DEV\Forked Projects\NekoNexus' `
  -WebServiceHost 'play.example.org'
```

HTTPS is used by default. For a local test host only, pass `-InsecureHttp` and set ports as needed. Close the game before running the installer. The patcher backs up the game assembly and original Photon transport using the upstream installation flow. This preview currently installs a NekoNexus-compatible client; it cannot show this repository's directory until the legacy game-service adapter is implemented. No game binaries or patcher executable are bundled here.

The `Build UniversalUnityPatcher for Windows` workflow runs on pushes to `main`, pull requests, and manual dispatch. It reads NekoNexus's Windows build steps, builds the generic UniversalUnityPatcher on `windows-latest`, and uploads a ZIP artifact. That artifact is only the generic patching utility and its license/readme; it is not a ready-to-install UberStrike community patch. Game references stay off GitHub runners because they are proprietary, and the community client patch definition/bootstrap still needs to be implemented.

## Current status

The repository investigation and compatibility report are in [docs/architecture-and-compatibility.md](docs/architecture-and-compatibility.md). The current repositories do not yet provide a complete automatic public directory:

- NekoNexus serves the game's legacy `AuthenticateApplication` response, but its server entries are configured statically in `ServerCredentials` and the database.
- Paradise stores server records in its `PhotonServers` database model. Its legacy `GetPhotonServers` implementation currently returns “Not Implemented”; its client still receives server entries from the `AuthenticateApplication` response.
- Both client integrations require a compatible UberStrike client build. The game consumes serialized `PhotonServer` views, not a modern JSON server list.

## Planned integration

1. Add a community directory API with authenticated server registration, heartbeats, health state, and a filtered public list.
2. Integrate NekoNexus server startup with the directory while keeping the game server running during directory outages.
3. Have NekoNexus's existing `AuthenticateApplication` response use verified directory entries, serialized with its existing UberStrike protocol library.
4. Add moderation and owner onboarding, then verify the list and realtime connection with a live compatible client.

The directory must not advertise an entry as online until a protocol-aware probe has validated its realtime listener. A heartbeat by itself is not gameplay verification. The current upstream server code does not yet expose a directory probe handler, so public discovery remains an integration task rather than a claim of completed end-to-end behavior.

## Upstream repositories

- [NekoNexus](https://github.com/NekoSune-GameServers-MP-Mods/NekoNexus)
- [Paradise](https://github.com/festivaldev/Paradise)
- [UberStrike reverse-engineered protocol sources](https://github.com/festivaldev/UberStrike-Reverse-Engineered)

Respect each project's license. Do not distribute proprietary UberStrike assets or reference assemblies.
