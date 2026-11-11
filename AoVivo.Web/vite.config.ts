/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// The React app calls relative URLs (/api/...). In development, Vite forwards them to AoVivo.Api,
// so the API needs no CORS setup and the browser can read its custom response headers.
// Change the target with API_TARGET (environment variable or .env.local), e.g. https://localhost:7080.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.API_TARGET || 'http://localhost:5080'

  const proxy = {
    '/api': {
      target: apiTarget,
      changeOrigin: true,
      secure: false, // accept the ASP.NET Core self-signed development certificate
    },
  }

  return {
    plugins: [react()],
    server: { port: 5173, proxy },
    preview: { port: 4173, proxy },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      restoreMocks: true,
    },
  }
})
