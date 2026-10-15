# ApolloBot — Scope (Draft v7)

> Status: **in review**. This document is the single source of truth for the rebuild.
> Once agreed, the GitHub backlog is derived from it, and every issue links back to a section here.

## 1. Vision

ApolloBot is a personal, self-hosted bot for the servers I manage. Its primary job is to **record activity data on my own infrastructure** so it can be analysed and turned into **recaps** (server-wide for the admin, personal for each member). The data must survive a move from Discord to **Fluxer**, so nothing in the core depends on Discord.

Secondary, later: general-purpose utility features (reaction roles, embed messages, …). These are added as code; there is no runtime on/off switch and no user-facing module management.

## 2. Principles

| Principle | Consequence |
|---|---|
| Platform-neutral core | Domain, storage and web never reference NetCord or Fluxer.Net types. Only adapter projects do. |
| Own the data | Everything we need for stats lives in our DB with internal IDs; platform IDs are a mapping. |
| Store facts, derive stats | Persist raw events (append-only). All stats are queries/aggregates over events. New stats later = new queries, not new collection. |
| Metadata, not content | Message text is never stored. Content can be re-scraped from channel history if ever needed. |
| One guild at a time | Every row carries a guild reference, but the bot is configured for one guild and ignores others. |
| Single-user product | No multi-tenant UX, no feature toggles, no polish for strangers. |
| English everywhere except UI | Code, issues, docs, commits in English. UI text in German. |

## 3. Non-goals (for now)

- Multi-guild operation or self-service onboarding of other servers
- Storing message content
- Backfilling history (planned later, see M5)
- Sending recaps to Discord (website only; export later)
- CI/CD pipeline (build & push from my machine)
- Runtime feature toggles
- Changing tracking or deleting data from the website (Discord commands only for now)

## 4. Architecture

### 4.1 Solution layout (proposed)

```
src/
  Apollo.AppHost/              Aspire orchestration + compose publishing
  Apollo.ServiceDefaults/      OTel, health checks, resilience
  Apollo.Domain/               Entities, value objects, platform-neutral event contracts
  Apollo.Platform.Abstractions/ IChatPlatform, platform events, IPlatformIdentity
  Apollo.Platform.Discord/     NetCord adapter (gateway -> platform events, REST calls)
  Apollo.Platform.Fluxer/      Fluxer.Net adapter (later; stub + parity notes first)
  Apollo.Data/                 EF Core DbContext, migrations, TimescaleDB setup
  Apollo.MigrationService/     Applies migrations on startup (pattern from topg)
  Apollo.Bot/                  Worker service: hosts platform adapter, writes events
  Apollo.Stats/                Query layer: date-range stats, aggregates, recap builders
  Apollo.Web/                  ASP.NET Core host: API, auth, serves WASM client
  Apollo.Web.Client/           Blazor WASM + MudBlazor + Blazor-ApexCharts (German UI)
tests/
  Apollo.*.Tests               Unit tests
  Apollo.IntegrationTests      Testcontainers (TimescaleDB image)
```

The bot and web are **separate processes**, so a web deploy never disconnects the bot. They share the DB.

### 4.2 Platform abstraction

```
Platform adapter (NetCord | Fluxer.Net)
   │  maps native gateway events
   ▼
Platform events (neutral records)   e.g. MessageCreated, VoiceStateChanged, ReactionAdded
   │
   ▼
Event recorder (Apollo.Bot)  ── resolves platform IDs → internal IDs ──► DB
```

