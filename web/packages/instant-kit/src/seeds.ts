import type { Seeds } from './api';

/** The fairness sheet for a game on the player's own seeds: the active hash, the client seed, rotation and the revealed seed. */
export function seedsHtml(seeds: Seeds) {
  return `
    <h2>Provably fair</h2>
    <p>Every round is drawn from your seeds: <b>HMAC-SHA256(server seed, "client seed:nonce:cursor")</b>. The server seed is fixed before you play; you see its SHA-256 hash now and the seed itself when you change seeds, so you can check every round you played with it.</p>
    <h3>Active seeds</h3>
    <div class="kv"><small>Server seed hash</small><code>${seeds.serverSeedHash}</code></div>
    <div class="kv"><small>Client seed</small><code>${seeds.clientSeed}</code></div>
    <div class="kv"><small>Next nonce</small><code>${seeds.nonce}</code></div>
    <h3>Change seeds</h3>
    <p class="note">Reveals the current server seed. The next one is already fixed: its hash is below, so it cannot be chosen to suit the client seed you set. Not possible during a round or for 30 seconds after one starts.</p>
    <div class="kv"><small>Next server seed hash</small><code>${seeds.nextServerSeedHash}</code></div>
    <div class="seedrow"><input id="client-seed" maxlength="64" value="${seeds.clientSeed}" aria-label="New client seed"><button class="primary-sm" id="rotate">Change</button></div>
    ${seeds.previousServerSeed ? `
    <h3>Previous seeds (revealed)</h3>
    <div class="kv"><small>Server seed</small><code>${seeds.previousServerSeed}</code></div>
    <div class="kv"><small>Client seed</small><code>${seeds.previousClientSeed}</code></div>
    <div class="kv"><small>Rounds</small><code>nonces 0 – ${(seeds.previousNonce ?? 1) - 1}</code></div>
    <div class="seedrow"><input id="verify-nonce" type="number" min="0" value="0" aria-label="Nonce to verify"><button class="primary-sm" id="verify">Verify round</button></div>
    <div id="verify-out" class="note"></div>` : ''}`;
}
