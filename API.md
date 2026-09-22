# TactIQ — Frontend üçün API Sənədi

Bu sənəd **backend hazır olan** TactIQ futbol simulyasiya API-nin tam təsviridir. Frontend (React + TypeScript) bu sənədə əsasən yazıla bilər — heç bir əlavə sual olmadan.

**Backend:** .NET 9 Web API + PostgreSQL + SignalR
**Base URL (development):** `http://localhost:5199`
**Swagger UI:** `http://localhost:5199/swagger`

---

## 0. Layihə nədir — 30 saniyəlik izah

8 komandalı futbol liqası simulyasiyası. İstifadəçi qeydiyyatdan keçir, liqa yaradır və mövsümü həftə-həftə oynayır (14 həftə, 56 matç).

**İki oyun rejimi var:**

1. **God Mode** — istifadəçi heç bir komandanı idarə etmir. "Simulate Week" düyməsini basır, həmin həftənin **bütün 4 matçı** dərhal hesablanır və nəticələr göstərilir.

2. **Manager Mode** — istifadəçi 1 komandanın meneceridir. "Simulate Week" basanda **yalnız 3 matç** dərhal oynanır. İstifadəçinin öz matçı oynanmır — o, **canlı** oynanmalıdır: 1-dən 90-cı dəqiqəyə qədər serverdə real vaxtda gedir, istifadəçi matç gedərkən taktika dəyişə, oyunçu əvəz edə bilər.

> **Frontend üçün ən vacib fərq:** God Mode-da matçlar sadəcə nəticə kimi göstərilir. Manager Mode-da istifadəçinin matçı üçün ayrıca **canlı matç ekranı** lazımdır (SignalR ilə).

---

## 1. Ümumi qaydalar

### 1.1. Autentifikasiya
Bütün endpoint-lər (`/api/auth/*` istisna olmaqla) JWT tələb edir:

```
Authorization: Bearer <token>
```

Token `localStorage`-da saxlanıla bilər. **Müddəti 12 saatdır.** Token bitəndə API `401` qaytarır — bu halda istifadəçini login səhifəsinə yönləndirin.

### 1.2. ⚠️ Enum-lar RƏQƏM kimi gedir və gəlir
Bu, ən çox səhv edilən yerdir. API enum-ları **string yox, integer** kimi göndərir və qəbul edir:

```json
{ "gameMode": 1, "position": 2, "eventType": 0 }
```

Frontend-də bunlar üçün const obyektləri yazın (bax bölmə 2.1).

### 1.3. JSON adlandırması
Bütün sahələr **camelCase**-dir (`homeTeamName`, `isPlayed`, `substitutionsRemaining`).

### 1.4. CORS
Backend artıq bu origin-lərə icazə verir:
- `http://localhost:5173` (Vite — tövsiyə olunur)
- `http://localhost:3000` (Create React App)

Başqa portdan işləsəniz, backend-də əlavə etmək lazımdır.

### 1.5. Xəta formatı
Xətalar RFC 7807 `ProblemDetails` formatındadır:

```json
{
  "title": "Invalid operation",
  "status": 409,
  "detail": "Only your own team's match can be played live."
}
```

| Kod | Nə vaxt | Frontend nə etməli |
|---|---|---|
| `400` | Yanlış request body | Validasiya mesajı göstər |
| `401` | Token yox / bitib | Login səhifəsinə yönləndir |
| `404` | Liqa/matç tapılmadı (başqasının liqası da 404 verir) | "Tapılmadı" göstər |
| `409` | Məntiqi xəta (`detail`-da izah var) | `detail` mətnini istifadəçiyə göstər |

---

## 2. TypeScript tipləri

Bu blokları birbaşa `src/types/api.ts` faylına köçürə bilərsiniz.

### 2.1. Enum-lar

