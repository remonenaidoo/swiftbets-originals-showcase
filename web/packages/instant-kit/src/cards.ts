import { Container, Graphics, Sprite, Text, type Texture } from 'pixi.js';

/** Card ids 0–51: rank = id % 13 + 1 (Ace low), suit = floor(id / 13). */
export const RANKS = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K'];
export const SUITS = ['♠', '♥', '♦', '♣'];

/** A playing card's face, drawn: width w, height 1.4 w, centred on its position. */
export function cardFace(card: number, w: number): Container {
  const h = w * 1.4, rank = RANKS[card % 13], suit = SUITS[Math.floor(card / 13)], red = suit === '♥' || suit === '♦';
  const ink = red ? 0xd12a3c : 0x161a2c;
  const c = new Container();
  c.addChild(new Graphics().roundRect(-w / 2 + 2, -h / 2 + 4, w, h, w * 0.09).fill({ color: 0x000000, alpha: 0.35 }));
  c.addChild(new Graphics().roundRect(-w / 2, -h / 2, w, h, w * 0.09).fill(0xfbf8f1).roundRect(-w / 2, -h / 2, w, h, w * 0.09).stroke({ width: Math.max(1, w * 0.02), color: 0xd9d2c3 }));
  const corner = (flip: boolean) => {
    const t = new Text({ text: `${rank}\n${suit}`, style: { fontFamily: 'Georgia, serif', fontWeight: '700', fontSize: w * 0.2, lineHeight: w * 0.2, fill: ink, align: 'center' } });
    t.anchor.set(0.5, 0);
    t.position.set(-w / 2 + w * 0.15, -h / 2 + w * 0.06);
    if (flip) { t.rotation = Math.PI; t.position.set(w / 2 - w * 0.15, h / 2 - w * 0.06); }
    return t;
  };
  c.addChild(corner(false), corner(true));
  const big = new Text({ text: card % 13 >= 10 ? `${rank}${suit}` : suit, style: { fontFamily: 'Georgia, serif', fontWeight: '700', fontSize: w * (card % 13 >= 10 ? 0.36 : 0.52), fill: ink } });
  big.anchor.set(0.5);
  c.addChild(big);
  return c;
}

/** A card's back from the game's art, the same size as a face. */
export function cardBack(texture: Texture, w: number): Container {
  const c = new Container();
  c.addChild(new Graphics().roundRect(-w / 2 + 2, -w * 0.7 + 4, w, w * 1.4, w * 0.09).fill({ color: 0x000000, alpha: 0.35 }));
  const s = new Sprite(texture);
  s.anchor.set(0.5);
  s.width = w; s.height = w * 1.4;
  c.addChild(s);
  return c;
}
