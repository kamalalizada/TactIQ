# ⚽ TactIQ — Football Simulation Engine — Architectural & Technical Specification

Bu sənəd **.NET (C#) Web API** və **React** əsaslı, 8 komandalıq simulyasiya və turnir idarəetmə sisteminin tam texniki, riyazi və arxitektura planını əks etdirir.

---

## 0. Qərar Qeydləri (Decisions Log)

Bu layihə üçün müəyyənləşdirilmiş texniki qərarlar:

| Mövzu | Qərar |
|---|---|
| Verilənlər bazası | **PostgreSQL** (EF Core Code-First) |
| Auth | **Sadə auth** əlavə olunacaq (ASP.NET Identity / JWT — bax bölmə 6.3) |
| Layihə strukturu | **Çoxlu .csproj — Clean Architecture** (Domain / Application / Infrastructure / API) |
| Canlı matç idarəetməsi | **İki fərqli rejim, matçın kim iştirak etməsindən asılı olaraq (bax bölmə 4.5):** (1) **İstifadəçinin öz komandasının matçı** — həqiqi **server-side tick-based simulyasiya**: matç 1→90 dəqiqə serverdə real vaxtda (canlı) irəliləyir, istifadəçi istənilən dəqiqədə taktika/formasiya/oyunçu dəyişikliyi edə bilər və bu, qalan hissənin nəticəsinə təsir edir. (2) **Bütün digər matçlar** (God Mode daxil, Manager Mode-da istifadəçi iştirak etməyən matçlar) — backend nəticəni bir dəfəyə tam hesablayır, ani/dərhal göstərilir, heç bir tick-loop yoxdur. |

**Nəticə:** İki ayrı simulyasiya yolu var: (A) sadə/ani `MatchEngine.SimulateInstant()` — yekun hesab + `MatchEvent[]` birbaşa hesablanır, digər 7 komanda üçün işlədilir; (B) **`LiveMatchSession`** — istifadəçinin komandası üçün, serverdə stateful `BackgroundService` daxilində saxlanılan davam edən simulyasiya, hər tick-də cari taktika/heyət məlumatına əsasən yenidən qiymətləndirilir və SignalR ilə frontend-ə yayımlanır. Bax bölmə 4.5 və 4.6.

---

## 1. Sistem Rejimləri (Game Modes)

Sistem istifadəçiyə başlanğıcda 2 rejim seçimi təqdim edir:

1. **God Mode (Avtonom Rejim):**
   * İstifadəçi heç bir komandanı idarə etmir.
   * Bütün matçlar və həftələr mərkəzi simulyasiya mühərriki tərəfindən avtomatik və ya addım-addım icra olunur.
   * Nəticələr dərhal (ani) hesablanıb göstərilir — dəqiqə-dəqiqə playback yoxdur.
   * İstifadəçi yalnız nəticələri və turnir cədvəlini izləyir.

2. **Manager Mode (Menecer Rejimi):**
   * İstifadəçi 8 komandadan birini seçir və onun meneceri olur.
   * İstifadəçi öz komandasının heyətini, atributlarını və taktikalarını nəzərdən keçirə bilir.
   * Qalan 7 komanda AI/Avto-simulyasiya tərəfindən idarə olunur və nəticələri ani göstərilir.
   * **İstifadəçinin öz komandasının matçı** oynanıldıqda frontend-də 1→90-cı dəqiqəyə qədər timer əsaslı "canlı" playback baş verir (qollar baş verdiyi dəqiqədə ekranda görünür) — bax bölmə 0 və 4.5.

---

## 2. Domain Data Modelləri (Entities)

### 2.1. Player (Oyunçu)
Oyunçunun gücünü müəyyən edən 6 əsas atributu var (1–99 arası):
* **Pace (PAC):** Sürət və təcil.
* **Shooting (SHO):** Zərbə gücü və dəqiqliyi.
* **Passing (PAS):** Pas və oyun quruculuğu.
* **Dribbling (DRI):** Topa nəzarət və texnika.
* **Defending (DEF):** Top alma və mövqe seçimi.
* **Physical (PHY):** Güc və dözümlülük.

* **Mövqelər (Positions):** `GK` (Qapıçı), `DEF` (Müdafiə), `MID` (Yarımmüdafiə), `FWD` (Hücum).
* **Overall Rating (OVR):**
  $$\text{OVR} = \frac{\text{PAC} + \text{SHO} + \text{PAS} + \text{DRI} + \text{DEF} + \text{PHY}}{6}$$

### 2.2. Team (Komanda)
* **Metadata:** `Id`, `LeagueId` (hər komanda dəqiq bir liqaya aiddir — bax 2.3, 2.7), `Name`, `LogoUrl`.
* **Players:** 11 əsas oyunçudan ibarət siyahı.
* **Calculated Ratings:**
  * $\text{AttackRating}$: `FWD` və `MID` oyunçularının (SHO + PAC + DRI) ortalaması.
  * $\text{DefenseRating}$: `DEF`, `GK` və `MID` oyunçularının (DEF + PHY + PAC) ortalaması.
  * $\text{TeamOverall}$: Bütün oyunçuların OVR ortalaması.

### 2.3. League / Season (Liqa)
* **Metadata:** `Id`, `SeasonName`, `TeamCount` (default `8`, MVP-də dəyişməz saxlanılır — bax bölmə 3.3), `CurrentWeek` (1 → $2 \times (\text{TeamCount}-1)$).
* **GameMode:** `GodMode` | `ManagerMode`.
* **UserTeamId:** İstifadəçinin seçdiyi komandanın ID-si (nullable).
* **UserId:** Liqanın sahibi olan istifadəçi.
* **Teams:** `List<Team>` — hər `Team` yalnız bir `League`-ə aiddir (`Team.LeagueId`, bax 2.2). Bir istifadəçi bir neçə `League` yarada bilər (məs. bir 8-komandalıq, sonra ayrıca başqa bir liqa) — hər `League` öz komandaları, matçları və cədvəli ilə tam izolə olunub (bax 2.7).

### 2.4. Match (Matç)
* **Attributes:** `Id`, `LeagueId`, `WeekNumber`, `HomeTeamId`, `AwayTeamId`, `HomeGoals`, `AwayGoals`, `IsPlayed`.
* **Events:** `List<MatchEvent>` — matçın tam dəqiqə-dəqiqə jurnalı (aşağıda 2.5).

### 2.5. MatchEvent (Matç Hadisəsi)
Matç zamanı yaranan, DB-də saxlanılan dəqiqə-əsaslı hadisə jurnalı (qol və s.). Bütün matçlar üçün (ani hesablanan da, canlı gedən də) eyni modeldə saxlanılır.

* **Attributes:** `Id`, `MatchId`, `Minute` (1–90+), `EventType` (`Goal`, `YellowCard`, `RedCard` — MVP-də əsasən `Goal`), `TeamId`, `PlayerId` (nullable, gələcək genişlənmə üçün), `Description`.
* **Ani hesablanan matçlar** üçün bu siyahı bir dəfəyə, tam yaradılır (bax 4.1-4.4).
* **Canlı (istifadəçinin komandası) matçlar** üçün bu siyahı tick-tick, matç davam etdikcə DB-yə əlavə olunur (bax 4.6).

### 2.6. TeamTactics (Taktika/Heyət Snapshot-u) — YENİ
İstifadəçinin canlı matç zamanı etdiyi taktiki dəyişiklikləri saxlamaq üçün.

* **Attributes:** `Id`, `MatchId`, `TeamId`, `Minute` (dəyişikliyin tətbiq olunduğu dəqiqə), `Formation` (məs. `"4-4-2"`), `PlayerChanges` (JSON: çıxan/giriş oyunçu ID-ləri), `TacticalStyle` (`Balanced`, `Defensive`, `Attacking` — bax 4.6).
* Hər canlı matç ən azı bir başlanğıc `TeamTactics` qeydi ilə başlayır (`Minute = 0`); istifadəçi matç ərzində istədiyi qədər yeni qeyd əlavə edə bilər.

### 2.7. Çoxlu Liqa Dəstəyi (Multi-League) — YENİ
Sistem başlanğıcdan belə qurulur ki, **bir istifadəçi bir neçə `League` yarada bilsin** (paralel və ya ardıcıl):

* `League.UserId` ilə hər liqa məhz bir istifadəçiyə aiddir; `GET /api/leagues` istifadəçinin bütün liqalarının siyahısını qaytarır.
* Bütün digər sorğular (`standings`, `simulate-week`, `fixtures`) `leagueId`-ni URL/parametr kimi qəbul edir — heç bir yerdə "cari tək liqa" fərziyyəsi olmur (single-tenant qlobal state yoxdur).
* Bir liqa silinsə/bitsə belə, digərləri təsirlənmir — `Team`, `Match`, `MatchEvent`, `TeamTactics` hamısı `LeagueId`/`MatchId` zənciri ilə izolə olunub.
* **Komanda sayı (N):** MVP-də `TeamCount = 8` bütün liqalar üçün sabitdir (hardcode). Round-Robin alqoritmi (bölmə 3) riyazi olaraq artıq istənilən cüt $N$ üçün ümumidir (sadəcə $N-1$ və $N/2$ düsturlarına əsaslanır) — ona görə gələcəkdə 20 (və ya istənilən cüt) komandalı liqaya keçid, alqoritmin özünü dəyişmədən, yalnız `TeamCount`-un hardcode-dan çıxarılıb `League` yaradılışında parametr kimi qəbul edilməsini tələb edir (bax bölmə 3.3).

---

## 3. Cədvəl Generatoru (Round-Robin Rotasiya Alqoritmi)

Liqada **8 komanda** iştirak edir (MVP-də sabit N, bax 3.3). Matçlar **ev-səfər** sistemilə keçirilir.

### 3.1. Riyazi Hesablama
* **1 Devrədəki Tur Sayı:** $N - 1 = 8 - 1 = 7\text{ həftə}$.
* **Ümumi Mövsüm Həftəsi:** $2 \times (N - 1) = 14\text{ həftə}$.
* **Hər Həftədəki Matç Sayı:** $\frac{N}{2} = 4\text{ matç}$.
* **Mövsümdəki Ümumi Matç Sayı:** $14 \times 4 = 56\text{ matç}$.

### 3.2. Rotasiya Məntiqi (Berger Tables / Round-Robin)
1. 1-ci komanda massivdə sabitlənir.
2. Qalan 7 komanda hər tur saat əqrəbi istiqamətində 1 addım fırlanır.
3. **1–7-ci həftələr (1-ci Devrə):** Əldə olunan cütlüklər formalaşdırılır.
4. **8–14-cü həftələr (2-ci Devrə):** 1-ci devrədəki matçlar eynilə götürülür, sadəcə ev və səfər komandalarının yerləri tərsinə dəyişdirilir ($\text{Home} \leftrightarrow \text{Away}$).

### 3.3. Gələcək Genişlənmə: Dəyişkən Komanda Sayı (N)
Yuxarıdakı bütün düsturlar $N=8$ üçün deyil, **istənilən cüt $N$** üçün ümumidir (Berger Tables alqoritmi standart olaraq belədir). Kod səviyyəsində bu, `IScheduleGenerator.Generate(List<Team> teams)` funksiyasının `teams.Count`-u oxuyub $N$ kimi istifadə etməsi deməkdir — MVP-də sadəcə `League` yaradılışı UI/API-da `TeamCount` seçimini vermir və daxili olaraq 8 fərz edir. 20-komandalıq (və ya istənilən cüt ölçülü) liqaya keçid üçün lazım olan tək şey: (1) `League` yaradılışına `TeamCount` parametrinin əlavə olunması, (2) test/seed məlumatının uyğun sayda komanda təmin etməsi. Alqoritmin, `Match`/`MatchEvent` modelinin və Standings Engine-in **heç biri dəyişmir**.

> Qeyd: Tək məhdudiyyət — komanda sayı **cüt** olmalıdır (klassik round-robin cütlük tələb edir); tək N halında "bye week" (istirahət həftəsi) məntiqi əlavə edilməlidir, bu MVP-nin əhatəsindən kənardır.

---

## 4. Simulyasiya Mühərriki (Match Engine Məntiqi)

Matçların nəticəsi subyektiv və təsadüfi deyil, riyazi ehtimal modeli ilə hesablanır. Əsas riyazi modeli (Addım 1–4) **hər iki simulyasiya rejimi** (ani və canlı) paylaşır — fərq yalnız bunun **nə vaxt/necə** icra olunmasındadır:
* **Ani rejim (4.1–4.4):** bütün addımlar bir funksiya çağırışında, matçın əvvəlindən sonuna qədər tək dəfəyə icra olunur.
* **Canlı rejim (4.6):** eyni riyazi model, hər tick-də (hər simulyasiya dəqiqəsi üçün) kiçildilmiş formada təkrar-təkrar icra olunur və cari `TeamTactics` vəziyyətindən asılı olaraq nəticə dəyişə bilər.

```
[Komanda Gücləri] + [Home Advantage] + [Forma (Luck)] ──► [Effective Ratings] ──► [xG Hesablanması] ──► [Poisson/Random Goals] ──► [Qollara Dəqiqə Təyini] ──► [MatchEvent[] + Yekun Hesab]
```

### Addım 1: Meydan Sahibi Üstünlüyü (Home Advantage)
Ev sahibinin hücum və müdafiə reytinqlərinə kiçik psixoloji/azarkeş dəstəyi bonusu əlavə edilir:
$$\text{HomeAttack} = \text{Team.AttackRating} \times 1.04$$
$$\text{HomeDefense} = \text{Team.DefenseRating} \times 1.04$$

### Addım 2: Günün Forması və Şans Faktoru (Variance / RNG)
Hər matç başladıqda hər iki komanda üçün $-8.0$ ilə $+8.0$ arasında təsadüfi `FormModifier` ($F$) seçilir. Bu, güclü komandaların zəif günündə olmasını və zəif komandaların sürpriz etməsini təmin edir:
$$\text{EffectiveAttack}_{\text{Home}} = \text{HomeAttack} + F_{\text{Home}}$$
$$\text{EffectiveDefense}_{\text{Away}} = \text{AwayDefense} + F_{\text{Away}}$$

### Addım 3: Gözlənilən Qol ($xG$) Modelləşdirilməsi
Ev və səfər komandalarının qol vurma ehtimalları qarşılıqlı xətt müqayisəsi ilə hesablanır:

$$\text{xG}_{\text{Home}} = \left( \frac{\text{EffectiveAttack}_{\text{Home}}}{\text{EffectiveDefense}_{\text{Away}}} \right) \times \text{BaseGoalFactor}\quad (\text{BaseGoalFactor} \approx 1.35)$$

$$\text{xG}_{\text{Away}} = \left( \frac{\text{EffectiveAttack}_{\text{Away}}}{\text{EffectiveDefense}_{\text{Home}}} \right) \times \text{BaseGoalFactor}$$

### Addım 4: Yekun Qol Sayının Müəyyən Edilməsi
Tapılmış $xG$ göstəricisi Poisson paylanması əsasında diskret tam ədədlərə (0, 1, 2, 3, ...) çevrilir və matçın yekun hesabı (`HomeGoals`, `AwayGoals`) müəyyən olunur.

### Addım 5: Qollara Dəqiqə Təyini (MatchEvent Generasiyası)
Yekun qol sayı müəyyən olunduqdan sonra, hər qol üçün 1–90 aralığında (əlavə vaxt nəzərə alınmadan MVP-də) təsadüfi/çəkilmiş bir dəqiqə seçilir (məs. uniform random və ya ikinci yarıda bir qədər yüksək sıxlıq). Bu dəqiqələr xronoloji sıralanır və hər biri üçün bir `MatchEvent` (`Goal`) yaradılır və `TeamId` təyin olunur. Bütün `MatchEvent`-lər matçla birlikdə DB-yə yazılır. (Bu addım yalnız **ani rejimdə** işlədilir; canlı rejimdə qollar tick-tick, real vaxtda yaranır — bax 4.6.)

### 4.5. Hansı Matç Hansı Rejimlə Simulyasiya Olunur
* **God Mode-dakı bütün matçlar** və **Manager Mode-da istifadəçinin komandası iştirak ETMƏYƏN matçlar:** **Ani rejim** (4.1–4.4) — backend `POST /simulate-week` çağırışında bütün bu matçları tam hesablayıb DB-yə yazır, frontend nəticəni dərhal göstərir.
* **Manager Mode-da istifadəçinin öz komandasının matçı:** **Canlı rejim** (4.6) — bu matç `simulate-week` zamanı avtomatik oynanmır, ayrıca bir "Start Match" hərəkəti ilə canlı sessiya kimi başladılır.

### 4.6. Canlı Rejim — Server-Side Tick-Based Simulyasiya (Live Match Session)

Yalnız istifadəçinin öz komandasının iştirak etdiyi matç üçün işləyir. İstifadəçi matçı istənilən vaxt fasiləyə vermədən izləyir və istənilən dəqiqədə (məs. 23-cü, 61-ci dəq.) taktika/formasiya/oyunçu dəyişikliyi edə bilər; bu dəyişiklik dərhal davam edən simulyasiyaya təsir edir.

**Axın:**
1. **Başlatma:** İstifadəçi `POST /api/match/{id}/start-live` çağırır. Backend `LiveMatchSession`-ı (in-memory state, `MatchId` açarı ilə) yaradır: `CurrentMinute = 0`, hər iki komandanın başlanğıc `TeamTactics`-i (bölmə 2.6), boş `MatchEvent` siyahısı.
2. **Tick Engine:** Server tərəfdə `IHostedService`/`BackgroundService` (və ya `System.Threading.Timer`) hər real ~1 saniyədə (konfiqurasiya oluna bilər, məs. x1/x2/x4 sürət) `CurrentMinute`-u 1 artırır və o dəqiqə üçün mini-simulyasiya addımı icra edir:
   * Cari `TeamTactics` (defensiv/hücumcul stil, aktiv 11 nəfər) əsasında **Effective Attack/Defense** yenidən hesablanır (bölmə 4-ün riyazi modelinin kiçildilmiş, dəqiqəlik versiyası — məs. həmin dəqiqədə qol ehtimalı $p = \frac{\text{xG}_{\text{per-90}}}{90}$ kimi bölünür).
   * Həmin dəqiqə üçün qol baş verib-vermədiyi təsadüfi ədədlə yoxlanılır; baş verərsə, yeni `MatchEvent` (`Goal`) yaradılıb DB-yə yazılır və `HomeGoals`/`AwayGoals` yenilənir.
3. **Yayım:** Hər tick-in nəticəsi (cari dəqiqə, hesab, yeni event varsa) SignalR Hub (`MatchHub`) vasitəsilə frontend-ə push olunur.
4. **Taktiki müdaxilə:** İstifadəçi istənilən anda `PATCH /api/match/{id}/tactics` çağıra bilər (yeni formasiya, oyunçu dəyişikliyi, üslub: `Defensive`/`Balanced`/`Attacking`). Backend yeni bir `TeamTactics` qeydi (`Minute = CurrentMinute`) yaradır və `LiveMatchSession`-dakı aktiv taktikanı dərhal yeniləyir — növbəti tick-lər bu yeni dəyərlərlə hesablanır. Məs. `Defensive` üslub `DefenseRating`-ə bonus verib `AttackRating`-i azalda bilər (əks halda `Attacking`).
5. **Bitmə:** `CurrentMinute == 90` (+ıxtiyari əlavə vaxt) olduqda session bağlanır, `Match.IsPlayed = true` təyin olunur, yekun hesab və tam `MatchEvent[]` DB-də qalır, `LiveMatchSession` yaddaşdan silinir.

**Vacib qeyd (Recovery/Idarəetmə):** `LiveMatchSession` in-memory saxlanıldığından, server restart olarsa aktiv canlı matç itə bilər — MVP üçün qəbul edilən risk (tək-istifadəçili lokal layihə). Əgər gələcəkdə lazım olarsa, session vəziyyəti dövri olaraq DB-yə snapshot edilə bilər.

---

## 5. Dinamik Turnir Cədvəli (Standings Engine)

Turnir cədvəli verilənlər bazasında statik saxlanılmır; `IsPlayed = true` olan matçların aggregatesi əsasında dinamik hesablanır.

### 5.1. Hesablanan Metriklər
* **P (Played):** Oynanılan matç sayısı.
* **W (Won):** Qələbə sayısı ($3\text{ xal}$).
* **D (Drawn):** Heç-heçə sayısı ($1\text{ xal}$).
* **L (Lost):** Məğlubiyyət sayısı ($0\text{ xal}$).
* **GF (Goals For):** Vurulan qollar.
* **GA (Goals Against):** Buraxılan qollar.
* **GD (Goal Difference):** Top fərqi ($\text{GF} - \text{GA}$).
* **PTS (Points):** $(\text{W} \times 3) + (\text{D} \times 1)$.

### 5.2. Sıralama Xətkeşi (Tie-Breaking Rules)
1. **Xal (PTS)**
2. **Top Fərqi (GD)**
3. **Daha çox vurulan qol (GF)**
4. **Əlifba sırası** (Əgər bütün digər göstəricilər bərabərdirsə)

---

## 6. Texniki Stek və Arxitektura

### 6.1. Backend: .NET 8 / 9 (C# Web API) — Clean Architecture (Çoxlu .csproj)

Solution 4 ayrı layihədən (csproj) ibarət olacaq:

* **`TactIQ.Domain`**
  * Entity-lər: `Player`, `Team`, `League`, `Match`, `MatchEvent`, `TeamTactics`.
  * Enum-lar: `Position`, `GameMode`, `MatchEventType`, `TacticalStyle`.
  * Heç bir asılılığı yoxdur (framework-agnostic, "təmiz" nüvə).

* **`TactIQ.Application`**
  * DTO-lar (`TeamDto`, `StandingsRowDto`, `MatchResultDto`, `MatchEventDto`, `TacticsUpdateDto`, `LiveTickDto` və s.).
  * Servis interfeysləri (`IMatchEngine`, `IScheduleGenerator`, `IStandingsCalculator`, `ILiveMatchSessionManager`).
  * Ani rejim Match Engine məntiqi (4.1–4.4) və Round-Robin cədvəl alqoritmi (bölmə 3) burada implementasiya olunur — DB-dən asılı deyil, sadəcə domain modelləri qəbul edib nəticə qaytarır (asan test olunan, saf funksiyalar).
  * Canlı rejimin (4.6) hesablama nüvəsi (bir tick-in riyaziyyatı) da burada, saf funksiya kimi — `LiveMatchSession`-ın özü (stateful hissə) Infrastructure/API-da yaşayır.

* **`TactIQ.Infrastructure`**
  * EF Core `AppDbContext` (PostgreSQL provider: `Npgsql.EntityFrameworkCore.PostgreSQL`).
  * Repository/DbContext implementasiyaları.
  * ASP.NET Identity inteqrasiyası (auth üçün, bax 6.3) — `IdentityDbContext` və ya ayrıca Identity cədvəlləri.
  * Migrations.

* **`TactIQ.API`**
  * Controller-lər, Swagger/OpenAPI, DI konfiqurasiyası, JWT middleware.
  * `MatchHub` (SignalR) — canlı matç tick-lərinin yayımı üçün.
  * `LiveMatchSessionManager` — aktiv `LiveMatchSession`-ları yaddaşda saxlayan singleton servis + hər aktiv matç üçün tick-i idarə edən `BackgroundService`/timer.
  * Mövcud `Program.cs`/`TactIQ.csproj` bu layihəyə çevriləcək (adı `TactIQ.API` olaraq restructure oluna bilər, ya da mövcud layihə birbaşa API qatı kimi saxlanılıb yanına 3 yeni layihə əlavə edilə bilər — icra zamanı qərarlaşdırılacaq).

* **Verilənlər Bazası:** PostgreSQL (EF Core Code-First, Npgsql).
* **Caching (Opsional, gələcək):** Cari həftənin matçları və cədvəli sürətli oxumaq üçün Redis — MVP-də lazım deyil (PostgreSQL kifayətdir).

### 6.2. Frontend: React + TypeScript + Tailwind CSS
* **State Management:** Zustand və ya React Query (TanStack Query) — API ilə asinxron data sinxronizasiyası üçün.
* **Real-time:** `@microsoft/signalr` client — `MatchHub`-a qoşulub canlı matçın tick-lərini dinləmək üçün.
* **Komponentlər:**
  * `ModeSelector`: God Mode / Manager Mode seçimi.
  * `LoginForm` / `RegisterForm`: sadə auth ekranları (bax 6.3).
  * `StandingsTable`: Xal cədvəli komponenti (canlı sıralama dəyişimi animasiyası ilə).
  * `FixtureWidget`: Cari həftənin matçları və "Next Week / Simulate" düymələri (yalnız istifadəçinin komandası olmayan matçlar üçün — bunlar ani nəticə ilə göstərilir).
  * `LiveMatchView`: **Yalnız** istifadəçinin öz komandasının matçı üçün — SignalR-dan gələn tick/event-ləri real vaxtda göstərən, həmçinin istifadəçiyə taktika/formasiya/oyunçu dəyişikliyi etmək imkanı verən (`PATCH /api/match/{id}/tactics` çağıran) komponent (bölmə 4.6).
  * `TacticsPanel`: Canlı matç zamanı formasiya seçimi, oyunçu dəyişikliyi və taktiki üslub (`Defensive`/`Balanced`/`Attacking`) idarəetməsi.
  * `TeamRoster`: Komanda heyəti, oyunçular və onların 6 atributlu kartları.

### 6.3. Autentifikasiya (Sadə Auth)
* **Yanaşma:** ASP.NET Identity + JWT Bearer token.
* **Əhatə dairəsi (MVP):**
  * Qeydiyyat (`POST /api/auth/register`) və giriş (`POST /api/auth/login`).
  * Hər `League`/mövsüm bir `UserId`-yə bağlanır — beləliklə hər istifadəçi öz simulyasiyasını ayrıca saxlaya bilər.
  * Rol sistemi, email təsdiqi, şifrə bərpası kimi əlavələr **MVP-dən kənar** saxlanılır (lazım olsa Future Scope-a əlavə oluna bilər).
* **Frontend:** JWT `localStorage`-da saxlanılır, `Authorization: Bearer` header ilə API-a göndərilir.

---

## 7. Mərhələli İnkişaf Xəritəsi (Roadmap)

* [x] **Mərhələ 1: Spesifikasiyanın Təsdiqi (Mövcud Sənəd)**
* [x] **Mərhələ 2: Backend Core Structure** ✅
  * Solution-un 4 layihəyə bölünməsi (Domain / Application / Infrastructure / API) + `tests/TactIQ.Application.Tests`.
  * Entity-lərin (`Player`, `Team`, `League`, `Match`, `MatchEvent`, `TeamTactics`) və `AppDbContext`-in (PostgreSQL) yaradılması.
  * Round-Robin alqoritminin unit testlərlə yazılması (8 və 20 komanda üçün təsdiqlənib).
  * Ani rejim `MatchEngine` servisi: nəticə + `MatchEvent[]` generasiyası, unit testlərlə (Poisson/xG modeli daxil).
  * `TeamFactory`: 19 nəfərlik heyət (11 əsas + 8 ehtiyat), real klub adları, mövqeyə görə atribut bias-ı.
* [x] **Mərhələ 3: Auth və API Endpoint-lərinin Açılması** ✅
  * ASP.NET Identity + JWT quraşdırılması (`POST /api/auth/register`, `POST /api/auth/login`).
  * `POST /api/leagues` (Mövsümün və cədvəlin yaradılması, `UserId`-ə bağlı), `GET /api/leagues`, `GET /api/leagues/{id}`, `DELETE /api/leagues/{id}`.
  * `GET /api/leagues/{id}/standings` (Turnir cədvəli), `GET /api/leagues/{id}/teams`, `GET /api/leagues/{id}/fixtures?week=`.
  * `POST /api/leagues/{id}/simulate-week` (İstifadəçinin komandası olmayan matçları ani hesablayır; istifadəçinin öz matçını isə `IsPlayed=false` saxlayıb canlı başlanmasını gözləyir — həftə də o matç bitənə qədər irəliləmir).
  * EF migration (`InitialCreate`) PostgreSQL-ə tətbiq olunub, tam axın end-to-end test edilib.
* [x] **Mərhələ 4: Canlı Matç Mühərriki (Live Match Engine)** ✅
  * `LiveMatchSession` (thread-safe in-memory state) + `LiveMatchService` (tick loop, `ConcurrentDictionary` ilə aktiv sessiyalar).
  * `MatchHub` (SignalR, `/hubs/match`) — `Tick`, `MatchEvent`, `TacticsUpdated`, `StatusChanged`, `MatchFinished` yayımları. JWT query string-dən oxunur (WebSocket header dəstəkləmir).
  * Endpoint-lər: `POST /api/matches/{id}/start-live`, `GET .../live`, `PATCH .../tactics`, `POST .../pause`, `.../resume`, `.../speed`, `.../finish`.
  * Taktiki müdaxilə: üslub (`Defensive`/`Balanced`/`Attacking`), formasiya, **5-ə qədər əvəzetmə** (19 nəfərlik heyətdən) — hamısı matç gedərkən, istənilən dəqiqədə.
  * Sürət nəzarəti (1x/2x/4x), pause/resume, "finish now" (qalan dəqiqələri bir anda hesablayır).
  * Matç bitəndə: nəticə + tam `MatchEvent[]` + `TeamTactics` DB-yə yazılır, liqanın həftəsi avtomatik irəliləyir.
  * 51 unit test (17-si canlı sessiya üçün): taktika təsiri, əvəzetmə qaydaları, pause, fast-forward, 90 dəqiqə limiti.
* [ ] **Mərhələ 5: React Frontend Tərtibatı**
  * Auth ekranları, Dashboard, Cədvəl və Fixture UI dizaynı.
  * `LiveMatchView` + `TacticsPanel` — SignalR ilə canlı matçı izləmək və taktika idarə etmək.
  * API/SignalR inteqrasiyası.
* [ ] **Mərhələ 6: Gələcək Genişlənmələr (Future Scope)**
  * Matç içi əlavə log tipləri (sarı/qırmızı vərəqələr, əvəzetmə animasiyaları).
  * Pley-off / Kubok (Knockout) sisteminin əlavə edilməsi.
  * Oyunçu yorğunluğu (Stamina) və zədələnmə sisteminin simulyasiyaya daxil edilməsi.
  * `LiveMatchSession` vəziyyətinin server restart-a davamlı olması üçün DB-ə snapshot mexanizmi.
  * Redis cache (çoxistifadəçili miqyaslanma lazım olarsa).
