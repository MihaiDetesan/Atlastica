import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // MapLibre GL's worker bundle isn't compatible with Vite's dep
  // optimizer (esbuild can't resolve the inline `new Worker(new URL(...))`
  // it constructs) - excluding it avoids the map silently hanging in
  // "loading style" forever.
  optimizeDeps: {
    exclude: ['maplibre-gl'],
  },
})
