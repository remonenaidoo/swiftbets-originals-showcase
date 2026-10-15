import { rand, type GameInfo, type RoundSummary } from './api';
import { assetUrl } from './scene';

const FILE: Record<string, string> = { Lion: 'lion', Elephant: 'elephant', Rhino: 'rhino', Buffalo: 'buffalo', Ace: 'ace', King: 'king', Queen: 'queen', Jack: 'jack', Ten: 'ten', Wild: 'wild', Scatter: 'scatter' };
const $ = (id: string) => document.getElementById(id)!;

export function open(id: string) { $(id).style.display = 'block'; }
export function close(id: string) { $(id).style.display = 'none'; }

for (const el of document.querySelectorAll<HTMLElement>('.sheet')) {
  el.addEventListener('click', (e) => {
    const t = e.target as HTMLElement;
    if (t === el || t.hasAttribute('data-close')) el.style.display = 'none';
  });
}

/** Rules and pay table, built from the server's game info so they always match the maths; pays shown for the current stake. */
export function renderRules(info: GameInfo, stake: number) {
  const lineBet = stake / info.lines;
  const img = (s: string) => `<img src="${assetUrl(`symbols/${FILE[s]}.webp`)}" alt="${s}">`;
  const pays = Object.entries(info.pays).map(([s, p]) =>
    `<div class="pay">${img(s)}<table>${[5, 4, 3].map((n) => `<tr><td>${n}×</td><td>${rand(p[n - 3] * lineBet)}</td></tr>`).join('')}</table></div>`).join('');
  const scatter = `<div class="pay">${img('Scatter')}<table>${[5, 4, 3].map((n) => `<tr><td>${n}×</td><td>${rand(info.scatterPays[n - 3] * stake)}</td></tr>`).join('')}</table></div>`;
  const lines = info.paylines.map((rows, i) =>
    `<div class="line"><span>${i + 1}</span>${[0, 1, 2].map((r) => [0, 1, 2, 3, 4].map((c) => `<i class="${rows[c] === r ? 'on' : ''}"></i>`).join('')).join('')}</div>`).join('');
  $('rules-body').innerHTML = `
    <h2>Gilded Savanna</h2>
    <p>A 5-reel, 3-row slot with ${info.lines} fixed lines by SwiftBets Originals.</p>
    <div class="facts">
      <div><b>${info.rtp.toFixed(2)}%</b><small>Return to player</small></div>
      <div><b>${info.maxWinStakes.toLocaleString('en-ZA')}×</b><small>Max win</small></div>
      <div><b>${info.lines}</b><small>Lines</small></div>
    </div>
    <h3>Pays at ${rand(stake)} stake</h3>
    <div class="pays">${pays}${scatter}</div>
    <h3>Wild and Scatter</h3>
    <p>${img('Wild')}</p>
    <p><b>Protea Wild</b> appears on reels 2, 3 and 4 and stands in for every symbol except the Scatter.</p>
    <p><b>Baobab Scatter</b> pays anywhere on the reels, multiplied by the total stake. 3 or more award <b>${info.freeSpins} free spins</b>.</p>
    <h3>Free spins</h3>
    <p>Free spins are played on their own reels at the stake that triggered them. Every free-spin win is multiplied by <b>×${info.freeSpinMultiplier}</b>. 3 or more Scatters during free spins award ${info.freeSpins} more.</p>
    <h3>How wins pay</h3>
    <p>Line wins pay left to right on adjacent reels starting from reel 1. Only the highest win per line is paid. Line wins and Scatter wins are added. Wins are shown for the current stake; the line bet is the stake divided by ${info.lines}.</p>
    <p>The most a single paid spin can win, together with the free spins it triggers, is ${info.maxWinStakes.toLocaleString('en-ZA')}× the stake; the feature ends when that is reached.</p>
    <h3>Paylines</h3>
    <div class="lines">${lines}</div>
    <h3>Fairness</h3>
    <p>Every result is decided on the server by a cryptographic random number generator before it is shown. The theoretical return to player is ${info.rtp.toFixed(2)}%, computed exactly over every reel combination; actual results over a session will vary.</p>
    <p>An interrupted round is kept: reopen the game to finish your free spins. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`;
}

export function renderHistory(rounds: RoundSummary[]) {
  $('history-body').innerHTML = rounds.length
    ? `<table class="hist"><tr><th>Time</th><th>Stake</th><th>Won</th><th></th></tr>${rounds.map((r) =>
        `<tr><td>${new Date(r.at).toLocaleString('en-ZA', { hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })}</td><td>${rand(r.stake)}</td><td>${rand(r.paid)}</td><td>${r.status === 'settled' ? '' : r.status.replace('_', ' ')}</td></tr>`).join('')}</table>`
    : '<p>No rounds yet. Your last 50 rounds appear here.</p>';
}
