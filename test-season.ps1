<#
.SYNOPSIS
    Plays a whole TactIQ season against a running API and prints what happened.

.DESCRIPTION
    Creates a throwaway account and league, simulates every week, then reports the
    league table, the scoring charts and the squad damage the season left behind.

.EXAMPLE
    ./test-season.ps1
    ./test-season.ps1 -Mode God -Weeks 5
#>
param(
    [string]$BaseUrl = "http://localhost:5199",
    [ValidateSet("Manager", "God")]
    [string]$Mode = "God",
    [int]$Weeks = 0,
    [switch]$KeepLeague
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Header($text) {
    Write-Host ""
    Write-Host ("=" * 62) -ForegroundColor DarkCyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host ("=" * 62) -ForegroundColor DarkCyan
}

function Invoke-Api($Method, $Path, $Body, $Token) {
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method  = $Method
        Uri     = "$BaseUrl$Path"
        Headers = $headers
    }

    if ($null -ne $Body) {
        $params["Body"] = ($Body | ConvertTo-Json -Depth 6)
        $params["ContentType"] = "application/json"
    }

    Invoke-RestMethod @params
}

# --- Reachability -----------------------------------------------------------
try {
    Invoke-WebRequest -Uri "$BaseUrl/swagger/index.html" -UseBasicParsing -TimeoutSec 5 | Out-Null
}
catch {
    Write-Host "Backend is not answering on $BaseUrl." -ForegroundColor Red
    Write-Host "Start it first:" -ForegroundColor Yellow
    Write-Host '  docker compose up -d' -ForegroundColor Gray
    Write-Host '  dotnet run --project src/TactIQ.API --urls "http://localhost:5199"' -ForegroundColor Gray
    exit 1
}

# --- Account ----------------------------------------------------------------
$email = "probe-$([guid]::NewGuid().ToString('N').Substring(0,8))@tactiq.local"
$auth = Invoke-Api POST "/api/auth/register" @{ email = $email; password = "probe123" }
$token = $auth.token
Write-Host "Signed in as $email" -ForegroundColor DarkGray

# --- League -----------------------------------------------------------------
$gameMode = if ($Mode -eq "Manager") { 1 } else { 0 }
$league = Invoke-Api POST "/api/leagues" @{
    seasonName = "Probe $(Get-Date -Format 'HH:mm:ss')"
    gameMode   = $gameMode
    userTeamId = $null
} $token

$leagueId = $league.id
$teams = Invoke-Api GET "/api/leagues/$leagueId/teams" $null $token
$playersById = @{}
foreach ($t in $teams) { foreach ($p in $t.players) { $playersById[$p.id] = $p } }

$userTeamName = ($teams | Where-Object { $_.id -eq $league.userTeamId }).name

Write-Header "$($league.seasonName)  -  $Mode Mode"
Write-Host "Teams:  $($league.teamCount)     Weeks: $($league.totalWeeks)"
if ($userTeamName) { Write-Host "You manage: $userTeamName" -ForegroundColor Yellow }

Write-Host ""
Write-Host "Squads:" -ForegroundColor White
foreach ($t in ($teams | Sort-Object { -$_.teamOverall })) {
    $mark = if ($t.id -eq $league.userTeamId) { ">" } else { " " }
    "{0} {1,-22} ATK {2,5}  DEF {3,5}  OVR {4,5}" -f $mark, $t.name, $t.attackRating, $t.defenseRating, $t.teamOverall | Write-Host
}

# --- Season -----------------------------------------------------------------
$targetWeeks = if ($Weeks -gt 0) { $Weeks } else { $league.totalWeeks }
$eventTypes = @{
    0 = "GOAL"; 1 = "YELLOW"; 2 = "RED"; 3 = "SUB"
    4 = "INJURY"; 5 = "ASSIST"; 6 = "SAVE"; 7 = "WIDE"
}

$positions = @{ 0 = "GK"; 1 = "DEF"; 2 = "MID"; 3 = "FWD" }

$goalsFor = @{}
$assistsFor = @{}
$cardsFor = @{}
$totalGoals = 0
$totalYellow = 0
$totalRed = 0
$totalInjury = 0
$playedMatches = 0

Write-Header "Season"

for ($week = 1; $week -le $targetWeeks; $week++) {
    $result = Invoke-Api POST "/api/leagues/$leagueId/simulate-week" $null $token

    Write-Host ""
    Write-Host "Week $($result.weekNumber)" -ForegroundColor White

    foreach ($m in $result.simulatedMatches) {
        $playedMatches++
        "  {0,-22} {1}-{2}  {3}" -f $m.homeTeamName, $m.homeGoals, $m.awayGoals, $m.awayTeamName | Write-Host

        foreach ($e in $m.events) {
            switch ($e.eventType) {
                0 { $totalGoals++;  if ($e.playerId) { $goalsFor[$e.playerId] = 1 + $goalsFor[$e.playerId] } }
                1 { $totalYellow++; if ($e.playerId) { $cardsFor[$e.playerId] = 1 + $cardsFor[$e.playerId] } }
                2 { $totalRed++ }
                4 { $totalInjury++ }
            }
            if ($e.assistPlayerId) { $assistsFor[$e.assistPlayerId] = 1 + $assistsFor[$e.assistPlayerId] }

            # Only the moments worth reading appear in the log.
            if ($e.eventType -in 0, 1, 2, 4) {
                '      {0,3}''  {1,-7} {2}' -f $e.minute, $eventTypes[[int]$e.eventType], $e.description | Write-Host -ForegroundColor DarkGray
            }
        }
    }

    if ($result.pendingUserMatch) {
        $p = $result.pendingUserMatch
        Write-Host "  $($p.homeTeamName) vs $($p.awayTeamName)  [yours, play it live]" -ForegroundColor Yellow
        Write-Host "  The week will not advance until this match is played." -ForegroundColor DarkYellow
        break
    }

    if ($result.seasonFinished) { break }
}

