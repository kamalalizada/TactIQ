# TactIQ Frontend — İş Jurnalı

React + TypeScript + Vite. Backend API sənədinə (`http://localhost:5199`) əsasən yazılıb.

---

## Vəziyyət

| Bölmə | Status |
|---|---|
| TypeScript miqrasiyası | ✅ bitdi |
| Auth (avtomatik demo giriş) | ✅ bitdi |
| Liqalar siyahısı / yaratma | ✅ bitdi |
| Dashboard (cədvəl + fixture + simulate) | ✅ bitdi |
| Komanda heyəti ekranı | ✅ bitdi |
| Canlı matç + SignalR | ✅ bitdi |
| Taktika paneli (üslub / formasiya / əvəzetmə) | ✅ bitdi |
| Qol bildirişi (toast) | ✅ bitdi |

---

## İşə salmaq

```bash
# 1. Backend (ayrı terminal)
cd C:\Users\Kamal\TactIQ
docker compose up -d
dotnet run --project src/TactIQ.API --urls "http://localhost:5199"

# 2. Frontend
cd C:\Users\Kamal\TactIQ-Frontend
npm run dev     # http://localhost:5173
```

**Port dəyişsə:** `.env` faylında `VITE_API_URL` düzəlt. Vite proxy `/api` və `/hubs` sorğularını oraya yönləndirir — brauzer tək origin-də qalır, CORS problemi olmur.

```
VITE_API_URL=http://localhost:5199
```

---

## Qovluq quruluşu

```
src/
  api/
    client.ts         fetch wrapper: JWT, 401 idarəsi, ProblemDetails parse
    endpoints.ts      bütün endpoint-lər tipli funksiyalar kimi
  components/
    GoalToast.tsx     "GOL!" bildirişi
    PlayerCard.tsx    6 atributlu oyunçu kartı
    Shell.tsx         header + layout
    StandingsTable.tsx
    States.tsx        Spinner / ErrorNote / EmptyNote
    TacticsPanel.tsx  üslub, formasiya, əvəzetmə
  lib/
    auth.ts           token saxlama + avtomatik demo giriş
    useLiveMatch.ts   SignalR hook
    useManagedSquad.ts idarə olunan komandanın heyətini tapır
  pages/
    LeaguesPage.tsx       liqa siyahısı + yaratma
    LeagueDetailPage.tsx  dashboard
    TeamsPage.tsx         8 komanda, hərəsi 19 oyunçu
    LiveMatchPage.tsx     canlı matç
    LoginPage.tsx         (hazırda keçilir)
  types.ts            backend DTO-larının tipləri
```

---

## Həll edilmiş vacib məqamlar

### 1. Enum-lar rəqəmdir
`types.ts`-də `as const` obyektləri kimi yazılıb (`GameMode.ManagerMode === 1`). String göndərilmir.

### 2. SignalR-a kick-off-dan ƏVVƏL qoşulmaq
Sənəd xəbərdarlıq edir ki, `start-live` cavabı gələndə matç artıq işləyir. Ona görə `useLiveMatch` bir `ready()` promise qaytarır; `LiveMatchPage` kick-off-dan əvvəl onu gözləyir:

```ts
mutationFn: async () => {
  await ready()                    // JoinMatch tamamlanana qədər
  return matchApi.startLive(...)
}
```

Beləliklə ilk dəqiqələr itmir.

### 3. Manager Mode-da həftə irəliləmir
`pendingUserMatch !== null` olanda "Simulyasiya et" düyməsi **deaktiv** olur və əvəzində "Matçı oyna" kartı çıxır.

### 4. Matç bitəndə refetch
`MatchFinished` gələndə backend nəticəni yazır və həftəni irəlilədir. Frontend `leagues`, `league`, `standings`, `fixtures` query-lərini invalidate edir.

### 5. Tick-lərin sırası
Şəbəkə gecikməsində köhnə tick yeni tick-i üstələyə bilər. `useLiveMatch` daha kiçik `minute` dəyərini rədd edir.

### 6. İki fərqli xəta formatı
Auth endpoint-ləri `{"errors": [...]}`, qalanları `ProblemDetails` qaytarır. `client.ts` hər ikisini oxuyur.

### 7. Logo yoxdur
`logoUrl` həmişə `null` — komanda adının ilk hərfi dairə içində placeholder kimi göstərilir.

### 8. `leagueId` ilə birbaşa heyət
`MatchDto` və `LiveStateDto` artıq `leagueId` daşıyır. `useManagedSquad(leagueId)` birbaşa həmin liqanın komandalarını çəkir — əvvəlki "bütün liqaların fikstürlərini gəz" axtarışı silindi.

---

## Müvəqqəti həllər

### Avtomatik demo giriş
Login ekranı hazırda keçilir. Token yoxdursa, tətbiq `demo@tactiq.local` / `demo1234` ilə özü giriş edir (hesab yoxdursa yaradır).

- Kod: `src/lib/auth.ts` → `ensureSignedIn()`
- Login divarını geri qaytarmaq üçün: `App.tsx`-də `RequireAuth` wrapper-ini bərpa et. `/login` marşrutu və `LoginPage` yerindədir, silinməyib.

---

## Backend-dən gözlənilən (bloklamır)

| Nə | Niyə lazımdır |
|---|---|
| Komandanı sonradan dəyişmək endpoint-i | İstifadəçi liqa yaradandan sonra komanda seçə bilsin |
| `MatchEvent.playerId` doldurulması | "Kim qol vurdu" göstərmək üçün |
| Logo URL-ləri | Placeholder əvəzinə real gerb |

---

## Yoxlama

```bash
npx tsc --noEmit -p tsconfig.app.json   # tip yoxlaması
npm run build                            # production build
npm run lint                             # eslint
```

Sonuncu icra: **hamısı təmiz** ✅
