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
- Backfilling history (planned later, see M7)
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

- **Inbound:** the adapter turns gateway events into neutral records like `PlatformMessageCreated(PlatformRef Guild, DateTimeOffset OccurredAt, PlatformRef Channel, PlatformRef Message, PlatformRef Author, …, MessageMetadata Metadata)` and delivers them through `IPlatformEventDispatcher` to every `IPlatformEventHandler<TEvent>` (#10).
- **Outbound:** an `IChatPlatform` interface with only the operations we need, such as `GetMemberRoles`, `GetMemberPermissions`, `ListChannels`, and later `AddRole` and `SendEmbed`.
- **Identity:** `PlatformRef = (Platform, ExternalId)`. The tables `users`, `channels`, `guilds` and `roles` each have an internal `Guid` plus one or more platform mappings. A future Fluxer account can be linked to an existing internal user, which keeps that user's history. Each mapping table is unique on `(platform, external_id)`, with `platform` stored as text (`discord`, `fluxer`); channels and roles belong to an internal guild. `IPlatformIdResolver` (`Apollo.Data`, #11) maps refs to internal IDs: `Resolve*` creates the row and mapping on first sight (one `INSERT ... ON CONFLICT DO NOTHING` statement, safe across processes), `TryGet*` never creates anything. Resolved IDs are cached in memory, since a mapping never changes once created.
- **Parity:** every issue that touches the platform includes a Fluxer.Net parity table. The parity spike (#9) confirmed that messages, reactions, voice, membership, threads, role changes and OAuth are portable to [Fluxer.Net](https://github.com/Nexfinity/Fluxer.Net). Presence activities and interactions (slash commands, ephemeral replies, buttons) don't exist on Fluxer; [platform-parity.md](platform-parity.md) lists the fallbacks.
- **Parity matrix:** [platform-parity.md](platform-parity.md) lists every capability with its NetCord and Fluxer.Net status.

### 4.3 Single guild

The bot is configured with `Apollo:Platform:Discord:GuildId`. Events from any other guild are dropped. Every table still has a `guild_id` column, so going multi-guild later is a config and UI change rather than a schema migration. This costs almost nothing, so I recommend it.

## 5. Data collection

### 5.1 What we record (raw events, append-only)

| Event table | Columns (besides id, guild_id, occurred_at) | Source | Needed for |
|---|---|---|---|
| `message_events` | user_id, channel_id, thread_parent_id?, message_ext_id, kind (create/edit/delete), length, word_count, attachment_count, embed_count, emoji_count, user_mention_count, role_mention_count, mentions_everyone, content_parts (flags: text, link, mention, emoji, attachment), reply_to_user_id?, is_bot | MESSAGE_CREATE/UPDATE/DELETE | messages per day/hour/channel, chattiest times, reply graph ("who talks to whom"), media sharer, link-only vs. text posts |
| `message_mentions` | user_id, channel_id, message_ext_id, mentioned_user_id? (one row per distinct mentioned user, on create) | MESSAGE_CREATE | mention graph ("who pings whom"), most mentioned |
| `voice_sessions` | user_id, channel_id, ended_at?, end_reason? (`Left`, `Moved`, `TrackingStopped` or `Unobserved`; `occurred_at` is the start; both `NULL` while open) | VOICE_STATE_UPDATE (join/leave/move), voice reconciliation (startup, reconnect, every 5 min) | voice time, favourite channel, longest session, voice buddies (overlap) |
| `voice_state_segments` | session_id, user_id, ended_at?, self_mute, self_deaf, streaming, video (`occurred_at` is the start, `ended_at` `NULL` while open) | VOICE_STATE_UPDATE, voice reconciliation | muted ratio, stream time |
| `reaction_events` | user_id, channel_id, message_ext_id, message_author_id?, emoji_name, emoji_custom_id? (unicode: no custom id), kind (add/remove) | MESSAGE_REACTION_ADD/REMOVE | top emojis, most-reacted-to user |
| `membership_events` | user_id, kind (join/leave/ban/unban) | GUILD_MEMBER_ADD/REMOVE, GUILD_BAN_ADD/REMOVE | growth curve, member tenure |
| `presence_activity_sessions` | user_id, activity_type (playing/listening/…), activity_name, ended_at (`occurred_at` is the start) | PRESENCE_UPDATE | top games, "hours played" |

Reference / snapshot tables: `guilds` (name, owner), `users` (display name, avatar), `channels` (name, type, category, thread parent, deleted_at), `roles` (name, colour, position, is_administrator, manages_roles, deleted_at), `guild_members` (joined_at, is_administrator, is_bot, left_at: the guild-scoped member facts, since `users` is guild-neutral), `member_roles` (current assignments), plus the platform mapping tables (`guild_platform_ids.bot_user_id` names the bot's own user on that platform, #33). They are not event tables: the guild state sync (#13) updates them, reconciles them against the full guild snapshot on every (re)connect and marks deleted roles and channels and departed members instead of removing them.

Settings and tracking tables (#14): `role_settings` with its append-only audit log `role_setting_changes` (§5.1.2), and `tracking_periods` (§5.1.1). They are plain tables, not hypertables.

**Notes**
- **Voice is stored as sessions, not individual events.** The recorder keeps the open sessions in memory. On startup, after a reconnect and every few minutes it reconciles them with the current voice states (D19): a session continues across a short blind spot, anything else the bot did not see ends as `end_reason = Unobserved` at the last moment the bot knew the state, so it can be filtered out later.
- **Message content intent.** Without it, Discord sends an empty `content` field, so we can't compute length, links or emoji. ApolloBot is in fewer than 100 servers, so turning on the privileged intents (Message Content, Presence, Members) is just a toggle in the developer portal. The content is used in memory to compute metadata and then thrown away.
- **Bot messages** are recorded with `is_bot = true` and excluded by default in queries.
- **Presence data is incomplete by design:**
  - Users can hide their activity in Discord.
  - Fluxer has no activities at all (verified in #9), so game data is Discord-only.
  - So recaps treat game data as a **bonus section**: it is shown only when there is data, and never assumed to be complete. Only sessions of at least 1 minute are stored.
- **Threads** are stored with their own `channel_id` and a `thread_parent_id`. Stats roll them up to the parent channel by default. Fluxer has threads with a parent reference too (verified in #9), so `thread_parent_id` is filled on both platforms.
- **Time zone:** all day and hour bucketing uses `Europe/Zurich`. Raw timestamps are stored in UTC.
- **Deleted messages** keep their create event and get a delete event added. Recaps count what was sent.
- **Messages (#15):** `kind` is stored as `Create`, `Edit` or `Delete`. Content metadata (`length` … `content_parts`) is computed in memory by `MessageMetadata.From` (`Apollo.Domain`) and is `NULL` on delete rows; `reply_to_user_id` is set on create rows only, and `message_mentions` is written on create only, so an edit that adds a mention updates the counts of its edit row but not the mention graph. `length` counts characters as shown: an emoji (unicode or custom) and a user, role or channel mention each count as one, links and all other text keep their characters. `word_count` counts the words of the text part (links, mentions and emoji removed). For the `Text` flag, Discord formatting characters (`*` `_` `~` `|` `` ` `` and a quote `>`) and brackets right around a link are not text; a URL ends before formatting characters and keeps brackets only in balanced pairs. `@everyone` and `@here` count only as case-sensitive whole words. An edit or delete without an author takes it from the message's create row; without one (message older than the bot, or its author untracked) nothing is stored. Embed-only updates and system messages are not recorded.
- **Voice (#16):** `voice_sessions(user_id, channel_id, occurred_at, ended_at, end_reason)` and `voice_state_segments(session_id, user_id, occurred_at, ended_at, self_mute, self_deaf, streaming, video)`, both hypertables on `occurred_at`: the session's `started_at` and the segment's `from` are stored as `occurred_at`, the segment's `to` as `ended_at` (`from`/`to` are SQL keywords). Segments carry `user_id` too, so per-user queries and deletes need no join; there is no foreign key from segment to session (its key is `(id, occurred_at)`). `end_reason` is stored as `Left`, `Moved`, `TrackingStopped` or `Unobserved` (the bot did not see the end: a restart, a disconnect or a failed save; stored as `BotRestart` before #98, rewritten by a migration); open rows have `ended_at` and `end_reason` `NULL`, and closing them is the only update. A move closes the old session and opens the new one at the same instant; each change of self mute, self deaf, stream or video starts a new segment, other updates (server mute, server deaf) are ignored. The pure `VoiceSessionStateMachine` (`Apollo.Domain`) decides; on Fluxer a user is in voice while any connection is in a channel, and the session follows their oldest connection in a channel. Reconciliation (#98): one rule (`VoiceSessionStateMachine.Reconcile`) compares, per tracked member, the session the bot holds with the member's current voice state after a time the bot could not see or store changes, given T, the last moment the bot knew the state. A member still in the same channel keeps their session if the blind spot is at most `Apollo:Voice:Reconciliation:MaxGap` (default 2 minutes); flags that differ end the current segment and start a new one at the reconciliation (the real time of the change is unknown). Anything else (left, another channel, a longer blind spot) closes the session as `Unobserved` at T (never before the start of its current segment), and a member in voice now gets a new session from the reconciliation; a member who lost tracking meanwhile is closed as `TrackingStopped` at T. A session the bot opened or changed after T counts as seen since then, so a second run over the same blind spot keeps what the first one decided. The rule runs on three triggers, each with its own T: on startup T is the guild's latest connected `bot_status` heartbeat before the start (written every 30 s, also before a crash; without one every session closes at the start); after a reconnect (the full guild sync) T is the disconnect, reported as `PlatformConnectionStateChanged` (a resume replays the missed events and needs no reconciliation); every `Apollo:Reconciliation:Interval` (default 5 minutes, shared with presence, `VoiceSessionLifecycleService`) the gateway stayed connected, so a member in the same channel always continues and a mismatch (a failed save) closes at the previous full reconciliation (or the start of the bot). Until the rule has run for a member in the current blind spot (startup, or a disconnect until the reconnect's sync), their first voice event applies the rule with the event's state instead of a plain update, so a move the bot did not see is never stored as `Moved` at the event's time; after that their events are handled as usual. The periodic run waits while the gateway is disconnected, after a reconnect until its sync (the new session's cache may still be empty; if that sync's reconciliation fails, the next periodic run retries it) and while the guild is unavailable (`IChatPlatform.IsGuildAvailable`), skips members whose voice event is still on its way (the seam for the event buffer, #99), and a failed run is logged and retried on the next tick. Every reconciliation reads the voice states and applies its changes under the recorder's lock, so a newer event is never overwritten. On startup, before anything is applied, the open rows (per user the newest open session with its newest open segment) are taken over into memory, so the rule decides whether they continue; any other open row is closed as `Unobserved` at the start. A graceful shutdown leaves the open sessions open: the next start decides; a bot that never starts again leaves them open, and the voice stats ignore open sessions (#36). Gaining tracking opens a session from the current voice state; losing it closes the session as `TrackingStopped`. Partial indexes on the open rows (`ended_at IS NULL`) serve the startup take-over. The in-memory state changes only once its rows are committed, so a failed save leaves both as they were until the next reconciliation corrects them.
- **Reactions (#17):** `reaction_events(user_id, channel_id, message_ext_id, message_author_id, emoji_name, emoji_custom_id, kind)`, a hypertable on `occurred_at`. `kind` is stored as `Add` or `Remove`. A unicode emoji is stored as `emoji_name` (the emoji itself) with `emoji_custom_id` `NULL`; a custom emoji as its platform id and name (the name is empty when Discord no longer knows the emoji); a check constraint keeps unicode rows from having an empty name. `channel_id` is the thread for reactions in a thread (no `thread_parent_id`; stats roll threads up through `channels`). The tracking gate decides by the reacting user. `message_author_id` is the author if tracked, else `NULL`, and never creates a user row; when the event has no author (Discord's remove never has one, Fluxer sends none), it comes from the latest earlier `Add` row of the same user, message and emoji (a custom emoji matches by id), else from the message's `Create` row in `message_events`, else `NULL`. Moderator bulk removals (remove all, remove emoji) are not recorded; super reactions count like normal ones.
- **Membership (#18):** `membership_events(user_id, kind)`, a hypertable on `occurred_at`. `kind` is stored as `Join`, `Leave`, `Ban` or `Unban`; a kick is a `Leave`, and a ban of a member is a `Ban` plus a `Leave` (Discord sends both events). Join and unban count if the tracking gate tracks the user when the event is processed (`MembershipRecorder` runs after the guild state sync, so a new member whose tracking role is already synced counts); a new member usually has no roles yet, so most joins are not recorded, and growth comes from `guild_members.joined_at` instead. Leave and ban count if the user had a `tracking_periods` row at `occurred_at` (both ends inclusive), so the leave that stops tracking is still recorded, whichever handler runs first; the member remove of a ban may arrive first and close the period, so a ban also counts if the user has a `Leave` row at most 10 s before it (tracking that ended any other way, such as a removed role, gets no grace). Discord's payloads carry no timestamp, so the events are stamped on receipt and a gateway replay arrives with a new `occurred_at`; a unique index cannot catch it. Instead the recorder skips an event if the user already has a row of that kind and their tracking did not change since the latest one: for a leave or ban no tracking period started after it, for a join or unban none ended after it. Known gap: a real repeat whose tracking left no new period in between is skipped (e.g. a leave before the first guild sync after a start, then a quick rejoin, then a second leave). The event dispatcher runs each handler in its own DI scope, so the recorder never saves `GuildStateSync`'s pending changes after a failure.
- **Presence (#19):** `presence_activity_sessions(user_id, activity_type, activity_name, occurred_at, ended_at)`, a hypertable on `occurred_at` (the session's start; there is no `started_at` column). `activity_type` is stored as `Playing`, `Streaming`, `Listening`, `Watching` or `Competing`; custom statuses (user-typed text) are not activities and are ignored. A session is one continuous activity of one user, identified by user, type and name, so simultaneous activities are independent sessions and a switch from one game to another closes and opens at the same instant; an offline user has no activities. Open sessions live only in memory (`PresenceSessionStateMachine` in `Apollo.Domain`), and a row is inserted when a session closes, only if `ended_at - occurred_at >= 1 minute`, so the table is strictly append-only. Presence updates carry no timestamp and are stamped on receipt. Gaining tracking opens sessions for the current activities (also for everyone tracked after the first sync on startup), losing it closes them; after every full guild sync (also after a reconnect) tracked users are reconciled with the current presences, and a session that no longer matches closes at that moment. Every `Apollo:Reconciliation:Interval` (the same 5 minutes as voice, `PresenceSessionLifecycleService`, #98) a periodic run compares every tracked member's open sessions with the current presences: a session whose activity is gone closes at the previous full reconciliation (a guild sync or periodic run, or the start of the bot), never before its own start, and is stored if it lasted at least a minute; a current activity without a session opens one from now; matching sessions stay untouched, so a run writes nothing when everything matches. Like the voice run it waits while the gateway is disconnected, after a reconnect until its sync and while the guild is unavailable, leaves members whose presence update is on its way to that update, runs under the recorder's lock, and a failed run is logged and retried on the next tick. A graceful shutdown closes and stores all open sessions, and nothing opens after it. After a crash the open sessions are lost and restart from the current presence (accepted: bonus data). The in-memory state changes only once its rows are committed, so a failed save keeps the sessions open until the next periodic run closes them at the previous run (before #98 they ran on until the member's next presence update). On Fluxer (`PresenceActivities = false`) the recorder is a no-op.

### 5.1.1 Who is tracked: the tracking role

Tracking is controlled by **one tracking role per platform** (e.g. "Statistik"). There is no list of tracked roles and no separate opt-out flag.

```
if a tracking role is configured for the platform (and still exists):
    record event  ⇔  user has the tracking role
else:
    record nothing
```

- **No tracking role, nobody tracked:** until a tracking role is set in the admin UI (§5.1.2), and after it is cleared, nobody is tracked, Administrators included (mode `NoTrackingRole`). No permission makes a member tracked; Admins who want to be tracked need the role themselves. The bot logs its mode at startup and on every change: the tracking role as information, `NoTrackingRole` and a deleted tracking role as warnings. M1 had a temporary Administrator fallback while no tracking role was set; #62 removed it after the admin UI (#33), before the first production deploy (#86), so on the first start of a deployment nobody is tracked. Open tracking periods of members the rule no longer tracks are closed by the gate's first rebuild after a start, as for any other member.
- **The gate (#14):** `ITrackingGate` answers `IsTracked` from an in-memory snapshot (no DB call per event). `Current` returns that snapshot as an immutable `ITrackingView`, so code that needs several answers to agree (e.g. the tracking role and whether a member holds it) reads them from one snapshot (#21). It is rebuilt after every full guild sync and every 30 s, and re-evaluates a single member on every member change from the guild state sync. Until the first full sync after a start, nobody is tracked. The rule itself is the pure `TrackingRule` in `Apollo.Domain`; it sees only a member's roles, never their permissions. A data deletion (#22) can keep one user untracked while the rule still tracks them (`TrackingGate.MarkUntrackedAsync`); the mark ends once a rebuild or member re-evaluation finds the rule no longer tracks them, or when the deletion releases it.
- **Intended setup:** I give the tracking role to all members by hand at the start. After that, members manage it themselves with `/apollo tracking on|off` (§5.4).
- **Role changes take effect live.** The member role sync (§6.2) keeps `member_roles` up to date, and the recorder checks it from an in-memory cache. It makes no difference whether the role was changed by command, by an admin in Discord, or by some other bot.
  - Losing the tracking role closes any open voice/presence session and stops recording. Existing data is **kept and stays visible**. Removing it requires an explicit delete (§5.4).
  - Gaining the tracking role starts recording. Voice and presence sessions for members already in a channel or game are opened right away, using the same reconciliation logic as at bot startup.
- **References to untracked users are dropped.** If a tracked user replies to or reacts to an untracked user, the event is stored but the untracked user's reference is NULL (`reply_to_user_id`, `message_author_id`). No data about untracked users ever lands in the DB, apart from the reference tables needed for role sync and access checks (`users`, `member_roles`).
- **Tracking vs. access:** the tracking role (whose data is recorded) and the member access roles (who can log in and see their stats, §6.2) are independent. A member who can log in but isn't tracked sees an empty state explaining why ("Für dich werden keine Daten erfasst").
- **Tracking periods:** the stats queries never need to know about roles, since untracked activity is never written. `tracking_periods(guild_id, user_id, from, to)` is stored so recaps can say "tracked since …" and leave untracked time out of averages. A member has at most one open period (`to IS NULL`). The gate opens and closes them on every gain or loss of tracking and, at startup, closes the periods of users who are no longer tracked.

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
- **No config bootstrap.** The settings exist only in the DB and are set in the admin UI; until the tracking role is set there, nobody is tracked (§5.1.1). Member access only matters from M4, when the UI already exists.
- **Bot permissions:** the bot needs *Manage Roles*, and its own role must sit above the tracking role in the role hierarchy. The admin settings page checks this and shows a warning if it's wrong.
- **Changes apply without a restart.** The bot reloads role settings every 30 s and keeps them in a cached snapshot. When the tracking role changes, every affected member goes through the same start/stop logic as a member role change: sessions are opened or closed and `tracking_periods` are updated.
- **Audit:** every change stores who made it and when. The admin UI shows a short change history, so gaps in the data can be explained later ("Tracking-Rolle am … entfernt"). The history is the append-only `role_setting_changes(guild_id, platform, purpose, role_id, action: Added | Removed, changed_by_user_id?, changed_at)`, written by a database trigger on every insert, update or delete of `role_settings`, in the same transaction, so rows added by hand in M1 are audited too. A removal takes its author and time from the transaction-local settings `apollo.role_settings_changed_by` / `apollo.role_settings_changed_at`, which `RoleSettingsStore` sets before every write; a delete without them (by hand) records no author and the database clock.
- **Admin page (#33):** `/admin/settings` (Einstellungen) has one tab per platform whose configured guild (the guild of its identity provider, §4.3) the guild state sync has seen. Endpoints under `/api/admin/settings` (policy `Admin`, writes antiforgery-protected): `platforms`, `{platform}` (roles with member counts, current settings, mode `noTrackingRole` / `active` / `trackingRoleDeleted`, bot check), `{platform}/preview?roleIds=…` (members holding any of the roles, and who would start or stop being tracked with them as the tracking role), `{platform}/history?take=20`, `PUT {platform}/tracking-role` (`roleId: null` clears it) and `PUT {platform}/member-access-roles`. Writes go through `RoleSettingsStore` (`Apollo.Data`), which rejects unknown or deleted roles and roles of another guild or platform (400). Member counts are current members without bots. The bot check (`BotPermissionCheck`) reads the synced state: one of the bot's roles grants Manage Roles (`roles.manages_roles`) or the bot has Administrator, and its highest role's position is above the tracking role's; @everyone permissions are not considered, as for the Administrator flag. Saving the tracking role asks for confirmation with who starts and stops being tracked; clearing it says that nothing is recorded anymore, for anyone.
- **Admin access is not a setting.** It stays hard-wired to the Administrator permission (D8), so nobody can lock themselves out or widen admin access by mistake through the UI.

### 5.2 Storage: TimescaleDB

TimescaleDB is a Postgres extension, so Npgsql and EF Core work unchanged. Timescale-specific objects are set up with **[CmdScale.EntityFrameworkCore.TimescaleDB](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)**, which uses the Fluent API and data annotations and generates normal EF migrations. Raw SQL is used only for what the package can't do.

| Need | How |
|---|---|
| Hypertables for the event tables | `IsHypertable(x => x.OccurredAt)` with a chunk interval |
| Daily rollups | `IsContinuousAggregate<TAgg, TSource>(...)` plus `AddAggregateFunction(...)` (Sum, Count, Min, Max, Avg, First, Last) |
| Refresh, compression, retention | Fluent policy APIs from the package |
| Time zone–aware daily buckets (`time_bucket('1 day', ts, 'Europe/Zurich')`) | The fluent API can't add the time zone. These aggregates are written by hand in the migration with CmdScale's `CreateContinuousAggregate(viewDefinition: ...)` and read through a keyless `ToView` entity ([ADR 0001](adr/0001-timescale-ef.md)) |
| Combining aggregates of several hypertables | Plain views over the continuous aggregates (raw SQL in the migration), read through keyless `ToView` entities (§5.3) |
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

   **As built (#34):** a continuous aggregate reads one hypertable, so there is one per source, each per Zurich day (`time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich')`, hand-written, ADR 0001), guild, user and channel: `daily_user_channel_messages` (`Create` rows only: edits don't count, a deleted message still counts once; characters = sum of `length`; grouped by `is_bot` too), `daily_user_channel_reactions_given` (`Add` rows by the reacting user), `daily_user_channel_reactions_received` (`Add` rows by `message_author_id`, `NULL` authors left out) and `daily_user_channel_voice` (closed sessions only; a session counts whole on the Zurich day it started, also when it runs past midnight). `daily_user_channel_activity` and `daily_guild_activity` are plain views over them: the first sums the four per day, guild, user and channel (a thread keeps its own `channel_id`) and takes `thread_parent_id` from `channels` and `is_bot` from the message rows or `guild_members` at query time; the second sums it per day, guild and `is_bot` (`reaction_count` = reactions given). `day` is the UTC instant of the Zurich midnight; `Apollo.Stats` converts it with `TimeZoneInfo`. `hourly_activity_heatmap` (per UTC hour, guild, user and `is_bot`, `Create` rows) uses CmdScale's fluent API: Zurich is always a whole number of hours off UTC, so its hours are Zurich hours; #35 derives the Zurich weekday and hour (the fall-back day has two buckets for 02:00). All five continuous aggregates use real-time aggregation (`materialized_only = false`, so today is current) and one refresh policy (`ActivityAggregates`: every 30 minutes, from 7 days to 1 hour ago). A change older than 7 days, e.g. a voice session closed more than a week after it started, reaches the aggregates only through a manual refresh ([deployment.md](deployment.md#day-to-day-operations)); a privacy delete refreshes the days it changed itself (#39, §5.4). Entities: `Apollo.Data/Activity` (keyless, read with LINQ).
2. **Date-range query layer (`Apollo.Stats`):** takes `(guild, user?, from, to)`, reads the daily aggregates and returns a stats DTO. The admin dashboard, the user's personal page and the recaps all use this one layer.

   **As built (#35):** `IActivityStatsService.GetAsync(ActivityStatsQuery(GuildId, UserId?, From, To, IncludeBots = false))` (`Apollo.Stats/Activity`, registered with `AddActivityStats()`). `From`/`To` are inclusive Zurich dates, turned into the UTC instants of their midnights (`[midnight of From, midnight after To)`), which is what the buckets hold; `From > To` throws `ArgumentException`. Everything comes from `daily_user_channel_activity` (its `is_bot` already combines the message rows and `guild_members`) and `hourly_activity_heatmap` (bots left out by its own flag and by `guild_members.is_bot`, the same rule), so `daily_guild_activity` is not read. `ActivityStatsDto` (records and lists only, so it round-trips through JSON for #46): totals (`ActivityCounts`: messages, characters, reactions given and received, voice seconds), per-day averages, active days (a message, a reaction given or voice time; reactions received alone don't count), for the guild active users (distinct, same rule), the daily series (one zero-filled entry per day), the channel ranking (messages and voice seconds, threads rolled up through `COALESCE(thread_parent_id, channel_id)`, name and deleted flag from `channels`, most messages first), the heatmap (7 × 24, Zurich weekday Monday first × hour, messages) and the voice heatmap (`VoiceHeatmap`, #111, same shape, voice seconds: the one raw SQL query of the service, over `voice_sessions`, since one session feeds several hour cells and a continuous aggregate can't split it; each closed session is clipped to the range, `generate_series` yields the UTC hours it overlaps, which are Zurich hours, and `LEAST`/`GREATEST` give the overlap with each, so a session from 21:40 to 23:10 adds 20, 60 and 10 minutes; the fall-back day's two 02:00 hours both go to 02:00, a DST day keeps the sessions' real length, open sessions are left out and bots are left out by `guild_members.is_bot`; the cells sum to the voice time inside the range, which can differ from `Totals.VoiceSeconds`, where a session counts whole on the day it started). For a user it adds tracked days (Zurich days in the range overlapping a `tracking_periods` row, an open period until now; the pure `TrackedDays.Count`) and tracked since (the Zurich day their current, open period started; `null` when they are not tracked now, as "Erfasst seit …" means). Averages divide by the days of the range for the guild and by the tracked days for a user (all zero for 0 days); a range reaching past today still counts its future days for the guild. Known limitation: with `IncludeBots = false`, a user's reactions received still include reactions *from* bots, because `reaction_events` has no bot flag for the reacting user (filtering them would need one).
3. **Recap snapshots:**
   - At the end of each month or year, a job runs layer 2 and stores the result as JSON in `recap_snapshots(guild, user?, period_type, period_start, payload, generated_at)`.
   - Snapshots are fixed and load instantly.
   - Any other date range is computed on the fly.

Things that don't fit daily sums are their own queries over raw data: voice buddies (sessions that overlap), reply graph, longest session.

**As built (#36):** `IRawStatsService` (`Apollo.Stats/Raw`, registered with `AddRawStats()`) answers the raw-data questions for `RawStatsQuery(GuildId, UserId?, From, To, Top = 5, IncludeBots = false)`, with #35's range semantics (`StatsRange.ForZurichDays`: `[midnight of From, midnight after To)`, a DST day lasts 23 or 25 hours; `From > To` throws `ArgumentException`, `Top < 1` `ArgumentOutOfRangeException`). Bots are left out by default through `guild_members.is_bot` (for replies also the message's own `is_bot`, and a reply *to* a bot member is left out too). With `UserId`, every answer holds only data involving that user. Open voice sessions are ignored by every voice query; sessions closed as `Unobserved` count as they are. **Voice buddies** (`VoiceBuddy`): the one raw SQL query of the service, a self-join of `voice_sessions` on guild and channel (different users, both closed) summing `LEAST(ends, range end) - GREATEST(starts, range start)` per pair, so only the overlap inside the range counts; each pair appears once for the guild, for a user `UserId` is that user; most seconds first, ties by IDs. **Reply graph** (`ReplyGraph`): `Create` rows with `reply_to_user_id`, counted per directed edge (author → replied-to user); replies to oneself are left out; `Edges` are the top edges (for a user, those involving them), for a user `RepliesTo` / `RepliedBy` are the top outgoing / incoming edges. **Longest session** (`LongestVoiceSession`, or `null`): the closed session with the largest `ended_at - occurred_at` that *started* in the range, with its full (unclipped) duration; ties by the earlier start, then the user ID. **Top emojis** (`EmojiUsage`): `Add` rows of `reaction_events` in the range (for a user, the reactions they gave); a unicode emoji is keyed by its name, a custom emoji by its ID, under its latest non-empty name (ties by name, ordinal; a renamed emoji stays one entry); ties by name (ordinal), unicode before custom, then custom ID. **Top games** (`GamePlaytime`): `Playing` sessions of `presence_activity_sessions` overlapping the range, clipped to it (`StatsRange.Clip`, in memory) and summed per activity name; empty without presence data (a bonus section, §5.1); ties by name. Every result is a record (lists as `IReadOnlyList`) so it round-trips through JSON for the recaps (#46). No new tables or aggregates.

### 5.4 Privacy

There are two separate actions: **stop tracking**, which is role-based and reversible, and **delete data**, which is permanent and only happens on explicit request.

| Feature | Behaviour |
|---|---|
| Stop tracking | `/apollo tracking off`: the bot removes the tracking role, and recording stops immediately (§5.1.1). The **ephemeral** reply (visible only to the user) explains: "Ab jetzt werden keine Daten mehr erfasst. Bereits erfasste Daten bleiben gespeichert. Mit `/apollo privacy delete` kannst du sie endgültig löschen." |
| Start tracking | `/apollo tracking on`: the bot adds the tracking role back. This works at any time: after `off`, after `delete`, or for members who never had the role. The time without the role remains a gap (`tracking_periods`). The ephemeral reply briefly explains what is recorded and links the privacy page. |
| Status | `/apollo tracking status`: shows whether the user is tracked, since when, and how much data is stored (e.g. event counts). |
| Stats link | `/apollo stats` (D5): the ephemeral reply links the user's personal page `/me` and contains no stats values (stats live on the website only, §3, §7.2). It is the same for everyone, tracked or not and with or without a tracking role; the website decides access. Until the `/me` page exists (M4), the link leads to the login or "Kein Zugriff" page. |
| Delete (hard) | `/apollo privacy delete`. The ephemeral reply has a confirm button and states plainly that this can't be undone. Confirming permanently removes the user's events, snapshots and `tracking_periods`, anonymises references to them in other users' data (e.g. `reply_to_user_id` becomes NULL), refreshes the affected aggregate ranges, **and removes the tracking role**, so recording doesn't simply resume. The user can still get the role back at any time with `/apollo tracking on`, which starts recording again from zero. |
| Not set up | If no tracking role is configured (none set, or the role was deleted), nobody is tracked and `/apollo tracking on\|off` replies "Tracking ist noch nicht eingerichtet"; `status` and `privacy delete` say the same. |
| Web (M4) | Read-only: the `/me` page shows tracking status, tracked-since date, and explains the commands. **No web toggle or web delete for now** (out of scope, see §3). |
| Admin view | The member stats page (M3) and the member list (M5) show each member's tracking status. |
| Transparency | A German privacy page explaining what is stored and how the tracking role works. It's linked from every command reply. Public at `/datenschutz` (#96, `Pages/Privacy.razor`): no login, content only (no control changes tracking or deletes data); it covers what is recorded (never message content), that only tracking role holders are recorded and references to others are not stored, stop vs. delete with the four commands, who sees the data (Administrators all of it, others nothing) and Zurich time. Its text must stay true to the recorders: a change to what is recorded or to the commands updates it. |

**Implementation:**
- No opt-out flag. The tracking-role check in the recorder is the only gate.
- **All `/apollo` replies are ephemeral.** NetCord's interaction responses support the ephemeral flag. Fluxer has no interactions; there, private replies become a DM and the invoking message is deleted ([platform-parity.md](platform-parity.md)).
- The commands only add or remove a role. The resulting `GUILD_MEMBER_UPDATE` flows through the normal role sync, so command, manual and admin role changes all behave identically.
- `IChatPlatform` gets `AddRoleAsync` and `RemoveRoleAsync`. That makes them, along with slash commands, ephemeral replies and buttons, parity items for Fluxer.Net.
- **Tracking commands (#21):** each command reads one gate view (`ITrackingGate.Current`). `on` and `off` take the tracking role from it (`ITrackingGate.TrackingRole`, the role on the bot's platform while one is configured and exists). Without one (none set, a deleted role, or before the first guild sync after a start) they answer "Tracking ist noch nicht eingerichtet." and change nothing. Whether the member already holds the role is the gate's answer (with a tracking role it tracks exactly its holders; the platform's member reads may come from an incomplete gateway cache), so `on` with the role and `off` without it make no call. A role change the platform refuses (missing *Manage Roles*, role hierarchy) is logged as an error and answered with a German error reply. `status` reports the gate's answer, the start of the open `tracking_periods` row as a `Europe/Zurich` date (`dd.MM.yyyy`), and the user's rows in `message_events`, `reaction_events`, `voice_sessions`, `presence_activity_sessions` and `membership_events` (joins, leaves, bans and unbans) from all periods (`IUserDataSummary`; the detail tables `message_mentions` and `voice_state_segments` belong to a counted message or session and are not counted). Without a tracking role it adds that tracking is not set up. It creates no data for a member the bot has never seen.
- **Privacy delete (#22):** `/apollo privacy delete` replies with the warning and a confirm button (`ButtonStyle.Danger`) issued to the invoking member; nothing is deleted before the click, and the router turns anyone else away. The button handler calls `IUserDataEraser` (`Apollo.Bot/Privacy`), which a Fluxer text confirm (M7) can call too. In this order:
  1. The gate marks the member untracked at once, so events on their way are dropped and the usual `TrackingStopped` hooks close their open voice and presence sessions (closing writes rows as usual; step 2 deletes them).
  2. One transaction runs every `IUserDataEraserStep`. It first asks each step for the time range of the rows it will delete or clear a reference in (`FindRangeAsync`; `RecordedDataEraserStep`: the earliest and latest `occurred_at`, for sessions their start, over every event table it changes, the member's own rows and those referencing them). Then `RecordedDataEraserStep` deletes the member's rows from `message_events`, `message_mentions` (as author), `voice_sessions`, `voice_state_segments`, `reaction_events`, `membership_events`, `presence_activity_sessions` and `tracking_periods`, and sets `reply_to_user_id`, `message_mentions.mentioned_user_id` and `message_author_id` to NULL where they point to the member. The reference tables (`users`, `guild_members`, `member_roles`) stay (§5.1.1). `user_sign_outs` stays too: it holds only the member's last website logout time, and erasing it would make a session cookie copied before that logout valid again (D18). A failure rolls everything back: the role stays, the mark ends (the sessions and the period closed in step 1 stay closed, and a member the rule still tracks is tracked again from a new period) and the reply is a German error.
  3. If the member holds the tracking role (the gate's answer, as for `tracking off`), the bot removes it; the mark ends once the echoed member update is synced, so recording does not resume in between. If the platform refuses, the data stays deleted, the mark ends, and the reply says recording continues and to ask an Admin. Only the deletion that added the mark ends it, on every way out (also a failure or a cancellation): a second confirm while the first one's role removal is not synced yet finds the mark, leaves it alone and removes the role again.
  4. **As built (#39):** last, after the role removal on every path (so a cancellation during a long refresh, e.g. a shutdown, cannot skip the removal once the rows are gone), `IAggregateRefresher` (`Apollo.Data/Activity/ContinuousAggregateRefresher`) refreshes every continuous aggregate over that range, widened to whole Zurich days (from the Zurich midnight before the earliest row to the Zurich midnight after the latest; 23 or 25 hours on DST days, and hour bounds too, so the hourly heatmap is covered). The aggregates are read from the TimescaleDB catalog (`timescaledb_information.continuous_aggregates`), so one a later migration adds is refreshed without another list to keep. `CALL refresh_continuous_aggregate` cannot run in a transaction; the refresher throws if the context has one open. A refresh policy alone would never reach rows older than 7 days (§5.2), and materialized buckets win over the raw tables, so without this the deleted member's numbers (and the reactions others gave on their messages, counted as received) would stay. A member without event rows gets no refresh. The refreshed range is logged with the internal user ID only. If the refresh fails, the deletion stays committed and the reply is unchanged; the error is logged with the range, and an admin refreshes by hand ([deployment.md](deployment.md#day-to-day-operations), *Refresh the aggregates by hand*). Policy runs re-materialize only their last 7 days, so they fix a failed refresh only for days in that window.

  Without a tracking role no role call is made, the mark ends at once (nobody is tracked), and the reply says tracking is not set up yet. The reply after a deletion with a tracking role points to `/apollo tracking on`. The deletion is logged once with the deleted rows and cleared references per table and the internal user ID only. A member the bot has never seen gets the same reply and no rows are created. Recap snapshots get their own step in M6.

## 6. Authentication & authorization

### 6.1 Login

- An OAuth2 authorization-code flow with `identify` scope only. **Discord** first. The design is platform-neutral (`IPlatformIdentityProvider`), so Fluxer OAuth can be added later; Fluxer.Net already has OAuth types.
- After login, the web host maps the platform user to the internal user and issues an ASP.NET Core **cookie session**. The cookie holds only internal claims (internal user id, platform, display name, login time, and the result of the last access check: the admin claim and when it was checked, §6.2). No platform tokens are stored after login.
- **Logout ends every session of the user.** `/auth/logout` deletes the cookie and stores the logout time (`user_sign_outs`); any session cookie of that user issued before it, a copied one included, is rejected at its next access check (D18).
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

Bot and web are separate processes, so the web host does not ask the bot: the bot syncs `member_roles` and the permission flags into the DB (#13), and the web host reads from there. This means no extra service-to-service API, and the data stays useful if the bot is temporarily down.

How it works (#30):
- **Admin** = `guild_members.is_administrator` of a current member (`left_at IS NULL`) of the configured guild of the platform the user logged in with (the bot's `Apollo:Platform:Discord:GuildId`). The flag includes the guild owner. No other code path grants it: role settings and configuration never do (D8). The reverse holds too: admin access never makes anyone tracked (§5.1.1).
- The check result lives in the cookie as the admin claim plus the check time. It is computed at login and again on the first request after `Apollo:Web:Auth:RevalidationInterval` (default 5 minutes); within the interval no DB lookup is made. The client gets the claim through `/auth/me` and uses the same policy definition (`ApolloPolicies`) to show or hide pages; the host enforces it on every API call.
- `/api/admin/**` requires `Admin`: anonymous callers get 401, others 403. `/admin` pages (`Pages/Admin` in the client) require it too: anonymous users go to the login, logged-in users without it see "Kein Zugriff" (`/kein-zugriff`).

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

Layout (D16, chosen from the wireframe concepts in #27): the member area is a **magazine page**, not an app shell. There is no drawer. A minimal header has the logo, a period switch and an avatar menu (Privatsphäre, Datenschutz, Abmelden). The overview is one long page in themed chapters (channels, voice, times, games), each opening with a headline and a key number. A row of recap covers at the top replaces a separate recaps list. A recap opens in the same magazine layout for its period. Admins reach `/admin` through an "Admin" entry in the avatar menu; the admin area keeps its drawer. Wireframes: [docs/design/wireframes/](design/wireframes/index.html).

**Admin area (`/admin`)**
- Bot status: connected, gateway latency, last event received, events per hour, open voice sessions. This is the "data is being collected" proof for M2.
  The page (#31, #91, `/admin`) reads `GET /api/admin/bot-status` for the configured guild (§4.3). Everything comes from the database; the bot only writes its heartbeat (#24).
  - Heartbeats (`bot_status`): the latest one; the latest one that said connected (the "Getrennt" alert says "Letzte Verbindung: …", or leaves the time out when none ever did); the gateway latency as the mean of the heartbeats of the last 5 minutes, rounded to whole ms, heartbeats without a latency ignored ("Mittel der letzten 5 min"). A heartbeat older than `Apollo:Web:BotStatus:StaleAfter` (default 2 minutes) shows a stale warning; no heartbeat at all shows "Noch kein Heartbeat empfangen".
  - "Letztes Ereignis": the newest recorded row of the five tables below by `occurred_at`: its age as the value, its type and, if it has one, its channel as the subtext ("Nachricht in #allgemein"; membership and presence rows have none). Without any recorded row the tile says "Noch nichts erfasst". The heartbeat's `last_event_at` (any event the bot received, untracked members included) is not shown.
  - Open voice sessions: `voice_sessions` rows with `ended_at IS NULL` (only tracked members have rows, and losing tracking closes them; while the bot is stopped its sessions stay open until the next start reconciles them), with member, channel and start (Zurich); the tile counts them and their channels ("In 2 Kanälen").
  - Events per hour: the rows of `message_events`, `reaction_events`, `membership_events`, `voice_sessions` and `presence_activity_sessions` (by `occurred_at`, the start for sessions) per `Europe/Zurich` hour over the last 24 hours, the current hour included and empty hours as 0, split per table (stacked bars).
  - While a synced platform has no tracking role (#62), an info banner "Keine Tracking-Rolle festgelegt: Es wird niemand erfasst." links to `/admin/settings`.
  - The page reloads every 30 s while it is open.
- Server dashboard: totals, growth, activity over time, channel ranking, heatmap
  **As built (#37):** the page "Server-Übersicht" (`/admin/dashboard`) reads `GET /api/admin/stats/server?from=yyyy-MM-dd&to=yyyy-MM-dd` (policy `Admin`): #35's `ActivityStatsDto` for the configured guild (§4.3), bots left out; a missing or malformed date, `from > to`, a start before 2000-01-01 or an end after tomorrow in Zurich is 400 with a validation problem, and before the bot has seen the guild the answer is the zero-filled range. The range presets are "Monat" (default) and "Jahr", the current calendar month or year up to today in `Europe/Zurich` (the guild averages count every day of the range, so a preset never reaches into the future), and "Benutzerdefiniert", a date range picker that cannot pick a day after today. A new range loads again (no auto refresh). Tiles: Voice-Stunden (hours and minutes, closed sessions only, so today's value grows as sessions end), Nachrichten, Reaktionen (given) and Aktive Mitglieder, each with the per-day average or the active days; then activity per day (voice hours or messages, one at a time), the channel ranking (a list, top 10 with "Alle anzeigen", threads rolled up, sorted by Voice-Stunden or Nachrichten, the other number breaking ties) and the weekday × hour heatmap of voice time (tooltip in hours and minutes) or messages, which below 600px sums the hours into 8 blocks of 3 hours. Voice comes first (D21, #111): the first tile, and the default of the activity chart, the ranking sort and the heatmap; each toggle switches to messages (`StatsMetric`). A range without any activity shows "Keine Daten im gewählten Zeitraum" instead of the charts. Growth (members joining and leaving) is not shown yet: there is no aggregate for it. The stats components live in `Components/Stats` and take their data as parameters, for the member pages (#38, #42).
- Members: a member's stats page (same view as `/me`), opened from the Daten browser or by internal user ID (M3); a list with search (M5)
  **As built (#38):** the page `/admin/members/{userId}` (internal user ID) reads `GET /api/admin/members/{userId}/stats?from=yyyy-MM-dd&to=yyyy-MM-dd` (policy `Admin`; the range is checked as for the server dashboard, `StatsDateRange`). A user who is no member of the configured guild (departed members count as members) or an ID that is no GUID is 404. The answer holds the member's synced name, avatar and platform, the tracking status from `tracking_periods` (`ITrackingStatusService` in `Apollo.Stats`, for #41 and #101 too: tracked since the Zurich day the open period started, else not tracked, until the day the latest period ended), #35's `ActivityStatsDto` for the user (averages per tracked day) and #36's results (top 5 emojis, voice buddies, whom they reply to and who replies to them, the longest session, top games), bots left out and names of other members and channels resolved on the server; custom emojis carry no platform ID. The head shows avatar, name, "Erfasst seit …" or "Nicht erfasst" (with "bis …" after a closed period) and the platform; the range presets are those of the dashboard and the head stays while a new range loads. A member who was never tracked sees "Für {Name} werden keine Daten erfasst" and no range selector. Everything else is the shared `MemberMagazineView` (`Components/Stats`, plain DTOs only, for `/me` in #42), in "du" form as the member will read it: tiles (Voice-Stunden, Nachrichten, Reaktionen given, Aktive Tage "x von y" tracked days), activity per day (voice first, D21; messages first for a member without voice time in the range, as is the ranking sort), then chapters led by a key number: "Du und deine Kanäle" (top channel and its share of messages; top 5 channels, top emojis, reply partners when there are any), "Deine Leute im Voice" (only with voice time, buddies or a longest session), "Deine Zeiten" (with voice time or messages: the key number is the weekday and hour with the most voice time, or without any voice time the most messages, and the heatmap opens on that metric) and "Spiele" (a bonus, only with game data). A range without anything shows "Keine Daten im gewählten Zeitraum". The channel ranking says "Keine Nachrichten und keine Voice-Zeit im gewählten Zeitraum" when it has no rows (a member who only reacted). The member column of the Daten browser links every row to this page (event rows exist only for tracked members). Not built here: recap covers and the period pill of the wireframe (recaps are #47), generated headline sentences, the "Du wirst gerade nicht erfasst" card (it speaks to the member, #42 decides), and the member list (#101).
- Recaps: server recap per month/year, regenerate a snapshot
- Data: event counts per table, tracked vs. untracked member counts, raw event browser (for debugging)
  The page (#32, `/admin/data`, "Daten") reads `GET /api/admin/data/…` (policy `Admin`) for the configured guild (§4.3); it loads once and has no auto refresh.
  - `counts?from=&to=`: the rows of `message_events`, `voice_sessions`, `voice_state_segments`, `reaction_events`, `membership_events` and `presence_activity_sessions` (by `occurred_at`, the start for sessions and segments) per `Europe/Zurich` day, every day of the range present (0 when empty), and the total per table. `from`/`to` are Zurich days, both included; the default is the last 30 days with today. The page shows a stacked chart per day and a table with today, yesterday, 7 days and 30 days.
  - `members`: per platform whose configured guild is synced, current members without bots split by the gate's rule (`RoleSettingsQuery.GetMemberTrackingAsync`, `TrackingRule`): tracked = holders of the tracking role, 0 without one or when it was deleted, plus the mode (`noTrackingRole` / `active` / `trackingRoleDeleted`). The page warns while nobody is tracked.
  - `events?type=&userId=&channelId=&from=&to=&page=&pageSize=`: one page of one table (type by its JSON name, default `message`), newest first (`occurred_at DESC, id DESC`), with the total; the filters combine with AND, and a channel filter on a table without a channel (segments, membership, presence) matches nothing. `to` defaults to now and `from` to 24 hours before it. Offset paging, page size 50 by default and at most 200; a range over 366 days on Zurich clocks (so a year that gains an hour at the end of summer time still fits), an unknown type, a page size out of range or a page whose row offset exceeds `int.MaxValue` is 400 with a validation problem. Rows carry metadata only (no field for message content exists), internal IDs with the synced user and channel names, and references to untracked users stay `NULL` (shown as "Nicht erfasst"). `users?search=` and `channels?search=` feed the filter autocompletes (members of the guild, departed ones included, and its channels, deleted ones included; at most 20, by name).
- Data sanity: heartbeat gaps, overlong or stale sessions, and data of untracked members (expected 0)
- Settings (Einstellungen), with a tab per platform:
  - **Tracking role:** single select from the synced roles, showing role colour and member count, plus a live preview "X Mitglieder werden erfasst". Shows a warning banner while none is set ("Keine Tracking-Rolle festgelegt: niemand wird erfasst") or the set one was deleted, and a role-hierarchy/permission check for the bot
  - **Member access roles:** multi-select, with the same preview
  - **Change history:** who changed what and when

**Public pages**
- Datenschutz (`/datenschutz`, #96): open without login, a single readable column (max 760px) inside the app shell; the drawer's Info entry and every bot reply link it (§5.4).

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
| **M1** | Data collection | Bot runs on the server and records events | Spike: NetCord ↔ Fluxer.Net parity · platform abstraction · NetCord adapter · ID mapping · recorders: messages, voice, reactions, membership, presence · voice session reconciliation · member role/permission sync · tracking-role gate (with a temporary Administrator fallback, removed by #62 before the first deploy) · `/apollo tracking on|off|status` + `/apollo privacy delete` + `/apollo stats` (all ephemeral) · hypertables · **deploy to Swarm** (data starts accumulating as early as possible) |
| **M2** | Admin access & data visibility | I can log in and see that data is flowing | Design: style guide + admin wireframes · Discord OAuth + BFF cookie · Admin policy (Administrator perm) · admin bot-status + data page · raw event browser · **role settings page** (tracked + member access roles, per platform, with change history) |
| **M3** | Stats visible to admin | Full round trip: stats can be queried and the admin sees them | Continuous aggregates · `Apollo.Stats` date-range layer and raw-data stats · admin server dashboard with charts · admin member stats page · aggregate refresh after privacy delete · data sanity checks |
| **M4** | Member access & privacy | Members see their own stats | Member policy via roles · `/me` overview · privacy page (read-only tracking status) · mobile pass |
| **M5** | Admin UI complete | Every planned admin feature | Member list with search · storage policies (compression, retention) |
| **M6** | Recaps | Wrapped-style monthly/yearly recaps | **Round-trip validation recap** (one rough recap to check the data is useful) · recap snapshot job · recap pages (member + admin) · full design pass |
| **M7** | Later / icebox | — | History backfill · Fluxer adapter · reaction roles · embed builder · recap export to Discord · multi-guild |

M1 is deployed before any UI exists, so data collection starts as early as possible. While M2–M4 are being built, the data keeps accumulating. Order (D20): usability first. Recaps need at least a month of data, so they come last.

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
| D11 | Tracking scope | Exactly one tracking role per platform, and only its holders are recorded. Without one nobody is recorded, Administrators included (the M1 Administrator fallback was removed by #62 once the admin UI could set the role). References to untracked users are stored as NULL (§5.1.1) |
| D14 | Role settings storage | Tracking role and member access roles live in the DB as `role_settings`, scoped to (guild, platform) and referencing internal role IDs. No config seeding. Edited in the admin UI from M2 on, and the bot picks up changes within 30 s. Admin access is never configurable |
| D15 | Opt-in/out = role | No separate opt-out flag. Users toggle the tracking role with `/apollo tracking on\|off`. Ephemeral replies explain that existing data stays and how to delete it. I give the role to all members by hand at the start. No web toggle or web delete for now |
| D12 | Development style | TDD (red → green → refactor), see [coding-guidelines.md](coding-guidelines.md) |
| D13 | Test stack | xUnit v3, Shouldly, NSubstitute (edges only), Testcontainers (Timescale image), bUnit, Aspire.Hosting.Testing |
| D16 | Member area layout | Magazine page without a drawer: header with logo, period switch and avatar menu; themed chapters; recap covers instead of a recaps list; admins reach `/admin` from the avatar menu. Picked over a bento grid and a timeline in the #27 wireframe review (§7.2) |
| D17 | DB retries | Aspire's EF retry strategy is off (`AddApolloDbContext` sets `DisableRetry`), because it rejects the explicit transactions the bot uses. Code that needs a retry does it itself (tracking gate reload, voice startup take-over and periodic reconciliation, `DatabaseMigrator`) instead of wrapping each transaction in `CreateExecutionStrategy()` ([#77](https://github.com/cosimo-augustoni/ApolloBot/issues/77)) |
| D18 | Logout revocation | Logout stores a per-user logout time (`user_sign_outs`). The access check (§6.2) rejects every session cookie of that user issued before it, so a copied cookie stops working within the revalidation interval, and logging out on one device ends the sessions on the others too (#30) |
| D19 | Voice reconciliation | One rule for startup, reconnect and a periodic run (every 5 min): a voice session continues across a blind spot of up to `MaxGap` (default 2 min) when the member is still in the same channel, so a short restart no longer splits sessions; anything else ends at T, the last moment the bot knew the state, as `Unobserved`, which replaces `BotRestart` since it marks every end the bot did not see (restart, reconnect, failed save). Graceful shutdown no longer closes voice sessions; presence keeps closing on shutdown and is not kept across a restart (bonus data, D1), but is reconciled in the same interval, so a failed save closes a presence session at the previous run instead of at the member's next presence update ([#98](https://github.com/cosimo-augustoni/ApolloBot/issues/98)) |
| D20 | Milestone order | MVP first: M3 ends once the admin sees the stats (a full round trip to watch in production), then member access, then the full admin UI, then recaps, which need at least a month of data |
| D21 | Voice first | The server is used far more for voice than for text, so voice time is the primary stat: there is a weekday × hour heatmap of voice time (`ActivityStatsDto.VoiceHeatmap`, split at the query over `voice_sessions`, not a continuous aggregate), and wherever a stats page offers voice or messages (tiles, activity chart, channel ranking sort, heatmap, the "Deine Zeiten" key number) it shows voice first; messages stay one toggle away. The member view falls back to messages for a member without voice time in the range ([#111](https://github.com/cosimo-augustoni/ApolloBot/issues/111)) |

## 12. Open questions

None blocking. Answered by spikes:
- CmdScale: daily `time_bucket` with a time zone in continuous aggregates, and compatibility with the latest EF Core version → [ADR 0001](adr/0001-timescale-ef.md)