```ts
export const GameMode = {
  GodMode: 0,
  ManagerMode: 1,
} as const;
export type GameMode = (typeof GameMode)[keyof typeof GameMode];

export const Position = {
  GK: 0,   // Qapıçı
  DEF: 1,  // Müdafiəçi
  MID: 2,  // Yarımmüdafiəçi
  FWD: 3,  // Hücumçu
} as const;
export type Position = (typeof Position)[keyof typeof Position];

export const MatchEventType = {
  Goal: 0,
  YellowCard: 1,   // hələ generasiya olunmur
  RedCard: 2,      // hələ generasiya olunmur
  Substitution: 3,
} as const;
export type MatchEventType = (typeof MatchEventType)[keyof typeof MatchEventType];

export const TacticalStyle = {
  Defensive: 0,   // müdafiə güclənir, hücum zəifləyir
  Balanced: 1,
  Attacking: 2,   // hücum güclənir, müdafiə zəifləyir
} as const;
export type TacticalStyle = (typeof TacticalStyle)[keyof typeof TacticalStyle];

export const LiveMatchStatus = {
  Running: 0,
  Paused: 1,
  Finished: 2,
} as const;
export type LiveMatchStatus = (typeof LiveMatchStatus)[keyof typeof LiveMatchStatus];

// UI üçün göstərilən adlar
export const POSITION_LABELS: Record<Position, string> = {
  0: "GK", 1: "DEF", 2: "MID", 3: "FWD",
};

export const STYLE_LABELS: Record<TacticalStyle, string> = {
  0: "Defensiv", 1: "Balanslı", 2: "Hücumçu",
};
```

### 2.2. Data tipləri

```ts
export interface AuthResponse {
  token: string;
  email: string;
  expiresAt: string;   // ISO 8601
}

export interface LeagueSummary {
  id: string;
  seasonName: string;
  gameMode: GameMode;
  userTeamId: string | null;   // yalnız Manager Mode-da dolu olur
  teamCount: number;           // hazırda həmişə 8
  currentWeek: number;         // 1-dən başlayır
  totalWeeks: number;          // 14
  isFinished: boolean;
  createdAt: string;
}

export interface Player {
  id: string;
  name: string;
  position: Position;
  shirtNumber: number;
  pace: number;        // 1-99
  shooting: number;
  passing: number;
  dribbling: number;
  defending: number;
  physical: number;
  overall: number;     // 6 atributun ortalaması
  isStarter: boolean;  // true = əsas 11-də, false = ehtiyat skamyasında
}

export interface Team {
  id: string;
  name: string;
  logoUrl: string | null;   // hazırda həmişə null — placeholder göstərin
  attackRating: number;     // yalnız əsas 11 üzərindən hesablanır
  defenseRating: number;
  teamOverall: number;
  players: Player[];        // 19 nəfər: 11 əsas + 8 ehtiyat
}

export interface MatchEvent {
  minute: number;
  eventType: MatchEventType;
  teamId: string;
  description: string;   // məs. "Goal for Inter Milan"
}

export interface Match {
  id: string;
  weekNumber: number;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  homeGoals: number;
  awayGoals: number;
  isPlayed: boolean;
  isUserMatch: boolean;   // true = bu matç canlı oynanmalıdır
  events: MatchEvent[];
}

export interface StandingsRow {
  rank: number;
  teamId: string;
  teamName: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
}

export interface SimulateWeekResult {
  weekNumber: number;
  simulatedMatches: Match[];
  pendingUserMatch: Match | null;   // null deyilsə → canlı oynanmalıdır
  nextWeek: number;
  seasonFinished: boolean;
}

export interface LiveEvent {
  minute: number;
  eventType: MatchEventType;
  teamId: string;
  description: string;
}

export interface LiveState {
  matchId: string;
  minute: number;                 // 0-90
  status: LiveMatchStatus;
  homeTeamId: string;
  homeTeamName: string;
  homeGoals: number;
  awayTeamId: string;
  awayTeamName: string;
  awayGoals: number;
  userStyle: TacticalStyle;
  userFormation: string;          // məs. "4-4-2"
  substitutionsUsed: number;
  substitutionsRemaining: number; // 5-dən başlayır
  speedMultiplier: number;        // 1, 2 və ya 4
  events: LiveEvent[];
}
```

