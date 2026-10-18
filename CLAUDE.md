# ApolloBot: agent guide

Personal, self-hosted bot that records server activity (Discord now, Fluxer later) for statistics and recaps.
.NET + Aspire, TimescaleDB, Blazor WASM + MudBlazor. One GitHub issue = one session = one PR.

## Before you start

1. Read the issue: `gh issue view <N> --comments`. Check that every issue under "Blocked by" is closed; if not, stop and say so.
2. Read [docs/scope.md](docs/scope.md) (source of truth: architecture, data model, decisions log §11) and [docs/coding-guidelines.md](docs/coding-guidelines.md) (how we work).
3. For platform work also read `docs/platform-parity.md` (created by #9).
4. If the issue is unclear or conflicts with scope.md, ask before writing code.

## Commands

Prerequisites: .NET SDK per `global.json`, Docker running (Aspire resources, Testcontainers).

```bash
dotnet build ApolloBot.slnx                          # must finish with 0 warnings (warnings are errors)
dotnet test                                          # all test projects (Microsoft.Testing.Platform, see global.json)
dotnet test --project tests/Apollo.Domain.Tests      # one test project
dotnet test -- --filter-method "*Some_Test_Name"     # filter by test method (also --filter-class)
dotnet test -- --coverage                            # with code coverage
dotnet format ApolloBot.slnx --verify-no-changes     # style check; run `dotnet format ApolloBot.slnx` to fix
dotnet run --project src/Apollo.AppHost              # local stack + Aspire dashboard
```

## Project map

| Project | Purpose |
|---|---|
| `src/Apollo.AppHost` | Aspire orchestration and compose publishing |
| `src/Apollo.ServiceDefaults` | OpenTelemetry, health checks, resilience (`AddServiceDefaults()`) |
| `src/Apollo.Domain` | Entities, value objects, pure domain logic (`Tracking/`: `TrackingRule`; `Messages/`: `MessageMetadata.From`, `MessageContentParts`; `Voice/`: `VoiceSessionStateMachine`, with the reconciliation rule `Reconcile` over a `VoiceBlindSpot`; `Presence/`: `PresenceSessionStateMachine`). No references |
| `src/Apollo.Platform.Abstractions` | `IChatPlatform`, `IPlatformConnectionStatus`, `PlatformRef`, neutral events (`Events/`), snapshots (`Snapshots/`), event dispatch (`Dispatch/`), command model and handler contracts (`Commands/`), event counters (`Diagnostics/RecordedEventMetrics`), login (`Identity/`: `IPlatformIdentityProvider`, with the configured `Guild` that decides web access) |
| `src/Apollo.Platform.Discord` | NetCord adapter: the only place NetCord types may appear |
| `src/Apollo.Platform.Fluxer` | Fluxer.Net adapter (stub for now): the only place Fluxer.Net types may appear |
| `src/Apollo.Data` | EF Core `DbContext`, migrations, TimescaleDB setup (CmdScale); `Identity/`: internal IDs with snapshot columns, platform ID mappings, `IPlatformIdResolver`; `Guilds/`: `guild_members`, `member_roles`, `GuildAccessReader` (web access: current member + Administrator flag); `Sessions/`: `user_sign_outs` (`UserSignOuts`, logout revocation); `Tracking/`: `role_settings` (a trigger writes `role_setting_changes`; removals take their author from `RoleSettingsStore`), `tracking_periods`, `RoleSettingsStore` (admin page writes), `RoleSettingsQuery` (roles with member counts, mode, bot facts, preview, history, tracked vs. untracked members); `Messages/`: `message_events`, `message_mentions`; `Voice/`: `voice_sessions`, `voice_state_segments`; `Reactions/`: `reaction_events`; `Membership/`: `membership_events`; `Presence/`: `presence_activity_sessions` |
| `src/Apollo.MigrationService` | Applies migrations on startup, then exits |
| `src/Apollo.Bot` | Worker: hosts the platform adapter, records events, routes commands (`Commands/`), German reply text (`Resources/`, `ReplyFormat`: de-CH numbers, Zurich dates); `GuildState/`: guild state sync and its change notifications (`IGuildStateNotificationHandler<T>`); `Status/`: 30 s heartbeat into `bot_status`, gateway health check; `Tracking/`: tracking gate (`ITrackingGate`; `Current` is one immutable `ITrackingView` for several answers that must agree; `ITrackingLifecycleHandler`), `TrackingHistory` (tracked at a past moment, from `tracking_periods`), `/apollo tracking on\|off\|status` (`AddTrackingCommands`, `TrackingRoleSwitch`, `IUserDataSummary`: rows per event table); `Stats/`: `/apollo stats` (`AddStatsCommands`, link from `LinksOptions.GetMePageUrl()`); `Privacy/`: `/apollo privacy delete` (`AddPrivacyCommands`, confirm button `PrivacyDeleteButtonHandler`, `IUserDataEraser`: gate mark (`TrackingGate.MarkUntrackedAsync`), one transaction over the `IUserDataEraserStep`s (`RecordedDataEraserStep`: every event table and `tracking_periods`), then the role removal); `Links/`: `LinksOptions` (`Apollo:Links:PublicWebUrl`, builds every website link); `Messages/`: `MessageRecorder`; `Voice/`: `VoiceSessionRecorder` (in-memory `VoiceSessionStore`, also the `IOpenVoiceSessionCounter`), `VoiceSessionLifecycleService` (startup take-over of the open rows, periodic reconciliation, `VoiceReconciliationOptions`: `Apollo:Voice:Reconciliation:MaxGap`); `Reconciliation/`: `ReconciliationOptions` (`Apollo:Reconciliation:Interval`, shared by the voice and presence runs), `PendingEvents` (members whose event is on its way); `Reactions/`: `ReactionRecorder`; `Membership/`: `MembershipRecorder`; `Presence/`: `PresenceSessionRecorder` (in-memory `PresenceSessionStore`, rows only on close), `PresenceSessionLifecycleService` (periodic reconciliation, shutdown close) |
| `src/Apollo.Stats` | Date-range stats queries and recap builders |
| `src/Apollo.Web` | ASP.NET Core host: API, auth (`Auth/`: BFF cookie session, `/auth/*`; `AccessRevalidator`: admin claim from the synced guild state, re-checked every `Apollo:Web:Auth:RevalidationInterval` (`WebAuthOptions`) by the cookie's `OnValidatePrincipal`, logout revocation), admin API (`Api/AdminEndpoints`: `/api/admin` group with the `Admin` policy, add admin endpoints with `MapAdminGroup()`; `Api/RoleSettingsEndpoints`: `/api/admin/settings`; `Api/BotStatusEndpoints`: `/api/admin/bot-status`), `Admin/BotPermissionCheck` (Manage Roles and role hierarchy of the bot), `Admin/BotStatusQuery` (latest heartbeat, stale after `Apollo:Web:BotStatus:StaleAfter` (`BotStatusOptions`), last connected heartbeat, 5 min latency mean, newest recorded row, open voice sessions, tracking mode, events per Zurich hour and table), `Api/DataEndpoints`: `/api/admin/data` (`Admin/DataOverviewQuery`: rows per event table and Zurich day, tracked members; `Admin/RawEventQuery`: raw event browser and filter lookups), `Admin/ConfiguredGuilds` (internal IDs of the configured guilds, for every admin query), `Admin/ZurichTime`, serves the WASM client |
| `src/Apollo.Web.Client` | Blazor WASM UI (German); `Auth/ApolloPolicies` (policies shared with the host), `Pages/Admin/` (every page there requires `Admin` via its `_Imports.razor`; `BotStatus` at `/admin` with `Status/EventsPerHourChart` (stacked by table), the open voice sessions table and the no-tracking-role banner, `IBotStatusClient`, 30 s `PeriodicTimer` refresh; `Settings` at `/admin/settings` with `RoleSettings/` components, `IRoleSettingsClient`; `Data` at `/admin/data` with `Events/` (`EventBrowser`, `EventsPerDayChart`, `DataEventText`), `IDataOverviewClient`), `Formatting/` (`UiTime`, `LocalizerExtensions.Ago`: "vor 12 s"), `Pages/NoAccess` ("Kein Zugriff", `/kein-zugriff`), `Pages/Privacy` (public "Datenschutz" at `/datenschutz`, `[AllowAnonymous]`; its text must match what the bot records) |
| `tests/Apollo.Testing` | Shared test helpers: `TimescaleFixture` (`CreateDbContext`, `CreateApplicationBuilder` for the runtime registration), `TestDataBuilder<,>`, `TestTime`; `Data/`: `GuildBuilder`, `GuildMemberBuilder`, `UserBuilder`, `ChannelBuilder`, `RoleBuilder`, `RoleSettingBuilder` (seed through `IPlatformIdResolver`); `Platform/`: `FakeChatPlatform`, `TestPlatform`, event and snapshot builders |
| `tests/Apollo.Domain.Tests` | Pure unit tests |
| `tests/Apollo.Platform.Abstractions.Tests` | `PlatformRef`, event contract rules, dispatcher, `FakeChatPlatform` self-tests |
| `tests/Apollo.Bot.Tests` | Recorders against `FakeChatPlatform` + Testcontainers DB |
| `tests/Apollo.Stats.Tests` | Stats queries against a seeded Testcontainers DB |
| `tests/Apollo.Platform.Discord.Tests` | NetCord payload → platform event mapping |
| `tests/Apollo.Web.Tests` | API endpoints and auth policies (`WebApplicationFactory`) |
| `tests/Apollo.Web.Client.Tests` | bUnit component tests |
| `tests/Apollo.IntegrationTests` | Few, slow Aspire smoke tests; AppHost model and data/migration tests |
| `tests/Apollo.ArchitectureTests` | Platform boundary: no NetCord/Fluxer types outside `Apollo.Platform.*` |

Packages are versioned centrally in `Directory.Packages.props`: never put `Version=` in a `.csproj`.
Test projects get xunit v3, Shouldly, NSubstitute and `Apollo.Testing` automatically via `tests/Directory.Build.props`.

## Writing a DB test

Derive from `TimescaleTest` (`Apollo.Testing.Data`): the class joins the `TimescaleCollection` (one container per test
assembly, migrations applied once) and the database is reset with Respawn before every test.

```csharp
public sealed class VoiceSessionRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task Record_WhenUserLeaves_ShouldPersistSession()
    {
        await using var db = Fixture.CreateDbContext();
        // ...
    }
}
```

- The reset deletes rows in schema `public` (not `__EFMigrationsHistory`). Continuous aggregates are not reset: refresh them after seeding.
- `Fixture.CreateDbContext()` builds a plain context. Code with explicit transactions must also be tested on the runtime
  registration: `Fixture.CreateApplicationBuilder()` returns a builder with `AddApolloDbContext()` applied; add your
  services to its `Services` (see `SyncHost`, `TrackingHost`).
- Need an unmigrated database? Use `[Collection<TimescaleCollection>]` and `fixture.NewDatabaseConnectionString()`.
- Test data: builders derived from `TestDataBuilder<TBuilder, T>`. Time: `TestTime.CreateProvider()` (fixed UTC "now", `Europe/Zurich` local zone).

## TDD loop (mandatory)

Details: [coding-guidelines §1](docs/coding-guidelines.md#1-workflow-test-driven-development).

1. **Red:** write one failing test for the next acceptance criterion. Run it and confirm it fails *for the expected reason* (an assertion, not a compile error).
2. **Green:** write the minimal code to pass. Run it.
3. **Refactor:** clean up while green; run the affected test project.
4. Repeat until every acceptance criterion has at least one test.

No production code without a failing test first. Exceptions: pure wiring (AppHost, `Program.cs`, DI registration), generated migrations, Razor markup without logic, config files. Bugs start with a reproducing test.

## Conventions (summary; the guidelines win)

- **C#:** file-scoped namespaces, one public type per file, `sealed` by default, `record` for events/DTOs/value objects, primary constructors for DI, async all the way with `CancellationToken` last. `.editorconfig` is enforced at build time.
- **Naming:** `PascalCase` types/members/constants, `_camelCase` private fields, `IInterface`. Tests: `Method_When_Condition_Should_Result`.
- **Time:** never `DateTime.Now`/`UtcNow`. Inject `TimeProvider` (tests: `FakeTimeProvider`). Store UTC (`timestamptz`); bucket by `Europe/Zurich`.
- **Platform boundary:** NetCord/Fluxer.Net types only in `Apollo.Platform.*`. Everything else uses neutral types and internal IDs.
- **Logging:** `ILogger<T>` with structured templates (`"Recorded {EventKind} for {UserId}"`), never string interpolation.
- **Data:** event tables are append-only. Message content is never stored. Untracked users are referenced as `NULL`.
- **Language:** UI text German (resource files), everything else English.
- **Mocking:** only at the edges we don't own (platform, OAuth, clock). Never mock `DbContext`; use Testcontainers.

## Configuration

- Typed options: `IOptions<T>` bound to a section, registered with `.ValidateDataAnnotations().ValidateOnStart()`. No magic config strings in code.
- Key naming: `Apollo:<Area>:<Key>` (e.g. `Apollo:Platform:Discord:GuildId`).
- Dev: non-secret values in `appsettings.Development.json`; secrets via `dotnet user-secrets` on the consuming project (or AppHost parameters).
- Prod: env vars `Apollo__<Area>__<Key>`, inlined into the Swarm stack file by `deploy/Render-Stack.ps1` (see [docs/deployment.md](docs/deployment.md)).
- Never commit secrets. `aspire-output/` is git-ignored.

## EF Core migrations

```bash
dotnet tool restore                                  # dotnet-ef pinned to the EF Core version (dotnet-tools.json)
dotnet ef migrations add <DescriptiveName> --project src/Apollo.Data --startup-project src/Apollo.MigrationService
```

- Works without a running AppHost (`ApolloDbContextFactory`). Generated files under `src/Apollo.Data/Migrations/` are excluded from style rules (`src/Apollo.Data/.editorconfig`).
- Event tables are hypertables: composite key `(Id, OccurredAt)`, `IsHypertable(x => x.OccurredAt)`, no unique index without `occurred_at` ([ADR 0001](docs/adr/0001-timescale-ef.md)).
- Services register the context with `builder.AddApolloDbContext()`. Only `Apollo.MigrationService` migrates; bot and web call `WaitForMigrationsAsync()` outside Development.
- The context has no retrying execution strategy (scope.md D17), so `BeginTransactionAsync` works; retry in code where needed.

- One migration per issue, with a descriptive name (`AddVoiceSessions`). Never edit a migration that has been merged.
- Timescale objects (hypertables, continuous aggregates, policies) via CmdScale fluent APIs. Raw SQL only with a comment explaining why.

## How to add a platform event

1. Neutral `sealed record` in `Apollo.Platform.Abstractions/Events`, deriving from `PlatformEvent(Guild, OccurredAt)`.
   Reference users, channels, roles and messages only as `PlatformRef`; reuse the shapes in `Snapshots/`
   (`PlatformMember`, `PlatformRole`, `PlatformChannel`, `PlatformVoiceState`, ...). References a platform may omit are
   nullable and resolved by the recorder, never the adapter. Message events carry `MessageMetadata`, never content.
   `PlatformEventContractTests` checks these rules for every event; add the event to its expected list.
2. Builder in `Apollo.Testing/Platform/Events`, derived from `PlatformEventBuilder<TBuilder, TEvent>`
   (defaults: `TestPlatform.Guild`, `TestTime.Now`).
3. Mapping from the NetCord event in `Apollo.Platform.Discord`, plus a mapping test in `Apollo.Platform.Discord.Tests`.
   The adapter delivers events with `IPlatformEventDispatcher.DispatchAsync`.
4. Handler in `Apollo.Bot`: implement `IPlatformEventHandler<TEvent>`, register it with
   `services.AddPlatformEventHandler<TEvent, THandler>()` (scoped, one DI scope per handler and event, so handlers never share a `DbContext`), apply the tracking gate
   (`ITrackingGate.IsTracked`; references to other users via `TryGetTrackedUserId`, which is `null` for untracked users).
   Test it against `FakeChatPlatform`: seed state with `With*`, deliver with `RaiseAsync`, assert `RoleChanges`.
   Turn every `PlatformRef` into an internal ID with `IPlatformIdResolver` (`Apollo.Data.Identity`): `Resolve*Async`
   creates the row on first sight; references that must not create data (e.g. untracked users) use `TryGet*Async`.
   After storing, call `RecordedEventMetrics.Recorded(event)`; an event the tracking gate drops counts as
   `Dropped(EventDropReason.Untracked)` (the adapter's guild filter already counts `OtherGuild`).
5. Entity + migration in `Apollo.Data` if it is persisted. A new event table also goes into `RecordedDataEraserStep`
   (privacy delete, #22) and `UserDataSummary` (`/apollo tracking status`).
6. Add or update the row in `docs/platform-parity.md`. If a platform cannot deliver it at all, add a flag to
   `PlatformCapabilities` (like `PresenceActivities`) instead of sending empty data.

## How to add a slash command

Neutral model in `Apollo.Platform.Abstractions/Commands` (#20); handlers never see NetCord types or interaction concepts.

1. Definition: `services.AddCommandDefinition(new CommandDefinition(ApolloCommand.Name, ApolloCommand.Description) { Groups = …, Subcommands = … })`.
   Each feature adds its own part of `apollo`; definitions with the same name are merged, and the first part gives the
   description, so every part uses the shared `ApolloCommand` constants (`Apollo.Bot/Commands`). Descriptions are German.
2. Handler in `Apollo.Bot`: implement `ICommandHandler` (its `Path`, e.g. `apollo tracking status`) and register it with
   `services.AddCommandHandler<THandler>()` (scoped). Return a `CommandReply`; it is private by default
   (`CommandReply.Private(text)`). The router adds the privacy footer and turns exceptions into a generic German error.
3. Buttons: `new ReplyButton(ButtonId.For(prefix, invocation.Member, argument).ToString(), label, style)` plus an
   `IButtonHandler` with that `IdPrefix` (`services.AddButtonHandler<THandler>()`). Only the member the button was
   issued to reaches the handler.
4. Reply text goes in `Apollo.Bot/Resources/Strings.resx` (`IStringLocalizer<Strings>`) and must read well as a DM too.
5. The bot refuses to start on duplicate paths or button prefixes and on a handler path that no definition declares.
   The Discord adapter defers on its own and answers mapping or routing failures with `ICommandRouter.CreateErrorReply()`.
6. Test against `FakeChatPlatform`: `.WithCommandRouter(router)`, then `InvokeCommandAsync` / `ClickButtonAsync`, and
   assert on the returned reply or `Replies`.

## How to add a typed API client (web)

Pages never call `HttpClient` directly ([coding-guidelines §8](docs/coding-guidelines.md#8-ui-conventions)). Sample: `IAppInfoClient` (#28).

1. DTO as a `record` in `Apollo.Web.Client/Contracts` (the host references it, so both sides share one type).
2. Endpoint in `Apollo.Web/Api/<Area>Endpoints.cs` (`Map<Area>Endpoints()`, under `/api`), plus a `WebApplicationFactory` test in `Apollo.Web.Tests`.
3. `I<Area>Client` and `Http<Area>Client` in `Apollo.Web.Client/Api`, `Mock<Area>Client` in `Api/Mock`.
4. Register both branches in `AddApolloApiClients` (`ApiClientServiceCollectionExtensions`); `Apollo:Web:Client:UseMockData` in the client's `wwwroot/appsettings*.json` picks the mock.
5. bUnit tests use the mocks by default (`ApolloTestContext`); `UseApiClients(false, …)` swaps in the HTTP client with a stub handler.

UI text goes in `Apollo.Web.Client/Resources/Strings.resx` and is read via `IStringLocalizer<Strings>` (`@inject IStringLocalizer<Strings> L`, then `@L["Key"]`). `StringsTests` fails on missing keys and on text literals in the shell files.

## Git workflow

- Branch off up-to-date `main`: `feature/<issue#>-<slug>`, `fix/<issue#>-<slug>`, `chore/<issue#>-<slug>`.
- Conventional Commits: `feat(bot): record voice sessions`, `test(stats): …`, `fix: …`, `chore: …`.
- One issue = one PR. If the work outgrows the issue's "In" scope, stop and propose a split instead of growing the PR.
- Never merge your own PR; the owner reviews every PR.

## PR checklist

Before opening the PR (the template in `.github/pull_request_template.md` asks for all of this):

- [ ] `dotnet build ApolloBot.slnx`: 0 warnings
- [ ] `dotnet test`: all green
- [ ] `dotnet format ApolloBot.slnx --verify-no-changes`: clean
- [ ] Description has `Closes #<N>`, a table mapping each acceptance criterion to its tests, the test-run summary, and any deviations from the issue with reasons
- [ ] docs/ updated if behaviour or config changed
