import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // El backend corre en 5220; el proxy evita configurar CORS.
    proxy: {
      '/api': 'http://localhost:5220',
    },
  },
})
