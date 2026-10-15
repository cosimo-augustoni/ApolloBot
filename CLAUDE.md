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
| `src/Apollo.Domain` | Entities, value objects, pure domain logic. No references |
| `src/Apollo.Platform.Abstractions` | `IChatPlatform`, neutral platform events, `PlatformRef` |
| `src/Apollo.Platform.Discord` | NetCord adapter: the only place NetCord types may appear |
| `src/Apollo.Platform.Fluxer` | Fluxer.Net adapter (stub for now): the only place Fluxer.Net types may appear |
| `src/Apollo.Data` | EF Core `DbContext`, migrations, TimescaleDB setup (CmdScale) |
| `src/Apollo.MigrationService` | Applies migrations on startup, then exits |
| `src/Apollo.Bot` | Worker: hosts the platform adapter, records events |
| `src/Apollo.Stats` | Date-range stats queries and recap builders |
| `src/Apollo.Web` | ASP.NET Core host: API, auth, serves the WASM client |
| `src/Apollo.Web.Client` | Blazor WASM UI (German) |
| `tests/Apollo.Testing` | Shared test helpers: fixtures, builders, `FakeChatPlatform` |
| `tests/Apollo.Domain.Tests` | Pure unit tests |
| `tests/Apollo.Bot.Tests` | Recorders against `FakeChatPlatform` + Testcontainers DB |
| `tests/Apollo.Stats.Tests` | Stats queries against a seeded Testcontainers DB |
| `tests/Apollo.Platform.Discord.Tests` | NetCord payload → platform event mapping |
| `tests/Apollo.Web.Tests` | API endpoints and auth policies (`WebApplicationFactory`) |
| `tests/Apollo.Web.Client.Tests` | bUnit component tests |
| `tests/Apollo.IntegrationTests` | Few, slow Aspire smoke tests |

Packages are versioned centrally in `Directory.Packages.props`: never put `Version=` in a `.csproj`.
Test projects get xunit v3, Shouldly, NSubstitute and `Apollo.Testing` automatically via `tests/Directory.Build.props`.

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
- Prod: env vars `Apollo__<Area>__<Key>`, inlined into the Swarm stack file (see `docs/deployment.md` once #8 lands).
- Never commit secrets. `aspire-output/` is git-ignored.

## EF Core migrations

```bash
dotnet ef migrations add <DescriptiveName> --project src/Apollo.Data --startup-project src/Apollo.MigrationService
```

- One migration per issue, with a descriptive name (`AddVoiceSessions`). Never edit a migration that has been merged.
- Timescale objects (hypertables, continuous aggregates, policies) via CmdScale fluent APIs. Raw SQL only with a comment explaining why.

## How to add a platform event (refine once #10 lands)

1. Neutral record in `Apollo.Platform.Abstractions` (e.g. `PlatformMessageCreated`), no platform types.
2. Mapping from the NetCord event in `Apollo.Platform.Discord`, plus a mapping test in `Apollo.Platform.Discord.Tests`.
3. Handler/recorder in `Apollo.Bot`, applying the tracking gate, with tests against `FakeChatPlatform`.
4. Entity + migration in `Apollo.Data` if it is persisted.
5. Add or update the row in `docs/platform-parity.md`.

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
