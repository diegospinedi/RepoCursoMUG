/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig, searchForWorkspaceRoot } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // El backend corre en 5220; el proxy evita configurar CORS.
    proxy: {
      '/api': 'http://localhost:5220',
    },
    // El logo y los colores se leen de ../Marca, fuera de la raíz de Vite.
    fs: {
      allow: [searchForWorkspaceRoot(process.cwd()), '../Marca'],
    },
    // Desde WSL sobre un disco de Windows (/mnt/c) no llegan eventos de cambios: hace falta polling.
    watch: process.env.WSL_DISTRO_NAME && process.cwd().startsWith('/mnt/') ? { usePolling: true } : undefined,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
  },
})
