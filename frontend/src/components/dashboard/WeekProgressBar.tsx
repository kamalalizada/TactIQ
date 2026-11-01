interface WeekProgressBarProps {
  currentWeek: number
  totalWeeks: number
}

export function WeekProgressBar({ currentWeek, totalWeeks }: WeekProgressBarProps) {
  const progress = totalWeeks > 0 ? Math.min((currentWeek / totalWeeks) * 100, 100) : 0

  return (
    <div className="mb-5">
      <div className="mb-1.5 flex items-center justify-between text-xs text-muted">
        <span>
          Həftə <span className="font-semibold text-ink">{Math.min(currentWeek, totalWeeks)}</span> /{' '}
          {totalWeeks}
        </span>
        <span className="font-semibold text-ink">{Math.round(progress)}%</span>
      </div>
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-line/50">
        <div
          className="progress-gradient h-full rounded-full transition-all duration-500"
          style={{ width: `${progress}%` }}
        />
      </div>
    </div>
  )
}
