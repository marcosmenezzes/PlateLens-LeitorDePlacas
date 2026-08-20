import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: { outDir: '../wwwroot', emptyOutDir: true },
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      // Mantém frontend e backend equivalentes à mesma origem durante o desenvolvimento.
      '/api': 'http://127.0.0.1:5055',
    },
  },
})
