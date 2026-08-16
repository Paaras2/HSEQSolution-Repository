import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The project lives on a VMware hgfs-mounted drive (Z:), whose realpath
  // resolution can return a bogus host-side path and break Rollup's module
  // resolution. Keeping the given paths as-is avoids that.
  resolve: {
    preserveSymlinks: true,
  },
  server: {
    fs: {
      strict: false,
    },
    // Native fs.watch() (used by chokidar/Vite's own config watcher) throws
    // EISDIR on this hgfs mount instead of working. Polling sidesteps the
    // native watch API entirely so `npm run dev` can start at all here.
    watch: {
      usePolling: true,
      interval: 300,
    },
  },
})
