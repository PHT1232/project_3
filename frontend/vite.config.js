import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In production the SPA is built to `dist/` and copied into the WebApi's `wwwroot`,
// so the API is same-origin (see Dockerfile). In dev the Vite server runs on its own
// port, so `/api` is proxied to the ASP.NET Core host from WebApi/Properties/launchSettings.json.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5263',
        changeOrigin: true,
      },
    },
  },
})
