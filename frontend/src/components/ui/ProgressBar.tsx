export function ProgressBar({
  value,
  max = 100,
  tone = 'accent',
  className = '',
}: {
  value: number
  max?: number
  tone?: 'accent' | 'win' | 'muted'
  className?: string
}) {
  const percent = Math.max(0, Math.min(100, (value / max) * 100))

  const tones = {
    accent: 'bg-accent',
    win: 'bg-win',
    muted: 'bg-muted',
  }

  return (
    <div className={`h-1.5 overflow-hidden rounded-full bg-bg ${className}`}>
      <div
        className={`h-full transition-[width] duration-500 ${tones[tone]}`}
        style={{ width: `${percent}%` }}
      />
    </div>
  )
}

/** A labelled 0-99 rating bar, used on team and player cards. */
export function RatingBar({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-center gap-2">
      <span className="w-8 shrink-0 text-[0.65rem] text-muted uppercase">{label}</span>
      <ProgressBar value={value} max={99} className="flex-1" />
      <span className="w-6 shrink-0 text-right text-xs font-semibold">{Math.round(value)}</span>
    </div>
  )
}
