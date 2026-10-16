# Repository investigation and implementation plan

Investigation date: 2026-10-14. Repositories were inspected in their existing local checkouts before implementation. No upstream files were changed.

## Repository state

| Repository | Branch / state | Relevant contents |
|---|---|---|
| `Uberstrike-community` | `main`, clean at start; one initial commit | Only a placeholder README; no implementation, build configuration, tests, or CI. |
| `NekoNexus` | `dev`; pre-existing untracked `BOT_AI_FIX_NOTES.txt` | Bun/TypeScript web services and admin, Sequelize models, realtime server and host, Photon compatibility shims, client patch and Docker/native hosting. Its `packages/Core` submodule is checked out; other submodules are present at pinned commits. |
| `Paradise` | `dev`, clean | Bun/TypeScript web services and models, .NET realtime server and client patch/bootstrap. Vendor submodules are checked out at pinned commits. |

The NekoNexus notes file was pre-existing and left untouched. Submodules were already initialized; no initialization or network fetch was needed. NekoNexus is GPL-3.0-or-later; Paradise is GPL-3.0-or-later. Any direct code reuse must preserve attribution and license terms. The community repository should avoid shipping proprietary game files or reference assemblies.

## Findings

### Existing discovery wire path

The existing in-game browser's entries arrive in the legacy UberStrike `AuthenticateApplication` response. Paradise's client `GlobalSceneLoaderHook` calls `ApplicationWebServiceClient.AuthenticateApplication`, then installs `authView.CommServer` and `authView.GameServers` in `GameServerManager`; server selection passes the resulting `PhotonServer.ConnectionString` into the normal game connection flow. NekoNexus's `packages/Client/README.md` and client settings describe the same web-service-driven realtime list.

The response is serialized using UberStrike's legacy `AuthenticateApplicationViewProxy` and contains `PhotonServer` views. A community JSON endpoint alone cannot populate the original screen. Paradise PR #2 fetches verified public game records and adapts them to this legacy response; the existing in-game browser and join flow remain unchanged. The shared hostname must route both the legacy web-service paths and `/api/community/servers`. The legacy `GetPhotonServers` endpoint is not currently the active list source in these client hooks.

### NekoNexus

- `packages/ws/src/ServiceHosts/routes/v102/ApplicationWebService.ts` and `routes/v2/ApplicationWebService.ts` serialize the authentication view. The v2 route reads server records from the `PhotonServer` database model; server auth configuration is also represented by `ServerCredentials` in `NekoNexusServiceSettings.ts`.
- `packages/nekonexus-models/src/PhotonServer.ts` represents the game endpoint (`IP`, `Port`, `Name`, region, usage type, enabled state and related legacy fields), but has no public/private or independently verified health lifecycle.
- `packages/ws/src/ServiceHosts/WebSocket/Host.ts` authenticates realtime nodes with configured IDs and passphrases. This is node-to-web-service connectivity, not a public registration/health protocol.
- `packages/Realtime/NekoNexus.Realtime.Host` and `shims/Photon.SocketServer.Shim` host LiteNetLib-backed Comm/Game listeners. Docker documentation lists UDP 5055 and 5155 and TCP web-service ports 8080–8082.
- `packages/admin` has live server status views but no directory registration/moderation lifecycle.
- Docker config and `misc/SERVER-SETUP.txt` explicitly require manually coordinating server credentials, names, IDs and realtime instances. Client setup selects a web-service URL; the patch does not install a shared dynamic directory by itself.
- Existing workflows build/release NekoNexus; none target this empty community repository.

### Paradise

- `packages/paradise-models/src/PhotonServer.ts` persists IDs, credentials, endpoint, name, enabled state, API version, region, usage type and latency metadata in the `PhotonServers` database table.
- Legacy `packages/ws/src/services/web-services/legacy/ApplicationWebService.ts` `GetPhotonServers` and `GetMyIP` currently throw `Not Implemented`. Its `AuthenticateApplication` path is the working discovery route and loads enabled, API-compatible game and comm server records.
- Paradise's normal server response derives realtime `ConnectionString` fields from server rows, which reinforces the compatibility requirement: listing an endpoint and connecting through realtime are separate behaviors.
- The client patch injects Paradise's bootstrap into the legacy client. Its service client and URL selection live in Paradise's client source, outside this repository.
- Paradise realtime nodes authenticate and report to their configured Paradise web service over its existing WebSocket. This does not register them with the separate community directory; a Paradise-side registration/heartbeat integration is required for automatic discovery.
- Docker and installer assets exist upstream; ARM64 installer metadata exists, but that alone does not establish that every server dependency/runtime combination works on ARM64.

## Compatibility and security boundary

The modern directory can own registration, credentials, visibility, moderation and online eligibility. The original browser should continue to consume the legacy serialized authentication view through the NekoNexus web-service implementation. A separate API cannot replace this client path without a patched client or a full service-compatible adapter.

Realtime validation must speak a bounded, explicit probe protocol implemented by the game server. A generic web request, open UDP port, or server heartbeat is not proof that UberStrike gameplay is reachable. NekoNexus's current realtime host has no directory probe contract identified in this inspection. Until that is added and externally verified, entries must not be represented as gameplay-verified online.

Registration credentials must be per-server and never included in public records. Directory probes must only target server endpoints established by authenticated registrations, with endpoint/address checks, bounded ports and rate limits. Public responses should contain only the legacy client fields needed to connect and display the server.

## Proposed implementation sequence

1. **Directory core in this repository:** define a stable server record, authenticated registration and heartbeat lifecycle, durable persistence, a public JSON read API, state transitions, and automated unit/integration coverage. The record model accepts both Paradise and NekoNexus software labels and distinguishes Comm/Game endpoints. Keep `online` contingent on a defined realtime probe; represent `unknown` distinctly.
2. **Server-side integration:** add opt-in directory configuration and a resilient registration/heartbeat worker to supported NekoNexus and Paradise hosts. Do not terminate gameplay when the directory is unavailable. Add graceful offline notification and a bounded reconnect policy.
3. **Realtime probe:** add a challenge/response operation to the supported realtime host and implement an allowlisted probe in the directory. Exclude unverified, stale, private, disabled, moderated and incompatible entries.
4. **Legacy client adapter:** Paradise PR #2 consumes verified public records in `AuthenticateApplication` and serializes them with the existing UberStrike proxy. Preserve CommServer requirements and existing selection/connection behavior. Equivalent NekoNexus-side consumption remains follow-up work.
5. **Operations and release:** add owner first-run credential setup, admin moderation/audit, Windows/Linux/Docker instructions, CI and live-client validation. Document required UDP/TCP ports and avoid promising unsupported LAN/invitation behavior.

## Verification status

The source inspection confirms the client entry point and current upstream limitations. No live game client, production directory, or externally reachable test server was available during this investigation. Thus actual serialized entry display and realtime join behavior require later live-client verification.