- **Inbound:** the adapter turns gateway events into neutral records like `PlatformMessageCreated(PlatformRef Guild, PlatformRef Channel, PlatformRef Author, DateTimeOffset At, MessageMetadata Meta)`.
- **Outbound:** an `IChatPlatform` interface with only the operations we need, such as `GetMemberRoles`, `GetMemberPermissions`, `ListChannels`, and later `AddRole` and `SendEmbed`.
- **Identity:** `PlatformRef = (Platform, ExternalId)`. The tables `users`, `channels`, `guilds` and `roles` each have an internal `Guid` plus one or more platform mappings. A future Fluxer account can be linked to an existing internal user, which keeps that user's history.
- **Parity:** every issue that touches the platform includes a Fluxer.Net parity table. The parity spike (#9) confirmed that messages, reactions, voice, membership, threads, role changes and OAuth are portable to [Fluxer.Net](https://github.com/Nexfinity/Fluxer.Net). Presence activities and interactions (slash commands, ephemeral replies, buttons) don't exist on Fluxer; [platform-parity.md](platform-parity.md) lists the fallbacks.
- **Parity matrix:** [platform-parity.md](platform-parity.md) lists every capability with its NetCord and Fluxer.Net status.

### 4.3 Single guild

The bot is configured with `Apollo:Platform:Discord:GuildId`. Events from any other guild are dropped. Every table still has a `guild_id` column, so going multi-guild later is a config and UI change rather than a schema migration. This costs almost nothing, so I recommend it.

## 5. Data collection

### 5.1 What we record (raw events, append-only)

| Event table | Columns (besides id, guild_id, occurred_at) | Source | Needed for |
|---|---|---|---|
| `message_events` | user_id, channel_id, thread_parent_id?, message_ext_id, kind (create/edit/delete), length, word_count, attachment_count, embed_count, has_link, emoji_count, mention_count, reply_to_user_id?, is_bot | MESSAGE_CREATE/UPDATE/DELETE | messages per day/hour/channel, chattiest times, reply graph ("who talks to whom"), media sharer |
| `voice_sessions` | user_id, channel_id, started_at, ended_at, end_reason | VOICE_STATE_UPDATE (join/leave/move) | voice time, favourite channel, longest session, voice buddies (overlap) |
| `voice_state_segments` | session_id, from, to, self_mute, self_deaf, streaming, video | VOICE_STATE_UPDATE | muted ratio, stream time |
| `reaction_events` | user_id, message_ext_id, message_author_id, channel_id, emoji (unicode or custom id+name), kind (add/remove) | MESSAGE_REACTION_ADD/REMOVE | top emojis, most-reacted-to user |
| `membership_events` | user_id, kind (join/leave/ban/unban), | GUILD_MEMBER_ADD/REMOVE, ban events | growth curve, member tenure |
| `presence_activity_sessions` | user_id, activity_type (playing/listening/…), activity_name, started_at, ended_at | PRESENCE_UPDATE | top games, "hours played" |

Reference / snapshot tables: `guilds`, `users` (display name, avatar, joined_at), `channels` (name, type, category, deleted), `roles`, `member_roles`, plus the platform mapping tables.

**Notes**
- **Voice is stored as sessions, not individual events.** The recorder keeps the open sessions in memory. When the bot starts, it reconciles them with the current voice states: sessions left open by a crash get closed, and users who are already in a channel get a new session. Any gap is recorded as `end_reason = bot_restart` so it can be filtered out later.
- **Message content intent.** Without it, Discord sends an empty `content` field, so we can't compute length, links or emoji. ApolloBot is in fewer than 100 servers, so turning on the privileged intents (Message Content, Presence, Members) is just a toggle in the developer portal. The content is used in memory to compute metadata and then thrown away.
- **Bot messages** are recorded with `is_bot = true` and excluded by default in queries.
- **Presence data is incomplete by design:**
  - Users can hide their activity in Discord.
  - Fluxer has no activities at all (verified in #9), so game data is Discord-only.
  - So recaps treat game data as a **bonus section**: it is shown only when there is data, and never assumed to be complete. Only sessions of at least 1 minute are stored.
- **Threads** are stored with their own `channel_id` and a `thread_parent_id`. Stats roll them up to the parent channel by default. Fluxer has threads with a parent reference too (verified in #9), so `thread_parent_id` is filled on both platforms.
- **Time zone:** all day and hour bucketing uses `Europe/Zurich`. Raw timestamps are stored in UTC.
- **Deleted messages** keep their create event and get a delete event added. Recaps count what was sent.

### 5.1.1 Who is tracked: the tracking role

Tracking is controlled by **one tracking role per platform** (e.g. "Statistik"). There is no list of tracked roles and no separate opt-out flag.

```
if a tracking role is configured for the platform:
    record event  ⇔  user has the tracking role
else (bootstrap, before the admin UI exists):
    record event  ⇔  user has the Administrator permission
```

- **Bootstrap fallback:** M1 deploys data collection before the admin UI exists (M2). Until a tracking role has been set in the admin UI, the bot tracks members with the **Administrator** permission. That's enough to check that collection works end to end on real data. The fallback ends for good once a tracking role is configured. Admins who want to stay tracked then need the role themselves. The bot logs at startup which mode it's in.
- **Intended setup:** I give the tracking role to all members by hand at the start. After that, members manage it themselves with `/apollo tracking on|off` (§5.4).
- **Role changes take effect live.** The member role sync (§6.2) keeps `member_roles` up to date, and the recorder checks it from an in-memory cache. It makes no difference whether the role was changed by command, by an admin in Discord, or by some other bot.
  - Losing the tracking role closes any open voice/presence session and stops recording. Existing data is **kept and stays visible**. Removing it requires an explicit delete (§5.4).
  - Gaining the tracking role starts recording. Voice and presence sessions for members already in a channel or game are opened right away, using the same reconciliation logic as at bot startup.
- **References to untracked users are dropped.** If a tracked user replies to or reacts to an untracked user, the event is stored but the untracked user's reference is NULL (`reply_to_user_id`, `message_author_id`). No data about untracked users ever lands in the DB, apart from the reference tables needed for role sync and access checks (`users`, `member_roles`).
- **Tracking vs. access:** the tracking role (whose data is recorded) and the member access roles (who can log in and see their stats, §6.2) are independent. A member who can log in but isn't tracked sees an empty state explaining why ("Für dich werden keine Daten erfasst").
- **Tracking periods:** the stats queries never need to know about roles, since untracked activity is never written. `tracking_periods(user_id, from, to)` is stored so recaps can say "tracked since …" and leave untracked time out of averages.

### 5.1.2 Role settings: per platform, stored in the DB

The tracking role and the member access roles are **settings stored in the DB, edited in the admin UI** (M2). They are not in `IConfiguration`.

- **Per platform.** Role IDs, role semantics and even role availability differ between Discord and Fluxer. Each setting is therefore scoped to a `(guild, platform)` pair:

  ```
  role_settings(guild_id, platform, role_id → roles.id,
                purpose: Tracking | MemberAccess,
                changed_by_user_id, changed_at)
  ```

  - `Tracking`: at most **one** role per platform.
  - `MemberAccess`: any number of roles.
- **No ID clashes.** `roles` (like every other platform object) holds internal IDs plus a `(platform, external_id)` mapping. A Discord role and a Fluxer role are always different rows, even if their external IDs happened to match. Settings reference only internal role IDs.
- **Selectable roles come from the role sync (§6.2).** The admin picks from the roles the bot has seen on that platform. Deleted roles are shown as such and ignored. If the tracking role is deleted on the platform, the bot does **not** fall back to Administrator. Nobody is tracked, and the admin UI shows a warning.
- **No config bootstrap.** The Administrator fallback (§5.1.1) covers M1, and member access only matters from M4, when the UI already exists.
- **Bot permissions:** the bot needs *Manage Roles*, and its own role must sit above the tracking role in the role hierarchy. The admin settings page checks this and shows a warning if it's wrong.
- **Changes apply without a restart.** The bot reloads role settings every 30 s and keeps them in a cached snapshot. When the tracking role changes, every affected member goes through the same start/stop logic as a member role change: sessions are opened or closed and `tracking_periods` are updated.
- **Audit:** every change stores who made it and when. The admin UI shows a short change history, so gaps in the data can be explained later ("Tracking-Rolle am … entfernt").
- **Admin access is not a setting.** It stays hard-wired to the Administrator permission (D8), so nobody can lock themselves out or widen admin access by mistake through the UI.

### 5.2 Storage: TimescaleDB

TimescaleDB is a Postgres extension, so Npgsql and EF Core work unchanged. Timescale-specific objects are set up with **[CmdScale.EntityFrameworkCore.TimescaleDB](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)**, which uses the Fluent API and data annotations and generates normal EF migrations. Raw SQL is used only for what the package can't do.

| Need | How |
|---|---|
| Hypertables for the event tables | `IsHypertable(x => x.OccurredAt)` with a chunk interval |
| Daily rollups | `IsContinuousAggregate<TAgg, TSource>(...)` plus `AddAggregateFunction(...)` (Sum, Count, Min, Max, Avg, First, Last) |
| Refresh, compression, retention | Fluent policy APIs from the package |
| Time zone–aware daily buckets (`time_bucket('1 day', ts, 'Europe/Zurich')`) | The fluent API can't add the time zone. These aggregates are written by hand in the migration with CmdScale's `CreateContinuousAggregate(viewDefinition: ...)` and read through a keyless `ToView` entity ([ADR 0001](adr/0001-timescale-ef.md)) |
| Reading aggregates | Keyless entities, queried with LINQ |

- **Aspire:** `AddPostgres("db").WithImage("timescale/timescaledb", "<latest-pg tag>")`. This needs no extra setup and is still one container in the compose output.
- **Testcontainers** uses the same image.
- **Fallback:** if Timescale ever gets in the way, plain Postgres with ordinary materialized views can serve the same stats layer.

### 5.3 Stats & recap computation

Stats are built in three layers:
1. **Continuous aggregates per day:**
   - `daily_user_channel_activity`: messages, characters, reactions given and received, voice seconds
   - `daily_guild_activity`
   - `hourly_activity_heatmap` (weekday × hour)
2. **Date-range query layer (`Apollo.Stats`):** takes `(guild, user?, from, to)`, reads the daily aggregates and returns a stats DTO. The admin dashboard, the user's personal page and the recaps all use this one layer.
3. **Recap snapshots:**
   - At the end of each month or year, a job runs layer 2 and stores the result as JSON in `recap_snapshots(guild, user?, period_type, period_start, payload, generated_at)`.
   - Snapshots are fixed and load instantly.
   - Any other date range is computed on the fly.

Things that don't fit daily sums are their own queries over raw data: voice buddies (sessions that overlap), reply graph, longest session.

### 5.4 Privacy

There are two separate actions: **stop tracking**, which is role-based and reversible, and **delete data**, which is permanent and only happens on explicit request.

| Feature | Behaviour |
|---|---|
| Stop tracking | `/apollo tracking off`: the bot removes the tracking role, and recording stops immediately (§5.1.1). The **ephemeral** reply (visible only to the user) explains: "Ab jetzt werden keine Daten mehr erfasst. Bereits erfasste Daten bleiben gespeichert. Mit `/apollo privacy delete` kannst du sie endgültig löschen." |
| Start tracking | `/apollo tracking on`: the bot adds the tracking role back. This works at any time: after `off`, after `delete`, or for members who never had the role. The time without the role remains a gap (`tracking_periods`). The ephemeral reply briefly explains what is recorded and links the privacy page. |
| Status | `/apollo tracking status`: shows whether the user is tracked, since when, and how much data is stored (e.g. event counts). |
| Delete (hard) | `/apollo privacy delete`. The ephemeral reply has a confirm button and states plainly that this can't be undone. Confirming permanently removes the user's events, snapshots and `tracking_periods`, anonymises references to them in other users' data (e.g. `reply_to_user_id` becomes NULL), refreshes the affected aggregate ranges, **and removes the tracking role**, so recording doesn't simply resume. The user can still get the role back at any time with `/apollo tracking on`, which starts recording again from zero. |
| Not set up | If no tracking role is configured yet (bootstrap mode), `/apollo tracking on\|off` replies "Tracking ist noch nicht eingerichtet". |
| Web (M4) | Read-only: the `/me` page shows tracking status, tracked-since date, and explains the commands. **No web toggle or web delete for now** (out of scope, see §3). |
| Admin view | The member list shows each member's tracking status. |
| Transparency | A German privacy page explaining what is stored and how the tracking role works. It's linked from every command reply. |

**Implementation:**
- No opt-out flag. The tracking-role check in the recorder is the only gate.
- **All `/apollo` replies are ephemeral.** NetCord's interaction responses support the ephemeral flag. Fluxer has no interactions; there, private replies become a DM and the invoking message is deleted ([platform-parity.md](platform-parity.md)).
- The commands only add or remove a role. The resulting `GUILD_MEMBER_UPDATE` flows through the normal role sync, so command, manual and admin role changes all behave identically.
- `IChatPlatform` gets `AddRoleAsync` and `RemoveRoleAsync`. That makes them, along with slash commands, ephemeral replies and buttons, parity items for Fluxer.Net.

## 6. Authentication & authorization

### 6.1 Login

- An OAuth2 authorization-code flow with `identify` scope only. **Discord** first. The design is platform-neutral (`IPlatformIdentityProvider`), so Fluxer OAuth can be added later; Fluxer.Net already has OAuth types.
- After login, the web host maps the platform user to the internal user and issues an ASP.NET Core **cookie session**. The cookie holds only internal claims (internal user id, platform, roles). No platform tokens are stored after login.
- The Blazor WASM client uses the BFF pattern: cookie auth on same-origin API calls and no tokens in the browser. This is the same as topg's web/client split.

### 6.2 Access rules (based on Discord roles)

**Who can do what is decided live from the guild, not from OAuth scopes.**
1. After login, the web host asks the **bot's platform layer** for the user's member info in the configured guild: is the user a member, which roles do they have, what are their permissions.
   - The bot already has this data from the gateway/members cache. That's why `identify` is enough as an OAuth scope, and it works the same way for Fluxer.
2. Policies:

| Policy | Rule (configurable) | Grants |
|---|---|---|
| `Member` | user is in guild **and** has at least one **member access role** for the platform they logged in with (role settings, §5.1.2) | Own stats, own recap, privacy controls |
| `Admin` | user has the **Administrator** permission in the guild. Nothing else. | Everything: all users' stats, server dashboard, bot status |
| — | anything else | "Kein Zugriff" page |

Member access roles are edited in the admin UI (§5.1.2, §7.2). Access is checked again every few minutes, so removing someone's role revokes their web access.

Bot and web are separate processes, so the web host needs a way to ask the bot for member info. **Recommendation:** the bot syncs `member_roles` and the permission flags into the DB, and the web host reads from there. This means no extra service-to-service API, and the data stays useful if the bot is temporarily down.

## 7. Frontend

### 7.1 Stack
Blazor WASM, MudBlazor, Blazor-ApexCharts. The UI is in German. All UI strings go in one resource file from the start, even though only German is planned.

### 7.1.1 Visual direction
- **Dark mode first**, styled after Discord/Fluxer: dark grey surfaces, a blurple-like accent, rounded cards, dense but readable. A light theme is optional and comes later.
- **Mobile-first responsive.** Every page must work at 360px width:
  - navigation collapses into a drawer
  - stat tiles stack vertically
  - charts resize, or switch to a simpler form on small screens
- Visual identity: "Discord-like" here means the same familiar look and feel, without copying Discord's logo or brand assets.

### 7.2 Areas

**Member area (`/me`)**
- Overview: messages, voice hours, top channels, top emojis, activity heatmap, all for a chosen date range
- Recaps: monthly and yearly snapshots, shown as "Wrapped"-style cards
- Privacy (read-only): tracking status, tracked since, explanation of what is stored and of the `/apollo tracking` / `/apollo privacy delete` commands

**Admin area (`/admin`)**
- Bot status: connected, gateway latency, last event received, events per hour, open voice sessions. This is the "data is being collected" proof for M2.
- Server dashboard: totals, growth, activity over time, channel ranking, heatmap
- Members: list with search, opening a member's stats page (same view as `/me`)
- Recaps: server recap per month/year, regenerate a snapshot
- Data: event counts per table, tracked vs. untracked member counts, raw event browser (for debugging)
- Settings (Einstellungen), with a tab per platform:
  - **Tracking role:** single select from the synced roles, showing role colour and member count, plus a live preview "X Mitglieder werden erfasst". Shows a bootstrap-mode banner while none is set, and a role-hierarchy/permission check for the bot
  - **Member access roles:** multi-select, with the same preview
  - **Change history:** who changed what and when

### 7.3 Design process (before real UI work)
1. **Style guide:**
   - colours, typography, spacing
   - MudBlazor theme (dark-first?), chart palette
   - card/stat-tile patterns, German wording conventions
   - Output: a MudBlazor theme file plus a style guide page in the app.
2. **Wireframes:** low-fidelity, grey boxes, for every page in §7.2. Reviewed by me.
3. **Hi-fi prototype in the real app:** pages built with real components on **mock data services** behind the same interfaces as the real API, so wiring them up later is just swapping the implementation.
4. Progressive refinement as real data arrives.

## 8. Deployment

Same approach as topg:
- **Aspire `AddContainerRegistry`** with `registry.kallisto.li`. Running `aspire do prepare-stack` from my machine builds the images, pushes them to the registry and generates `src/Apollo.AppHost/aspire-output/docker-compose.yaml` plus the env file with the values (not `aspire deploy`, which would also run `docker compose up` locally).
- **Compose file:**
  - `AddDockerComposeEnvironment` with the dashboard turned off
  - an internal overlay network, plus the external reverse-proxy overlay network attached only to `apollo-web`
  - DB and pgAdmin data in bind mounts, `deploy:` restart policy for the migration service, no `expose:`/`depends_on`
- **No manual post-processing:** `deploy/Render-Stack.ps1` inlines the env values (Swarm doesn't read `.env`) into `src/Apollo.AppHost/aspire-output/apollo.stack.yaml`. The exact steps are in [deployment.md](deployment.md).
- **Config:** everything goes through `IConfiguration`:
  - dev: `appsettings.Development.json` plus `dotnet user-secrets` (Discord token, OAuth client secret)
  - prod: environment variables in the compose file
  - Azure Key Vault is removed.
- **Note from topg:** `service.Networks = ["topg_internal, topg"]` is a single string, so the output had a broken network entry that had to be fixed by hand. In Apollo the two networks are listed separately.

## 9. Milestones

| # | Milestone | Outcome | Contents (epics) |
|---|---|---|---|
| **M0** | Clean slate & foundation | Empty solution runs locally via Aspire, test infrastructure ready for TDD | Remove old code · solution skeleton (§4.1) · `Directory.Build.props` / CPM / `.editorconfig` · ServiceDefaults · TimescaleDB via Aspire + CmdScale · spike: CmdScale time zone buckets · MigrationService · typed config conventions · test projects + `Apollo.Testing` (builders, `FakeChatPlatform`, Testcontainers fixture) · architecture test for the platform boundary · CLAUDE.md + coding guidelines |
| **M1** | Data collection | Bot runs on the server and records events | Spike: NetCord ↔ Fluxer.Net parity · platform abstraction · NetCord adapter · ID mapping · recorders: messages, voice, reactions, membership, presence · voice session reconciliation · member role/permission sync · tracking-role gate with Administrator bootstrap fallback · `/apollo tracking on|off|status` + `/apollo privacy delete` + `/apollo stats` (all ephemeral) · hypertables · **deploy to Swarm** (data starts accumulating as early as possible) |
| **M2** | Admin access & data visibility | I can log in and see that data is flowing | Design: style guide + admin wireframes · Discord OAuth + BFF cookie · Admin policy (Administrator perm) · admin bot-status + data page · raw event browser · **role settings page** (tracked + member access roles, per platform, with change history) |
| **M3** | Stats foundation | Numbers you can trust | Continuous aggregates · `Apollo.Stats` date-range layer · admin server dashboard with charts · data sanity checks · **round-trip validation recap** (one rough recap to check the data is useful) |
| **M4** | Member access & privacy | Members see their own stats | Member policy via roles · `/me` overview · privacy page (read-only tracking status) · mobile pass |
| **M5** | Recaps | Wrapped-style monthly/yearly recaps | Recap snapshot job · recap pages (member + admin) · full design pass |
| **M6** | Later / icebox | — | History backfill · Fluxer adapter · reaction roles · embed builder · recap export to Discord · multi-guild |

M1 is deployed before any UI exists, so data collection starts as early as possible. While M2–M4 are being built, the data keeps accumulating.

## 10. Issue format

Each issue is sized so that one Claude Code session can implement it, verify it and open a PR. That means roughly one PR of about 50–500 changed lines.

**Labels**
- `type:` `feature` | `chore` | `spike` | `design` | `bug`
- `area:` `platform` | `bot` | `data` | `stats` | `web-api` | `web-ui` | `auth` | `infra` | `docs`
- `size:` `S` | `M` | `L` (L should be split)
- `parity:` `needed` (touches platform APIs)
- `agent-ready`: the issue has been reviewed and has no open questions

**Template**

```markdown
## Context
Why this exists. Link to scope section (docs/scope.md#...) and parent epic.

## Goal
One sentence describing the observable outcome.

## Scope
**In:** bullet list
**Out:** explicit non-goals (prevents agent scope creep)

## Acceptance Criteria
- [ ] Testable statement (Given/When/Then where useful)
- [ ] ...

## Technical Notes
- Projects/files to touch, interfaces to add or implement
- Data model / migration changes
- Config keys introduced (name, dev source, prod env var)
- Relevant docs links (NetCord, Fluxer.Net, Aspire, MudBlazor)

## Platform Parity  (only if parity:needed)
| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Notes |
|---|---|---|---|
| ... | ✅ | ⚠️ / ❌ / ❓ | |

## Test Plan
Tests to write first (TDD), one or more per acceptance criterion, with layer
(unit / Testcontainers / bUnit / Aspire). Plus any manual check
(e.g. "run AppHost, send a message, row appears in message_events").

## Dependencies
Blocked by: #x · Blocks: #y

## Definition of Done
- [ ] Implemented test-first per docs/coding-guidelines.md
- [ ] Builds without warnings; all tests green; `dotnet format` clean
- [ ] PR maps each acceptance criterion to its tests
- [ ] No platform types outside adapter projects
- [ ] UI text German, everything else English
- [ ] docs/ updated if behaviour or config changed
```

Alongside the template, the repo gets a **`CLAUDE.md`** with build and test commands, the project layout, conventions (naming, EF migration workflow, how to add a platform event), and the rule "read docs/scope.md and the linked issue first". Issues then stay short, because the shared context lives in the repo.

## 11. Decisions log

| # | Topic | Decision |
|---|---|---|
| D1 | Presence / games | Tracked from M1 on. Treated as optional bonus data in recaps (Fluxer has no activities, and users can hide their activity) |
| D2 | Threads | Stored with their parent reference and rolled up to the parent channel by default |
| D3 | Time zone | `Europe/Zurich` for all bucketing; storage in UTC |
| D4 | Opt-out | ~~Soft-delete flag~~ → **replaced by D15**. Removing the tracking role stops recording, and existing data stays. Hard delete only on explicit request |
| D5 | Slash commands (M1) | `/apollo tracking on\|off\|status`, `/apollo privacy delete` and `/apollo stats` (replies with a link to `/me`) |
| D6 | Style | Dark-first, Discord/Fluxer-like, mobile-usable |
| D7 | Versions | Latest .NET and Aspire at implementation time (currently .NET 10 / Aspire 13.x); EF Core version must match what CmdScale supports (today: latest EF Core 10.0.x, not 11, see [ADR 0001](adr/0001-timescale-ef.md)) |
| D8 | Admin | Administrator permission only, no allowlist |
| D9 | Member access | List of member access roles, any match grants access. Stored in the DB per platform and edited in the admin UI (§5.1.2) |
| D10 | Timescale in EF | CmdScale.EntityFrameworkCore.TimescaleDB first, raw SQL only as fallback |
| D11 | Tracking scope | Exactly one tracking role per platform, and only its holders are recorded. Until one is configured, Administrator holders are tracked (bootstrap). References to untracked users are stored as NULL (§5.1.1) |
| D14 | Role settings storage | Tracking role and member access roles live in the DB as `role_settings`, scoped to (guild, platform) and referencing internal role IDs. No config seeding. Edited in the admin UI from M2 on, and the bot picks up changes within 30 s. Admin access is never configurable |
| D15 | Opt-in/out = role | No separate opt-out flag. Users toggle the tracking role with `/apollo tracking on\|off`. Ephemeral replies explain that existing data stays and how to delete it. I give the role to all members by hand at the start. No web toggle or web delete for now |
| D12 | Development style | TDD (red → green → refactor), see [coding-guidelines.md](coding-guidelines.md) |
| D13 | Test stack | xUnit v3, Shouldly, NSubstitute (edges only), Testcontainers (Timescale image), bUnit, Aspire.Hosting.Testing |

## 12. Open questions

None blocking. Answered by spikes:
- CmdScale: daily `time_bucket` with a time zone in continuous aggregates, and compatibility with the latest EF Core version → [ADR 0001](adr/0001-timescale-ef.md)
