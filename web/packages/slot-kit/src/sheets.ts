import { rand, type GameInfo, type RoundSummary } from './api';
import type { SlotConfig } from './config';

const $ = (id: string) => document.getElementById(id)!;

export function open(id: string) { $(id).style.display = 'block'; }
export function close(id: string) { $(id).style.display = 'none'; }

export function wireSheets() {
  for (const el of document.querySelectorAll<HTMLElement>('.sheet')) {
    el.addEventListener('click', (e) => {
      const t = e.target as HTMLElement;
      if (t === el || t.hasAttribute('data-close')) el.style.display = 'none';
    });
  }
}

/** One symbol's pays at this stake, highest count first: a grid of count and amount, as long as the pay table is. */
function payGrid(counts: number[], amount: (n: number) => number) {
  return `<div class="grid">${counts.map((n) => `<div><small>${n}</small><b>${rand(amount(n))}</b></div>`).join('')}</div>`;
}

/**
 * Rules and pay table, built from the server's game info so they always match the maths; pays shown for the current
 * stake. `images` holds each symbol's picture as a data URL.
 */
export function renderRules(config: SlotConfig, info: GameInfo, stake: number, images: Record<string, string>) {
  const lineBet = stake / info.lines;
  const img = (s: string) => `<img src="${images[s] ?? ''}" alt="${s}">`;
  const splits = (name: string) => info.symbols.filter((s) => s.paysAs === name && s.name !== name).map((s) => s.name);
  const pays = Object.entries(info.pays).map(([s, p]) => {
    const counts = p.map((_, i) => i + 3).reverse();
    return `<div class="pay${counts.length > 5 ? ' wide' : ''}"><div class="pics">${img(s)}${splits(s).map(img).join('')}</div>${payGrid(counts, (n) => p[n - 3] * lineBet)}</div>`;
  }).join('');
  const scatter = info.symbols.find((s) => s.kind === 'Scatter')!.name;
  const scatterPays = `<div class="pay"><div class="pics">${img(scatter)}</div>${payGrid([5, 4, 3], (n) => info.scatterPays[n - 3] * stake)}</div>`;
  const lines = info.paylines.map((rows, i) =>
    `<div class="line"><span>${i + 1}</span>${[0, 1, 2].map((r) => [0, 1, 2, 3, 4].map((c) => `<i class="${rows[c] === r ? 'on' : ''}"></i>`).join('')).join('')}</div>`).join('');
  $('rules-body').innerHTML = `
    <h2>${info.name}</h2>
    <p>A 5-reel, 3-row slot with ${info.lines} fixed lines by SwiftBets Originals.</p>
    <div class="facts">
      <div><b>${info.rtp.toFixed(2)}%</b><small>Return to player</small></div>
      <div><b>${info.maxWinStakes.toLocaleString('en-ZA')}×</b><small>Max win</small></div>
      <div><b>${info.lines}</b><small>Lines</small></div>
    </div>
    <h3>Pays at ${rand(stake)} stake</h3>
    <p class="note">The number is how many symbols in a row from reel 1${info.symbols.some((s) => s.weight > 1) ? '; a double counts as 2 and a triple as 3' : ''}.</p>
    <div class="pays">${pays}${scatterPays}</div>
    ${config.rules(info, stake, img)}
    <h3>How wins pay</h3>
    <p>Line wins pay for one symbol running across at least three adjacent reels, left to right from reel 1. Only the highest win per line is paid. Line wins and Scatter wins are added. Wins are shown for the current stake; the line bet is the stake divided by ${info.lines}.</p>
    <p>The most a single paid spin can win, together with the free spins it triggers, is ${info.maxWinStakes.toLocaleString('en-ZA')}× the stake; the feature ends when that is reached.</p>
    <h3>Paylines</h3>
    <div class="lines">${lines}</div>
    <h3>Fairness</h3>
    <p>Every result is decided on the server by a cryptographic random number generator before it is shown. The theoretical return to player is ${info.rtp.toFixed(2)}%; actual results over a session will vary.</p>
    <p>An interrupted round is kept: reopen the game to finish your free spins. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;
}

export function renderHistory(rounds: RoundSummary[]) {
  $('history-body').innerHTML = rounds.length
    ? `<table class="hist"><tr><th>Time</th><th>Stake</th><th>Won</th><th></th></tr>${rounds.map((r) =>
        `<tr><td>${new Date(r.at).toLocaleString('en-ZA', { hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })}</td><td>${rand(r.stake)}</td><td>${rand(r.paid)}</td><td>${r.status === 'settled' ? '' : r.status.replace('_', ' ')}</td></tr>`).join('')}</table>`
    : '<p>No rounds yet. Your last 50 rounds appear here.</p>';
}
