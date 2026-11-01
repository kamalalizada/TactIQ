interface SimulateButtonProps {
  onSimulate: () => void
  isPending: boolean
  hasPendingUserMatch: boolean
}

export function SimulateButton({
  onSimulate,
  isPending,
  hasPendingUserMatch,
}: SimulateButtonProps) {
  return (
    <>
      <button
        type="button"
        className="mt-4 w-full rounded-md bg-accent px-4 py-2.5 text-sm font-bold tracking-wide text-white uppercase transition-colors hover:bg-accent/85 disabled:cursor-not-allowed disabled:opacity-40"
        onClick={onSimulate}
        disabled={isPending || hasPendingUserMatch}
        title={hasPendingUserMatch ? 'Play your own match first' : undefined}
      >
        {isPending ? 'Simulating…' : 'Simulate Week'}
      </button>

      {hasPendingUserMatch && (
        <p className="mt-2 text-center text-xs text-muted">
          Play your match to complete the week.
        </p>
      )}
    </>
  )
}
