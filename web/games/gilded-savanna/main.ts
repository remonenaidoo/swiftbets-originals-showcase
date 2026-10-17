import { imageArt, startSlot } from '@originals/slot-kit';

const symbol = (file: string) => `assets/symbols/${file}.webp`;

startSlot({
  gameId: 'gilded-savanna',
  title: 'Gilded Savanna',
  art: imageArt({
    Lion: symbol('lion'), Elephant: symbol('elephant'), Rhino: symbol('rhino'), Buffalo: symbol('buffalo'),
    Ace: symbol('ace'), King: symbol('king'), Queen: symbol('queen'), Jack: symbol('jack'), Ten: symbol('ten'),
    Wild: symbol('wild'), Scatter: symbol('scatter'),
  }),
  background: { portrait: 'assets/bg-portrait.jpg', landscape: 'assets/bg-landscape.jpg' },
  logo: 'assets/logo.webp',
  colours: { background: 0x0b1d22, backgroundTop: 0x0b1d22, frame: 0xd9a441, accent: 0x46e0c8, featureTint: 0x6a1b5a },
  rules: (info, _stake, img) => `
    <h3>Wild and Scatter</h3>
    <p>${img('Wild')}</p>
    <p><b>Protea Wild</b> appears on reels 2, 3 and 4 and stands in for every symbol except the Scatter.</p>
    <p><b>Baobab Scatter</b> pays anywhere on the reels, multiplied by the total stake. 3 or more award <b>${info.freeSpins} free spins</b>.</p>
    <h3>Free spins</h3>
    <p>Free spins are played on their own reels at the stake that triggered them. Every free-spin win is multiplied by <b>×${info.freeSpinMultiplier}</b>. 3 or more Scatters during free spins award ${info.freeSpins} more.</p>`,
});
