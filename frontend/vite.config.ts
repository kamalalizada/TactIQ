import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// The API port differs between setups, so it stays configurable.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  // 127.0.0.1 rather than localhost: Node resolves localhost to ::1 first, and
  // a backend bound only to IPv4 would then refuse the proxied request.
  const target = env.VITE_API_URL ?? 'http://127.0.0.1:5199'

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      // Listen on both IPv4 and IPv6, so 127.0.0.1 and ::1 both reach the app.
      host: true,
      strictPort: true,
      proxy: {
        // Keeps the browser on one origin, so JWT and SignalR need no CORS dance.
        '/api': { target, changeOrigin: true },
        '/hubs': { target, changeOrigin: true, ws: true },
      },
    },
  }
})
