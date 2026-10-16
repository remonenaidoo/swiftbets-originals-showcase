import { imageArt, startSlot } from '@originals/slot-kit';

const symbol = (file: string) => `assets/gold-reef-rush/symbols/${file}.webp`;

startSlot({
  gameId: 'gold-reef-rush',
  title: 'Gold Reef Rush',
  art: imageArt({
    GoldBar: symbol('goldbar'), GoldBar2: symbol('goldbar2'), GoldBar3: symbol('goldbar3'),
    Diamond: symbol('diamond'), Diamond2: symbol('diamond2'), Cart: symbol('cart'), Cart2: symbol('cart2'),
    Lantern: symbol('lantern'), Lantern2: symbol('lantern2'), Copper: symbol('copper'), Silver: symbol('silver'), Quartz: symbol('quartz'),
    Miner: symbol('miner'), Miner2: symbol('miner2'), Dynamite: symbol('dynamite'),
  }),
  background: { portrait: 'assets/gold-reef-rush/bg-portrait.jpg', landscape: 'assets/gold-reef-rush/bg-landscape.jpg' },
  logo: 'assets/gold-reef-rush/logo.webp',
  colours: { background: 0x120c06, backgroundTop: 0x2b1d0e, frame: 0xe0a93a, accent: 0xffc94a, featureTint: 0x5a2a08 },
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
    <p>Free spins are played on their own reels at the stake that triggered them. Every double or triple that lands, Wilds included, <b>locks in place</b> for the rest of the free spins. Free spins cannot be retriggered.</p>`,
});
