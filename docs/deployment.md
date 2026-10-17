# Deployment

Apollo runs as a Docker Swarm stack behind the existing reverse proxy. There is no CI/CD ([scope §3](scope.md#3-non-goals-for-now)): the owner builds, pushes and deploys from their own machine.

The flow has three commands, and the stack file needs no hand editing:

1. `aspire do prepare-stack` builds the images, pushes them to the registry and writes the compose file plus an env file with the values.
2. `deploy/Render-Stack.ps1` inlines those values into `src/Apollo.AppHost/aspire-output/apollo.stack.yaml`, because Swarm does not read `.env` files.
3. `docker stack deploy` deploys it.

For a first deployment, work through the sections in order: [Prerequisites](#prerequisites),
[Discord application](#discord-application), [Values to provide](#values-to-provide),
[Prepare the storage](#prepare-the-storage), [Steps](#steps), [Verify the deployment](#verify-the-deployment).
Later deploys need only [Steps](#steps) and the checks in [Verify the deployment](#verify-the-deployment).

## Prerequisites

- **A clone of the repository** at the commit you want to deploy, and the **.NET SDK** from `global.json` (the AppHost
  builds the images from source). Run `dotnet test` first: deploy only a green build.
- **Docker**, logged in to the registry: `docker login registry.kallisto.li`.
- **Aspire CLI with the same version as the AppHost SDK** (`src/Apollo.AppHost/Apollo.AppHost.csproj`, currently 13.6.1). Check it with `aspire --version` and run `aspire update --self` if it is older. An older CLI can't talk to a newer AppHost: it writes the files and then hangs with `Run completed without returning a backchannel`.
- **PowerShell 7** (`pwsh`) for the render script.
- **The reverse-proxy network** exists in the Swarm as an attachable overlay network. Its name is the value of `Apollo:Deploy:ReverseProxyNetwork` (see below).
- **The bind-mount directories** (`PostgresDataPath`, `PgAdminDataPath`) exist on every Swarm node that may run `db`
  and `pgadmin`, with the right owners (see [Prepare the storage](#prepare-the-storage)).

## Discord application

One Discord application serves both the bot and the web login. Set it up once in the
[developer portal](https://discord.com/developers/applications); none of it is in code.

1. **Bot page: privileged intents.** Under *Privileged Gateway Intents*, switch on all three:
   - **Presence Intent**: game and activity sessions (`presence_activity_sessions`);
   - **Server Members Intent**: the member list for the guild state sync and the tracking gate, plus joins and leaves;
   - **Message Content Intent**: without it Discord sends empty message content, so length, words, links and emoji
     can't be computed ([scope §5.1](scope.md#51-what-we-record-raw-events-append-only)). The content is never stored.

   The bot connects with these intents (`src/Apollo.Platform.Discord/DiscordGatewayIntents.cs`). If one of them is
   off, Discord closes the gateway connection right after the bot connects: the bot logs a `Discord gateway Disconnected`
   warning with close status 4014 (disallowed intents), and `bot_status` shows `connected = false`. An application in
   fewer than 100 servers needs no verification for them.
2. **Bot page: token.** *Reset Token* shows the bot token once. It goes into the `DiscordBotToken` secret parameter
   ([Values to provide](#values-to-provide)). Don't paste it anywhere else: not into an issue, a PR, a chat or a file in
   the repository. If it leaks, reset it here, set the new value and delete Aspire's deployment state before you deploy
   again: Aspire otherwise reuses the old token it cached ([Values to provide](#values-to-provide)).
3. **OAuth2 page: client ID, client secret and redirect.** The *Client ID* (it is also the application ID) goes into
   `DiscordOAuthClientId`, the *Client Secret* into `DiscordOAuthClientSecret`. Under *Redirects*, add
   `<PublicWebUrl>/signin-discord` exactly as written (see [Web login](#web-login-discord-oauth)).
4. **Invite the bot** to the guild with this URL, filling in the application ID and the guild ID:

   ```text
   https://discord.com/oauth2/authorize?client_id=<application id>&scope=bot+applications.commands&permissions=268502144&guild_id=<guild id>&disable_guild_select=true
   ```

   Scopes: `bot` (the bot user joins the guild) and `applications.commands` (the bot may register its `/apollo`
   commands). `permissions=268502144` is the sum of:

   | Permission | Value | Why |
   |---|---|---|
   | View Channels | 1024 | Discord only sends message, reaction and voice events for channels the bot can see |
   | Read Message History | 65536 | Not needed for live message events; part of the invite so a later history backfill (M6, [scope §9](scope.md#9-milestones)) needs no new invite |
   | View Audit Log | 128 | Discord sends ban and unban events only to bots with *View Audit Log* or *Ban Members* (`membership_events`, [platform parity](platform-parity.md)). *View Audit Log* is the read-only one of the two |
   | Manage Roles | 268435456 | `/apollo tracking on\|off` and `/apollo privacy delete` add and remove the tracking role (#21, #22) |

   The bot sends no channel messages: its command replies are interaction responses, which need no permission. The
   guild ID is the server's ID (*Copy Server ID* with developer mode on in Discord's settings); it also goes into
   `DiscordGuildId`. Channels with permission overwrites that hide them from the bot's role are not recorded.
5. **Role hierarchy.** Discord lets a bot add or remove only roles below its own highest role. In *Server Settings →
   Roles*, drag the bot's role (named after the application, created by the invite) **above the tracking role**. Until
   the admin UI exists (M2) there is no tracking role yet, and the bot tracks Administrators
   ([scope §5.1.1](scope.md#511-who-is-tracked-the-tracking-role)); when you create the tracking role later, keep it
   below the bot's role. If the order is wrong, `/apollo tracking on|off` answers with an error and the bot logs the
   refused role change.

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
| `PublicWebUrl` | parameter | Public address of the website, the reverse-proxy URL in front of `apollo-web`, e.g. `https://apollo.example.ch`. Passed to `bot` as `Apollo__Links__PublicWebUrl`; the bot builds its links from it (privacy page `/datenschutz` in every command reply, the member page `/me` in the `/apollo stats` reply). Aspire only knows the internal address `http://apollo-web:80`, so the public one has to be given here | as above |
| `DiscordOAuthClientId` | parameter | Client ID of the Discord application used for the web login; becomes `Apollo__Platform__Discord__OAuth__ClientId` on `apollo-web` | as above |
| `DiscordOAuthClientSecret` | **secret** parameter | Its client secret; becomes `Apollo__Platform__Discord__OAuth__ClientSecret` on `apollo-web` | as above |
| `DiscordBotToken` | **secret** parameter | The bot token ([Discord application](#discord-application), step 2); becomes `Apollo__Platform__Discord__Token` on `bot` | as above |
| `DiscordGuildId` | parameter | ID of the one guild the bot records (step 4); becomes `Apollo__Platform__Discord__GuildId` on `bot` | as above |

Set AppHost user-secrets with, for example:

```bash
dotnet user-secrets set "Parameters:PgAdminPassword" "<value>" --project src/Apollo.AppHost
dotnet user-secrets set "Parameters:DiscordBotToken" "<token>" --project src/Apollo.AppHost
```

Type secrets only into the user-secrets command or the CLI prompt; they end up in the git-ignored files below and in
Aspire's deployment state, never in the repository.

Parameters that are still missing are prompted for by the Aspire CLI. **Aspire saves every parameter value, secrets included, in its deployment state** (`~/.aspire/deployments/<hash>/production.json`) and reuses those values on the next run, even over changed user-secrets or env vars. To change a value, delete that state file, or use `aspire deploy --clear-cache` (which `aspire do` does not offer in 13.6).

Bot settings (`src/Apollo.Bot`, read by the Discord adapter, `src/Apollo.Platform.Discord/DiscordPlatformOptions.cs`).
They are validated at startup, and a missing value stops the bot with an error that names the key:

| Name | Kind | Default | What it is |
|---|---|---|---|
| `Apollo:Platform:Discord:Token` (`Apollo__Platform__Discord__Token`) | **secret** | none, required | The bot token from the Discord developer portal. Published stack: the `DiscordBotToken` secret parameter |
| `Apollo:Platform:Discord:GuildId` (`Apollo__Platform__Discord__GuildId`) | config | none, required | The one guild the bot records. Events from any other guild and DMs are dropped. Published stack: the `DiscordGuildId` parameter |
| `Apollo:Platform:Discord:Connect` (`Apollo__Platform__Discord__Connect`) | config | `true` | `false` starts the bot without connecting to Discord. Only the AppHost smoke tests use it |
| `Apollo:Links:PublicWebUrl` (`Apollo__Links__PublicWebUrl`) | config | none, required; set by the AppHost | Public address of the website. Must be an absolute http or https URL. Run mode: the `apollo-web` endpoint (https when its launch profile has one). Published stack: the `PublicWebUrl` parameter |

For local development, set token and guild ID as user-secrets on the bot (the AppHost runs it in Development, so they
are loaded; the AppHost passes its `DiscordBotToken`/`DiscordGuildId` parameters only to the published stack):

```bash
dotnet user-secrets set "Apollo:Platform:Discord:Token" "<token>" --project src/Apollo.Bot
dotnet user-secrets set "Apollo:Platform:Discord:GuildId" "<guild id>" --project src/Apollo.Bot
```

`Apollo:Links:PublicWebUrl` needs no user-secret: the AppHost sets it in both modes.

On READY the bot overwrites the slash commands of the configured guild with the ones it defines (guild-scoped only,
no global commands). An empty set clears them. A failed registration is logged and leaves the previous commands.

The bot connects with the intents `Guilds`, `GuildMessages`, `MessageContent`, `GuildMessageReactions`,
`GuildVoiceStates`, `GuildPresences`, `GuildUsers` and `GuildModeration`. Message content, presences and members are
privileged intents and must be switched on in the developer portal ([Discord application](#discord-application)).

Service settings with a default (not AppHost parameters):

| Name | Default | What it is |
|---|---|---|
| `Apollo:Data:MigrationWaitTimeout` (`Apollo__Data__MigrationWaitTimeout`) | `00:05:00` | How long `bot` and `apollo-web` wait at startup for pending migrations before they exit (see below). Validated at startup: 1 second to 1 day |

### Environment of each service

This is every environment variable the published stack sets on `bot`, `apollo-web` and `migrationservice`, and where its
value comes from. No other setting is needed in production; the services run with the `Production` environment (the
stack sets no `DOTNET_ENVIRONMENT`), so `appsettings.Development.json` and user-secrets are not read.

| Variable | Services | Source |
|---|---|---|
| `ConnectionStrings__apollo` | all three | Set by Aspire from the `db` resource and its `apollo` database: host `db`, user `postgres`, password from the `db-password` secret parameter |
| `APOLLO_HOST`, `APOLLO_PORT`, `APOLLO_USERNAME`, `APOLLO_PASSWORD`, `APOLLO_URI`, `APOLLO_JDBCCONNECTIONSTRING`, `APOLLO_DATABASENAME` | all three | Set by Aspire for the same database reference. Apollo doesn't read them (it uses `ConnectionStrings__apollo`) |
| `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` | all three | Set by Aspire. The stack has no OTLP endpoint, so traces and metrics are not exported in production; logs go to `docker service logs` |
| `Apollo__Platform__Discord__Token` | `bot` | `DiscordBotToken` secret parameter |
| `Apollo__Platform__Discord__GuildId` | `bot` | `DiscordGuildId` parameter |
| `Apollo__Links__PublicWebUrl` | `bot` | `PublicWebUrl` parameter |
| `Apollo__Platform__Discord__OAuth__ClientId` | `apollo-web` | `DiscordOAuthClientId` parameter |
| `Apollo__Platform__Discord__OAuth__ClientSecret` | `apollo-web` | `DiscordOAuthClientSecret` secret parameter |
| `HTTP_PORTS` | `apollo-web` | AppHost: `80`, the port the reverse proxy targets |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `apollo-web` | Set by Aspire (`true`) |

`Apollo__Platform__Discord__Connect` and `Apollo__Data__MigrationWaitTimeout` are not set and keep their defaults.
`db` gets `POSTGRES_PASSWORD` from `db-password`, `pgadmin` its `PgAdmin*` parameters.

## Web login (Discord OAuth)

`apollo-web` logs users in with Discord's OAuth2 authorization-code flow (scope `identify` only, with `state` and PKCE)
and then keeps them in an HttpOnly session cookie that holds internal claims only ([scope §6.1](scope.md#61-login)).
The OAuth handler lives in `src/Apollo.Platform.Discord/Identity/`.

Settings (`src/Apollo.Platform.Discord/Identity/DiscordOAuthOptions.cs`) are validated at startup; a missing value
stops `apollo-web` with an error that names the key:

| Name | Kind | What it is |
|---|---|---|
| `Apollo:Platform:Discord:OAuth:ClientId` (`Apollo__Platform__Discord__OAuth__ClientId`) | config | The application's client ID (developer portal, OAuth2 page). Production: AppHost parameter `DiscordOAuthClientId` |
| `Apollo:Platform:Discord:OAuth:ClientSecret` (`Apollo__Platform__Discord__OAuth__ClientSecret`) | **secret** | The client secret. Production: AppHost secret parameter `DiscordOAuthClientSecret` |

For local development, set them as user-secrets on the web project (the AppHost runs it in Development):

```bash
dotnet user-secrets set "Apollo:Platform:Discord:OAuth:ClientId" "<client id>" --project src/Apollo.Web
dotnet user-secrets set "Apollo:Platform:Discord:OAuth:ClientSecret" "<client secret>" --project src/Apollo.Web
```

**Redirect URI.** The callback path is fixed at `/signin-discord`; there is no setting for the redirect URL. Register
it in the developer portal (OAuth2, Redirects) exactly as written:

- production: `<PublicWebUrl>/signin-discord`, where `PublicWebUrl` is the public reverse-proxy address of
  `apollo-web` (e.g. `https://apollo.example.ch/signin-discord`): the `PublicWebUrl` AppHost parameter above
- local: the `apollo-web` endpoint shown in the Aspire dashboard plus `/signin-discord` (e.g.
  `https://localhost:7174/signin-discord`)

**Reverse proxy.** The redirect URI is built from the request's scheme and host. `apollo-web` listens on plain HTTP
behind the proxy, so the proxy must send `X-Forwarded-Proto` (`https`) and `X-Forwarded-Host` (the public host).
`apollo-web` applies them (and `X-Forwarded-For`) before anything else. Without them Discord rejects the login with an
invalid `redirect_uri`, and the `Secure` session cookie is set on what looks like an HTTP request. `apollo-web` trusts
these headers from any sender. That is safe only because nothing but the proxy can reach it (no published host port,
see below).

**Sessions across deploys.** The cookie keys (ASP.NET Core data protection, application name `Apollo`) are stored in
the database table `data_protection_keys`, so logins survive restarts and redeploys. Deleting that table's rows logs
everyone out.

**Security: database access is admin access.** The keys in `data_protection_keys` are stored **unencrypted** (#29).
Anyone who can read the database can use them to forge a login cookie for any user, admins included. Treat every way
into the database as admin access to the website: the pgAdmin login (`PgAdminEmail`/`PgAdminPassword`, and keep
`PgAdminPort` reachable only from your own network), the `db-password`, the `PostgresDataPath` directory on the node,
and every backup of it. If one of them leaks, change the leaked credential (see below), then delete the rows of
`data_protection_keys` and restart the web host (`docker service update --force apollo_apollo-web`, it caches the
keys): everyone is logged out and new keys are created.

**Changing a secret.** Aspire reuses the values cached in its deployment state
(`~/.aspire/deployments/<hash>/production.json`, see [Values to provide](#values-to-provide)), so for every secret:
set the new value (user-secret or env var), delete that state file, then run the [Steps](#steps) again. Two secrets
are only read when their service first creates its data, so a new parameter value alone changes nothing:

- **`db-password`:** Postgres reads `POSTGRES_PASSWORD` only during initdb. First change the password in the running
  database (in pgAdmin: `ALTER USER postgres PASSWORD '<new password>';`), then set the new `db-password` value,
  delete the state file and redeploy, so the connection strings of `bot`, `apollo-web` and `migrationservice` match
  it again. Until the redeploy they can't reconnect.
- **`PgAdminPassword`:** pgAdmin reads it only when it first creates its own database in `PgAdminDataPath`. Change
  the password in the pgAdmin UI (*Change Password* in the user menu); also set the new parameter value so a fresh
  `PgAdminDataPath` gets it.

`DiscordBotToken`, `DiscordOAuthClientSecret` and the other parameters take effect with the next deploy (after
deleting the state file); rotate the Discord ones in the developer portal first.

## Prepare the storage

Do this once, before the first deploy. Both data directories are bind mounts, and neither container can set up its
directory itself. The stack has no placement constraints, so Swarm may start `db` and `pgadmin` on any node: the paths
must exist on every node, which in practice means a shared NFS directory.

**Postgres (`PostgresDataPath`).** The TimescaleDB image is Alpine-based and runs Postgres as `postgres` with uid **70**
(not 999 as in the Debian images). It keeps its data in `/var/lib/postgresql/18/docker` (Postgres 18+ layout, the `18`
is the major version in the `TimescaleImage` tag), and the stack mounts `PostgresDataPath` at `/var/lib/postgresql`.
On NFS with root squashing, root in the container can't create the version folder (`mkdir: can't create directory
'/var/lib/postgresql/18/': Permission denied`), so create it beforehand:

```bash
mkdir -p <PostgresDataPath>/18/docker
chown -R 70:70 <PostgresDataPath>
chmod 700 <PostgresDataPath>/18/docker
```

**pgAdmin (`PgAdminDataPath`).** pgAdmin runs as uid **5050**, not as root, so it can neither create nor take over its
folder:

```bash
mkdir -p <PgAdminDataPath>
chown -R 5050:5050 <PgAdminDataPath>
```

Run these as a user whose root rights are not squashed, e.g. on the NFS server itself. A Postgres major upgrade (a
new `-pgNN` tag) needs a new `NN/docker` folder prepared the same way and a data migration; it is not covered here.

## Steps

From the repository root, with the [values](#values-to-provide) set and the [storage](#prepare-the-storage) prepared:

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
docker stack deploy -c src/Apollo.AppHost/aspire-output/apollo.stack.yaml --with-registry-auth apollo
```

Run it against the Swarm manager, for example through a Docker context. `--with-registry-auth` passes your registry
login on to the nodes, so they can pull the images from the registry. To check the file first: `docker stack config -c src/Apollo.AppHost/aspire-output/apollo.stack.yaml`.

On a first deploy, `db` pulls its image and runs initdb, so `migrationservice` may fail and be restarted a few times,
and `bot` and `apollo-web` wait for the migrations ([below](#what-the-stack-looks-like)). Give it a few minutes, then
[verify](#verify-the-deployment).

`aspire-output/` folders are git-ignored wherever they are. `.env.Production` and `apollo.stack.yaml` contain secrets, so never commit or share them.

## Verify the deployment

Run all checks after the first deploy, and the first four after every redeploy. SQL runs in pgAdmin
(`http://<node>:<PgAdminPort>`, log in with `PgAdminEmail`/`PgAdminPassword`). The first time, register the database
there: host `db`, port `5432`, user `postgres`, password `db-password`, database `apollo`.

1. **Services are up.**

   ```bash
   docker stack services apollo
   docker service ps apollo_migrationservice
   ```

   `db`, `bot`, `apollo-web` and `pgadmin` show `1/1` replicas. `migrationservice` shows `0/1` and its latest task
   `Complete` (earlier `Failed` tasks on a first deploy are restarts while `db` was starting).
2. **Migrations are applied.** `docker service logs apollo_migrationservice` contains
   `Database migrated, <n> migrations applied` (`0` on a redeploy without new migrations). The newest row of
   `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;` is the newest file in
   `src/Apollo.Data/Migrations/`.
3. **The bot is connected, has registered its commands and is in bootstrap mode.** `docker service logs apollo_bot`
   shows, after `Database is up to date`:
   - `Discord gateway Connected, recording guild <guild id>`;
   - `Registered 1 slash commands for guild <guild id>` (every feature adds its part to the one `apollo` command, so
     the count is 1);
   - the warning `Bootstrap mode: tracking Administrators on Discord until a tracking role is set (temporary, #62)`
     while no tracking role is configured ([scope §5.1.1](scope.md#511-who-is-tracked-the-tracking-role)).

   No `fail:` or `crit:` lines. A `Discord gateway Disconnected` warning with close status 4014 means a privileged
   intent is off, 4004 a wrong token ([Discord application](#discord-application)).
4. **Heartbeat every 30 s.**

   ```sql
   SELECT occurred_at, occurred_at - lag(occurred_at) OVER (ORDER BY occurred_at) AS gap, connected, last_event_at
   FROM bot_status ORDER BY occurred_at DESC LIMIT 10;
   ```

   `gap` is about `00:00:30` and `connected` is `true`. See [Is the bot collecting?](#day-to-day-operations) for what
   other values mean.
5. **The Administrators are tracked.** The two counts are equal (every current member with the Administrator
   permission, the guild owner included, has an open tracking period):

   ```sql
   SELECT (SELECT count(*) FROM tracking_periods WHERE "to" IS NULL) AS tracked,
          (SELECT count(*) FROM guild_members WHERE is_administrator AND left_at IS NULL) AS administrators;
   ```
6. **Event rows for an Administrator, none for anyone else.** Do this at a quiet moment: every Administrator is
   tracked, so another Administrator's activity during the test adds rows too. The query counts only the last
   5 minutes:

   ```sql
   SELECT (SELECT count(*) FROM message_events WHERE occurred_at > now() - interval '5 minutes') AS messages,
          (SELECT count(*) FROM voice_sessions WHERE occurred_at > now() - interval '5 minutes') AS voice_sessions,
          (SELECT count(*) FROM reaction_events WHERE occurred_at > now() - interval '5 minutes') AS reactions;
   ```

   With a test account **without** the Administrator permission, post a message, join a voice channel and react to a
   message, then run the query: all counts are 0 (if not, check that no Administrator was active, and repeat). Then do
   the same as an Administrator and run it again within 5 minutes: each count is at least 1 (the voice session row
   appears on joining, with `ended_at` empty until you leave). Rows hold internal IDs only, so the query output contains
   no personal data.
7. **The `/apollo` commands reply privately.** In the guild, type `/apollo`: Discord offers `tracking on`,
   `tracking off`, `tracking status`, `privacy delete` and `stats`. Run `/apollo tracking status` as the Administrator:
   Discord marks the reply as visible only to you (*Only you can see this* / *Nur für dich sichtbar*). In bootstrap
   mode its first paragraph says you are tracked and ends with "Tracking ist noch nicht eingerichtet: Bis eine
   Tracking-Rolle festgelegt ist, werden nur Admins erfasst."; then it lists your stored rows per table, and the last
   line is `Datenschutz: <PublicWebUrl>/datenschutz`. Don't confirm `/apollo privacy delete` with an account whose data you
   want to keep: it erases that data.
8. **The web login works.** Open `<PublicWebUrl>/auth/login`: Discord asks to authorize the application, then sends you
   back to `<PublicWebUrl>/`. `<PublicWebUrl>/auth/me` now returns your user as JSON. An `invalid redirect_uri` from
   Discord means the redirect is not registered exactly as `<PublicWebUrl>/signin-discord`, or the reverse proxy doesn't
   send the forwarded headers ([Web login](#web-login-discord-oauth)). `docker service logs apollo_apollo-web` shows no
   `fail:` lines.

## What the stack looks like

| Service | Networks | Notes |
|---|---|---|
| `db` | `apollo_bot_internal` | TimescaleDB (pinned tag, `TimescaleImage`), bind mount `PostgresDataPath` → `/var/lib/postgresql`. Postgres 18+ refuses a mount at `/var/lib/postgresql/data` |
| `migrationservice` | `apollo_bot_internal` | `deploy.restart_policy`: `condition: on-failure`, `delay: 10s`, `max_attempts: 10`. It applies the EF migrations, then exits with 0 (or 1 on failure). Exit 0 is never restarted, so it runs once per deploy. While the database is unreachable it retries itself (10 attempts, 3 s apart; the EF retry strategy is off, scope.md D17). If `db` takes longer than that, e.g. on a first deploy while the TimescaleDB image is still being pulled or initdb is running, Swarm starts it again (up to 10 more runs, 10 s apart) |
| `bot` | `apollo_bot_internal` | Swarm's default restart policy, so it restarts on crash |
| `apollo-web` | `apollo_bot_internal`, reverse-proxy network (`apollo_bot`) | Listens on port 80 (`HTTP_PORTS=80`), no published host port. The reverse proxy targets **`apollo-web:80`** |
| `pgadmin` | `apollo_bot_internal` | `dpage/pgadmin4` with a pinned tag (`PgAdminImage`), host port `PgAdminPort` → 80, bind mount `PgAdminDataPath` → `/var/lib/pgadmin`. Production only: in development `WithPgAdmin()` adds it |

There is no Aspire dashboard, no `expose:` section and no `depends_on` in the stack. Swarm ignores `depends_on`, and its stack schema rejects the long form Aspire emits. Bot and web wait for the migrations themselves instead of relying on start order: outside Development they call `WaitForMigrationsAsync()` (`src/Apollo.Data/MigrationExtensions.cs`) before starting, which polls every 2 s until no migration is pending. Errors while the database is unreachable or not created yet count as "still waiting". After `Apollo:Data:MigrationWaitTimeout` they exit with a `TimeoutException`, and Swarm restarts them, so they keep waiting until the migration service has run. In local development the AppHost orders them instead (`WaitForCompletion(migrationservice)`).

The service name `apollo-web` is its DNS alias on the shared reverse-proxy network, so it has to be unique there. That is why the resource isn't called `web`.

## Day-to-day operations

**Redeploy (also to update a single service):** the same three [Steps](#steps), deploying the whole stack again, then
the first four checks of [Verify the deployment](#verify-the-deployment). Swarm only restarts services whose definition
changed. Each run builds and tags all three images again, so `bot`, `apollo-web` and `migrationservice` are updated and
`migrationservice` applies any new migration; `db` and `pgadmin` keep running. To roll one service to a given image by
hand, see *Roll back*. Take a [backup](#back-up-the-database) first when the deploy brings a migration.

**Roll back:** Swarm keeps the previous definition of every service, so `docker service rollback apollo_<service>`
(e.g. `apollo_bot`) goes back one deploy. For an older image, `docker service ps apollo_<service> --no-trunc` lists the
images of the last tasks; deploy one with
`docker service update --image <registry>/<repo>/<service>:<tag> --with-registry-auth apollo_<service>`. To return the
whole stack to an earlier state, keep the previous `apollo.stack.yaml` (it contains secrets: store it like them, never
in the repository) and run `docker stack deploy` with it. Migrations are never rolled back: no down migrations run, and
an older `bot` or `apollo-web` starts against a newer schema (they only wait for migrations missing from the database),
which works only if the migration kept the old columns. To undo a migration, restore the backup taken before the
deploy.

**Migration-service logs:**

```bash
docker service logs apollo_migrationservice
docker service ps apollo_migrationservice
```

The latest task should show `Complete`; earlier `Failed` tasks are restarts after a non-zero exit (`on-failure`). A migration that fails on every attempt (a broken migration, wrong credentials) ends `Failed` after 10 restarts and stays so until the next deploy; the log has the exception under `crit: Apollo.MigrationService.MigrationWorker`. Meanwhile `bot` and `apollo-web` keep waiting and restarting (see above).

**Rerun migrations** without other changes: `docker service update --force apollo_migrationservice`.

**Is the bot collecting?** The bot appends a heartbeat row to `bot_status` every 30 s, connected or not (#24). In pgAdmin:

```sql
SELECT occurred_at, connected, gateway_latency_ms, last_event_at, open_voice_sessions
FROM bot_status ORDER BY occurred_at DESC LIMIT 10;
```

No row for the last minute means the bot process is down or cannot write; rows with `connected = false` mean it runs but
the gateway is disconnected. After 2 minutes of that the bot's `gateway` health check turns unhealthy and the bot logs
`Health check gateway changed from Degraded to Unhealthy` at Error (and logs again when it recovers; at start it logs the initial status once, e.g. `Health check gateway is Degraded` at Information). `last_event_at`
counts only guild events, not reconnects, so far behind `occurred_at` on a connected row means nothing is arriving. Locally, the counters `apollo.events.recorded`
(tag `event_type`) and `apollo.events.dropped` (tag `reason`) of meter `Apollo.Bot` are in the Aspire dashboard's
metrics view for the `bot` resource.

### Back up the database

All data is in the `PostgresDataPath` bind mount. A file copy is consistent only while Postgres is stopped, so stop the
writers first (the bot closes its open voice and presence sessions on a graceful shutdown), then the database:

```bash
docker service scale apollo_bot=0 apollo_apollo-web=0
docker service scale apollo_db=0
# on a host where <PostgresDataPath> is mounted without root squashing, e.g. the NFS server:
tar --numeric-owner -cpzf apollo-db-<yyyy-mm-dd>.tar.gz -C <PostgresDataPath> .
docker service scale apollo_db=1
docker service scale apollo_bot=1 apollo_apollo-web=1
```

Events that happen while the bot is stopped are not recorded, so keep the pause short. `--numeric-owner` and `-p` keep
uid 70 and the `0700` mode, which Postgres requires. To restore, stop the same services, move the current
`<PostgresDataPath>` contents aside, extract with `tar --numeric-owner -xpzf apollo-db-<yyyy-mm-dd>.tar.gz -C
<PostgresDataPath>`, check that everything is owned by `70:70`, and scale the services up again in the same order. The
backup must come from the same TimescaleDB image tag (`TimescaleImage`).

Without downtime, `pg_dump` works too: on the node that runs `db`,
`docker exec -it -u postgres $(docker ps -q -f name=apollo_db) pg_dump -U postgres -Fc -f /var/lib/postgresql/apollo-<yyyy-mm-dd>.dump apollo`
asks for `db-password` and writes the dump into `<PostgresDataPath>`; move it out of there afterwards. A TimescaleDB dump
is restored into an empty database with the same TimescaleDB version, between `SELECT timescaledb_pre_restore();` and
`SELECT timescaledb_post_restore();`.

A backup holds personal data and the web login's cookie keys (`data_protection_keys`), so it needs the same protection
as the database itself ([security note](#web-login-discord-oauth)). A restore also brings back data deleted with
`/apollo privacy delete` after the backup was taken: members who deleted their data since then have to run the command
again.

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

- `tests/Apollo.IntegrationTests/ComposePublishTests.cs` runs the AppHost in publish mode with placeholder values and asserts the compose shape above: networks, restart policy, no `expose`/`depends_on`/dashboard, bind mounts, pgAdmin, and the parameters that reach `bot` (Discord token and guild ID, `PublicWebUrl`) and `apollo-web` (OAuth client). `AppHostModelTests.cs` checks that the bot token is a secret parameter.
- `tests/Apollo.IntegrationTests/RenderStackScriptTests.cs` runs `Render-Stack.ps1` with `pwsh`: full substitution, `:-default`, missing or empty values, escaping, and no values in the output.
- `tests/Apollo.IntegrationTests/AppHostTests.cs` starts the AppHost and checks that `migrationservice` finishes with exit code 0 before `bot` and `apollo-web` start. `tests/Apollo.IntegrationTests/Data/` covers the migrator's retries and `WaitForMigrationsAsync` against a TimescaleDB container.
