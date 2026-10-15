# ApolloBot — Backlog Draft (for review)

> Derived from [scope.md](scope.md) Draft v7. Nothing is on GitHub yet.
> Once approved, each row becomes a GitHub issue using the template in scope.md §10, assigned to its milestone.
> IDs like `M1-07` are draft handles only. They become real issue numbers when created, and the dependencies are rewritten as `#123`.

**Slicing approach**
- **Vertical slices:** each recorder issue contains its own NetCord mapping, recorder logic, table/hypertable and tests. An agent can take one issue end to end without waiting for "the adapter issue".
- **Size:** S = under 1 h of agent work, M = one solid session, L = split before starting (none left as L).
- **Dependencies:** listed only where they really block. Issues without dependencies can run in parallel.

---

## M0 — Clean slate & foundation

*Outcome: an empty solution runs via Aspire, the test infrastructure is ready for TDD, and images can be published.*

| ID | Title | Labels | Size | Depends on |
|---|---|---|---|---|
| M0-01 | Remove legacy code and Azure Key Vault integration | chore, infra | S | — |
| M0-02 | Create solution skeleton with build conventions | chore, infra | M | M0-01 |
| M0-03 | Add CLAUDE.md, issue template and PR template | chore, docs | S | M0-02 |
| M0-04 | Set up Aspire AppHost with TimescaleDB and ServiceDefaults | chore, infra | M | M0-02 |
| M0-05 | Spike: CmdScale TimescaleDB with time-zone-aware continuous aggregates | spike, data | M | M0-04 |
| M0-06 | Add Apollo.Data DbContext and MigrationService | feature, data | M | M0-04, M0-05 |
| M0-07 | Build test infrastructure (Apollo.Testing, Testcontainers, architecture test) | chore, infra | M | M0-06 |
| M0-08 | Publish images to registry.kallisto.li and generate Swarm-ready compose | chore, infra | M | M0-04 |

<details><summary>Key acceptance criteria</summary>

- **M0-01:** All legacy projects, the Key Vault code and its config are deleted. Repo contains only `docs/`, `.gitignore`, README stub.
- **M0-02:**
  - Projects per scope §4.1 plus test projects per guidelines §3 exist, using the latest .NET.
  - `Directory.Build.props` sets nullable and warnings-as-errors.
  - `Directory.Packages.props` (CPM) and `.editorconfig` are in place.
  - `dotnet build` and `dotnet format --verify-no-changes` pass.
- **M0-03:**
  - CLAUDE.md contains build/test commands, the project map, "read scope + guidelines + issue first", the TDD loop and the PR checklist.
  - `.github/ISSUE_TEMPLATE/agent-task.md` follows scope §10.
  - `.github/pull_request_template.md` has the AC→tests mapping section.
- **M0-04:**
  - The AppHost starts a `timescale/timescaledb` container (persistent lifetime, pgAdmin in dev) plus the bot and web projects.
  - Bot and web report healthy in the Aspire dashboard.
- **M0-05:** Proves on a real container:
  - a hypertable via CmdScale
  - a continuous aggregate with **daily buckets in Europe/Zurich**, or documents why raw SQL is needed
  - compatibility with the latest EF Core

  Result written to `docs/adr/0001-timescale-ef.md`.
- **M0-06:** `ApolloDbContext` is registered via Aspire Npgsql EF integration. The MigrationService applies migrations, then exits. Web and bot wait for completion in dev (topg pattern).
- **M0-07:**
  - `Apollo.Testing` has a shared Timescale Testcontainers fixture, Respawn reset and a base for builders.
  - One sample DB test is green.
  - An architecture test fails if NetCord or Fluxer.Net types are referenced outside `Apollo.Platform.*`.
- **M0-08:**
  - `AddContainerRegistry` is configured from parameters.
  - The compose output uses an internal overlay network plus the external reverse-proxy network on `web` only, listed as **separate entries** (fixes the topg bug).
  - The dashboard is disabled.
  - `docs/deployment.md` documents publish → manual Swarm adjustments → deploy.

</details>

---

## M1 — Data collection

*Outcome: the bot runs on the server and records events. It's deployed to Swarm so data starts accumulating.*

