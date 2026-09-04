import { execSync } from 'node:child_process'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Short commit + build time, surfaced on the Help page's "System info" panel so bug
// reports carry the exact build. Falls back gracefully when git isn't available.
function gitShortSha() {
  try {
    return execSync('git rev-parse --short HEAD').toString().trim()
  } catch {
    return 'unknown'
  }
}

// In production the SPA is built to `dist/` and copied into the WebApi's `wwwroot`,
// so the API is same-origin (see Dockerfile). In dev the Vite server runs on its own
// port, so `/api` is proxied to the ASP.NET Core host from WebApi/Properties/launchSettings.json.
export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(gitShortSha()),
    __BUILD_TIME__: JSON.stringify(new Date().toISOString()),
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5263',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    environmentOptions: {
      jsdom: { url: 'http://localhost/' },
    },
    setupFiles: ['./src/test/setup.js'],
    globals: true,
  },
})
