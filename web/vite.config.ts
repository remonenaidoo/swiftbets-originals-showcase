import { defineConfig } from 'vite';

// Served by the originals host behind the gateway at /originals/. `npm run dev` proxies the game API to the
// preview gateway so the client can be played against the real wallet while it is built.
export default defineConfig({
  base: '/originals/',
  build: { target: 'es2022', assetsInlineLimit: 0, chunkSizeWarningLimit: 1500 },
  server: {
    port: 7198,
    proxy: {
      '/originals/slot': { target: 'https://127.0.0.1', changeOrigin: false, secure: false, headers: { host: 'swiftbets.remonenaidoo.com' } },
    },
  },
});
