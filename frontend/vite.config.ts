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
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
  },
})
