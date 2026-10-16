import { rand, sound } from '@originals/slot-kit';
import { times, type RoundSummary, type Seeds } from './api';
import './style.css';

export interface ShellConfig {
  title: string;
  /** Accent colours as CSS colours: the action button, highlights. */
  accent: string;
  accent2: string;
  /** The page background behind the stage. */
  background: string;
  stakes: number[];
  rtp: number;
  /**
   * 'stacked': on phones the stage keeps a fixed share of the height and the panel scrolls under it (a game with
   * several bet boxes and a list); the stage and panel otherwise share the screen. The game draws its own action
   * buttons when `ownActions` is set (the shared stake and action row are hidden).
   */
  layout?: 'default' | 'stacked';
  ownActions?: boolean;
}

const $ = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;

/**
 * The page every instant game shares: the stage (a canvas host that fills the space left), the panel (balance, win,
 * stake, the game's own controls and its action button), and the rules, history and fairness sheets. Phones: the
 * panel sits under the stage. Wide screens: it is a column beside it.
 */
export function mountShell(config: ShellConfig) {
  document.documentElement.style.setProperty('--accent', config.accent);
  document.documentElement.style.setProperty('--accent-2', config.accent2);
  document.documentElement.style.setProperty('--bg', config.background);
  if (config.layout === 'stacked') document.body.classList.add('stacked');
  if (config.ownActions) document.body.classList.add('own-actions');
  document.body.insertAdjacentHTML('afterbegin', `
    <div id="app">
      <div id="stage"><div id="top"><span class="pill" id="clock">00:00</span><span class="pill">RTP ${config.rtp.toFixed(2)}%</span></div></div>
      <aside id="panel">
        <div class="row stats">
          <div class="stat"><small>Balance</small><b id="balance">…</b></div>
          <div class="stat win"><small>Win</small><b id="win">R 0.00</b></div>
          <div class="icons">
            <button class="icon" id="btn-sound" aria-label="Sound">${sound.muted ? '🔇' : '🔊'}</button>
            <button class="icon" id="btn-history" aria-label="History">🕘</button>
            <button class="icon" id="btn-fair" aria-label="Fairness">🛡</button>
            <button class="icon" id="btn-rules" aria-label="Rules">ⓘ</button>
          </div>
        </div>
        <div id="controls"></div>
        <div class="row act">
          <div class="stake">
            <button id="stake-down" aria-label="Lower stake">−</button>
            <div class="v"><small>STAKE</small><b id="stake">R 1.00</b></div>
            <button id="stake-up" aria-label="Raise stake">+</button>
          </div>
          <button id="action" class="action">BET</button>
        </div>
      </aside>
    </div>
    <div id="toast"></div>
    <div id="bigwin"><div class="card"><div class="kicker" id="bw-kicker">BIG WIN</div><div class="amount" id="bw-amount"></div><div class="sub" id="bw-sub"></div></div></div>
    <div class="sheet" id="sheet-rules"><div class="panel"><button class="close" data-close>✕</button><div id="rules-body"></div></div></div>
    <div class="sheet" id="sheet-history"><div class="panel"><button class="close" data-close>✕</button><h2>Your recent rounds</h2><div id="history-body"></div></div></div>
    <div class="sheet" id="sheet-fair"><div class="panel"><button class="close" data-close>✕</button><div id="fair-body"></div></div></div>`);

  for (const el of document.querySelectorAll<HTMLElement>('.sheet')) {
    el.addEventListener('click', (e) => {
      const t = e.target as HTMLElement;
      if (t === el || t.hasAttribute('data-close')) el.style.display = 'none';
    });
  }
  const openSheet = (id: string) => { $(id).style.display = 'block'; };
  $('btn-rules').onclick = () => openSheet('sheet-rules');
  $('btn-history').onclick = () => openSheet('sheet-history');
  $('btn-fair').onclick = () => openSheet('sheet-fair');
  $('btn-sound').onclick = () => { $('btn-sound').textContent = sound.toggle() ? '🔇' : '🔊'; };
  document.addEventListener('pointerdown', () => sound.unlock(), { once: true });

  const started = Date.now();
  setInterval(() => {
    const s = Math.floor((Date.now() - started) / 1000);
    $('clock').textContent = `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }, 1000);

  // Stake: the server's ladder; the last choice is remembered per device.
  const stakeKey = `stake:${config.title}`;
  let stakeIndex = Math.max(0, config.stakes.indexOf(Number((() => { try { return localStorage.getItem(stakeKey); } catch { return null; } })())));
  const showStake = () => {
    $('stake').textContent = rand(config.stakes[stakeIndex]);
    ($('stake-down') as HTMLButtonElement).disabled = locked || stakeIndex === 0;
    ($('stake-up') as HTMLButtonElement).disabled = locked || stakeIndex === config.stakes.length - 1;
    try { localStorage.setItem(stakeKey, String(config.stakes[stakeIndex])); } catch { /* private mode */ }
  };
  let locked = false;
  $('stake-down').onclick = () => { stakeIndex = Math.max(0, stakeIndex - 1); showStake(); };
  $('stake-up').onclick = () => { stakeIndex = Math.min(config.stakes.length - 1, stakeIndex + 1); showStake(); };
  showStake();

  let toastTimer = 0;
  return {
    stage: $('stage'),
    controls: $('controls'),
    action: $<HTMLButtonElement>('action'),
    get stake() { return config.stakes[stakeIndex]; },
    /** Stake and game controls are locked while a round is being played. */
    lock(on: boolean) {
      locked = on;
      showStake();
      for (const b of $('controls').querySelectorAll<HTMLButtonElement | HTMLInputElement>('button, input')) b.disabled = on;
    },
    setBalance(minor: number | null) { if (minor !== null) $('balance').textContent = rand(minor); },
    setWin(minor: number) { $('win').textContent = rand(minor); },
    toast(message: string) {
      const t = $('toast');
      t.textContent = message;
      t.style.display = 'block';
      clearTimeout(toastTimer);
      toastTimer = window.setTimeout(() => { t.style.display = 'none'; }, 3200);
    },
    /** The big-win card, from 10× stake: counts the amount up, tap to close. */
    bigWin(minor: number, bp: number) {
      const k = bp >= 1_000_000 ? 'EPIC WIN' : bp >= 250_000 ? 'MEGA WIN' : 'BIG WIN';
      $('bw-kicker').textContent = k;
      $('bw-sub').textContent = times(bp);
      const el = $('bigwin');
      el.style.display = 'grid';
      sound.big();
      const t0 = performance.now(), ms = 1600;
      const step = (t: number) => {
        const p = Math.min(1, (t - t0) / ms);
        $('bw-amount').textContent = rand(Math.round(minor * (1 - Math.pow(1 - p, 3))));
        if (p < 1) requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
      const close = () => { el.style.display = 'none'; };
      el.onclick = close;
      setTimeout(close, 4200);
    },
    rules(html: string) { $('rules-body').innerHTML = html; },
    history(rounds: RoundSummary[]) {
      $('history-body').innerHTML = rounds.length
        ? `<table class="hist"><tr><th>Time</th><th>Stake</th><th>Won</th></tr>${rounds.map((r) =>
            `<tr><td>${new Date(r.at).toLocaleString('en-ZA', { hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })}</td><td>${rand(r.stake)}</td><td class="${r.paid > 0 ? 'won' : ''}">${rand(r.paid)}</td></tr>`).join('')}</table>`
        : '<p>No rounds yet. Your last 50 rounds appear here.</p>';
    },
    fairness(html: string) { $('fair-body').innerHTML = html; },
    openSheet,
  };
}

export type Shell = ReturnType<typeof mountShell>;

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
    <p class="note">Reveals the current server seed and starts a new one. Not possible during a round or for 30 seconds after one starts.</p>
    <div class="seedrow"><input id="client-seed" maxlength="64" value="${seeds.clientSeed}" aria-label="New client seed"><button class="primary-sm" id="rotate">Change</button></div>
    ${seeds.previousServerSeed ? `
    <h3>Previous seeds (revealed)</h3>
    <div class="kv"><small>Server seed</small><code>${seeds.previousServerSeed}</code></div>
    <div class="kv"><small>Client seed</small><code>${seeds.previousClientSeed}</code></div>
    <div class="kv"><small>Rounds</small><code>nonces 0 – ${(seeds.previousNonce ?? 1) - 1}</code></div>
    <div class="seedrow"><input id="verify-nonce" type="number" min="0" value="0" aria-label="Nonce to verify"><button class="primary-sm" id="verify">Verify round</button></div>
    <div id="verify-out" class="note"></div>` : ''}`;
}
