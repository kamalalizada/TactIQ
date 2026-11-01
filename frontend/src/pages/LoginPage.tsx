import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'

import { authApi } from '@/api/endpoints'
import { ErrorNote } from '@/components/States'
import { auth } from '@/lib/auth'

// Mirrors the backend rule: six characters, nothing else required.
const schema = z.object({
  email: z.string().min(1, 'Email is required').email('Enter a valid email'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
})

type FormValues = z.infer<typeof schema>
type Mode = 'login' | 'register'

export function LoginPage() {
  const navigate = useNavigate()
  const [mode, setMode] = useState<Mode>('login')
  const [showPassword, setShowPassword] = useState(false)

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  })

  const submit = useMutation({
    mutationFn: (values: FormValues) =>
      mode === 'login'
        ? authApi.login(values.email, values.password)
        : authApi.register(values.email, values.password),
    onSuccess: (result) => {
      auth.save(result.token, result.email)
      navigate('/', { replace: true })
    },
  })

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand panel: a dark pitch gradient rather than a stock photo. */}
      <aside className="relative hidden flex-col justify-center overflow-hidden px-12 lg:flex">
        <div
          aria-hidden
          className="absolute inset-0 bg-gradient-to-br from-panel via-bg to-black"
        />
        <div
          aria-hidden
          className="absolute inset-0 opacity-[0.07]"
          style={{
            backgroundImage:
              'repeating-linear-gradient(90deg, transparent 0 48px, #fff 48px 49px)',
          }}
        />
        <div className="relative">
          <h1 className="text-5xl font-black tracking-tight">
            Tact<span className="text-win">IQ</span>
          </h1>
          <p className="mt-3 text-lg font-semibold text-muted">Manage. Simulate. Win.</p>
          <p className="mt-6 max-w-sm text-sm leading-relaxed text-muted">
            Football management simulation with real teams, real players and real emotions.
          </p>
        </div>
      </aside>

      <main className="flex items-center justify-center px-4 py-10">
        <div className="w-full max-w-sm">
          <div className="mb-6 text-center lg:hidden">
            <h1 className="text-3xl font-black tracking-tight">
              Tact<span className="text-win">IQ</span>
            </h1>
            <p className="mt-1 text-sm text-muted">Manage. Simulate. Win.</p>
          </div>

          <form
            className="rounded-lg border border-line bg-panel p-6"
            onSubmit={form.handleSubmit((values) => submit.mutate(values))}
            noValidate
          >
            <div className="mb-6 flex border-b border-line">
              {(['login', 'register'] as const).map((value) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => {
                    setMode(value)
                    submit.reset()
                  }}
                  aria-pressed={mode === value}
                  className={`flex-1 border-b-2 pb-2.5 text-sm font-medium transition-colors ${
                    mode === value
                      ? 'border-accent text-accent'
                      : 'border-transparent text-muted hover:text-ink'
                  }`}
                >
                  {value === 'login' ? 'Login' : 'Register'}
                </button>
              ))}
            </div>

            <div className="space-y-4">
              <div>
                <label className="mb-1.5 block text-xs text-muted" htmlFor="email">
                  Email address
                </label>
                <input
                  id="email"
                  type="email"
                  autoComplete="email"
                  className="field"
                  placeholder="you@example.com"
                  aria-invalid={Boolean(form.formState.errors.email)}
                  {...form.register('email')}
                />
                <FieldError message={form.formState.errors.email?.message} />
              </div>

              <div>
                <label className="mb-1.5 block text-xs text-muted" htmlFor="password">
                  Password
                </label>
                <div className="relative">
                  <input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                    className="field pr-10"
                    placeholder="••••••••"
                    aria-invalid={Boolean(form.formState.errors.password)}
                    {...form.register('password')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((shown) => !shown)}
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                    className="absolute inset-y-0 right-0 px-3 text-muted transition-colors hover:text-ink"
                  >
                    {showPassword ? '◡' : '◉'}
                  </button>
                </div>
                <FieldError message={form.formState.errors.password?.message} />
              </div>

              {submit.isError && <ErrorNote error={submit.error} />}

              <button type="submit" className="btn-primary w-full" disabled={submit.isPending}>
                {submit.isPending ? 'Please wait…' : 'Continue'}
              </button>
            </div>

            <p className="mt-5 text-center text-xs text-muted">
              {mode === 'login' ? "Don't have an account? " : 'Already registered? '}
              <button
                type="button"
                className="font-medium text-accent hover:underline"
                onClick={() => {
                  setMode(mode === 'login' ? 'register' : 'login')
                  submit.reset()
                }}
              >
                {mode === 'login' ? 'Register' : 'Login'}
              </button>
            </p>
          </form>
        </div>
      </main>
    </div>
  )
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null
  return <p className="mt-1 text-xs text-loss">{message}</p>
}
