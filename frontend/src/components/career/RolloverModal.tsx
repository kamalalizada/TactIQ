import { Modal } from '@/components/ui/Modal'
import { money } from '@/lib/money'
import type { Development, Retirement, SeasonRecord, SeasonRollover } from '@/types'

/**
 * What a year did, shown as the next one begins.
 *
 * This is the one moment a manager sees his squad change without having chosen
 * it: a defender has retired, a nineteen-year-old has grown into the side, and
 * the player he built the team around has started to slow down. Reading it is
 * how he decides what the summer needs to be about.
 */
export function RolloverModal({
  summary,
  onClose,
}: {
  summary: SeasonRollover | null
  onClose: () => void
}) {
  if (!summary) return null

  const won = summary.records.filter((record) => record.userWon)

  return (
    <Modal
      open
      wide
      title={`${summary.year}. mövsüm bitdi`}
      onClose={onClose}
      footer={
        <button
          type="button"
          onClick={onClose}
          className="rounded-lg bg-violet px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-violet/85"
        >
          {summary.nextYear}. mövsümə başla
        </button>
      }
    >
      {won.length > 0 && (
        <p className="mb-4 rounded-lg border border-win/30 bg-win/10 px-4 py-3 text-sm font-semibold text-win">
          🏆 {won.map((record) => record.competitionName).join(', ')} çempionusunuz!
        </p>
      )}

      <section className="mb-5">
        <h3 className="mb-2 text-sm font-semibold">Yekun cədvəllər</h3>
        <ul className="space-y-1">
          {summary.records.map((record) => (
            <li key={record.competitionName}>
              <FinalTableRow record={record} />
            </li>
          ))}
        </ul>
      </section>

      <div className="grid gap-5 sm:grid-cols-2">
        <Column
          title="İrəli gedənlər"
          empty="Bu mövsüm heç kim irəliləmədi."
          items={summary.improved}
          render={(player) => <DevelopmentRow player={player} />}
        />

        <Column
          title="Geri gedənlər"
          empty="Heç kim geriləmədi."
          items={summary.declined}
          render={(player) => <DevelopmentRow player={player} />}
        />
      </div>

      {summary.retired.length > 0 && (
        <section className="mt-5">
          <h3 className="mb-2 text-sm font-semibold">Karyerasını bitirənlər</h3>
          <ul className="space-y-1">
            {summary.retired.map((player) => (
              <li key={player.name}>
                <RetirementRow player={player} />
              </li>
            ))}
          </ul>
        </section>
      )}
    </Modal>
  )
}

function FinalTableRow({ record }: { record: SeasonRecord }) {
  return (
    <div
      className={`flex flex-wrap items-center gap-x-2 gap-y-0.5 rounded-lg border px-3 py-2 text-sm ${
        record.userWon ? 'border-win/30 bg-win/10' : 'border-line'
      }`}
    >
      <span className="font-medium">{record.competitionName}</span>
      <span className="text-muted">{record.championTeamName ?? '—'}</span>

      {record.userRank !== null && (
        <span className="ml-auto text-muted">
          Siz <span className="font-bold text-ink">{record.userRank}.</span> yer
          {record.userPoints !== null && ` · ${record.userPoints} xal`}
        </span>
      )}

      {record.userBudget !== null && (
        <span className="w-20 text-right text-[0.7rem] text-muted">
          {money(record.userBudget)}
        </span>
      )}
    </div>
  )
}

function DevelopmentRow({ player }: { player: Development }) {
  const delta = player.after - player.before
  const rising = delta > 0

  return (
    <div className="flex items-center gap-2 rounded-lg border border-line px-3 py-1.5 text-sm">
      <span className="min-w-0 flex-1 truncate">{player.name}</span>
      <span className="shrink-0 text-[0.7rem] text-muted">{player.age} yaş</span>
      <span className="shrink-0 text-muted">{player.before}</span>
      <span aria-hidden className="shrink-0 text-muted">
        →
      </span>
      <span className={`shrink-0 font-bold ${rising ? 'text-win' : 'text-loss'}`}>
        {player.after}
      </span>
      <span className={`w-8 shrink-0 text-right text-[0.7rem] ${rising ? 'text-win' : 'text-loss'}`}>
        {rising ? '+' : ''}
        {delta}
      </span>
    </div>
  )
}

function RetirementRow({ player }: { player: Retirement }) {
  return (
    <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5 rounded-lg border border-line px-3 py-1.5 text-sm">
      <span className="font-medium">{player.name}</span>
      <span className="text-[0.7rem] text-muted">
        {player.age} yaş · {player.overall} OVR
      </span>

      {player.replacedBy && (
        <span className="ml-auto text-[0.7rem] text-muted">
          Akademiyadan <span className="text-ink">{player.replacedBy}</span>
        </span>
      )}
    </div>
  )
}

function Column<T>({
  title,
  empty,
  items,
  render,
}: {
  title: string
  empty: string
  items: T[]
  render: (item: T) => React.ReactNode
}) {
  return (
    <section>
      <h3 className="mb-2 text-sm font-semibold">{title}</h3>

      {items.length === 0 ? (
        <p className="text-sm text-muted">{empty}</p>
      ) : (
        <ul className="space-y-1">
          {items.map((item, index) => (
            <li key={index}>{render(item)}</li>
          ))}
        </ul>
      )}
    </section>
  )
}
