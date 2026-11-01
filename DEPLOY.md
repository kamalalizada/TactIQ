# Deploy

TactIQ goes online on **Render** so the project can be linked from a CV, with
the database on **Neon**. Free tier throughout: no server bill, no domain bill.
The address is `https://<service name>.onrender.com`.

## Decisions already made

These were settled on 2026-11-01. They replace an earlier Fly.io plan: Fly.io no
longer has a free tier for new organizations.

| | |
|---|---|
| Server | **Render** free web service, built from the Dockerfile |
| Database | **Neon** free Postgres: Render's free Postgres is deleted after 30 days plus a 14-day grace period, Neon's has no expiry |
| Access | **guest accounts** — one career each, no registration |
| Seed | league structure and clubs only, **no played matches** |
| Guest cleanup | untouched guests deleted after **7 days** |
| Swagger | **open in production**, as a signal to technical reviewers |
| Frontend | served by the API itself, one origin, no CORS |

Guests rather than one shared demo account: two visitors must be able to run
different clubs at the same time without overwriting each other. `Season.UserId`
already exists, so this needs no schema change.

No pre-played matches: every guest starts at week zero and plays their own
season, so there is nothing to pre-play.

## What the free tier costs us

Checked against Render's and Neon's documentation on 2026-11-01.

**Render**

- Sleeps after 15 minutes without inbound traffic; waking takes about a minute.
- 750 instance hours a month, enough for one service all month (744 at most).
  Used up, the service is suspended until the next month.
- Past the monthly bandwidth allowance, an account with no payment method has
  its free services suspended for the month rather than being billed. Past the
  build-minute allowance, new builds stop and the service keeps running.
- May restart at any time. The disk is not kept, and a live match in progress
  is lost, because live sessions are held in memory.

**Neon**

- 1 GB per project; past that, writes are blocked. Every career copies all clubs
  and players, so guest cleanup is what keeps this from filling up.
- 100 CU-hours a month. Sleeps after 5 minutes and wakes in a few hundred
  milliseconds.

## Work

Claude's, unless marked otherwise.

### 1 — Production configuration

Done:

- Secrets from environment variables (`Jwt__Key`, `ConnectionStrings__Default`).
- CORS and `UseHttpsRedirection` in development only: one origin, and the host
  terminates TLS, so the redirect would loop.
- Swagger open in every environment.
- Serve the built frontend: `/api` and `/hubs` to the API, a wrong API path is a
  404, everything else goes to `index.html` for React Router.
- `GET /health`, which does not touch the database.

### 2 — Guest accounts

Done:

- `POST /api/auth/guest` creates a throwaway user and returns a JWT. The token
  lasts 90 days, but a guest whose account is gone gets a 401 regardless.
- A "Qonaq kimi oyna" button on the login page, which is now what a visitor
  lands on. The automatic demo sign-in exists only in development, and its
  password is not in the production bundle.
- A background service deletes guests untouched for 7 days, with everything they
  own, on boot and then hourly. Registered users are left alone.

The limits, all under `Guests` and `LiveMatches` in `appsettings.json`:

| Limit | Value | When reached |
|---|---|---|
| Careers per guest | 1 | A second one is refused |
| Guests in total | 50 | The guest away longest is deleted to make room |
| New guests per address | 5 an hour | Refused with a 429 |
| Live matches at once | 3 | Refused; simulating the week still works |

A live match paused for 10 minutes is closed, so abandoned ones free their slot.

The address is the first one in `X-Forwarded-For`. Render does not document this
header's contents, so it is unconfirmed until step 5, and a client can forge it;
the cap of 50 is what bounds the harm.

### 3 — Dockerfile

Done. Multi-stage: Node builds the frontend, the .NET SDK builds the API and takes
`frontend/dist` into `wwwroot`, and the runtime image carries only the output, as
a non-root user on port `10000`, Render's default, with workstation GC.

Checked on an empty database under `--cpus=0.1 --memory=512m`: it boots and applies
every migration in 16 s, and peaks at 163 MB. On that first boot the log shows two
`fail` lines for `__EFMigrationsHistory`. They are harmless: EF looks for the table
before creating it.

### 4 — Deploy (Kamal runs these)

1. **Neon:** create a project in a region near Render's, and copy the **direct**
   connection details, not the pooled ones, because migrations run on boot.
2. **Render:** New → Web Service → this GitHub repo. Language **Docker**, plan
   **Free**, health check path `/health`, same region if there is one.
   The first build takes a few minutes.
3. Set the environment variables on the service:

   ```
   Jwt__Key=<32+ random characters>
   ConnectionStrings__Default=Host=<host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require
   ```

Migrations run themselves: `Program.cs` calls `MigrateDatabaseAsync()` on boot.
Do not add a payment method unless Render insists: without one, going over a
limit suspends the service instead of billing for it. Render's pages do not say
whether signing up asks for one.

### 5 — Check and publish

- Open the link in a **private window**, play through as a guest.
- Press "Qonaq kimi oyna" in six fresh private windows on one network: the
  sixth must be refused. If a visitor on **another** network is refused too, every
  request looks like one address, and `GuestRules.ClientKey` needs another header.
- Open a second guest in another browser, pick a different club, confirm the two
  careers do not touch.
- Try it on a phone.
- Write the README: live link, screenshots, stack, local setup.

## Keeping it awake (optional)

An uptime monitor calling `/health` every 10 minutes counts as inbound traffic,
so the service should not sleep and a visitor should not wait a minute. It must
be `/health`: Render answers `/robots.txt` itself without waking the service.
Render's documentation neither allows nor forbids this. Without it, the first
visitor after a quiet spell simply waits.

## If Render falls through

The Dockerfile and the configuration work are platform-neutral. Oracle Cloud
Always Free does not sleep, but it is a bare VM: Docker, Nginx, TLS and Postgres
are yours to set up, and Oracle may reclaim an idle instance.

## What not to do

Do not deploy the frontend alone to Vercel or Netlify. Every screen reads from
the API, so without a backend the visitor gets the login page and an error, and
nothing else — worse for a CV than no link at all.