---

## 3. Auth endpoint-ləri

### `POST /api/auth/register`

```json
// request
{ "email": "user@example.com", "password": "test123" }

// 200 OK
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "email": "user@example.com",
  "expiresAt": "2026-09-23T10:00:00Z"
}
```

**Parol qaydaları:** minimum 6 simvol. Böyük hərf, rəqəm və ya xüsusi simvol **tələb olunmur**.

**400 xətası formatı fərqlidir** (ProblemDetails yox):
```json
{ "errors": ["Email 'x@y.com' is already taken."] }
```

### `POST /api/auth/login`

```json
// request
{ "email": "user@example.com", "password": "test123" }
// 200 OK — register ilə eyni cavab
// 401 → { "errors": ["Invalid email or password."] }
```

---

## 4. Liqa endpoint-ləri

### `POST /api/leagues` — liqa yarat

```json
// request
{
  "seasonName": "2026 Season",
  "gameMode": 1,          // 0 = God Mode, 1 = Manager Mode
  "userTeamId": null      // aşağıdakı qeydə bax
}
```

**`userTeamId` haqqında mühüm qeyd:** Liqa yaradılana qədər komandalar mövcud deyil — yəni istifadəçi əvvəlcədən komanda seçə bilmir. Ona görə `null` göndərin; Manager Mode-da backend **təsadüfi komanda təyin edir**.

> İstifadəçiyə komanda seçdirmək istəyirsinizsə: əvvəlcə liqanı `null` ilə yaradın → `GET /teams` ilə 8 komandanı göstərin → istifadəçi seçsin. **Diqqət:** hazırda komandanı sonradan dəyişmək üçün endpoint **yoxdur**. Lazımdırsa, backend-ə deyin, əlavə edilsin.

Cavab: `201 Created` + `LeagueSummary`.

### `GET /api/leagues` — istifadəçinin bütün liqaları

```json
[ { "id": "...", "seasonName": "2026 Season", "currentWeek": 3, ... } ]
```
Ən yenidən köhnəyə doğru sıralanıb. Bir istifadəçinin **bir neçə liqası ola bilər**.

### `GET /api/leagues/{leagueId}`
Bir liqanın `LeagueSummary`-si.

### `GET /api/leagues/{leagueId}/teams`
8 komanda, hərəsinin 19 oyunçusu ilə (`Team[]`). Komanda adına görə əlifba sırası.

### `GET /api/leagues/{leagueId}/standings`
Turnir cədvəli (`StandingsRow[]`), artıq sıralanmış halda gəlir.

**Sıralama qaydası:** Xal → Qol fərqi → Vurulan qol → Əlifba sırası.

### `GET /api/leagues/{leagueId}/fixtures?week=3`
Matçlar (`Match[]`). `week` parametri **opsionaldır** — verilməsə bütün 56 matç gəlir.

### `POST /api/leagues/{leagueId}/simulate-week`
Cari həftəni simulyasiya edir. Body **lazım deyil**.

```json
// 200 OK
{
  "weekNumber": 1,
  "simulatedMatches": [ /* 3 və ya 4 matç */ ],
  "pendingUserMatch": {
    "id": "6942fce2-...",
    "homeTeamName": "Inter Milan",
    "awayTeamName": "Benfica",
    "isPlayed": false,
    "isUserMatch": true,
    ...
  },
  "nextWeek": 1,
  "seasonFinished": false
}
```

**Diqqət — ən vacib məntiq:**

| | God Mode | Manager Mode |
|---|---|---|
| `simulatedMatches` | 4 matç | 3 matç |
| `pendingUserMatch` | `null` | İstifadəçinin matçı |
| `nextWeek` | `weekNumber + 1` | **`weekNumber` (dəyişmir!)** |

