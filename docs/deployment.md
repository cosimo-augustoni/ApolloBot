# Deployment

Apollo runs as a Docker Swarm stack behind the existing reverse proxy. There is no CI/CD ([scope §3](scope.md#3-non-goals-for-now)): the owner builds, pushes and deploys from their own machine.

The flow has three commands, and the stack file needs no hand editing:

1. `aspire do prepare-stack` builds the images, pushes them to the registry and writes the compose file plus an env file with the values.
2. `deploy/Render-Stack.ps1` inlines those values into `src/Apollo.AppHost/aspire-output/apollo.stack.yaml`, because Swarm does not read `.env` files.
3. `docker stack deploy` deploys it.

## Prerequisites

- **Docker**, logged in to the registry: `docker login registry.kallisto.li`.
- **Aspire CLI with the same version as the AppHost SDK** (`src/Apollo.AppHost/Apollo.AppHost.csproj`, currently 13.6.1). Check it with `aspire --version` and run `aspire update --self` if it is older. An older CLI can't talk to a newer AppHost: it writes the files and then hangs with `Run completed without returning a backchannel`.
- **PowerShell 7** (`pwsh`) for the render script.
- **The reverse-proxy network** exists in the Swarm as an attachable overlay network. Its name is the value of `Apollo:Deploy:ReverseProxyNetwork` (see below).
- **The bind-mount directories** exist on the Swarm node that runs the stack (`PostgresDataPath`, `PgAdminDataPath`).

## Values to provide

All values are read by the AppHost. Secrets are listed by name only and are never committed.

| Name | Kind | What it is | Where it comes from |
|---|---|---|---|
| `REGISTRY_ENDPOINT` | config | Registry host, `registry.kallisto.li` | env var or AppHost user-secret |
| `REGISTRY_REPOSITORY` | config | Repository below the registry, e.g. `apollo` | env var or AppHost user-secret |
| `Apollo:Deploy:ReverseProxyNetwork` | config | Name of the external reverse-proxy overlay network | `src/Apollo.AppHost/appsettings.json` sets `apollo_bot` (the reverse proxy has one attachable network per service). Override it with an AppHost user-secret or the env var `Apollo__Deploy__ReverseProxyNetwork` |
| `db-password` | **secret** parameter | Postgres password | AppHost user-secret `Parameters:db-password`, env var `Parameters__db-password`, or the CLI prompt. If none is set, Aspire generates one |
| `PostgresDataPath` | parameter | Host directory for the database (mounted at `/var/lib/postgresql`) | AppHost user-secret `Parameters:PostgresDataPath`, env var, or prompt |
| `PgAdminEmail` | parameter | pgAdmin login e-mail | as above |
| `PgAdminPassword` | **secret** parameter | pgAdmin login password | as above |
| `PgAdminDataPath` | parameter | Host directory for pgAdmin (mounted at `/var/lib/pgadmin`) | as above |
| `PgAdminPort` | parameter | Host port that publishes pgAdmin | as above |

Set AppHost user-secrets with, for example:

```bash
dotnet user-secrets set "Parameters:PgAdminPassword" "<value>" --project src/Apollo.AppHost
```

Parameters that are still missing are prompted for by the Aspire CLI. **Aspire saves every parameter value, secrets included, in its deployment state** (`~/.aspire/deployments/<hash>/production.json`) and reuses those values on the next run, even over changed user-secrets or env vars. To change a value, delete that state file, or use `aspire deploy --clear-cache` (which `aspire do` does not offer in 13.6).

Discord and OAuth values for the bot and web are added in #25.

Service settings with a default (not AppHost parameters):

| Name | Default | What it is |
|---|---|---|
| `Apollo:Data:MigrationWaitTimeout` (`Apollo__Data__MigrationWaitTimeout`) | `00:05:00` | How long `bot` and `apollo-web` wait at startup for pending migrations before they exit (see below). Validated at startup: 1 second to 1 day |

## Steps

From the repository root:

```bash
aspire do prepare-stack --apphost src/Apollo.AppHost/Apollo.AppHost.csproj
```

This builds `bot`, `apollo-web` and `migrationservice`, pushes them to `<REGISTRY_ENDPOINT>/<REGISTRY_REPOSITORY>/<service>:aspire-deploy-<timestamp>`, and writes into `src/Apollo.AppHost/aspire-output/` (Aspire's default output folder next to the AppHost; `-o <dir>` changes it, then pass the same folder to the render script with `-OutputPath`):

- `docker-compose.yaml` with `${NAME}` placeholders
- `.env` as an empty template
- `.env.Production` with the values, including the pushed image tags and the secrets

Push and env file are written in the same run, so the tags always match. `prepare-stack` is an AppHost pipeline step that depends on Aspire's `push` and `prepare-docker-compose` steps.

Don't use `aspire deploy` here: in Aspire 13.6 it ends with `docker compose up` on the local machine. `aspire publish` alone writes only the empty `.env` template.

```bash
pwsh deploy/Render-Stack.ps1
```

This replaces every `${NAME}` / `${NAME:-default}` in `docker-compose.yaml` with the value from `.env.Production` (or `.env` if there is no environment-specific file) and writes `src/Apollo.AppHost/aspire-output/apollo.stack.yaml`. If any placeholder has no value, it names the variables, writes nothing and exits with 1. It never prints values. `-Environment <name>` picks `.env.<name>` and `-OutputPath <dir>` picks another output directory.

```bash
docker stack deploy --with-registry-auth -c src/Apollo.AppHost/aspire-output/apollo.stack.yaml apollo
```

Run it against the Swarm manager, for example through a Docker context. `--with-registry-auth` passes your `docker login` credentials to the nodes. Without it, a node other than the one you logged in on cannot pull the private `registry.kallisto.li` images, and Swarm rejects the task with "No such image". The public images (`timescale/timescaledb`, `dpage/pgadmin4`) come from Docker Hub: if a task is rejected with "No such image" for one of them, run `docker pull <image>` on that node to see the real error. Anonymous Docker Hub pulls are rate-limited (`toomanyrequests`); `docker login` with a Docker Hub account on the manager plus `--with-registry-auth` avoids that. To check the file first: `docker stack config -c src/Apollo.AppHost/aspire-output/apollo.stack.yaml`.

`aspire-output/` folders are git-ignored wherever they are. `.env.Production` and `apollo.stack.yaml` contain secrets, so never commit or share them.

## What the stack looks like

| Service | Networks | Notes |
|---|---|---|
| `db` | `apollo_bot_internal` | TimescaleDB (pinned tag, `TimescaleImage`), bind mount `PostgresDataPath` → `/var/lib/postgresql`. Postgres 18+ refuses a mount at `/var/lib/postgresql/data` |
| `migrationservice` | `apollo_bot_internal` | `deploy.restart_policy`: `condition: on-failure`, `delay: 10s`, `max_attempts: 10`. It applies the EF migrations, then exits with 0 (or 1 on failure). Exit 0 is never restarted, so it runs once per deploy. While the database is unreachable it retries itself (10 attempts, 3 s apart, on top of the EF retry strategy). If `db` takes longer than that, e.g. on a first deploy while the TimescaleDB image is still being pulled or initdb is running, Swarm starts it again (up to 10 more runs, 10 s apart) |
| `bot` | `apollo_bot_internal` | Swarm's default restart policy, so it restarts on crash |
| `apollo-web` | `apollo_bot_internal`, reverse-proxy network (`apollo_bot`) | Listens on port 80 (`HTTP_PORTS=80`), no published host port. The reverse proxy targets **`apollo-web:80`** |
| `pgadmin` | `apollo_bot_internal` | `dpage/pgadmin4` with a pinned tag (`PgAdminImage`), host port `PgAdminPort` → 80, bind mount `PgAdminDataPath` → `/var/lib/pgadmin`. Production only: in development `WithPgAdmin()` adds it |

There is no Aspire dashboard, no `expose:` section and no `depends_on` in the stack. Swarm ignores `depends_on`, and its stack schema rejects the long form Aspire emits. Bot and web wait for the migrations themselves instead of relying on start order: outside Development they call `WaitForMigrationsAsync()` (`src/Apollo.Data/MigrationExtensions.cs`) before starting, which polls every 2 s until no migration is pending. Errors while the database is unreachable or not created yet count as "still waiting". After `Apollo:Data:MigrationWaitTimeout` they exit with a `TimeoutException`, and Swarm restarts them, so they keep waiting until the migration service has run. In local development the AppHost orders them instead (`WaitForCompletion(migrationservice)`).

The service name `apollo-web` is its DNS alias on the shared reverse-proxy network, so it has to be unique there. That is why the resource isn't called `web`.

## Day-to-day operations

**Update a single service:** run `aspire do prepare-stack` and the render script again, then deploy the whole stack again with the same `docker stack deploy` command. Swarm only restarts services whose definition changed. Every build gets a new image tag, so the services you rebuilt are updated. To roll one service by hand instead, use `docker service update --image <registry>/<repo>/<service>:<tag> apollo_<service>`.

**Migration-service logs:**

```bash
docker service logs apollo_migrationservice
docker service ps apollo_migrationservice
```

The latest task should show `Complete`; earlier `Failed` tasks are restarts after a non-zero exit (`on-failure`). A migration that fails on every attempt (a broken migration, wrong credentials) ends `Failed` after 10 restarts and stays so until the next deploy; the log has the exception under `crit: Apollo.MigrationService.MigrationWorker`. Meanwhile `bot` and `apollo-web` keep waiting and restarting (see above).

**Rerun migrations** without other changes: `docker service update --force apollo_migrationservice`.

## Why no hand editing (topg comparison)

For topg, the generated compose had to be edited by hand before Swarm accepted it. This is how Apollo handles each of those edits:

| # | Change made by hand in topg | Why | How Apollo handles it |
|---|---|---|---|
| 1 | All `${VAR}` placeholders replaced with literal values (passwords, connection strings, `HTTP_PORTS`, image names, bind-mount path) | `docker stack deploy` does not read `.env` files | `deploy/Render-Stack.ps1` resolves placeholders from the generated env file into a deployable stack file |
| 2 | `migrationservice` got `deploy.restart_policy.condition: none` | Swarm restarts exited containers forever, and the migration service exits on purpose | AppHost sets `Deploy.RestartPolicy` to `on-failure` (10 s delay, max 10 attempts) instead of `none`: a successful exit is still never restarted, but a run that gave up because `db` was not ready yet (first deploy: image pull, initdb) is retried instead of staying failed and leaving bot and web crash-looping |
| 3 | Network list `"topg_internal, topg"` split into two entries | Bug in topg's AppHost (one string instead of two) | AppHost lists the networks separately |
| 4 | `web` renamed to `topg_web` | The service name is its DNS alias on the shared reverse-proxy network and must be unique there. The proxy targets that name | AppHost names the resource `apollo-web` |
| 5 | `expose:` entries removed (`db`, `web`) | Ignored by Swarm, just noise | AppHost clears `Expose` |
| 6 | `pgadmin` service added (fixed image tag, host port, bind-mounted data dir, credentials) | pgAdmin is only added in dev by `WithPgAdmin`, but is wanted in production too | AppHost adds a pgAdmin container in publish mode, configured by parameters |
| 7 | Image references set to `registry.kallisto.li/topg/<service>:aspire-deploy-<timestamp>` | Swarm needs the pushed image tags | `aspire do prepare-stack` writes them to the env file, and the render script inlines them (#1) |

Apollo found two more:

| # | Problem | How Apollo handles it |
|---|---|---|
| 8 | `depends_on` in long form (`condition: service_started`) fails the Swarm stack schema (`depends_on must be a list`) | AppHost clears `DependsOn`. Ordering is handled by #6 |
| 9 | `WithExternalHttpEndpoints()` publishes host ports for `apollo-web` (`"80"`, `"${APOLLO_WEB_PORT}"`) | AppHost clears `Ports`. Only the reverse proxy reaches the web service |

## Tests

- `tests/Apollo.IntegrationTests/ComposePublishTests.cs` runs the AppHost in publish mode with placeholder values and asserts the compose shape above: networks, restart policy, no `expose`/`depends_on`/dashboard, bind mounts and pgAdmin.
- `tests/Apollo.IntegrationTests/RenderStackScriptTests.cs` runs `Render-Stack.ps1` with `pwsh`: full substitution, `:-default`, missing or empty values, escaping, and no values in the output.
- `tests/Apollo.IntegrationTests/AppHostTests.cs` starts the AppHost and checks that `migrationservice` finishes with exit code 0 before `bot` and `apollo-web` start. `tests/Apollo.IntegrationTests/Data/` covers the migrator's retries and `WaitForMigrationsAsync` against a TimescaleDB container.
