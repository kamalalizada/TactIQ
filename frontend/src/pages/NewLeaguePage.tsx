import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'

import { seasonApi } from '@/api/endpoints'
import { Shell } from '@/components/Shell'
import { ErrorNote } from '@/components/States'
import { GameMode } from '@/types'

const schema = z.object({
  seasonName: z.string().min(1, 'Season name is required').max(120, 'That name is too long'),
})

type FormValues = z.infer<typeof schema>

const MODES = [
  {
    value: GameMode.GodMode,
    icon: '👁',
    title: 'God Mode',
    hint: 'Simulate the league and watch results.',
    note: null,
  },
  {
    value: GameMode.ManagerMode,
    icon: '🛡',
    title: 'Manager Mode',
    hint: 'Control one team during live matches.',
    note: 'Your team will be randomly assigned.',
  },
] as const

export function NewLeaguePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [gameMode, setGameMode] = useState<GameMode>(GameMode.ManagerMode)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { seasonName: '' },
  })

  const create = useMutation({
    mutationFn: (values: FormValues) =>
      seasonApi.create({
        name: values.seasonName.trim(),
        gameMode,
        // Teams do not exist until the season is created, so the backend picks one.
        userTeamId: null,
      }),
    onSuccess: (season) => {
      queryClient.invalidateQueries({ queryKey: ['seasons'] })

      // A manager picks their club before kick-off; God Mode has none to pick.
      navigate(
        season.gameMode === GameMode.ManagerMode
          ? `/seasons/${season.id}/pick-team`
          : `/seasons/${season.id}`,
      )
    },
  })

  return (
    <Shell>
      <div className="mx-auto max-w-2xl">
        <h2 className="mb-5 text-xl font-bold tracking-tight">Create New League</h2>

        <form
          className="space-y-6 rounded-lg border border-line bg-panel p-6"
          onSubmit={form.handleSubmit((values) => create.mutate(values))}
          noValidate
        >
          <div>
            <label className="mb-1.5 block text-xs text-muted" htmlFor="seasonName">
              Season Name
            </label>
            <input
              id="seasonName"
              className="field"
              placeholder="e.g. Premier League 2026/27"
              aria-invalid={Boolean(form.formState.errors.seasonName)}
              {...form.register('seasonName')}
            />
            {form.formState.errors.seasonName && (
              <p className="mt-1 text-xs text-loss">{form.formState.errors.seasonName.message}</p>
            )}
          </div>

          <div>
            <span className="mb-2 block text-xs text-muted">Game Mode</span>
            <div className="grid gap-3 sm:grid-cols-2">
              {MODES.map((option) => {
                const isActive = gameMode === option.value

                return (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => setGameMode(option.value)}
                    aria-pressed={isActive}
                    className={`rounded-lg border p-4 text-left transition-colors ${
                      isActive
                        ? 'border-win bg-win/5'
                        : 'border-line hover:border-line hover:bg-white/5'
                    }`}
                  >
                    <span
                      aria-hidden
                      className={`mb-3 flex size-10 items-center justify-center rounded-lg text-lg ${
                        isActive ? 'bg-win/15' : 'bg-accent/15'
                      }`}
                    >
                      {option.icon}
                    </span>
                    <span className="block font-semibold">{option.title}</span>
                    <span className="mt-1 block text-xs leading-relaxed text-muted">
                      {option.hint}
                    </span>
                    {option.note && isActive && (
                      <span className="mt-2 flex items-start gap-1.5 text-[0.7rem] text-muted">
                        <span aria-hidden>ⓘ</span>
                        {option.note}
                      </span>
                    )}
                  </button>
                )
              })}
            </div>
          </div>

          {create.isError && <ErrorNote error={create.error} />}

          <button type="submit" className="btn-primary w-full" disabled={create.isPending}>
            {create.isPending ? 'Creating…' : 'Create League'}
          </button>
        </form>
      </div>
    </Shell>
  )
}
