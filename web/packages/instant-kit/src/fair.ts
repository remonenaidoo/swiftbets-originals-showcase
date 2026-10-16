// Round verification in the browser, the same draw as the server (Fair.cs): HMAC-SHA256(serverSeed,
// "clientSeed:nonce:cursor"), 8 floats of 4 bytes per block.
export async function floats(serverSeed: string, clientSeed: string, nonce: number, count: number): Promise<number[]> {
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey('raw', enc.encode(serverSeed), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const out: number[] = [];
  for (let cursor = 0; out.length < count; cursor++) {
    const b = new Uint8Array(await crypto.subtle.sign('HMAC', key, enc.encode(`${clientSeed}:${nonce}:${cursor}`)));
    for (let i = 0; i < 8 && out.length < count; i++) {
      out.push(b[i * 4] / 256 + b[i * 4 + 1] / 65536 + b[i * 4 + 2] / 16777216 + b[i * 4 + 3] / 4294967296);
    }
  }
  return out;
}

export async function sha256(text: string): Promise<string> {
  const h = new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text)));
  return [...h].map((x) => x.toString(16).padStart(2, '0')).join('');
}

/** Mines layout for a round: Fisher–Yates over 25 tiles by its floats, the first `mines` (Mines.cs). */
export async function minesLayout(serverSeed: string, clientSeed: string, nonce: number, mines: number): Promise<number[]> {
  const f = await floats(serverSeed, clientSeed, nonce, 25);
  const tiles = Array.from({ length: 25 }, (_, i) => i);
  for (let i = 24; i > 0; i--) {
    const j = Math.floor(f[24 - i] * (i + 1));
    [tiles[i], tiles[j]] = [tiles[j], tiles[i]];
  }
  return tiles.slice(0, mines).sort((a, b) => a - b);
}

/** Crash point of a shared round (Crash.cs), in basis points. */
export async function crashPoint(seed: string, seq: number): Promise<number> {
  const [u] = await floats(seed, 'crash', seq, 1);
  const hundredths = Math.floor(96 / (1 - u));
  return Math.min(Math.max(hundredths, 100), 500000) * 100;
}