Manager Mode-da həftə **irəliləmir**, çünki istifadəçinin matçı hələ oynanmayıb. Həftə yalnız canlı matç bitəndən sonra avtomatik irəliləyir.

> **Frontend məntiqi:** `pendingUserMatch !== null` olduqda "Simulate Week" düyməsini deaktiv edin və əvəzində **"Matçı oyna"** düyməsi göstərin.

`409` — mövsüm artıq bitibsə (`"The season is already finished."`).

### `DELETE /api/leagues/{leagueId}`
Liqanı və bütün məlumatlarını silir. `204 No Content`.

---

## 5. Canlı matç (Manager Mode)

Bu bölmə yalnız **istifadəçinin öz komandasının matçı** üçündür.

### 5.1. Necə işləyir

Matç **serverdə** real vaxtda gedir — frontend sadəcə izləyir və müdaxilə edir:

```
1x sürət → hər 1 real saniyə = 1 oyun dəqiqəsi → 90 dəqiqə ≈ 90 saniyə
2x sürət → ≈ 45 saniyə
4x sürət → ≈ 22 saniyə
```

Hər "tick"-də server SignalR vasitəsilə yeni vəziyyəti göndərir. Frontend taймer saxlamır — sadəcə gələn məlumatı göstərir.

### 5.2. Endpoint-lər

#### `POST /api/matches/{matchId}/start-live`

```json
// request
{
  "style": 1,              // TacticalStyle: 0/1/2
  "formation": "4-4-2",
  "speedMultiplier": 1     // 1, 2 və ya 4
}

// 200 OK → LiveState (minute: 0, status: 0)
```

Cavab gəldiyi andan matç **artıq işləyir**. Ona görə SignalR-a **bu çağırışdan ƏVVƏL** qoşulun (bax 5.4), yoxsa ilk dəqiqələri qaçıra bilərsiniz.

Xətalar:
- `409 "Only your own team's match can be played live."` — başqa matç
- `409 "This match has already been played."` — artıq oynanıb
- `409 "Live matches are only available in Manager Mode."` — God Mode

#### `GET /api/matches/{matchId}/live`
Cari vəziyyət (`LiveState`). Matç bitibsə və ya başlamayıbsa `404`.

> Bu, SignalR bağlantısı qırılanda **vəziyyəti bərpa etmək** üçün faydalıdır.

#### `PATCH /api/matches/{matchId}/tactics` — taktiki müdaxilə

Üç şeyi ayrı-ayrı və ya birlikdə dəyişə bilər. İstəmədiyiniz sahəni `null` göndərin:

```json
// yalnız üslub dəyişmək
{ "style": 2, "formation": null, "substitution": null }

// yalnız oyunçu əvəz etmək
{
  "style": null,
  "formation": null,
  "substitution": {
    "playerOutId": "a2fbf14e-...",   // meydanda olan (isStarter: true)
    "playerInId": "a49b7dcd-..."     // skamyada olan (isStarter: false)
  }
}

// hamısını birdən
{ "style": 2, "formation": "3-4-3", "substitution": { ... } }
```

Cavab: yenilənmiş `LiveState`.

**Əvəzetmə qaydaları** (pozulsa `409` + izah gəlir):
- Maksimum **5 əvəzetmə**
- Çıxan oyunçu meydanda olmalıdır (`isStarter: true`)
- Girən oyunçu skamyada olmalıdır (`isStarter: false`)
- Hər ikisi istifadəçinin komandasından olmalıdır

**Taktikanın real təsiri var:**
- `Attacking` → hücum reytinqi +12%, müdafiə −12%
- `Defensive` → müdafiə +12%, hücum −12%

Bu, matçın **qalan dəqiqələrinə** dərhal təsir edir. Güclü oyunçunu meydana buraxmaq da komandanın reytinqini artırır.