# --- Table ------------------------------------------------------------------
Write-Header "Final table"
$standings = Invoke-Api GET "/api/leagues/$leagueId/standings" $null $token

"{0,-3}{1,-22}{2,3}{3,4}{4,4}{5,4}{6,5}{7,5}{8,5}{9,6}" -f "#", "Team", "P", "W", "D", "L", "GF", "GA", "GD", "PTS" | Write-Host -ForegroundColor White
foreach ($r in $standings) {
    $colour = if ($r.teamId -eq $league.userTeamId) { "Yellow" }
              elseif ($r.rank -eq 1) { "Green" }
              elseif ($r.rank -ge $standings.Count - 1) { "DarkRed" }
              else { "Gray" }

    "{0,-3}{1,-22}{2,3}{3,4}{4,4}{5,4}{6,5}{7,5}{8,5}{9,6}" -f `
        $r.rank, $r.teamName, $r.played, $r.won, $r.drawn, $r.lost,
        $r.goalsFor, $r.goalsAgainst, $r.goalDifference, $r.points | Write-Host -ForegroundColor $colour
}

# --- Charts -----------------------------------------------------------------
function Show-Chart($title, $tally, $suffix) {
    Write-Host ""
    Write-Host $title -ForegroundColor White

    if ($null -eq $tally -or $tally.Count -eq 0) {
        Write-Host "  none" -ForegroundColor DarkGray
        return
    }

    $tally.GetEnumerator() |
        Sort-Object Value -Descending |
        Select-Object -First 8 |
        ForEach-Object {
            $p = $playersById[$_.Key]
            if ($p) {
                "  {0,-24} {1,3} {2,-8} {3,-4} OVR {4}" -f $p.name, $_.Value, $suffix, $positions[[int]$p.position], $p.overall | Write-Host
            }
        }
}

Write-Header "Season charts"
Show-Chart "Top scorers" $goalsFor "goals"
Show-Chart "Top assists" $assistsFor "assists"
Show-Chart "Most booked" $cardsFor "cards"

# --- Squad damage -----------------------------------------------------------
Write-Header "Squad condition"
$teams = Invoke-Api GET "/api/leagues/$leagueId/teams" $null $token

$allPlayers = $teams | ForEach-Object { $_.players }
$starters = $allPlayers | Where-Object { $_.isStarter }
$bench = $allPlayers | Where-Object { -not $_.isStarter }

"Starters   avg condition {0,3}   (lowest {1})" -f `
    [int](($starters | Measure-Object condition -Average).Average),
    ($starters | Measure-Object condition -Minimum).Minimum | Write-Host
"Bench      avg condition {0,3}   (lowest {1})" -f `
    [int](($bench | Measure-Object condition -Average).Average),
    ($bench | Measure-Object condition -Minimum).Minimum | Write-Host

$unavailable = $allPlayers | Where-Object { -not $_.isAvailable }
if ($unavailable) {
    Write-Host ""
    Write-Host "Unavailable:" -ForegroundColor White
    foreach ($p in $unavailable) {
        $reason = if ($p.matchesInjured -gt 0) { "injured $($p.matchesInjured) match(es)" }
                  else { "suspended $($p.matchesSuspended) match(es)" }
        "  {0,-24} {1}" -f $p.name, $reason | Write-Host -ForegroundColor DarkYellow
    }
}

# --- Totals -----------------------------------------------------------------
Write-Header "Totals"
if ($playedMatches -gt 0) {
    "Matches simulated   {0}" -f $playedMatches | Write-Host
    "Goals per match     {0:N2}" -f ($totalGoals / $playedMatches) | Write-Host
    "Yellows per match   {0:N2}" -f ($totalYellow / $playedMatches) | Write-Host
    "Reds per match      {0:N3}" -f ($totalRed / $playedMatches) | Write-Host
    "Injuries per match  {0:N3}" -f ($totalInjury / $playedMatches) | Write-Host
}

if (-not $KeepLeague) {
    Invoke-Api DELETE "/api/leagues/$leagueId" $null $token | Out-Null
    Write-Host ""
    Write-Host "Probe league deleted. Pass -KeepLeague to keep it." -ForegroundColor DarkGray
}
else {
    Write-Host ""
    Write-Host "League kept: $leagueId" -ForegroundColor DarkGray
    Write-Host "Token: $token" -ForegroundColor DarkGray
}
