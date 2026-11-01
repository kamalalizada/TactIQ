import { Position, PlayerRole, ROLE_LABELS } from '@/types'

const POSITION_LABELS: Record<Position, string> = {
  [Position.GK]: 'GK',
  [Position.DEF]: 'DEF',
  [Position.MID]: 'MID',
  [Position.FWD]: 'FWD',
}

const POSITION_COLOURS: Record<Position, string> = {
  [Position.GK]: 'bg-pos-gk/15 text-pos-gk border-pos-gk/30',
  [Position.DEF]: 'bg-pos-def/15 text-pos-def border-pos-def/30',
  [Position.MID]: 'bg-pos-mid/15 text-pos-mid border-pos-mid/30',
  [Position.FWD]: 'bg-pos-fwd/15 text-pos-fwd border-pos-fwd/30',
}

export function PositionBadge({ position }: { position: Position }) {
  return (
    <span
      className={`inline-block rounded border px-1.5 py-0.5 text-[0.65rem] font-bold ${POSITION_COLOURS[position]}`}
    >
      {POSITION_LABELS[position]}
    </span>
  )
}

export function Badge({
  children,
  tone = 'neutral',
}: {
  children: React.ReactNode
  tone?: 'neutral' | 'accent' | 'win'
}) {
  const tones = {
    neutral: 'border-line bg-white/5 text-muted',
    accent: 'border-accent/30 bg-accent/15 text-accent',
    win: 'border-win/30 bg-win/15 text-win',
  }

  return (
    <span
      className={`inline-block rounded border px-2 py-0.5 text-[0.65rem] font-semibold tracking-wide uppercase ${tones[tone]}`}
    >
      {children}
    </span>
  )
}

export { POSITION_LABELS }

/** Which line of the side each role belongs to, for colouring its chip. */
const ROLE_LINE_COLOURS: Record<PlayerRole, string> = {
  [PlayerRole.GK]: 'border-l-pos-gk',
  [PlayerRole.LB]: 'border-l-pos-def',
  [PlayerRole.CB]: 'border-l-pos-def',
  [PlayerRole.RB]: 'border-l-pos-def',
  [PlayerRole.LWB]: 'border-l-pos-def',
  [PlayerRole.RWB]: 'border-l-pos-def',
  // A holding midfielder is printed apart from the rest of the middle, in the
  // keeper's amber, the way a football game's team sheet does it.
  [PlayerRole.CDM]: 'border-l-pos-gk',
  [PlayerRole.CM]: 'border-l-pos-mid',
  [PlayerRole.CAM]: 'border-l-pos-mid',
  [PlayerRole.LM]: 'border-l-pos-mid',
  [PlayerRole.RM]: 'border-l-pos-mid',
  [PlayerRole.LW]: 'border-l-pos-fwd',
  [PlayerRole.ST]: 'border-l-pos-fwd',
  [PlayerRole.RW]: 'border-l-pos-fwd',
}

/**
 * A role printed the way a team sheet in a football game prints it: a dark tag
 * with a stripe down its left edge in the colour of the player's line.
 */
export function RoleChip({ role, large = false }: { role: PlayerRole; large?: boolean }) {
  return (
    <span
      className={`inline-block shrink-0 bg-[#1e1f22]/90 font-pitch leading-none font-bold tracking-wide text-white shadow-sm ${
        large
          ? 'min-w-[2.75rem] border-l-4 px-2 py-0.5 text-center text-[1.05rem]'
          : 'min-w-[2.1rem] border-l-[3px] px-1.5 py-px text-center text-[0.8rem]'
      } ${ROLE_LINE_COLOURS[role]}`}
    >
      {ROLE_LABELS[role]}
    </span>
  )
}

/** Ratings read hotter the better the player, as a football game colours them. */
export function ratingTone(overall: number) {
  if (overall >= 95) return 'text-[#ff5a36]'
  if (overall >= 85) return 'text-[#ffa733]'
  if (overall >= 80) return 'text-[#ffc544]'
  if (overall >= 75) return 'text-[#b9e36e]'
  return 'text-white'
}

/** The star beside a rating carries the player's fitness: gold, pale, or red when spent. */
function conditionStar(condition: number) {
  if (condition >= 80) return 'text-[#ffb347]'
  if (condition >= 55) return 'text-[#d6d6d6]'
  return 'text-loss'
}

/** Black edging that keeps team-sheet text legible on grass. */
export const PITCH_TEXT_EDGE =
  '[text-shadow:0_0_2px_rgb(0_0_0/0.9),0_1px_2px_rgb(0_0_0/0.9),0_0_1px_rgb(0_0_0)]'

/** A rating as a team sheet prints it: a small star, then the number. */
export function RatingStar({
  overall,
  condition,
  large = false,
}: {
  overall: number
  condition: number
  large?: boolean
}) {
  return (
    <span
      className={`inline-flex items-start font-pitch leading-none font-extrabold ${PITCH_TEXT_EDGE}`}
    >
      <span
        aria-hidden
        className={`${large ? '-mr-px text-[0.95rem]' : 'text-[0.7rem]'} ${conditionStar(condition)}`}
      >
        ★
      </span>
      <span className={`${large ? 'text-[1.6rem]' : 'text-[1.2rem]'} ${ratingTone(overall)}`}>
        {overall}
      </span>
    </span>
  )
}
