import { placeholderArt, startSlot, type TileSpec } from '@originals/slot-kit';

// Placeholder tiles until the art pass: each symbol's name and colour, and a ×2 / ×3 badge on split symbols.
const tile = (label: string, colour: number, badge?: string, ink?: number): TileSpec => ({ label, colour, badge, ink });

startSlot({
  gameId: 'gold-reef-rush',
  title: 'Gold Reef Rush',
  art: placeholderArt({
    GoldBar: tile('GOLD BAR', 0xc9921a), GoldBar2: tile('GOLD BAR', 0xc9921a, '×2'), GoldBar3: tile('GOLD BAR', 0xc9921a, '×3'),
    Diamond: tile('DIAMOND', 0x2f9fc4), Diamond2: tile('DIAMOND', 0x2f9fc4, '×2'),
    Cart: tile('MINE CART', 0x8a4b2a), Cart2: tile('MINE CART', 0x8a4b2a, '×2'),
    Lantern: tile('LANTERN', 0xd8691c), Lantern2: tile('LANTERN', 0xd8691c, '×2'),
    Copper: tile('COPPER', 0x9c5a2c), Silver: tile('SILVER', 0x7d8791), Quartz: tile('QUARTZ', 0xcdb8e0, undefined, 0x2a1838),
    Miner: tile('MINER WILD', 0x2e6b3c), Miner2: tile('MINER WILD', 0x2e6b3c, '×2'),
    Dynamite: tile('DYNAMITE', 0xb3241b),
  }),
  colours: { background: 0x120c06, backgroundTop: 0x2b1d0e, frame: 0xe0a93a, accent: 0x5fd0e6, featureTint: 0x5a2a08 },
  css: { '--teal': '#5fd0e6', '--panel': 'rgba(24, 16, 8, 0.88)' },
  rules: (info, _stake, img) => `
    <h3>Doubles and triples</h3>
    <p>${img('GoldBar3')} ${img('Diamond2')}</p>
    <p>A double counts as 2 symbols and a triple as 3, so one line can count up to 15. <b>Gold Bar</b> comes single, double and triple; <b>Diamond</b>, <b>Mine Cart</b> and <b>Lantern</b> single and double.</p>
    <h3>Wild and Scatter</h3>
    <p>${img('Miner')} ${img('Miner2')}</p>
    <p><b>Miner Wild</b> appears on reels 2, 3 and 4 and stands in for every symbol except Dynamite. The <b>Double Miner</b> counts as 2.</p>
    <p><b>Dynamite</b> pays anywhere on the reels, multiplied by the total stake. 3 or more award <b>${info.freeSpins} free spins</b>.</p>
    <h3>Free spins</h3>
    <p>Free spins are played on their own reels at the stake that triggered them . Every double or triple that lands, Wilds included, <b>locks in place</b> for the rest of the free spins. Free spins cannot be retriggered.</p>`,
});
