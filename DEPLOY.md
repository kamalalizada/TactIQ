# Deploy

TactIQ goes online at **tactiq.fly.dev** so the project can be linked from a CV.
Free tier throughout: no server bill, no domain bill.

## Decisions already made

These were settled on 2026-11-01. They are not open questions.

| | |
|---|---|
| Platform | **Fly.io**, region `fra` (Frankfurt) |
| Why not Render | its free backend sleeps; a 30-second wait kills a demo |
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

## Work

Claude's, unless marked otherwise.

### 1 — Production configuration

- Secrets from environment variables (`Jwt__Key`, `ConnectionStrings__Default`).
  `Program.cs` currently dies pointing at `appsettings.Development.json`, which
  is gitignored and never reaches a server.
- CORS off in production: one origin, so it is not needed.
- `UseHttpsRedirection` off: Fly terminates TLS at the edge and forwards HTTP,
  so the redirect would loop.
- Serve the built frontend: `/api` and `/hubs` to controllers, everything else
  to `index.html` for React Router.

### 2 — Guest accounts

- `POST /api/auth/guest` — creates a throwaway user, returns a JWT.
- A "Qonaq kimi oyna" button on the login page, with a note that the save is
  kept for 7 days.
- A background service that deletes guests untouched for 7 days, with their
  leagues, clubs, players and matches. Registered users are left alone.

### 3 — Dockerfile

Multi-stage: Node builds the frontend, the .NET SDK builds the API and takes
`frontend/dist` into `wwwroot`, and the runtime image carries only the output.

### 4 — Deploy (Kamal runs these)

```sh
iwr https://fly.io/install.ps1 -useb | iex     # PowerShell
fly auth signup                                 # card required, not charged

fly postgres create --name tactiq-db --region fra
fly launch --name tactiq --region fra --no-deploy

fly secrets set Jwt__Key="<32+ characters>"
fly secrets attach tactiq-db

fly deploy
```

Migrations run themselves: `Program.cs` calls `MigrateDatabaseAsync()` on boot.

### 5 — Check and publish

- Open the link in a **private window**, play through as a guest.
- Open a second guest in another browser, pick a different club, confirm the two
  careers do not touch.
- Try it on a phone.
- Write the README: live link, screenshots, stack, local setup.

## If Fly.io falls through

The Dockerfile and the configuration work are platform-neutral. Railway
(~$5/month) and Oracle Cloud Always Free (free, but a bare VM: Docker, Nginx and
TLS are yours to set up) both take the same image.

## What not to do

Do not deploy the frontend alone to Vercel or Netlify. Every screen reads from
the API, so without a backend the visitor gets the login page and an error, and
nothing else — worse for a CV than no link at all.
