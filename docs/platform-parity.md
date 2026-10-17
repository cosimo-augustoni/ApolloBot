# Platform parity: NetCord (Discord) vs. Fluxer.Net (Fluxer)

This document records, capability by capability, whether the two platform libraries ApolloBot uses or plans to use
can deliver what the bot needs. Every platform issue fills its "Platform Parity" table from here.
Scope background: [scope.md §4.2](scope.md#42-platform-abstraction). Spike issue: #9.

**Legend:** ✅ supported · ⚠️ partial or a workaround is needed · ❌ not supported.
Every status cell links to its source. Library sources are pinned to the commit that was checked.

## What was checked

Checked on **2026-10-15**.

| Library / source | Version | Commit | Where |
|---|---|---|---|
| NetCord | `1.0.0-beta.28` (tag) | [`52bc355`][nc-root] | `NetCord`, `NetCord.Services`, guides on [netcord.dev](https://netcord.dev) |
| Fluxer.Net | `3.7.6` (tag on `release`) | [`b8c0e98`][fn-root] | `Fluxer.Net` project |
| Fluxer server + API docs | `main` | [`8f4c966`][fx-root] | `fluxer_docs` (published at [docs.fluxer.app](https://docs.fluxer.app)) |

How the Fluxer column was decided: a capability needs **both** the Fluxer server to send or accept it (Fluxer API
docs in the server repo) **and** Fluxer.Net to expose it. When the server supports something and Fluxer.Net does not,
the cell is ⚠️ and the note says which raw call works around it. When the server does not have it, the cell is ❌.

No live Fluxer guild was used. Every Fluxer status comes from the server's API docs plus the Fluxer.Net source.
Rows that depend on runtime behaviour the docs don't pin down say "needs live check".

Shorthand in the Source column: **NC** = NetCord source, **FN** = Fluxer.Net source, **FX** = Fluxer API docs,
**DC** = Discord developer docs (for Discord platform facts that NetCord simply passes through).

## Gateway events

| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Source | Notes |
|---|---|---|---|---|
| Message create / update / delete | [✅][nc-msg-events] | [✅][fn-msg-events] | NC [events][nc-msg-events] · FN [dispatch][fn-msg-events], [delete payload][fn-msg-delete] · FX [Message Create][fx-msg-create], [Message Delete][fx-msg-delete] | Discord's delete payload has no author; the recorder takes `user_id` from its own create row. Fluxer's delete carries `author_id`. Fluxer.Net drops `MESSAGE_CREATE` / `UPDATE` / `DELETE` for channels missing from its cache, so a channel or thread created while disconnected loses events until the next `GUILD_CREATE` (needs live check). Discord (#15): `MessageCreate` / `MessageUpdate` / `MessageDelete` → `PlatformMessageCreated` / `Updated` / `Deleted`; `MessageDeleteBulk` → one `PlatformMessageDeleted` per message. Only messages people write are mapped (types default, reply, slash and context-menu command); system messages (joins, pins, boosts, thread notices) are skipped. An update without `edited_timestamp` (Discord resolving a link's embed) is not an edit and is skipped. |
| Reaction add / remove | [✅][nc-reaction-events] | [✅][fn-reaction-events] | NC [events][nc-reaction-events] · FN [dispatch][fn-reaction-events] · FX [Reaction Add][fx-reaction-add], [Reaction Remove][fx-reaction-remove] | Both deliver `user_id`, `channel_id`, `message_id`, `emoji`. |
| Voice state updates | [✅][nc-voice-event] | [✅][fn-voice-event] | NC [`VoiceStateUpdate`][nc-voice-event] · FN [dispatch][fn-voice-event] · FX [Voice State Update][fx-voice-state] | Fluxer keys voice states by `session_id` / `connection_id`, so one user can hold more than one connection (FN stores `member.VoiceStates` per session). See Consequences, #16. |
| Presence updates incl. activities | [✅][nc-presence-event] | [❌][fx-presence-object] | NC [`PresenceUpdate`][nc-presence-event], [`Presence.Activities`][nc-presence-activities] · FN [`activities` commented out][fn-presence-gw] · FX [Presence object][fx-presence-object], [published presence has no activities][fx-cmd-presence] | The Fluxer presence object has only `status`, `mobile`, `afk` and `custom_status`, with no activities at all. Fluxer.Net's `PresenceGatewayData` has `activities` commented out ("Defunct or wip?"). A bot also gets no presence until it subscribes members through Lazy Request opcode 14 ([FX][fx-filter-presence]), which Fluxer.Net does not expose. **Degrades:** `presence_activity_sessions` stays empty on Fluxer; recaps hide the games section (D1). |
| Member add / remove / update (roles) | [✅][nc-member-events] | [✅][fn-member-events] | NC [events][nc-member-events] · FN [dispatch][fn-member-events] · FX [Member Add][fx-member-add], [Member Update][fx-member-update], [Member Remove][fx-member-remove] | Update carries the full member incl. `roles` on both platforms. Fluxer's remove carries only `user.id`. Discord (#13): `GuildUserAdd` / `GuildUserUpdate` / `GuildUserRemove` → `PlatformMemberJoined` / `PlatformMemberUpdated` / `PlatformMemberLeft`. |
| Bans add / remove | [✅][nc-ban-events] | [✅][fn-ban-events] | NC [events][nc-ban-events] · FN [dispatch][fn-ban-events] · FX [Ban Add][fx-ban-add], [Ban Remove][fx-ban-remove] | Fluxer payload: `guild_id` + `user.id` only. Discord sends it only with *Ban Members* or *View Audit Log*. |
| Threads create / update / delete + parent reference | [✅][nc-thread-events] | [✅][fn-thread-events] | NC [events][nc-thread-events], [`GuildThread.ParentId`][nc-thread-parent] · FN [dispatch][fn-thread-events], [`parent_id`][fn-channel-parent] · FX [Gateway threads][fx-threads], [thread object `parent_id`][fx-thread-object] | Fluxer has threads, contrary to the earlier guess in scope §5.1. A **bot** session always has thread access; only user sessions need the `CHANNEL_THREADS` Identify flag ([FX][fx-threads-flag]), which Fluxer.Net never sends ([FN][fn-identify]). `thread_parent_id` can therefore be filled on Fluxer too. Discord (#13): thread and channel events both map to `PlatformChannelCreated` / `Updated` / `Deleted`; a thread carries `ThreadParent`. GUILD_CREATE lists only active threads, so the reconciliation never marks a missing thread deleted (it may only be archived). |
| Guild / channel / role snapshot on connect | [✅][nc-guild-create] | [⚠️][fn-guild-create] | NC [`GuildCreate`][nc-guild-create] · FN [`GUILD_CREATE`][fn-guild-create], [`READY` skips member-less guilds][fn-ready] · FX [Guild ready object][fx-guild-ready], [Guild Create][fx-guild-create] | Fluxer sends complete `roles`, `channels`, `voice_states` and `threads`, but `members` holds only the bot itself and voice participants, with `user` reduced to `{id}` in the post-Ready burst. **Fallback:** follow every `GUILD_CREATE` with request-all-members (next row). Needs live check that Fluxer.Net's member cache copes with the reduced `user` objects. Discord (#13): `GuildCreate` → `PlatformGuildAvailable` (incl. the guild owner) on every new session, so also after a reconnect; @everyone is left out. |
| Gateway connection, reconnect / resume | [✅][nc-root] | ❓ | NC `GatewayClient` `Ready` / `Resume` / `Disconnect` events | Discord (#12): NetCord reconnects by itself; `Disconnect` → `PlatformConnectionStateChanged(Disconnected)`, then either `Resume` (`Resumed`, missed events replayed) or `Ready` (`Connected`, new session). The same transitions drive `IPlatformConnectionStatus.Current.IsConnected` (#24), which the heartbeat and the bot's health check read. Fluxer not checked yet. |
| Gateway latency | [✅][nc-root] | ❓ | NC `GatewayClient.LatencyUpdate` (NetCord.Hosting `ILatencyUpdateGatewayHandler`) | Discord (#24): each heartbeat ack updates `IPlatformConnectionStatus.Current.Latency`, stored as `bot_status.gateway_latency_ms`; `null` while disconnected and after each connect or resume until the new session's first heartbeat ack. Fluxer not checked yet; without it the column stays `null`. |
| Guild id on every guild event (single-guild filter) | [✅][nc-root] | ❓ | NC event args `GuildId` | Discord (#12): every guild event carries the guild id; `DiscordEventMapper` drops other guilds and guild-less events (DMs) before mapping, and counts each drop as `apollo.events.dropped{reason=other_guild}` (#24). Fluxer not checked yet. |
| Request all members | [✅][nc-request-users] | [✅][fn-download-members] | NC [`RequestGuildUsersAsync`][nc-request-users], [`GuildUserChunk`][nc-member-events] · FN [`DownloadMembersAsync`][fn-download-members], [`RequestMembersPacket`][fn-request-members-packet] · FX [Request Guild Members][fx-request-members] | Fluxer: a bot may ask for the full list once per 30 s per guild, in chunks of ≤ 1,000. Discord needs the `GuildUsers` intent. Discord (#13): `IChatPlatform.ListMembersAsync` sends `RequestGuildUsersAsync` with a nonce and collects every `GuildUserChunk` (2 min timeout), without waiting for the guild cache. The sync calls it after every `PlatformGuildAvailable`, retries an empty or failed list twice (after 30 s and 60 s, which also respects Fluxer's limit) and never marks members left without a full list. |

## Data fields the recorders need

| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Source | Notes |
|---|---|---|---|---|
| Message content / content intent | [✅][nc-intent-content] | [✅][fx-no-intents] | NC [`GatewayIntents.MessageContent`][nc-intent-content], [`Content`][nc-msg-fields] · FN [`content`][fn-msg-fields] · FX [no intents][fx-no-intents], [message object][fx-msg-object] | Discord: privileged `MessageContent` intent (portal toggle, scope §5.1). Fluxer has **no intent system**, so content is always delivered to sessions that can view the channel. |
| Reply reference (replied-to user) | [✅][nc-msg-reference] | [⚠️][fn-msg-fields] | NC [`MessageReference`, `ReferencedMessage`][nc-msg-reference] · FN [`message_reference`][fn-msg-fields] · FX [`referenced_message`][fx-msg-object] | The Fluxer server sends `referenced_message`, but Fluxer.Net's `MessageJson` has no property for it; only `message_reference` (channel + message id) is mapped. **Fallback:** resolve the replied-to author from our own `message_events` by `message_ext_id` (an untracked author is NULL anyway, scope §5.1.1), or via `GetMessageAsync` over REST. Discord (#15): `ReplyToAuthor` comes from `referenced_message` (only for type-19 replies, not forwards); when Discord omits it (original deleted), the recorder's lookup in `message_events` fills in. |
| Attachments / embeds / mentions | [✅][nc-msg-fields] | [✅][fn-msg-fields] | NC [`RestMessage`][nc-msg-fields] · FN [`MessageJson`][fn-msg-fields] · FX [message object][fx-msg-object] | Counts only: `attachments`, `embeds`, `mentions`, `mention_roles` on both. Discord (#15): `mentions` also lists the replied-to author when a reply pings; the adapter drops that author unless the text itself mentions them. `mention_everyone` is false when the author may not ping everyone, so `@everyone` then counts as text. Mentions are taken from the payload and parsed from the text (`<@id>`, `<@&id>`, `@everyone`, `@here`) only where a payload lacks them (`MessageMetadata.From`). |
| Bot flag on author | [✅][nc-user-bot] | [✅][fn-user-bot] | NC [`User.IsBot`][nc-user-bot] · FN [`UserJson.IsBot`][fn-user-bot] · FX [message object `author`][fx-msg-object] | |
| Reaction emoji (unicode vs. custom id + name) | [✅][nc-reaction-emoji] | [✅][fn-emoji] | NC [`MessageReactionEmoji`][nc-reaction-emoji] · FN [`EmojiJson`][fn-emoji] · FX [Reaction emoji object][fx-reaction-emoji] | Same shape on both: `id` absent/null means unicode, then `name` is the emoji itself. Fluxer sends `animated` only on the first reaction with that emoji, so it is not reliable. |
| Message author on reaction events | [⚠️][nc-reaction-author] | [⚠️][fn-reaction-payload] | NC [`MessageReactionAddEventArgs.MessageAuthorId`][nc-reaction-author], [remove args have none][nc-reaction-remove-args] · FN [`MessageReactionGatewayData`][fn-reaction-payload] · FX [Reaction Add fields][fx-reaction-add] · DC [Reaction Remove][dc-reaction-remove] | Discord: add carries `message_author_id`, remove does not. Fluxer: neither does. **Fallback (both):** reuse the author from the matching earlier `add` row (already in #17), else look it up in our own `message_events`; on Fluxer, Fluxer.Net also hands out a `Cacheable<Message>` that fetches the message over REST. |
| Voice self-mute / self-deaf / streaming / video | [✅][nc-voice-fields] | [✅][fn-voice-fields] | NC [`VoiceState`][nc-voice-fields] · FN [`VoiceStateJson`][fn-voice-fields] · FX [Voice state object][fx-voice-state] | Same fields: `self_mute`, `self_deaf`, `self_stream`, `self_video`. On Fluxer, `self_stream` is forced false without the `STREAM` permission. |
| Activity type + name | [✅][nc-activity] | [❌][fn-presence-gw] | NC [`UserActivity.Name` / `Type`][nc-activity], [`UserActivityType`][nc-activity-type] · FN [`activities` commented out][fn-presence-gw] · FX [Presence object][fx-presence-object] | Fluxer.Net has an `ActivityJson` type ([FN][fn-activity-json]), but nothing on the gateway fills it and the Fluxer presence object has no activities. Same degradation as the presence row. |
| Member permissions (Administrator) and role list | [✅][nc-member-perms] | [✅][fn-member-perms] | NC [`PartialGuildUser.RoleIds`][nc-member-roles], [`GetPermissions`][nc-member-perms], [`Permissions.Administrator`][nc-perm-admin] · FN [`GuildMemberJson.roles`][fn-member-roles], [`SocketGuildMember.HasPermission`][fn-member-perms], [`GuildPermission.Administrator`][fn-perm-admin] · FX [Permission computation][fx-perm-compute] | Both compute guild permissions from the member's roles (+ owner). Our role sync should store the computed `is_administrator` flag itself rather than rely on library caches. #13 stores `roles.is_administrator` and computes `guild_members.is_administrator` from `member_roles` plus the guild owner (`guilds.owner_user_id`); @everyone permissions are not considered. |

## REST

| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Source | Notes |
|---|---|---|---|---|
| Add role to member | [✅][nc-rest-role] | [✅][fn-rest-role] | NC [`AddGuildUserRoleAsync`][nc-rest-role] · FN [`AddMemberRoleAsync`][fn-rest-role] · FX [Add guild member role][fx-add-role] | Needs *Manage Roles* and role hierarchy on both. |
| Remove role from member | [✅][nc-rest-role] | [✅][fn-rest-role] | NC [`RemoveGuildUserRoleAsync`][nc-rest-role] · FN [`RemoveMemberRoleAsync`][fn-rest-role] · FX [Remove guild member role][fx-remove-role] | |
| List roles | [✅][nc-rest-roles] | [⚠️][fx-list-roles] | NC [`GetGuildRolesAsync`][nc-rest-roles] · FN [`ApiClient`, no roles list][fn-rest-guild] · FX [List guild roles][fx-list-roles] | The server has `GET /guilds/{id}/roles`, but Fluxer.Net has no method for it (searched `ApiClient.cs` for `Roles`; only create/update/delete/positions exist). **Fallback:** take roles from the gateway cache (`SocketGuild.Roles`, complete on every `GUILD_CREATE`) or `GetGuildAsync`, or call the route with a plain `HttpClient`. |
| List channels | [✅][nc-rest-channels] | [✅][fn-rest-channels] | NC [`GetGuildChannelsAsync`][nc-rest-channels] · FN [`GetChannelsAsync`][fn-rest-channels] · FX [List guild channels][fx-list-channels] | |
| List members | [✅][nc-rest-members] | [✅][fn-rest-members] | NC [`GetGuildUsersAsync`][nc-rest-members] · FN [`GetMembersAsync`][fn-rest-members] · FX [List guild members][fx-list-members] | Fluxer: pages of ≤ 1,000 by `after` cursor. The server default `limit` is 1, but Fluxer.Net always sends 1,000. |

## Commands

| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Source | Notes |
|---|---|---|---|---|
| Slash commands with subcommands | [✅][nc-subcommands] | [❌][fx-flags] | NC [`SubSlashCommandAttribute`][nc-subcommands], [guide][nc-guide-subcommands] · FN [text-prefix `CommandService`][fn-command-service], [`HasStringPrefix`][fn-prefix], [`GroupAttribute`][fn-group] · FX [docs index][fx-root-docs] | Not found: Fluxer.Net has no `Interaction` / `INTERACTION_CREATE` / application-command types (searched the whole source tree), and the Fluxer API docs have no interaction or application-command routes or events. **Fallback:** text-prefix commands via Fluxer.Net's `CommandService` with `[Group]` for `apollo tracking on` etc. (e.g. `!apollo tracking on`). |
| Guild-scoped command registration | [✅][nc-guild-register] | [❌][fx-root-docs] | NC [`RegisterCommandsAsync(…, guildId)`][nc-guild-register] · FN / FX: no registration API (see row above) | Not applicable on Fluxer: text commands need no registration. The single-guild filter on incoming messages does the scoping. |
| Ephemeral replies | [✅][nc-ephemeral] | [❌][fx-flags] | NC [`MessageFlags.Ephemeral`][nc-ephemeral], [guide][nc-guide-respond] · FX [message flags][fx-flags] | Not found: Fluxer's sendable message flags are only `SUPPRESS_EMBEDS`, `SUPPRESS_NOTIFICATIONS`, `VOICE_MESSAGE`. **Fallback:** reply by DM ([`CreateDMChannelAsync`][fn-dm]) and delete the invoking message ([`DeleteMessageAsync`][fn-delete-msg]) so the command isn't left in the channel. DMs can fail if the user blocks them; then post a short public "check your DMs / DMs closed" note. |
| Deferred replies | [✅][nc-deferred] | [❌][fx-root-docs] | NC [`InteractionCallback.DeferredMessage`][nc-deferred], [guide][nc-guide-respond] · FN [`TriggerTypingIndicatorAsync`][fn-typing] | No deadline exists on Fluxer (no interaction token), so deferral isn't needed. **Fallback:** optionally send a typing indicator, then the DM reply when done. |
| Buttons / component interactions | [✅][nc-buttons] | [❌][fx-root-docs] | NC [`ButtonProperties`][nc-buttons], [`ComponentInteractionService`][nc-component-service], [`InteractionCreate`][nc-interaction-event] · FX [docs index][fx-root-docs] | Not found: no component or interaction types in Fluxer.Net or the Fluxer docs. **Fallback** for the `/apollo privacy delete` confirm: a confirmation command with a one-time code (`!apollo privacy delete confirm <code>`), or a reaction on the bot's DM (✅ via [`AddReactionAsync`][fn-add-reaction] + reaction add event). |

## OAuth

| Capability | Discord (NetCord) | Fluxer (Fluxer.Net) | Source | Notes |
|---|---|---|---|---|
| Authorization-code flow with `identify`-equivalent scope | [⚠️][dc-oauth] | [✅][fn-oauth-options] | DC [authorization code grant][dc-oauth] · NC: no OAuth handler (only bearer-token REST, see next row) · FN [`FluxerOAuthHandler`][fn-oauth-handler], [`Scope.Add("identify")`][fn-oauth-options], [endpoints][fn-oauth-defaults] · FX [OAuth2 scopes][fx-oauth-scopes], [authorisation code grant][fx-oauth-code] | Discord supports the flow, but NetCord does not implement the web flow. **Fallback (#29):** ASP.NET Core's generic OAuth handler (`AddOAuth`), registered by `AddDiscordIdentityProvider()` in `Apollo.Platform.Discord` (`Identity/`); `Apollo.Web` only sees `IPlatformIdentityProvider`. Fluxer.Net ships an ASP.NET Core `OAuthHandler` with `identify` as the default scope; its endpoints default to `api.fluxer.app` and are overridable. |
| State / PKCE | [✅][dc-state] / [⚠️][dc-implicit] | [✅][fx-oauth-code] / [✅][fx-oauth-authorize] | DC [state and security][dc-state], [implicit grant warning][dc-implicit] · FX [authorisation code grant][fx-oauth-code], [authorise application][fx-oauth-authorize], [exchange token][fx-oauth-token] · FN [`FluxerOAuthOptions`][fn-oauth-options] | Checked in #29. `state` is documented on both. Fluxer documents PKCE (`code_challenge` with `S256` or `plain` on authorize, `code_verifier` on the token request). Discord recommends the "authorization code grant with PKCE" but lists no PKCE parameters; Apollo sends them anyway (`UsePkce = true`, S256) and still has `state` if Discord ignores them. Fluxer.Net's options leave `UsePkce` at the base default (off), so the M6 Fluxer provider must set `UsePkce = true` itself. |
| User id endpoint | [✅][nc-current-user] | [✅][fx-oauth-userinfo] | NC [`BearerToken`][nc-bearer], [`GetCurrentUserAsync`][nc-current-user] · DC [`/users/@me`][dc-users-me] · FN [`UserInformationEndpoint`][fn-oauth-defaults] · FX [Get OAuth2 user information][fx-oauth-userinfo] | Discord: `GET /users/@me` with the bearer token. Fluxer: `GET /v1/oauth2/userinfo` returns `id` (and `sub`). |

## Consequences

What the findings mean for the platform abstraction and the recorders. Each item names the backlog issue it affects.

### `IChatPlatform` and neutral event records

- **#10: presence must be optional per platform.** Fluxer has no activities, so `PlatformPresenceUpdated` will never
  carry an activity on Fluxer, and the Fluxer adapter needs no presence subscription at all. Add a capability flag
  (e.g. `IChatPlatform.Capabilities.PresenceActivities`) so the bot and the UI can tell "no data" from "not supported",
  and `ListPresencesAsync` returns empty on Fluxer.
- **#10: `PlatformMessageDeleted` carries an optional author.** Fluxer sends `author_id`, Discord does not. Model it as
  `PlatformRef? Author`; the recorder (#15) falls back to its own create row when it is null.
- **#10: reply and reaction author references are optional.** `PlatformMessageCreated.ReplyToAuthor` and
  `PlatformReactionAdded/Removed.MessageAuthor` stay nullable and are resolved in the recorder, not the adapter
  (see #15, #17). Keep `ReplyToMessage` (message id) as a separate field so the recorder can resolve the author itself.
- **#10: voice states are per connection, not per user.** Fluxer can report several voice connections for one user.
  `PlatformVoiceStateChanged` needs a connection/session discriminator (nullable on Discord), and `ListVoiceStatesAsync`
  may return more than one entry per user.
- **#10: thread parent stays a first-class field on both platforms.** Fluxer has threads with `parent_id`, so the
  channel snapshot's thread parent is populated on Fluxer too.
- **#10 / #13: listing roles is not a uniform REST call.** `ListRolesAsync` on Fluxer reads the gateway cache (or a
  raw HTTP call), because Fluxer.Net has no list-roles method. The contract should not promise a fresh REST read.
- **#13: snapshot then request members.** On Fluxer the guild snapshot has almost no members, so the sync must always
  follow `PlatformGuildAvailable` with a full member request (≤ once per 30 s on Fluxer) before computing
  `member_roles` and Administrator flags. Done in #13: `GuildStateSync` takes members only from
  `IChatPlatform.ListMembersAsync`, which requests the full list; the Fluxer adapter must do the same there.

### Command model

- **#20: the neutral command model must not assume interactions.** On Fluxer, commands are text messages
  (`!apollo tracking on`). `CommandInvocation` should come from either a slash interaction or a parsed text command;
  `Path` and `Options` already fit both.
- **#20: "ephemeral" becomes a delivery intent, not a flag.** Model replies as `CommandReply` with a
  `Visibility.Private` intent; Discord maps it to `MessageFlags.Ephemeral`, Fluxer to a DM plus deleting the invoking
  message. Handlers must not depend on a reply being attached to the invocation.
- **#20 / #22: buttons need a text fallback.** `ReplyButton` cannot render on Fluxer. Give each button a text
  equivalent (a confirm sub-command with a one-time code) so `IButtonHandler` can be reached from both a button click
  and a text command. `/apollo privacy delete` (#22) uses this for its confirmation.
- **#20: deferral is Discord-only.** Keep it inside the Discord adapter (3 s rule); the neutral model needs no
  "deferred" concept.
- **#20: registration is Discord-only.** `CommandDefinition` is still useful on Fluxer for help text and parsing, but
  there is nothing to register.
- **#21, #23:** reply texts (German) must read well in a DM as well as in an ephemeral reply.
- **Built in #20** (`Apollo.Platform.Abstractions/Commands`): `CommandInvocation`, `ButtonInvocation`, `CommandReply`
  with `ReplyVisibility`, `ReplyButton`, `CommandDefinition` (merged by name with `CommandDefinition.Combine`), and the
  contracts `ICommandHandler`, `IButtonHandler`, `ICommandRouter`. Button ids are `ButtonId` (`prefix:owner[:argument]`,
  owner = the member's platform id), so a Fluxer confirm command can carry the same id as text. The router lives in
  `Apollo.Bot`; the Discord adapter maps interactions, applies the single-guild filter, registers the definitions per
  guild and defers on its own after 2 s. A Fluxer adapter (M6) calls the same `ICommandRouter` from parsed text commands
  and delivers `ReplyVisibility.Private` as a DM.

### Recorders that degrade on Fluxer

| Recorder | Issue | Effect on Fluxer |
|---|---|---|
| Presence activity sessions | #19 | No data at all (no activities in Fluxer presence). The recorder is a no-op for Fluxer; recaps hide the games section, as D1 already allows. |
| Message metadata | #15 | Works. `reply_to_user_id` needs the recorder-side lookup because Fluxer.Net doesn't map `referenced_message`. Delete events come with the author (better than Discord). |
| Reactions | #17 | Works. `message_author_id` always comes from the lookup rule (no payload author on add or remove). |
| Voice sessions | #16 | Works, incl. mute/deaf/stream/video. The recorder must cope with more than one connection per user (e.g. collapse to "in voice while any connection is in a channel"). |
| Membership | #18 | Works (join, leave, ban, unban). |
| Threads | #15, #13 | Works. `thread_parent_id` is filled on Fluxer too, so the "Fluxer probably has no threads" note in scope §5.1 is out of date. |
| Login | #29 (M6 for Fluxer) | Works with `identify`; Fluxer.Net already has an ASP.NET Core handler for `IPlatformIdentityProvider`. The Fluxer provider must sign in to the host's default sign-in scheme (the external cookie), turn `/v1/oauth2/userinfo` into the claims its `GetIdentity` reads, and set `UsePkce = true`. |

### Open points that need a live Fluxer guild

- Fluxer.Net's handling of the reduced member objects in the post-Ready `GUILD_CREATE` burst.
- Whether Fluxer.Net drops message events for channels created while the bot was disconnected (cache miss on
  `Channels`).
- Multi-connection voice in practice (how often one user has two voice connections in a guild).

<!-- NetCord @ 52bc355 -->
[nc-root]: https://github.com/NetCordDev/NetCord/tree/52bc355e8e866f1cb8433334e37d9f566b91a265
[nc-msg-events]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L559-L613
[nc-reaction-events]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L631-L640
[nc-voice-event]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L709
[nc-presence-event]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L670
[nc-presence-activities]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/Presence.cs#L13
[nc-member-events]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L361-L392
[nc-ban-events]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L307-L322
[nc-thread-events]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L144-L164
[nc-thread-parent]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Channels/TextChannels/Guild/Threads/GuildThread.cs#L15
[nc-guild-create]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L260-L274
[nc-request-users]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L1101-L1103
[nc-intent-content]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayIntents.cs#L135
[nc-msg-fields]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestMessage.cs#L115-L164
[nc-msg-reference]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestMessage.cs#L214-L227
[nc-user-bot]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/User.cs#L110
[nc-reaction-emoji]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/MessageReactionEmoji.cs#L13-L18
[nc-reaction-author]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/EventArgs/MessageReactionAddEventArgs.cs#L33
[nc-reaction-remove-args]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/EventArgs/MessageReactionRemoveEventArgs.cs
[nc-voice-fields]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/VoiceState.cs#L35-L41
[nc-activity]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/UserActivity.cs#L10-L11
[nc-activity-type]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/UserActivityType.cs
[nc-member-roles]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/PartialGuildUser.cs#L44
[nc-member-perms]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/PartialGuildUserExtensions.cs#L29
[nc-perm-admin]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Permissions.cs#L40
[nc-rest-role]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestClient.Guild.cs#L121-L126
[nc-rest-roles]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestClient.Guild.cs#L182
[nc-rest-channels]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestClient.Guild.cs#L37
[nc-rest-members]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestClient.Guild.cs#L68
[nc-subcommands]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord.Services/ApplicationCommands/SubSlashCommandAttribute.cs#L9
[nc-guide-subcommands]: https://netcord.dev/guides/services/application-commands/subcommands.html
[nc-guild-register]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord.Services/ApplicationCommands/ApplicationCommandServiceManager.cs#L43-L78
[nc-ephemeral]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/MessageFlags.cs#L42
[nc-deferred]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/InteractionCallback.cs#L24
[nc-guide-respond]: https://netcord.dev/guides/basic-concepts/responding-to-interactions.html
[nc-buttons]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/ComponentProperties/ButtonProperties.cs#L7
[nc-component-service]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord.Services/ComponentInteractions/ComponentInteractionService.cs#L15
[nc-interaction-event]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Gateway/GatewayClient.cs#L741
[nc-bearer]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/IToken.cs#L28
[nc-current-user]: https://github.com/NetCordDev/NetCord/blob/52bc355e8e866f1cb8433334e37d9f566b91a265/NetCord/Rest/RestClient.User.cs#L8

<!-- Fluxer.Net @ b8c0e98 (v3.7.6) -->
[fn-root]: https://github.com/Nexfinity/Fluxer.Net/tree/b8c0e9851bad6c80930553bb6c3b6b4099ff9197
[fn-msg-events]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L1708-L1754
[fn-msg-delete]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/Data/Messages/MessageDeleteGatewayData.cs#L10-L11
[fn-reaction-events]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L1784-L1841
[fn-reaction-payload]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/Data/Messages/MessageReactionGatewayData.cs
[fn-voice-event]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L1922-L1965
[fn-voice-fields]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Voice/VoiceStateJson.cs#L41-L54
[fn-presence-gw]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/Data/Users/PresenceUpdatedGatewayData.cs#L19-L25
[fn-activity-json]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Users/ActivityJson.cs#L9-L14
[fn-member-events]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L1561-L1632
[fn-ban-events]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L1652-L1678
[fn-thread-events]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L1390-L1438
[fn-channel-parent]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Channels/ChannelJson.cs#L26-L27
[fn-identify]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/Data/IdentifyGatewayData.cs
[fn-ready]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L556-L580
[fn-guild-create]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L855-L950
[fn-download-members]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/GatewayClient.cs#L266-L287
[fn-request-members-packet]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Gateway/Packets/RequestMembersPacket.cs
[fn-msg-fields]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Messages/MessageJson.cs#L33-L94
[fn-user-bot]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Users/UserJson.cs#L41-L42
[fn-emoji]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Expressions/EmojiJson.cs#L9-L22
[fn-member-roles]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Guilds/Members/GuildMemberJson.cs#L61
[fn-member-perms]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Guilds/Members/SocketGuildMember.cs#L23-L46
[fn-perm-admin]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Data/Guilds/Permissions/GuildPermission.cs#L31
[fn-rest-role]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L1270-L1280
[fn-rest-guild]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L1058
[fn-rest-channels]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L1333
[fn-rest-members]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L1111-L1118
[fn-command-service]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Commands/CommandService.cs#L534-L547
[fn-prefix]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Commands/Extensions/MessageExtensions.cs#L17-L30
[fn-group]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Commands/Attributes/GroupAttribute.cs
[fn-dm]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L254
[fn-delete-msg]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L483
[fn-typing]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L513
[fn-add-reaction]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/Rest/ApiClient.cs#L575
[fn-oauth-handler]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/OAuth/FluxerOAuthHandler.cs#L18
[fn-oauth-options]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/OAuth/FluxerOAuthOptions.cs#L14-L27
[fn-oauth-defaults]: https://github.com/Nexfinity/Fluxer.Net/blob/b8c0e9851bad6c80930553bb6c3b6b4099ff9197/Fluxer.Net/OAuth/FluxerOAuthDefaults.cs#L13-L17

<!-- Fluxer API docs: live site, source pinned @ 8f4c966 -->
[fx-root]: https://github.com/fluxerapp/fluxer/tree/8f4c966d3a4ae5ec3c4535aa989598e89cfc4e4e/fluxer_docs/src/content/docs
[fx-root-docs]: https://github.com/fluxerapp/fluxer/tree/8f4c966d3a4ae5ec3c4535aa989598e89cfc4e4e/fluxer_docs/src/content/docs
[fx-no-intents]: https://docs.fluxer.app/gateway/event-filtering/
[fx-filter-presence]: https://docs.fluxer.app/gateway/event-filtering/#presence-subscriptions
[fx-msg-create]: https://docs.fluxer.app/gateway/events/#message-create
[fx-msg-delete]: https://docs.fluxer.app/gateway/events/#message-delete
[fx-reaction-add]: https://docs.fluxer.app/gateway/events/#message-reaction-add
[fx-reaction-remove]: https://docs.fluxer.app/gateway/events/#message-reaction-remove
[fx-reaction-emoji]: https://docs.fluxer.app/gateway/events/#reaction-emoji-object
[fx-voice-state]: https://docs.fluxer.app/gateway/events/#voice-state-object
[fx-presence-object]: https://docs.fluxer.app/gateway/events/#presence-object
[fx-cmd-presence]: https://docs.fluxer.app/gateway/commands/#presence-update
[fx-member-add]: https://docs.fluxer.app/gateway/events/#guild-member-add
[fx-member-update]: https://docs.fluxer.app/gateway/events/#guild-member-update
[fx-member-remove]: https://docs.fluxer.app/gateway/events/#guild-member-remove
[fx-ban-add]: https://docs.fluxer.app/gateway/events/#guild-ban-add
[fx-ban-remove]: https://docs.fluxer.app/gateway/events/#guild-ban-remove
[fx-threads]: https://docs.fluxer.app/gateway/threads/
[fx-threads-flag]: https://docs.fluxer.app/gateway/threads/#session-flag
[fx-thread-object]: https://docs.fluxer.app/http-api/threads/#thread-object
[fx-guild-ready]: https://docs.fluxer.app/gateway/events/#guild-ready-object
[fx-guild-create]: https://docs.fluxer.app/gateway/events/#guild-create
[fx-request-members]: https://docs.fluxer.app/gateway/commands/#request-guild-members
[fx-msg-object]: https://docs.fluxer.app/http-api/messages/#message-object
[fx-flags]: https://docs.fluxer.app/http-api/messages/#message-flags
[fx-perm-compute]: https://docs.fluxer.app/http-api/permissions/#permission-computation
[fx-add-role]: https://docs.fluxer.app/http-api/guild-members/#add-guild-member-role
[fx-remove-role]: https://docs.fluxer.app/http-api/guild-members/#remove-guild-member-role
[fx-list-roles]: https://docs.fluxer.app/http-api/permissions/#list-guild-roles
[fx-list-channels]: https://docs.fluxer.app/http-api/guild-channels/#list-guild-channels
[fx-list-members]: https://docs.fluxer.app/http-api/guild-members/#list-guild-members
[fx-oauth-scopes]: https://docs.fluxer.app/http-api/oauth2/#oauth2-scopes
[fx-oauth-code]: https://docs.fluxer.app/http-api/oauth2/#authorisation-code-grant
[fx-oauth-userinfo]: https://docs.fluxer.app/http-api/oauth2/#get-oauth2-user-information
[fx-oauth-authorize]: https://docs.fluxer.app/http-api/oauth2/#authorise-application
[fx-oauth-token]: https://docs.fluxer.app/http-api/oauth2/#exchange-oauth2-token

<!-- Discord developer docs -->
[dc-reaction-remove]: https://discord.com/developers/docs/events/gateway-events#message-reaction-remove
[dc-oauth]: https://discord.com/developers/docs/topics/oauth2#authorization-code-grant
[dc-users-me]: https://discord.com/developers/docs/resources/user#get-current-user
[dc-state]: https://docs.discord.com/developers/topics/oauth2#state-and-security
[dc-implicit]: https://docs.discord.com/developers/topics/oauth2#implicit-grant