> **Qeyd:** `formation` hazırda sadəcə **saxlanılır və geri qaytarılır** — simulyasiya nəticəsinə təsir etmir. UI-da göstərə bilərsiniz, amma real təsiri yalnız `style` və əvəzetmələrin var.

#### `POST /api/matches/{matchId}/pause` və `/resume`
Body lazım deyil. Matçı dayandırır/davam etdirir. Cavab: `LiveState`.

#### `POST /api/matches/{matchId}/speed`
```json
{ "speedMultiplier": 4 }   // 1, 2 və ya 4 (kənar dəyərlər avtomatik məhdudlaşdırılır)
```
Matç gedərkən sürət dəyişə bilər.

#### `POST /api/matches/{matchId}/finish`
Qalan bütün dəqiqələri bir anda hesablayıb matçı bitirir ("Skip to end"). Cavab: yekun `LiveState`.

### 5.3. Matç bitəndə nə olur

Backend avtomatik olaraq:
1. Nəticəni və bütün hadisələri DB-yə yazır
2. `match.isPlayed = true` edir
3. **Liqanın həftəsini irəlilədir**
4. Sessiyanı yaddaşdan silir (`GET /live` artıq `404` verir)

Frontend `MatchFinished` hadisəsini alanda cədvəli və fixture-ları yeniləməlidir (`refetch`).

### 5.4. SignalR inteqrasiyası

```bash
npm install @microsoft/signalr
```

**Hub URL:** `http://localhost:5199/hubs/match`

⚠️ **Token query string-də göndərilir** (`access_token`), header-də yox — çünki WebSocket header dəstəkləmir.

```ts
import * as signalR from "@microsoft/signalr";

export function createMatchConnection(token: string) {
  return new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5199/hubs/match", {
      accessTokenFactory: () => token,   // avtomatik ?access_token=... əlavə edir
    })
    .withAutomaticReconnect()
    .build();
}
```

**Server-dən gələn hadisələr:**

| Hadisə | Payload | Nə vaxt |
|---|---|---|
| `Tick` | `LiveState` | Hər oyun dəqiqəsində |
| `MatchEvent` | `LiveEvent` | Qol və ya əvəzetmə baş verəndə |
| `TacticsUpdated` | `LiveState` | Taktika dəyişəndə |
| `StatusChanged` | `LiveState` | Pause/resume/sürət dəyişəndə |
| `MatchFinished` | `LiveState` | Matç bitəndə |

**Client-dən çağırılan metodlar:**

| Metod | Arqument | Təsvir |
|---|---|---|
| `JoinMatch` | `matchId: string` | Matçın yayım qrupuna qoşulur |
| `LeaveMatch` | `matchId: string` | Qrupdan çıxır |

> `Tick` onsuz da tam `LiveState` göndərir, ona görə `MatchEvent`-i əsasən **animasiya/bildiriş** üçün istifadə edin (məs. "GOL!" effekti).

### 5.5. Tam işlək React nümunəsi