| ID | Title | Labels | Size | Depends on |
|---|---|---|---|---|
| M1-01 | Spike: NetCord vs. Fluxer.Net parity matrix | spike, platform, parity:needed | M | — |
| M1-02 | Define platform abstractions and FakeChatPlatform | feature, platform | M | M0-07 |
| M1-03 | Internal identity and platform ID mapping | feature, data | M | M1-02 |
| M1-04 | NetCord adapter: gateway connection, intents, single-guild filter | feature, platform, parity:needed | M | M1-02 |
| M1-05 | Sync guild state: channels, roles, members, member roles, permissions | feature, bot, parity:needed | M | M1-03, M1-04 |
| M1-06 | Tracking gate: tracking role setting, Administrator bootstrap, tracking periods | feature, bot | M | M1-05 |
| M1-07 | Record message metadata (create, edit, delete) | feature, bot, data, parity:needed | M | M1-06 |
| M1-08 | Record voice sessions and state segments with reconciliation | feature, bot, data, parity:needed | M | M1-06 |
| M1-09 | Record reactions | feature, bot, data, parity:needed | S | M1-06 |
| M1-10 | Record membership events (join, leave, ban, unban) | feature, bot, data, parity:needed | S | M1-06 |
| M1-11 | Record presence activity sessions | feature, bot, data, parity:needed | M | M1-06 |
| M1-12 | Platform-neutral slash command infrastructure with ephemeral replies | feature, platform, parity:needed | M | M1-04 |
| M1-13 | `/apollo tracking on\|off\|status` | feature, bot | M | M1-06, M1-12 |
| M1-14 | `/apollo privacy delete` with confirmation | feature, bot, data | M | M1-07…M1-11, M1-12 |
| M1-15 | `/apollo stats` link command | feature, bot | S | M1-12 |
| M1-16 | Bot heartbeat and observability | feature, bot | S | M1-04, M0-06 |
| M1-17 | First production deployment and runbook | chore, infra | S | M0-08, M1-07…M1-11, M1-16 |

<details><summary>Key acceptance criteria</summary>

- **M1-01:** `docs/platform-parity.md` has a row for every capability in scope, each marked ✅/⚠️/❌ with links to the source. Capabilities:
  - gateway: message create/update/delete, reactions, voice state, presence activities, member add/remove/update, bans, threads
  - REST: add/remove role
  - commands: slash commands, ephemeral replies, buttons
  - OAuth: identify
- **M1-02:**
  - `PlatformRef`, the neutral event records (scope §4.2) and `IChatPlatform` (member lookup, roles, permissions, `AddRoleAsync`/`RemoveRoleAsync`).
  - `FakeChatPlatform` in `Apollo.Testing` can hold guild state and raise events.
- **M1-03:**
  - Tables `guilds`, `users`, `channels`, `roles` with internal Guids, each with a `(platform, external_id)` mapping.
  - A resolver does get-or-create, idempotent under concurrency (tested).
- **M1-04:**
  - Connects with the required intents.
  - Events from any guild other than `Apollo:Platform:Discord:GuildId` are dropped (tested via mapper).
  - Reconnects are logged.
- **M1-05:**
  - On startup: full sync of channels, roles, members, `member_roles` and the Administrator flag.
  - After that: live updates from gateway events.
  - Deleted roles/channels are marked, not removed.
- **M1-06:**
  - `role_settings` table (purpose `Tracking` | `MemberAccess`).
  - The gate rule is per scope §5.1.1. The Administrator fallback applies only while no tracking role exists.
  - Settings are cached and reloaded every 30 s.
  - Gaining or losing tracking raises start/stop hooks and writes `tracking_periods`.
  - References to untracked users are stored as NULL.
