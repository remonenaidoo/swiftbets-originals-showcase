import type { Application, Texture } from 'pixi.js';
import type { GameInfo } from './api';

/** Makes a game's symbol textures, by symbol name. */
export type ArtLoader = (app: Application, info: GameInfo, onProgress: (p: number) => void) => Promise<Record<string, Texture>>;

/** Everything a slot game supplies to the kit; the rest (reels, HUD, money, rules, history, sound) is shared. */
export interface SlotConfig {
  gameId: string;
  title: string;
  /** Symbol textures: image files (imageArt) or drawn tiles (placeholderArt). */
  art: ArtLoader;
  /** Optional backdrop and logo images (asset paths under /originals/); without them the kit draws a gradient and a text logo. */
  background?: { portrait: string; landscape: string };
  logo?: string;
  colours: {
    background: number; // canvas clear colour and gradient base
    backgroundTop: number; // gradient top when no backdrop image
    frame: number; // reel frame border
    accent: number; // inner frame line, glow
    featureTint: number; // wash over the scene during free spins
  };
  /** CSS custom properties for the HUD (e.g. { '--gold': '#f2c25b' }). */
  css?: Record<string, string>;
  /** Game-specific rules sections (HTML), placed after the pay table; `img` gives a symbol's picture. */
  rules: (info: GameInfo, stake: number, img: (symbol: string) => string) => string;
}
