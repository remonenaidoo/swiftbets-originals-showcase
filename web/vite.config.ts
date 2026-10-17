import { readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { defineConfig } from 'vite';

// Every folder in games/ is a page: dist/games/<id>/index.html, served by the originals host at /play?game=<id>.
// Served behind the gateway at /originals/. `npm run dev` proxies the games' API to the preview gateway so a game can
// be played against the real wallet while it is built (open /originals/games/<id>/index.html?session=…).
const games = Object.fromEntries(readdirSync(resolve(__dirname, 'games')).map((id) => [id, resolve(__dirname, 'games', id, 'index.html')]));

export default defineConfig({
  base: '/originals/',
  build: { target: 'es2022', assetsInlineLimit: 0, chunkSizeWarningLimit: 1500, rollupOptions: { input: games } },
  server: {
    port: 7198,
    proxy: {
      '^/originals/games/[^/]+/(state|spin|feature/next)': { target: 'https://127.0.0.1', changeOrigin: false, secure: false, headers: { host: 'swiftbets.remonenaidoo.com' } },
      '^/originals/instant/': { target: 'https://127.0.0.1', changeOrigin: false, secure: false, headers: { host: 'swiftbets.remonenaidoo.com' } },
      '^/originals/hubs/': { target: 'https://127.0.0.1', changeOrigin: false, secure: false, ws: true, headers: { host: 'swiftbets.remonenaidoo.com' } },
    },
  },
});
