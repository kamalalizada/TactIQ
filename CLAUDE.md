# TactIQ

A football management simulation. A manager picks a club, sets a line-up, plays
matches minute by minute, buys and sells, and carries one save across seasons.

**All user-facing text is Azerbaijani.** Error messages, labels and buttons
included. Write it as plain UTF-8.

## Layout

This is one repository holding both halves:

```
src/TactIQ.Domain/         entities and enums
src/TactIQ.Application/    game logic: match engine, transfers, seasons, careers
src/TactIQ.Infrastructure/ EF Core, PostgreSQL, Identity, migrations
src/TactIQ.API/            controllers, SignalR hubs, JWT
tests/                     257 tests, all passing
frontend/                  React 19 + TypeScript + Vite
```

The frontend moved here from a separate `TactIQ-Frontend` repository on
2026-11-01, so that one Docker image can carry the API and the pages it serves.
That old folder and its GitHub repo are an archive: edits made there reach
nothing. If a path like `TactIQ-Frontend\src\...` comes up, it means
`frontend\src\...` here.

## Running it

```sh
docker compose up -d                 # PostgreSQL
cd src/TactIQ.API && dotnet run      # API on :5199, migrates on boot
cd frontend && npm run dev           # Vite on :5173, proxies /api and /hubs
```

`src/TactIQ.API/appsettings.Development.json` holds the connection string and
JWT key. It is gitignored; copy the `.example` beside it. The frontend's `.env`
is likewise local — see `frontend/.env.example`.

## Verifying

```sh
dotnet test                                  # from the root
cd frontend && npx tsc -p tsconfig.app.json --noEmit
cd frontend && npm run build
```

**`npx tsc --noEmit` on its own checks nothing here.** The root `tsconfig.json`
is a solution file with `"files": []`, so the bare command exits 0 having read
no source. Always pass `-p tsconfig.app.json`. This has hidden real errors
before.

## Things worth knowing before changing code

**A starter plays the role of the slot he stands in, at full rating.** If the
squad data puts a winger in a left-midfield slot, he is a left-midfielder and
is not marked down for it. The seed importer derives a starter's role from the
formation's shape, and a guard test fails if any seeded starter is rated down
for his own shirt.

**The frontend mirrors several backend tables and must stay in step with them:**

| Backend | Frontend |
|---|---|
| `Simulation/FormationShape.cs` | `src/lib/formations.ts` |
| `Simulation/RoleFitness.cs` | `src/lib/formations.ts` |
| `Simulation/PitchZones.cs` | `src/lib/pitch.ts` |
| `Domain/Enums/PlayerRole.cs` | `src/types.ts` |

If they drift, the side drawn on the pitch is not the side the engine fields.
The frontend copies exist to show the manager the consequence of a choice
before he makes it; the backend still decides.

**`PlayerRole` is append-only.** The values are stored in the database, so a new
role goes at the end of the enum. Inserting one in the middle renumbers the
roles after it and corrupts every stored value.

**Seed data lives in `src/TactIQ.Application/Seeding/Data/squads.json`.** Clubs
with no players are leagues not yet entered (Serie A is partly done, Bundesliga
is empty), which is why 58 of 96 clubs are playable. A club is playable at
16–32 players; the ceiling is `SquadLimits.MaximumSquad`.

## Working agreement

Kamal runs the git commands. Finish and verify the work, then give him the
commit message to copy — do not run `git commit` or `git push`.
