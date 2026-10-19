import { Application, Assets, Sprite, Texture } from 'pixi.js';
import { assetUrl } from '@originals/slot-kit';
import { call, describe, query, type RoundSummary, type Seeds } from './api';
import { floats } from './fair';
import { mountShell, seedsHtml, type Shell, type ShellConfig } from './shell';

export interface InstantState<I = Record<string, unknown>, O = unknown> { balance: number | null; open: O | null; recent: RoundSummary[]; seeds: Seeds; info: I & { stakes: number[]; rateBp: number; maxWinStakes: number; name: string } }

export interface BootOptions {
  gameId: string;
  title: string;
  accent: string;
  accent2: string;
  background: string;
  /** Art under assets/<gameId>/ to load (bg-landscape.jpg and bg-portrait.jpg are loaded always). */
  art: string[];
  /** Shows what a revealed seed gives for one round: the game's own reading of its floats. */
  verify: (serverSeed: string, clientSeed: string, nonce: number) => Promise<string>;
  shell?: Partial<ShellConfig>;
}

/**
 * The start every instant game shares: load the state, mount the shell, the stage (a Pixi app with the game's
 * backdrop filling it) and the fairness sheet (rotate, verify a round of the revealed seed), and keep history and
 * seeds fresh after each round.
 */
export async function bootInstant<I = Record<string, unknown>, O = unknown>(o: BootOptions) {
  const path = `instant/${o.gameId}/state`;
  const state = await call<InstantState<I, O>>(query(path));
  const shell: Shell = mountShell({ title: o.title, accent: o.accent, accent2: o.accent2, background: o.background, stakes: state.info.stakes, ...o.shell });
  shell.setBalance(state.balance);
  shell.history(state.recent);

  const app = new Application();
  await app.init({ resizeTo: shell.stage, background: 0x05060c, antialias: true, resolution: Math.min(3, window.devicePixelRatio || 1), autoDensity: true });
  shell.stage.prepend(app.canvas);
  // The stage changes size when the game adds its controls under it (and on rotation): keep the canvas matched.
  new ResizeObserver(() => app.resize()).observe(shell.stage);
  const A = (f: string) => assetUrl(`assets/${o.gameId}/${f}`);
  const files = ['bg-landscape.jpg', 'bg-portrait.jpg', ...o.art];
  const loaded = await Assets.load<Texture>(files.map(A));
  for (const t of Object.values(loaded)) { (t as Texture).source.autoGenerateMipmaps = true; (t as Texture).source.updateMipmaps(); }
  const tex = (f: string) => loaded[A(f)] as Texture;
  const backdrop = new Sprite(tex('bg-landscape.jpg'));
  backdrop.anchor.set(0.5);
  app.stage.addChild(backdrop);
  const fit = () => {
    const w = app.screen.width, h = app.screen.height;
    backdrop.texture = tex(h > w ? 'bg-portrait.jpg' : 'bg-landscape.jpg');
    backdrop.scale.set(Math.max(w / backdrop.texture.width, h / backdrop.texture.height));
    backdrop.position.set(w / 2, h / 2);
  };
  fit();
  app.renderer.on('resize', fit);

  const fairness = () => {
    shell.fairness(seedsHtml(state.seeds));
    const rotate = document.getElementById('rotate');
    if (rotate) rotate.onclick = async () => {
      try {
        state.seeds = await call<Seeds>('instant/seed', { clientSeed: (document.getElementById('client-seed') as HTMLInputElement).value.trim() });
        fairness();
      } catch (e) { shell.toast(describe(e)); }
    };
    const verify = document.getElementById('verify');
    if (verify && state.seeds.previousServerSeed) verify.onclick = async () => {
      const nonce = Number((document.getElementById('verify-nonce') as HTMLInputElement).value);
      document.getElementById('verify-out')!.textContent = `Round ${nonce}: ${await o.verify(state.seeds.previousServerSeed!, state.seeds.previousClientSeed!, nonce)}`;
    };
  };
  fairness();

  /** After a round: history and seeds from the server (the nonce moved on). */
  const refresh = async () => {
    try {
      const s = await call<InstantState<I, O>>(query(path));
      state.seeds = s.seeds;
      shell.history(s.recent);
      fairness();
      return s;
    } catch { return null; }
  };

  return { state, shell, app, tex, refresh, floats };
}
