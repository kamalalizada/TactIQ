interface QuickStatsRowProps {
  userRank: number | null
  userPoints: number | null
  userGoalsFor: number | null
  userWon: number | null
  userPlayed: number | null
  totalTeams: number
}

export function QuickStatsRow({
  userRank,
  userPoints,
  userGoalsFor,
  userWon,
  userPlayed,
  totalTeams,
}: QuickStatsRowProps) {
  const played = userPlayed ?? 0
  const won = userWon ?? 0
  const winRate = played > 0 ? Math.round((won / played) * 100) : 0

  const stats = [
    {
      label: 'Cədvəl Yeri',
      value: userRank ? `#${userRank}` : '—',
      sub: userRank ? `${totalTeams} komanda arasında` : '',
      icon: '🏆',
      accent: userRank === 1 ? 'text-draw' : userRank && userRank <= 4 ? 'text-win' : 'text-ink',
    },
    {
      label: 'Xal',
      value: userPoints ?? '—',
      sub: played > 0 ? `${played} matçda` : '',
      icon: '⚡',
      accent: 'text-violet',
    },
    {
      label: 'Vurulan Qol',
      value: userGoalsFor ?? '—',
      sub: played > 0 ? `orta ${(Number(userGoalsFor ?? 0) / played).toFixed(1)}` : '',
      icon: '⚽',
      accent: 'text-win',
    },
    {
      label: 'Qələbə Faizi',
      value: played > 0 ? `${winRate}%` : '—',
      sub: played > 0 ? `${won}/${played} matç` : '',
      icon: '📈',
      accent: winRate >= 60 ? 'text-win' : winRate >= 40 ? 'text-draw' : 'text-loss',
    },
  ]

  return (
    <div className="mb-5 grid grid-cols-2 gap-3 lg:grid-cols-4">
      {stats.map((stat) => (
        <div key={stat.label} className="stat-card">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium tracking-wide text-muted uppercase">
              {stat.label}
            </span>
            <span aria-hidden className="text-lg">
              {stat.icon}
            </span>
          </div>
          <p className={`text-2xl font-black tracking-tight ${stat.accent}`}>{stat.value}</p>
          {stat.sub && <p className="text-xs text-muted">{stat.sub}</p>}
        </div>
      ))}
    </div>
  )
}
