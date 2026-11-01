import { TacticalStyle, type LiveState } from '@/types'

const STYLES = [
  { value: TacticalStyle.Defensive, label: 'Defensive' },
  { value: TacticalStyle.Balanced, label: 'Balanced' },
  { value: TacticalStyle.Attacking, label: 'Attacking' },
] as const

const SPEEDS = [1, 2, 4] as const

export function TacticsPanel({
  state,
  disabled,
  onStyle,
  onSpeed,
  onOpenSubstitution,
  onPause,
  onFinish,
  finishing,
  paused,
}: {
  state: LiveState
  disabled: boolean
  onStyle: (style: TacticalStyle) => void
  onSpeed: (multiplier: number) => void
  onOpenSubstitution: () => void
  onPause: () => void
  onFinish: () => void
  finishing: boolean
  paused: boolean
}) {
  return (
    <div className="space-y-4">
      <div>
        <span className="mb-1.5 block text-xs text-muted">Playing Style</span>
        <div className="grid grid-cols-3 gap-1.5">
          {STYLES.map((style) => (
            <button
              key={style.value}
              type="button"
              disabled={disabled}
              onClick={() => onStyle(style.value)}
              aria-pressed={state.userStyle === style.value}
              className={`rounded border px-2 py-1.5 text-xs font-medium transition-colors disabled:opacity-40 ${
                state.userStyle === style.value
                  ? 'border-accent bg-accent text-white'
                  : 'border-line bg-bg text-muted hover:text-ink'
              }`}
            >
              {style.label}
            </button>
          ))}
        </div>
      </div>


      <div>
        <div className="mb-1.5 flex items-baseline justify-between">
          <span className="text-xs text-muted">Substitutions</span>
          <span className="text-xs text-win">{state.substitutionsRemaining} remaining</span>
        </div>
        <button
          type="button"
          className="w-full rounded bg-accent px-3 py-2 text-xs font-bold tracking-wide text-white uppercase transition-colors hover:bg-accent/85 disabled:cursor-not-allowed disabled:opacity-40"
          disabled={disabled || state.substitutionsRemaining <= 0}
          onClick={onOpenSubstitution}
        >
          Make Substitution
        </button>
      </div>

      <div>
        <span className="mb-1.5 block text-xs text-muted">Speed</span>
        <div className="grid grid-cols-3 gap-1.5">
          {SPEEDS.map((multiplier) => (
            <button
              key={multiplier}
              type="button"
              disabled={disabled}
              onClick={() => onSpeed(multiplier)}
              aria-pressed={state.speedMultiplier === multiplier}
              className={`rounded border px-2 py-1.5 text-xs font-semibold transition-colors disabled:opacity-40 ${
                state.speedMultiplier === multiplier
                  ? 'border-accent bg-accent text-white'
                  : 'border-line bg-bg text-muted hover:text-ink'
              }`}
            >
              {multiplier}x
            </button>
          ))}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-1.5">
        <button
          type="button"
          disabled={disabled}
          onClick={onPause}
          className="rounded border border-line bg-bg px-2 py-2 text-xs font-semibold transition-colors hover:bg-white/5 disabled:opacity-40"
        >
          {paused ? '▶ Resume' : '❙❙ Pause'}
        </button>
        <button
          type="button"
          disabled={disabled || finishing}
          onClick={onFinish}
          className="rounded bg-loss px-2 py-2 text-xs font-semibold text-white transition-colors hover:bg-loss/85 disabled:opacity-40"
        >
          {finishing ? 'Ending…' : '⏭ End Match'}
        </button>
      </div>
    </div>
  )
}