- **M1-07:** `message_events` hypertable with the metadata columns from scope §5.1. Content is never persisted (a test asserts there's no content column). `is_bot` is flagged. Thread parent is stored.
- **M1-08:**
  - `voice_sessions` and `voice_state_segments`; join/leave/move/mute/deaf/stream are handled.
  - On startup, open sessions are closed with `end_reason = bot_restart`, and current voice states open new sessions.
  - Gaining tracking mid-call opens a session.
- **M1-09:** `reaction_events` stores add/remove with unicode or custom emoji and the message author (NULL if untracked).
- **M1-10:** `membership_events` stores join/leave/ban/unban.
- **M1-11:** `presence_activity_sessions`; sessions under 1 min are discarded; multiple simultaneous activities are handled.
- **M1-12:**
  - A neutral command model (name, subcommands, options, invoking member).
  - Handlers are registered via DI.
  - All replies are ephemeral; button-confirm flow is supported.
  - The NetCord registration is guild-scoped.
- **M1-13:**
  - `on`/`off` add or remove the tracking role.
  - The `off` reply explains that data stays and how to delete it (scope §5.4 text).
  - `status` shows whether the user is tracked, since when, and event counts.
  - "Tracking ist noch nicht eingerichtet" while in bootstrap mode.
- **M1-14:**
  - A confirm button.
  - A hard delete of the user's events, sessions and tracking periods.
  - NULLing of references in other users' rows.
  - Removal of the tracking role.
  - Aggregate refresh is added in M3-06.
- **M1-15:** Replies with the `/me` URL from config.
- **M1-16:**
  - The bot writes a heartbeat to `bot_status` every 30 s: connected, gateway latency, last event at, open voice sessions.
  - OTel counters for recorded events.
- **M1-17:**
  - `docs/deployment.md` runbook: developer portal intents (Message Content, Presence, Members), invite URL with permissions (incl. Manage Roles), env vars.
  - Deployed. Rows appear for Administrator holders.

</details>

---

## M2 — Admin access & data visibility

*Outcome: I can log in, see that data is flowing, and configure the tracking role.*

| ID | Title | Labels | Size | Depends on |
|---|---|---|---|---|
| M2-01 | Design: style guide and MudBlazor theme | design, web-ui | M | — |
| M2-02 | Design: low-fi wireframes for admin and member pages | design, web-ui | M | M2-01 |
| M2-03 | Web host and WASM client skeleton | feature, web-ui | M | M0-04, M2-01 |
| M2-04 | Discord OAuth login with BFF cookie session | feature, auth, parity:needed | M | M1-03, M2-03 |
| M2-05 | Admin authorization policy and access revalidation | feature, auth | S | M1-05, M2-04 |
| M2-06 | Admin page: bot status | feature, web-ui, web-api | S | M1-16, M2-05 |
| M2-07 | Admin page: data overview and raw event browser | feature, web-ui, web-api | M | M2-05, M1-07…M1-11 |
| M2-08 | Admin page: role settings per platform | feature, web-ui, web-api | M | M1-06, M2-05 |

<details><summary>Key acceptance criteria</summary>

- **M2-01:**
  - A MudBlazor dark theme in a Discord/Fluxer-like style: surfaces, blurple-like accent, typography, spacing.
  - A chart palette for ApexCharts.
  - Stat-tile and card patterns.
  - A German wording guide.
  - A living `/styleguide` page in the app.
- **M2-02:**
  - Low-fi, clickable **static HTML** wireframes (no build step, open directly in a browser) for every page in scope §7.2, desktop and 360px.
  - Stored in `docs/design/wireframes/` and reviewed by me before M2-06…08 start.
- **M2-03:**
  - The responsive layout: app bar, plus a drawer that collapses on mobile.
  - German resource files.
  - The typed API client pattern with interface + mock implementation.
  - A bUnit test for the layout.
- **M2-04:**
  - `IPlatformIdentityProvider` and the Discord implementation (`identify` scope).
  - Maps the platform user to an internal user.
  - Cookie session with internal claims only; logout.
  - No tokens in the browser.
- **M2-05:**
  - `Admin` policy = Administrator flag from synced data, re-checked every few minutes.
  - "Kein Zugriff" page.
  - Allowed and denied tests.
- **M2-06:** Shows connected status, latency, last event, events per hour (24 h) and open voice sessions. Shows a stale warning if the heartbeat is older than 2 min.
- **M2-07:**
  - Event counts per table and per day.
  - Tracked vs. untracked member counts.
  - A paged raw event browser with filters (type, user, channel, date).
- **M2-08:**
  - A tab per platform.
  - Tracking role: single select with member-count preview, a bootstrap-mode banner, and a bot hierarchy/permission check.
  - Member access roles: multi-select.
  - Change history.
  - The bot picks up changes within 30 s (integration test with FakeChatPlatform).

</details>

---

## M3 — Stats foundation

*Outcome: trustworthy numbers, an admin dashboard, and a rough recap to validate the data.*

| ID | Title | Labels | Size | Depends on |
|---|---|---|---|---|
| M3-01 | Continuous aggregates for daily activity and hourly heatmap | feature, data | M | M0-05, M1-07…M1-09 |
| M3-02 | Stats query layer for arbitrary date ranges | feature, stats | M | M3-01 |
| M3-03 | Raw-data stats: voice buddies, reply graph, longest session, top emojis, top games | feature, stats | M | M1-08, M1-09, M1-11 |
| M3-04 | Admin server dashboard with charts and date range picker | feature, web-ui, web-api | M | M3-02, M2-02 |
| M3-05 | Admin member list and member stats page | feature, web-ui, web-api | M | M3-02, M3-03 |
| M3-06 | Refresh aggregates after privacy delete | feature, data | S | M1-14, M3-01 |
| M3-07 | Validation recap and data sanity checks | feature, stats, web-ui | M | M3-02, M3-03 |

<details><summary>Key acceptance criteria</summary>

- **M3-01:**
  - `daily_user_channel_activity`, `daily_guild_activity`, `hourly_activity_heatmap`, all bucketed in Europe/Zurich.
  - Refresh policies.
  - Tests seed events around DST and midnight and assert the buckets.
- **M3-02:**
  - `(guild, user?, from, to)` → stats DTO.
  - Excludes bots by default.
  - Thread → parent roll-up.
  - Uses tracking periods for averages.
- **M3-03:** Each query is tested against seeded data. Games are optional (empty when no data).
- **M3-04:**
  - Totals, activity over time, channel ranking and heatmap.
  - Range presets (Monat, Jahr, benutzerdefiniert).
  - Works at 360px.
- **M3-05:** A searchable member list with tracking status. The member page reuses the components that `/me` will use.
- **M3-06:** After a delete, the affected buckets are refreshed. A test asserts the aggregates no longer contain the user.
- **M3-07:**
  - A rough server recap for the last month.
  - A sanity report: gaps in the heartbeat, sessions over 24 h, events from untracked users (must be 0).

</details>

---

## M4 — Member access

*Outcome: members log in and see their own stats.*

| ID | Title | Labels | Size | Depends on |
|---|---|---|---|---|
| M4-01 | Member authorization policy via member access roles | feature, auth | S | M2-05, M2-08 |
| M4-02 | `/me` personal stats overview | feature, web-ui, web-api | M | M3-02, M3-05, M4-01 |
| M4-03 | Privacy page and personal tracking status (read-only) | feature, web-ui | S | M4-01 |
| M4-04 | Mobile pass across all pages | chore, web-ui | M | M4-02, M4-03 |

<details><summary>Key acceptance criteria</summary>

- **M4-01:**
  - `Member` = has any member access role for the login platform; members can only see their own data (tested).
  - Empty state for members who aren't tracked.
- **M4-02:** Date range, totals, top channels, top emojis, heatmap, voice time; games when available.
- **M4-03:**
  - A public German privacy explainer.
  - The member's status: tracked yes/no, tracked since, and how to use the commands.
- **M4-04:** Every page is checked at 360px; issues fixed; bUnit/visual notes documented.

</details>

---

## M5 — Recaps

*Outcome: Wrapped-style monthly and yearly recaps.*

| ID | Title | Labels | Size | Depends on |
|---|---|---|---|---|
| M5-01 | Design: hi-fi recap cards | design, web-ui | M | M3-07 |
| M5-02 | Recap snapshots and monthly/yearly generation job | feature, stats, data | M | M3-02, M3-03 |
| M5-03 | Member recap pages | feature, web-ui | M | M5-01, M5-02, M4-02 |
| M5-04 | Server recap pages and regeneration | feature, web-ui, web-api | M | M5-01, M5-02 |

<details><summary>Key acceptance criteria</summary>

- **M5-02:**
  - `recap_snapshots` table.
  - A job runs after each month or year ends (Europe/Zurich).
  - Idempotent; regenerating replaces the snapshot.
  - Optional sections (games) are only included when there's data; the tracked-since info is included.
- **M5-03 / M5-04:**
  - The recap list and detail view in card style.
  - The admin can regenerate.
  - Works on mobile.

</details>

---

## M6 — Icebox (not created as issues yet, or created without a milestone)

- History backfill from channel history (metadata only)
- Fluxer adapter (`Apollo.Platform.Fluxer`) + Fluxer OAuth
- Reaction roles
- Embed message builder
- Recap export to Discord
- Web tracking toggle / web delete
- Multi-guild operation

---

## Totals

| Milestone | Issues | S | M |
|---|---|---|---|
| M0 | 8 | 2 | 6 |
| M1 | 17 | 5 | 12 |
| M2 | 8 | 2 | 6 |
| M3 | 7 | 1 | 6 |
| M4 | 4 | 2 | 2 |
| M5 | 4 | 0 | 4 |
| **Total** | **48** | 12 | 36 |

---

## Example: one issue written out in full

This is how every issue will look when created on GitHub.

> **Title:** Record message metadata (create, edit, delete)
> **Milestone:** M1 — Data collection · **Labels:** `type:feature` `area:bot` `area:data` `size:M` `parity:needed`

```markdown
## Context
ApolloBot records activity as append-only events so stats can be derived later
(docs/scope.md §5.1). Messages are recorded as metadata only, never content.
Part of milestone M1 — Data collection.

## Goal
Every message created, edited or deleted by a tracked user in the configured guild
results in exactly one `message_events` row with correct metadata.

## Scope
**In:**
- NetCord mapping of MESSAGE_CREATE / MESSAGE_UPDATE / MESSAGE_DELETE → `PlatformMessage*` events
- `MessageRecorder` applying the tracking gate (M1-06)
- `message_events` entity, CmdScale hypertable on `occurred_at`, migration
- Metadata: length, word_count, attachment_count, embed_count, has_link, emoji_count,
  mention_count, reply_to_user_id, thread_parent_id, is_bot

**Out:**
- Aggregates or stats queries (M3)
- Backfill of past messages (M6)
- Storing message content in any form

## Acceptance Criteria
- [ ] Given a tracked user, when they send a message, then one `create` row is stored with correct metadata
- [ ] Given an untracked user, when they send a message, then nothing is stored
- [ ] Given a tracked user replying to an untracked user, then `reply_to_user_id` is NULL
- [ ] Given a message in a thread, then `channel_id` is the thread and `thread_parent_id` the parent
- [ ] Edits store an `edit` row with updated metadata; deletes store a `delete` row
- [ ] Bot authors are stored with `is_bot = true`
- [ ] No column or log line contains message content
- [ ] `message_events` is a hypertable (verified in an integration test)

## Technical Notes
- Projects: Apollo.Platform.Abstractions (events), Apollo.Platform.Discord (mapping),
  Apollo.Bot (recorder), Apollo.Data (entity + migration `AddMessageEvents`)
- Metadata extraction is a pure function `MessageMetadata.From(string content, ...)` in Apollo.Domain,
  so it's unit-testable and the content never leaves the adapter → recorder call
- Requires the Message Content intent (enabled in M1-04)
- Docs: NetCord gateway events, CmdScale hypertables

## Platform Parity
| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Notes |
|---|---|---|---|
| Message create/update/delete events | ✅ | ❓ | fill from docs/platform-parity.md (M1-01) |
| Reply reference | ✅ | ❓ | |
| Threads | ✅ | ❓ | likely ❌, then thread_parent_id stays NULL |

## Test Plan
1. Unit (Apollo.Domain.Tests): `MessageMetadata.From` (links, custom/unicode emoji, mentions, empty)
2. Unit (Apollo.Platform.Discord.Tests): NetCord payload → PlatformMessageCreated mapping
3. Testcontainers (Apollo.Bot.Tests): FakeChatPlatform raises events → assert rows for each AC above
4. Manual: run AppHost, post in Discord as an Administrator (bootstrap mode), see the row in pgAdmin

## Dependencies
Blocked by: M1-06 · Blocks: M1-14, M2-07, M3-01

## Definition of Done
- [ ] Implemented test-first per docs/coding-guidelines.md
- [ ] Builds without warnings; all tests green; `dotnet format` clean
- [ ] PR maps each acceptance criterion to its tests
- [ ] No platform types outside adapter projects
- [ ] UI text German, everything else English
- [ ] docs/ updated if behaviour or config changed
```

---

## Review decisions

1. **Wireframe format (M2-02):** clickable static HTML in `docs/design/wireframes/`.
2. **Issue granularity:** 48 issues, kept as drafted.
3. **GitHub Projects board:** none; milestones + labels only.
