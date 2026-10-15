# ApolloBot — Coding Guidelines (Draft)

> Applies to humans and AI agents alike. `CLAUDE.md` links here; agents must read this before starting an issue.
> Scope and architecture live in [scope.md](scope.md). This document covers *how* we write code.

## 1. Workflow: test-driven development

Every behaviour change follows **red → green → refactor**:

1. **Red:** write one failing test that expresses the next acceptance criterion. Run it and confirm it fails *for the expected reason*, e.g. an assertion failure. A compile error or a missing DI registration doesn't count.
2. **Green:** write the minimal production code that makes it pass. Run the test.
3. **Refactor:** clean up production and test code while everything stays green. Run the full affected test project.
4. Repeat for the next criterion.

Rules:
- **No production code without a failing test first.** Exceptions: pure wiring (AppHost, `Program.cs`, DI registration), generated migrations, Razor markup without logic, and config files. Each of these is still covered indirectly by an integration test where practical.
- **Acceptance criteria drive the tests.** Each `- [ ]` in an issue maps to at least one test. Name tests so the link is obvious.
- **Bugs start with a reproducing test.** First the test that fails because of the bug, then the fix.
- **Agents show their work.** In the PR description, list the tests added per acceptance criterion and paste the final test-run summary.
- **Commit rhythm:** small commits are welcome, but every commit on `main` must be green. When a PR is squash-merged, the intermediate red commits don't matter.

## 2. Test stack

| Purpose | Tool |
|---|---|
| Test framework | **xUnit v3** on **Microsoft.Testing.Platform** (opted in via `global.json`; run with `dotnet test`) |
| Assertions | **Shouldly** (FluentAssertions is no longer free for commercial use; Shouldly keeps it simple) |
| Test doubles | **NSubstitute**, sparingly (see §4) |
| Time | `TimeProvider` everywhere; `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) in tests |
| Database | **Testcontainers.PostgreSql** with the same `timescale/timescaledb` image as production |
| Blazor components | **bUnit** |
| End-to-end / host | **Aspire.Hosting.Testing** (`DistributedApplicationTestingBuilder`) for a few smoke tests |
| Coverage | `Microsoft.Testing.Extensions.CodeCoverage` (`dotnet test -- --coverage`). No hard gate; check coverage in reviews |

## 3. Test layout & naming

```
tests/
  Apollo.Domain.Tests/           pure unit tests, no I/O
  Apollo.Bot.Tests/              recorder logic against FakeChatPlatform + in-memory/Testcontainers DB
  Apollo.Stats.Tests/            query layer against seeded Testcontainers DB
  Apollo.Platform.Discord.Tests/ mapping NetCord payloads → platform events
  Apollo.Web.Tests/              API endpoints, auth policies (WebApplicationFactory)
  Apollo.Web.Client.Tests/       bUnit component tests
  Apollo.IntegrationTests/       Aspire smoke tests (few, slow)
```

- One test class per unit under test: `VoiceSessionRecorderTests`.
- Test method naming: `Method_or_Behaviour_When_Condition_Should_Result` in plain English, e.g. `RecordMessage_WhenUserHasNoTrackedRole_ShouldNotPersistEvent`.
- Arrange / Act / Assert, separated by a blank line. No comments needed when the name says it.
- One logical assertion per test. Several `Should` calls on the same result are fine.
- Test data comes from **builders** (`new MessageEventBuilder().ByUser(alice).InChannel(general).Build()`) in a shared `Apollo.Testing` project, not copy-pasted object initialisers.

## 4. What to test where (and what not to mock)

| Layer | Approach |
|---|---|
| Domain logic | Pure unit tests. No mocks needed. |
| Platform adapters | Feed recorded or hand-built library payloads (NetCord types) into the mapper and assert on the neutral platform events. **No live Discord in tests.** |
| Bot recorders | Use **`FakeChatPlatform`** from `Apollo.Testing`, an in-memory implementation of `IChatPlatform` that can raise platform events and hold fake guild state (members, roles, voice states). Persist into a real DB through Testcontainers. |
| Data / EF | **Never mock `DbContext`.** Use Testcontainers. Share one container per test collection and reset with Respawn or a transaction per test. |
| Stats queries | Seed raw events, refresh the continuous aggregates explicitly (`CALL refresh_continuous_aggregate(...)`), then assert on the stats DTOs. Time zone edge cases (DST switch, midnight in Zurich vs UTC) must be tested. |
| Web API / auth | `WebApplicationFactory` with a test auth handler that issues claims. Every policy gets allowed and denied tests. |
| UI | bUnit for components with logic: formatting, empty states, conditional rendering. Visual layout is checked by eye, not by test. |

Mocking rule of thumb: **mock only at the edges we don't own** (platform, OAuth provider, clock). Within our own code, prefer real collaborators.

## 5. C# conventions

- **Latest C#, nullable enabled, `TreatWarningsAsErrors=true`** (set in `Directory.Build.props`).
- **Central Package Management** (`Directory.Packages.props`). No versions in `.csproj` files.
- `.editorconfig` is the source of truth for formatting. `dotnet format --verify-no-changes` must pass.
- File-scoped namespaces, one public type per file, file name = type name.
- Use `record` for platform events, DTOs and value objects; `sealed` classes by default.
- Primary constructors for DI. Use `required`/`init` for data shapes.
- Async all the way: `Async` suffix and `CancellationToken` as the last parameter on every async public method. Never `.Result` or `.Wait()`.
- Logging through `ILogger<T>` with structured templates (`"Recorded {EventKind} for {UserId}"`), never string interpolation.
- No magic strings for config. Use typed `IOptions<T>` classes with `ValidateDataAnnotations().ValidateOnStart()`.
- **Platform boundary:** NetCord and Fluxer.Net types appear **only** in `Apollo.Platform.*` projects. This is enforced by an architecture test (NetArchTest or a reflection-based test) in M0.
- No `DateTime.Now` / `DateTime.UtcNow`. Always inject `TimeProvider`.

## 6. Data & migrations

- EF Core migrations live in `Apollo.Data`. One migration per issue, with a descriptive name (`AddVoiceSessions`).
- Timescale objects are defined via CmdScale fluent APIs. Raw SQL goes in a migration only with a comment explaining why the package can't do it.
- Raw event tables are **append-only**. Updates are allowed only for closing sessions (`ended_at`) and privacy operations.
- All timestamps are `timestamptz` and stored as UTC. Bucketing uses the `Europe/Zurich` time zone in SQL.

## 7. Git & PR conventions

- **Branch per issue:** `feature/<issue#>-<slug>`, `fix/<issue#>-<slug>`, `chore/<issue#>-<slug>`.
- **Conventional Commits:** `feat(bot): record voice sessions`, `test(stats): …`, `fix: …`, `chore: …`.
- **PR description:**
  - `Closes #<issue>`
  - a mapping from acceptance criteria to tests
  - the test-run summary
  - any deviation from the issue, with reasons
- **One issue = one PR.** If an issue turns out bigger, stop and propose a split in the issue rather than growing the PR.
- **Before opening a PR:**
  - `dotnet build`: no warnings
  - `dotnet test`: all green
  - `dotnet format --verify-no-changes`

## 8. UI conventions

- All user-facing text is German and lives in resource files. Code, identifiers and comments are English.
- MudBlazor components first; custom CSS only through the theme or scoped `.razor.css`.
- Every page must render correctly at a width of 360px.
- Pages talk to the API through typed client services behind interfaces, so mock implementations can back the prototype and bUnit tests.
