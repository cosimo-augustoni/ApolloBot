# ADR 0001: TimescaleDB objects in EF Core (CmdScale) and Europe/Zurich daily buckets

- **Status:** Proposed (accepted when the owner merges the PR for #5)
- **Date:** 2026-10-15
- **Issue:** #5 · **Blocks:** #6, #34
- **Spike:** [`spikes/timescale-ef/`](../../spikes/timescale-ef) (not part of `ApolloBot.slnx`)

## Context

Raw events live in TimescaleDB hypertables. Stats are built on continuous aggregates bucketed per calendar day in
`Europe/Zurich`, with all timestamps stored in UTC ([scope §5.2, §5.3](../scope.md#52-storage-timescaledb), D3).
D10 says Timescale objects are defined with
[CmdScale.EntityFrameworkCore.TimescaleDB](https://github.com/cmdscale/CmdScale.EntityFrameworkCore.TimescaleDB)
and its Fluent API, with raw SQL only as a fallback. D7 says the EF Core version must match what CmdScale supports.

Scope §12 left two questions open:

1. Can CmdScale produce `time_bucket('1 day', ts, 'Europe/Zurich')` in a continuous aggregate?
2. Does CmdScale work with the latest EF Core and Npgsql EF provider, or does it force an older EF Core?

## Short answers

| Question | Answer |
|---|---|
| Time-zone bucket through CmdScale's **fluent** `IsContinuousAggregate(...)` | **No.** The structured generator always emits `time_bucket('<width>', "<column>")` with no time-zone argument, so `"1 day"` means a UTC day. |
| Time-zone bucket through CmdScale **at all** | **Yes**, through CmdScale's migration operation `migrationBuilder.CreateContinuousAggregate(..., viewDefinition: "<SELECT>")`. The SELECT is ours (raw SQL); CREATE, DROP, the refresh policy and the non-transactional execution stay with CmdScale. |
| Latest EF Core | **Yes for EF Core 10:** CmdScale 10.4.0 + EF Core 10.0.13 (latest stable) + Npgsql EF provider 10.0.3 work together. **No for EF Core 11** (rc.2 today): CmdScale has no 11.x build and the Npgsql 10.x provider caps EF Core at `[10.0.4, 11.0.0)`. No pinning below the latest 10.0.x is needed. |
| Refresh policy via fluent API | **Yes:** `.WithRefreshPolicy(...)` on the continuous aggregate builder. |
| Compression policy via fluent API | **Yes:** `.WithCompressionSegmentBy(...)` + `.WithCompressionPolicy(after: ...)` on the hypertable (emits the new columnstore SQL). |
| Retention policy via fluent API | **Yes:** `.WithRetentionPolicy(dropAfter: ...)` on the hypertable. |
| `CREATE EXTENSION timescaledb` | **Not handled by CmdScale.** Add `modelBuilder.HasPostgresExtension("timescaledb")`; Npgsql then emits `CREATE EXTENSION IF NOT EXISTS timescaledb;` in the first migration. |
| Primary key on hypertables | Every unique index, the primary key included, must contain the partition column. Convention below. |

## Options considered

| # | Option | Verdict |
|---|---|---|
| A | Fluent `IsContinuousAggregate<TAgg, TSource>(name, "1 day", x => x.OccurredAt)` + `AddAggregateFunction(...)` | **Rejected for daily aggregates.** Works end to end (migration, policy, LINQ), but buckets by UTC day. The spike proves it: `2026-01-14T23:01Z` (00:01 on 15 Jan in Zurich) lands in the 14 Jan bucket. |
| B | Bend the fluent API (raw `AddGroupByColumn("time_bucket(..., 'Europe/Zurich')")`, an offset, a custom width string) | **Not possible.** `ContinuousAggregateSqlGenerator` (v10.4.0) always selects `time_bucket(width, column)` and groups by ordinal `1`; raw GROUP BY expressions are not added to the SELECT list; the width is escaped as a string literal. A fixed offset could not follow DST anyway. The `ViewDefinition` model annotation exists, but it is only read by scaffolding, not by the migration differ. |
| C | Keyless entity mapped with `ToView(...)` + hand-written `migrationBuilder.CreateContinuousAggregate(viewDefinition: ...)` and `AddContinuousAggregatePolicy(...)` in the migration | **Chosen.** Raw SQL is limited to the SELECT. CmdScale still generates `CREATE MATERIALIZED VIEW ... WITH (timescaledb.continuous, ...)`, runs it outside a transaction, and owns the policy and the Down migration. |
| D | `migrationBuilder.Sql(...)` for the whole view and policy | Works too, but we would have to repeat what CmdScale already does (WITH options, `suppressTransaction: true`, policy and drop SQL). Option C is strictly less raw SQL. |
| E | Plain Postgres materialized views (scope §5.2 fallback) | Not needed; noted only. |

## Decision

1. **Hypertables, compression, retention and refresh policies use CmdScale's fluent API** (`IsHypertable(...).WithChunkTimeInterval(...)`,
   `WithCompressionSegmentBy`/`WithCompressionPolicy`, `WithRetentionPolicy`, `WithRefreshPolicy`). Migrations are generated with `dotnet ef`.
2. **Continuous aggregates bucketed by `Europe/Zurich` day are written by hand in the migration** with CmdScale's
   `CreateContinuousAggregate(..., viewDefinition: ...)` and `AddContinuousAggregatePolicy(...)`, and read through a keyless entity mapped with
   `ToView("<view name>")`. A comment in the migration says why (guidelines §6). This is the only raw SQL.
3. **Aggregates whose buckets do not depend on the local calendar day may use the fluent API**, e.g. a `"1 hour"` bucket (Zurich is always a whole-hour
   offset from UTC, so UTC hours and Zurich hours have the same boundaries). This is reasoning, not spike evidence; #34 proves it for `hourly_activity_heatmap`.
4. **EF Core stays on the latest 10.0.x** (10.0.13 today) with Npgsql EF provider 10.0.x and CmdScale 10.x. Moving to EF Core 11 waits for a CmdScale
   release built against it and for Npgsql EF provider 11 GA.
5. **Hypertable primary-key convention (all event tables):** the primary key is composite and includes the partition column, e.g.
   `HasKey(x => new { x.Id, x.OccurredAt })`. Every other unique index must include `occurred_at` as well. TimescaleDB rejects anything else
   (`cannot create a unique index without the column "occurred_at" (used in partitioning)`), and the spike test asserts that rejection.
6. **The extension is declared in the model:** `modelBuilder.HasPostgresExtension("timescaledb")`.

### The exact SQL and how it stays in sync

The spike migration
([`20261015194943_AddActivityEventsAndDailyAggregates.cs`](../../spikes/timescale-ef/Migrations/20261015194943_AddActivityEventsAndDailyAggregates.cs))
contains:

```csharp
// Hand-written: CmdScale's structured aggregate always emits time_bucket(width, column) with no time zone
// argument (ContinuousAggregateSqlGenerator, v10.4.0), which buckets by UTC day. viewDefinition is passed
// through verbatim, so CmdScale still owns CREATE/DROP and the policy. Aliases must match the column names
// configured for DailyActivityZurich in SpikeDbContext; the spike tests query every column through LINQ.
migrationBuilder.CreateContinuousAggregate(
    materializedViewName: "daily_activity_zurich",
    parentName: "activity_events",
    schema: "public",
    chunkInterval: "70 days",
    materializedOnly: true,
    viewDefinition: """
        SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
               channel_id,
               count(id) AS event_count
        FROM public.activity_events
        GROUP BY 1, channel_id
        """);

migrationBuilder.AddContinuousAggregatePolicy(
    materializedViewName: "daily_activity_zurich",
    schema: "public",
    startOffset: "3 days",
    endOffset: "1 hour",
    scheduleInterval: "1 hour");
```

and the matching `Down` calls `RemoveContinuousAggregatePolicy` and `DropContinuousAggregate`.

The keyless entity:

```csharp
modelBuilder.Entity<DailyActivityZurich>(entity =>
{
    entity.HasNoKey();
    entity.ToView("daily_activity_zurich");   // EF never creates or diffs it
    entity.Property(x => x.Day).HasColumnName("day");
    entity.Property(x => x.ChannelId).HasColumnName("channel_id");
    entity.Property(x => x.EventCount).HasColumnName("event_count");
});
```

Keeping the two in sync:

- The view column aliases are the entity's column names. A test that reads a whole row through LINQ (`SingleAsync(...)` on the keyless set)
  fails with `column ... does not exist` as soon as they drift. Every hand-written aggregate gets such a test.
- `Database.HasPendingModelChanges()` stays `false` because `ToView` entities are not part of migrations; a test asserts it.
- The model snapshot does not know the view. Changing the aggregate later means a new hand-written migration with `DropContinuousAggregate`
  + `CreateContinuousAggregate` (+ policy), never an edit of a merged migration.
- `Day` is a `timestamptz` holding the UTC instant of Zurich midnight (e.g. `2026-03-28T23:00Z` for 29 Mar). `Apollo.Stats` turns it into a
  Zurich `DateOnly` with `TimeZoneInfo` and builds range filters from Zurich-midnight instants. Do not cast it to `date` in SQL: that uses the
  session `TimeZone`.

## Consequences

- One small, commented raw-SQL SELECT per Zurich-day aggregate; everything else is typed and generated.
- CmdScale runs `CREATE MATERIALIZED VIEW ... WITH (timescaledb.continuous)` outside a transaction (TimescaleDB requires it). A migration that
  creates aggregates is therefore **not atomic**: if a later statement fails, the earlier transactions are already committed. Keep aggregate
  migrations small (#6 / #34).
- Generated migrations violate the repo's style rules (block namespaces, unused usings). The spike marks them as generated code in
  [`spikes/timescale-ef/.editorconfig`](../../spikes/timescale-ef/.editorconfig); `Apollo.Data` needs the same.
- CmdScale emits the new columnstore SQL (`timescaledb.enable_columnstore`, `add_columnstore_policy`) by default; `UseLegacyCompressionSql()` exists
  for older servers. Our pinned image supports the new names.
- `WithRetentionPolicy` also emits an `alter_job(...)` with defaults (`max_runtime 0`, `max_retries -1`, `retry_period 1 day`).
- Upgrading to EF Core 11 is blocked on CmdScale and Npgsql. CmdScale's release policy targets the current .NET **LTS**, so an EF Core 11 (STS) build
  is not guaranteed; we may stay on EF Core 10 until .NET 12. This refines D7.
- The global `dotnet-ef` tool on the spike machine is 10.0.7 and warns that it is older than the runtime (10.0.13); it still works.

## Evidence

### Versions proven together

| Component | Version | How it was verified |
|---|---|---|
| CmdScale.EntityFrameworkCore.TimescaleDB (+ `.Design`) | **10.4.0** (assembly `1.0.0+0c1f648944f67bbe47b2d390946713b1c2081c53`, the commit in the 10.4.0 nuspec) | `Versions_WhenRunning_ShouldBeTheOnesRecordedInTheAdr` |
| Microsoft.EntityFrameworkCore (+ Relational, Design) | **10.0.13** (latest stable on 2026-10-15; 11.0.0-rc.2 is the newest prerelease) | same test |
| Npgsql.EntityFrameworkCore.PostgreSQL | **10.0.3** (latest stable; depends on EF Core `[10.0.4, 11.0.0)`) | same test |
| Npgsql | 10.0.3 | same test |
| TimescaleDB image | **`timescale/timescaledb:2.30.2-pg18`** via `TimescaleImage.cs` (linked, not copied); extension 2.30.2, PostgreSQL 18.6 | `Migrate_WhenDatabaseDoesNotExist_ShouldCreateTimescaleExtension`, versions test |
| Testcontainers.PostgreSql | 4.16.0 | |
| .NET SDK / runtime | 10.0.401 / 10.0.x, xUnit v3 4.0.2 on Microsoft.Testing.Platform | |

Versions test output:

```
EF Core 10.0.13 | Npgsql EF provider 10.0.3+5e912bf071068a44e5abbdab497b313ba99d3ee1 | Npgsql 10.0.3+d3768398c17877b3a916c3c4d87e8e11698991fc | CmdScale 1.0.0+0c1f648944f67bbe47b2d390946713b1c2081c53 | PostgreSQL 18.6 on x86_64-pc-linux-musl, compiled by gcc (Alpine 15.2.0) 15.2.0, 64-bit
```

### Generated migration SQL (excerpt of `dotnet ef migrations script`)

```sql
START TRANSACTION;
CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE activity_events (
    id bigint GENERATED ALWAYS AS IDENTITY,
    occurred_at timestamp with time zone NOT NULL,
    channel_id bigint NOT NULL,
    CONSTRAINT "PK_activity_events" PRIMARY KEY (id, occurred_at)
);

SELECT create_hypertable('public."activity_events"', 'occurred_at', if_not_exists => true, chunk_time_interval => INTERVAL '7 days');
ALTER TABLE "public"."activity_events" SET (timescaledb.enable_columnstore = true, timescaledb.segmentby = '"channel_id"');
COMMIT;

-- Fluent IsContinuousAggregate: note the missing time-zone argument
CREATE MATERIALIZED VIEW "public"."daily_activity_utc"
WITH (timescaledb.continuous, timescaledb.materialized_only = true, timescaledb.chunk_interval = '70 days') AS
SELECT time_bucket('1 day', "occurred_at") AS "day", "channel_id", COUNT("id") AS "event_count"
FROM "public"."activity_events"
GROUP BY 1, "channel_id";
START TRANSACTION;
SELECT add_continuous_aggregate_policy('public."daily_activity_utc"', start_offset => INTERVAL '3 days', end_offset => INTERVAL '1 hour', schedule_interval => INTERVAL '1 hour');
COMMIT;

-- CreateContinuousAggregate(viewDefinition: ...)
CREATE MATERIALIZED VIEW "public"."daily_activity_zurich"
WITH (timescaledb.continuous, timescaledb.materialized_only = true, timescaledb.chunk_interval = '70 days') AS
SELECT time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich') AS day,
       channel_id,
       count(id) AS event_count
FROM public.activity_events
GROUP BY 1, channel_id;
START TRANSACTION;
SELECT add_continuous_aggregate_policy('public."daily_activity_zurich"', start_offset => INTERVAL '3 days', end_offset => INTERVAL '1 hour', schedule_interval => INTERVAL '1 hour');
CALL add_columnstore_policy('public."activity_events"', after => INTERVAL '30 days');
SELECT add_retention_policy('public."activity_events"', drop_after => INTERVAL '10 years');
SELECT alter_job(job_id, schedule_interval => INTERVAL '1 day', max_runtime => INTERVAL '00:00:00', max_retries => -1, retry_period => INTERVAL '1 day')
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_retention' AND hypertable_schema = 'public' AND hypertable_name = 'activity_events';
COMMIT;
```

### Bucket correctness (seed points, all UTC)

Each seed event has its own channel, so each case is one aggregate row. "Bucket" is the value of `day` read through LINQ.

| Seed (UTC) | Zurich local time | Expected Zurich day | Zurich bucket (`daily_activity_zurich`) | Fluent UTC bucket (`daily_activity_utc`) |
|---|---|---|---|---|
| 2026-01-14 22:59 | 14 Jan 23:59 CET | 14 Jan | 2026-01-13T23:00Z ✅ | 2026-01-14T00:00Z |
| 2026-01-14 23:01 | 15 Jan 00:01 CET | 15 Jan | 2026-01-14T23:00Z ✅ | 2026-01-14T00:00Z ❌ |
| 2026-03-28 23:30 | 29 Mar 00:30 CET (spring-forward day) | 29 Mar | 2026-03-28T23:00Z ✅ | |
| 2026-03-29 21:59 | 29 Mar 23:59 CEST | 29 Mar | 2026-03-28T23:00Z ✅ | |
| 2026-03-29 22:30 | 30 Mar 00:30 CEST | 30 Mar | 2026-03-29T22:00Z ✅ | 2026-03-29T00:00Z ❌ |
| 2026-10-24 22:30 | 25 Oct 00:30 CEST (fall-back day) | 25 Oct | 2026-10-24T22:00Z ✅ | |
| 2026-10-25 00:30 | 25 Oct 02:30 CEST (1st pass) | 25 Oct | 2026-10-24T22:00Z ✅ | |
| 2026-10-25 01:30 | 25 Oct 02:30 CET (2nd pass) | 25 Oct | 2026-10-24T22:00Z ✅ | |
| 2026-10-25 22:30 | 25 Oct 23:30 CET | 25 Oct | 2026-10-24T22:00Z ✅ | |
| 2026-10-25 23:00 | 26 Oct 00:00 CET | 26 Oct | 2026-10-25T23:00Z ✅ | |

The 29 Mar bucket spans 23 h (`28T23:00Z` → `29T22:00Z`) and holds 2 events; the 25 Oct bucket spans 25 h (`24T22:00Z` → `25T23:00Z`) and holds 4.
No bucket starts inside either day.

### Spike tests → issue "In" list

| "In" point | Test(s) in `TimeZoneBucketSpikeTests` |
|---|---|
| 1. Hypertable + chunk interval via generated migration | `Migrate_WhenDatabaseIsEmpty_ShouldCreateHypertableWithConfiguredChunkInterval` (7 days, from `timescaledb_information.dimensions`) |
| 2. Daily Zurich aggregate (fluent impossible → CmdScale `viewDefinition`) | `ZurichAggregate_ViewDefinition_ShouldUseTimeZoneAwareTimeBucket`, `FluentUtcAggregate_WhenRefreshed_ShouldBucketByUtcDayNotZurichDay` (3 cases) |
| 3. Refresh policy (fluent + migration op) and manual `CALL refresh_continuous_aggregate` | `RefreshPolicy_WhenMigrated_ShouldBeScheduledForAggregate` (both views), `ManualRefresh_WhenNewEventArrives_ShouldMaterializeItOnlyAfterRefresh` |
| 4. LINQ through a keyless entity | every `ZurichAggregate_*` / `FluentUtcAggregate_*` test; `Model_WhenAllMigrationsAreApplied_ShouldHaveNoPendingChanges` |
| 5. Zurich midnight and both DST switches | `ZurichAggregate_WhenRefreshed_ShouldCountEventInItsZurichCalendarDay` (10 cases), `ZurichAggregate_OnDstSwitchDay_ShouldCountTheWholeLocalDayInOneBucket` (2 cases) |
| 6. CmdScale / EF Core / Npgsql compatibility | `Versions_WhenRunning_ShouldBeTheOnesRecordedInTheAdr` |
| Check: compression, retention | `HypertablePolicy_WhenMigrated_ShouldBeScheduled` (`policy_compression`, `policy_retention`) |
| Check: `CREATE EXTENSION` | `Migrate_WhenDatabaseDoesNotExist_ShouldCreateTimescaleExtension` (EF creates `apollo_spike` itself, so the extension comes from the migration, not the image's init script) |
| Check: primary-key rule | `Hypertable_WhenUniqueIndexLacksPartitionColumn_ShouldRejectIt` |

### Test run (2026-10-15, Windows 11, Docker Desktop 29.8.0)

```
dotnet test --project spikes/timescale-ef
TimescaleEfSpike.dll (net10.0|x64) erfolgreich (5s 884ms)
  gesamt: 26
  fehlgeschlagen: 0
  erfolgreich: 26
  übersprungen: 0
  Dauer: 6s 154ms
```

### Re-running the spike

Requires Docker and the .NET SDK from `global.json`. From the repository root:

```bash
dotnet test --project spikes/timescale-ef
```

Add `--output detailed` to list every test, or `-- --filter-method "*Versions*" --show-live-output on` to see the versions line.
If Docker Hub rate-limits the image pull (`toomanyrequests`), pull it from the mirror and retag it:

```bash
docker pull mirror.gcr.io/timescale/timescaledb:2.30.2-pg18
docker tag mirror.gcr.io/timescale/timescaledb:2.30.2-pg18 timescale/timescaledb:2.30.2-pg18
```

To regenerate the migration SQL: `dotnet ef migrations script --project spikes/timescale-ef`.

## Follow-ups

### #6 (DbContext + MigrationService)

- Reference `CmdScale.EntityFrameworkCore.TimescaleDB` and `.Design` 10.4.0, and lift EF Core to the latest 10.0.x explicitly
  (`Microsoft.EntityFrameworkCore`, `.Relational`, `.Design`); the versions are already in `Directory.Packages.props`.
- `options.UseNpgsql(...).UseTimescaleDb()` and `modelBuilder.HasPostgresExtension("timescaledb")` in `ApolloDbContext`.
- Hypertable convention for every event table: composite PK `(Id, OccurredAt)`, `IsHypertable(x => x.OccurredAt).WithChunkTimeInterval(...)`,
  no unique index without `occurred_at`.
- Add `[Migrations/*.cs] generated_code = true` to an `.editorconfig` in `src/Apollo.Data` (or the generated migrations break the build).
- Pin `dotnet-ef` to the runtime version with a local tool manifest (`dotnet new tool-manifest`, `dotnet tool install dotnet-ef --version 10.0.13`).
- The MigrationService must not wrap migrations in its own transaction: continuous-aggregate statements run outside one.
- Test helper (#7): a "migrate, seed, `CALL refresh_continuous_aggregate(...)`, query" fixture like `TimescaleFixture` in the spike.

### #34 (real continuous aggregates)

- `daily_user_channel_activity` and `daily_guild_activity`: hand-written `CreateContinuousAggregate(..., viewDefinition: ...)` with
  `time_bucket(INTERVAL '1 day', occurred_at, 'Europe/Zurich')`, plus `AddContinuousAggregatePolicy(...)`; keyless entities with `ToView(...)`;
  the comment from the spike migration; one LINQ round-trip test per aggregate that reads every column.
- `hourly_activity_heatmap`: try the fluent `IsContinuousAggregate(..., "1 hour", ...)` first (UTC hours line up with Zurich hours); derive weekday
  and hour in Zurich at query time; prove it with tests on both DST days.
- Decide `materialized_only` (the spike uses `true`, so only refreshed data is visible) and the refresh-policy offsets; `start_offset` must cover
  at least one full Zurich day plus late events.
- If a daily aggregate is built on top of an hourly one (hierarchical), check TimescaleDB's rules for time-zone buckets on hierarchical
  aggregates first; the spike did not test this.
- Reuse the spike's seed points (Zurich midnight, both DST switches, the repeated 02:30 hour) in the stats tests.