```tsx
import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { LiveState, LiveEvent } from "../types/api";

const API = "http://localhost:5199";

export function useLiveMatch(matchId: string, token: string) {
  const [state, setState] = useState<LiveState | null>(null);
  const [toast, setToast] = useState<LiveEvent | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    let cancelled = false;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API}/hubs/match`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.on("Tick", (s: LiveState) => setState(s));
    connection.on("TacticsUpdated", (s: LiveState) => setState(s));
    connection.on("StatusChanged", (s: LiveState) => setState(s));
    connection.on("MatchFinished", (s: LiveState) => setState(s));
    connection.on("MatchEvent", (e: LiveEvent) => setToast(e));

    (async () => {
      await connection.start();
      await connection.invoke("JoinMatch", matchId);
      if (cancelled) return;

      // Qoşulduqdan SONRA başlat ki, ilk tick-ləri qaçırmayaq.
      const res = await fetch(`${API}/api/matches/${matchId}/start-live`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ style: 1, formation: "4-4-2", speedMultiplier: 1 }),
      });
      if (res.ok) setState(await res.json());
    })();

    return () => {
      cancelled = true;
      connection.invoke("LeaveMatch", matchId).catch(() => {});
      connection.stop();
    };
  }, [matchId, token]);

  const call = async (path: string, body?: unknown, method = "POST") => {
    const res = await fetch(`${API}/api/matches/${matchId}/${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.ok) setState(await res.json());
    else throw new Error((await res.json()).detail ?? "Xəta baş verdi");
  };

  return {
    state,
    toast,
    setStyle: (style: number) =>
      call("tactics", { style, formation: null, substitution: null }, "PATCH"),
    substitute: (playerOutId: string, playerInId: string) =>
      call("tactics", { style: null, formation: null, substitution: { playerOutId, playerInId } }, "PATCH"),
    pause: () => call("pause"),
    resume: () => call("resume"),
    setSpeed: (speedMultiplier: number) => call("speed", { speedMultiplier }),
    finishNow: () => call("finish"),
  };
}
```

İstifadəsi:

```tsx
function LiveMatchScreen({ matchId, token }: { matchId: string; token: string }) {
  const { state, toast, setStyle, pause, resume, setSpeed, finishNow } =
    useLiveMatch(matchId, token);

  if (!state) return <div>Yüklənir…</div>;

  return (
    <div>
      <h2>
        {state.homeTeamName} {state.homeGoals} – {state.awayGoals} {state.awayTeamName}
      </h2>
      <p>{state.minute}′</p>

      {toast && <div className="toast">{toast.description}</div>}

      <button onClick={() => setStyle(0)}>Defensiv</button>
      <button onClick={() => setStyle(1)}>Balanslı</button>
      <button onClick={() => setStyle(2)}>Hücumçu</button>

      <button onClick={state.status === 1 ? resume : pause}>
        {state.status === 1 ? "Davam et" : "Dayandır"}
      </button>

      <button onClick={() => setSpeed(1)}>1x</button>
      <button onClick={() => setSpeed(2)}>2x</button>
      <button onClick={() => setSpeed(4)}>4x</button>

      <button onClick={finishNow}>Matçı bitir</button>

      <p>Əvəzetmə qalıq: {state.substitutionsRemaining}</p>

      <ul>
        {state.events.map((e, i) => (
          <li key={i}>{e.minute}′ — {e.description}</li>
        ))}
      </ul>
    </div>
  );
}
```

---

## 6. Tipik istifadəçi axını (ekranlar)

```
┌─ Login / Register ─────────────────────────────┐
│  POST /api/auth/login → token-i saxla          │
└────────────────────┬───────────────────────────┘
                     ▼
┌─ Liqalar siyahısı ─────────────────────────────┐
│  GET /api/leagues                              │
│  → mövcud liqa seç, və ya "Yeni liqa" bas      │
└────────────────────┬───────────────────────────┘
                     ▼
┌─ Yeni liqa ────────────────────────────────────┐
│  Mövsüm adı + rejim seçimi                     │
│  POST /api/leagues                             │
└────────────────────┬───────────────────────────┘
                     ▼
┌─ Dashboard ────────────────────────────────────┐
│  GET /api/leagues/{id}            → başlıq     │
│  GET /api/leagues/{id}/standings  → cədvəl     │
│  GET /api/leagues/{id}/fixtures?week=N         │
│  GET /api/leagues/{id}/teams      → heyət      │
│                                                │
│  [Simulate Week] düyməsi                       │
│  POST /api/leagues/{id}/simulate-week          │
└────────────────────┬───────────────────────────┘
                     ▼
        pendingUserMatch === null ?
                     │
         ┌───────────┴────────────┐
        Hə                       Yox
         │                        │
         ▼                        ▼
   Nəticələri göstər     ┌─ Canlı matç ekranı ──────┐
   Dashboard-a qayıt     │  SignalR + start-live    │
                         │  Taktika / əvəzetmə      │
                         │  MatchFinished → refetch │
                         └──────────────────────────┘
```

---

## 7. Ekran-ekran tövsiyələr

### 7.1. Dashboard
- **Başlıq:** `seasonName`, `Həftə {currentWeek} / {totalWeeks}`
- Manager Mode-dursa, istifadəçinin komandasını vurğulayın (`userTeamId` ilə müqayisə edin)
- **Cədvəl:** `standings` — ilk 1-2 yeri yaşıl, sonuncunu qırmızı fonla vurğulaya bilərsiniz
- **Fixture:** cari həftənin 4 matçı. `isUserMatch: true` olanı fərqli göstərin
- `isFinished: true` olduqda "Simulate" düyməsini gizlədin, "Mövsüm bitdi" göstərin

### 7.2. Komanda heyəti (`teams`)
- `players` 19 nəfərdir — `isStarter` ilə iki qrupa ayırın: **İlk 11** və **Ehtiyat**
- Oyunçu kartında 6 atributu göstərin, `overall`-ı böyük yazın
- `position` ilə rəng kodlayın (GK sarı, DEF mavi, MID yaşıl, FWD qırmızı kimi)

### 7.3. Canlı matç ekranı
- **Yuxarıda:** hesab + dəqiqə (böyük). Dəqiqə üçün progress bar (`minute / 90`) yaxşı görünür
- **Ortada:** hadisə lenti (`events`) — yeni gələn animasiya ilə
- **Aşağıda:** taktika paneli — 3 üslub düyməsi, sürət (1x/2x/4x), pause, əvəzetmə
- **Əvəzetmə modalı:** meydandakı 11 və skamyadakı 8 oyunçunu yan-yana göstərin, `substitutionsRemaining` sayğacını görünən yerdə saxlayın
- `MatchEvent` gələndə qol üçün böyük "GOL!" animasiyası göstərin
- `status === 2` (Finished) olduqda idarəetmə düymələrini gizlədin, "Dashboard-a qayıt" göstərin

---

## 8. Backend-i işə salmaq

```bash
# 1. Konfiqurasiya (yalnız bir dəfə)
#    Sirlər git-ə düşmür, ona görə nümunə faylı köçürüb doldurun:
cp src/TactIQ.API/appsettings.Development.json.example \
   src/TactIQ.API/appsettings.Development.json

# 2. PostgreSQL (Docker)
docker compose up -d

# 3. API
dotnet run --project src/TactIQ.API --urls "http://localhost:5199"

# 4. Yoxla
#    http://localhost:5199/swagger
```

Verilənlər bazası migration-ları API başlayanda **avtomatik tətbiq olunur**.

> 1-ci addımı atlasanız, API işə düşməyib aydın xəta verəcək: `Jwt:Key is not configured…`

---

## 9. Hazırda OLMAYAN şeylər

Frontend planlaşdırarkən nəzərə alın — bunlar backend-də hələ yoxdur:

| Yoxdur | Qeyd |
|---|---|
| Komandanı sonradan dəyişmək | Liqa yaradılanda təyin olunur, dəyişmək üçün endpoint yoxdur |
| Logo şəkilləri | `logoUrl` həmişə `null` — placeholder/initial göstərin |
| Qol vuran oyunçunun adı | `MatchEvent.playerId` var, amma **doldurulmur** — yalnız komanda bilinir |
| Sarı/qırmızı vərəqə | Enum-da var, generasiya olunmur |
| Oyunçu statistikası | Qol kralı, assist və s. yoxdur |
| Mövsümü yenidən başlatmaq | Yalnız liqanı silib yenisini yaratmaq olar |
| Canlı matçın server restart-a davamlılığı | Sessiya yaddaşdadır — server yenidən başlasa, davam edən matç itir |

Bunlardan hansısa lazım olsa, backend-ə əlavə edilə bilər — soruşun.
