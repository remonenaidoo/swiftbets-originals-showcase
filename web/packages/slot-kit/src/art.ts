import { Assets, Container, Graphics, Text, type Texture } from 'pixi.js';
import type { ArtLoader } from './config';

/** Asset path under the site's /originals/ base. */
export const assetUrl = (path: string) => `${import.meta.env.BASE_URL}${path}`;

/** Symbol art from image files: { Lion: 'assets/symbols/lion.webp', … }. */
export function imageArt(files: Record<string, string>): ArtLoader {
  return async (_app, _info, onProgress) => {
    const urls = Object.values(files).map(assetUrl);
    const loaded = await Assets.load(urls, onProgress);
    return Object.fromEntries(Object.entries(files).map(([name, path]) => [name, smooth(loaded[assetUrl(path)] as Texture)]));
  };
}

/** Mipmapped, so art drawn smaller than its file stays crisp instead of shimmering. */
function smooth(texture: Texture): Texture {
  texture.source.autoGenerateMipmaps = true;
  texture.source.updateMipmaps();
  return texture;
}

export interface TileSpec {
  label: string;
  colour: number;
  /** Shown as a badge, e.g. '×2' for a double. */
  badge?: string;
  /** Text colour; defaults to near-white. */
  ink?: number;
}

/**
 * Symbol art drawn in code: a rounded, shaded tile with the symbol's name and an optional badge. For a game whose
 * artwork is still to come; swapping in imageArt later changes nothing else.
 */
export function placeholderArt(tiles: Record<string, TileSpec>): ArtLoader {
  return async (app, _info, onProgress) => {
    const size = 256;
    const out: Record<string, Texture> = {};
    const names = Object.keys(tiles);
    names.forEach((name, i) => {
      const t = tiles[name];
      const tile = new Container();
      const g = new Graphics()
        .roundRect(8, 8, size - 16, size - 16, 36).fill(t.colour)
        .roundRect(8, 8, size - 16, (size - 16) / 2, 36).fill({ color: 0xffffff, alpha: 0.14 })
        .roundRect(8, 8, size - 16, size - 16, 36).stroke({ width: 8, color: 0xffffff, alpha: 0.35 });
      tile.addChild(g);
      const text = new Text({ text: t.label, style: { fontFamily: 'system-ui, sans-serif', fontWeight: '800', fontSize: t.label.length > 7 ? 38 : 48, fill: t.ink ?? 0xfff8e8, align: 'center', wordWrap: true, wordWrapWidth: size - 40, dropShadow: { color: 0x000000, alpha: 0.45, distance: 3, blur: 4 } } });
      text.anchor.set(0.5);
      text.position.set(size / 2, size / 2 + (t.badge ? -14 : 0));
      tile.addChild(text);
      if (t.badge) {
        const badge = new Graphics().roundRect(size / 2 - 52, size - 86, 104, 56, 28).fill(0x1a0f05).stroke({ width: 4, color: 0xffd36b });
        const b = new Text({ text: t.badge, style: { fontFamily: 'system-ui, sans-serif', fontWeight: '900', fontSize: 38, fill: 0xffd36b } });
        b.anchor.set(0.5);
        b.position.set(size / 2, size - 58);
        tile.addChild(badge, b);
      }
      out[name] = app.renderer.generateTexture({ target: tile, resolution: 2 });
      tile.destroy({ children: true });
      onProgress((i + 1) / names.length);
    });
    return out;
  };
}
