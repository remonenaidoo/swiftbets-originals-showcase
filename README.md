# swiftbets-originals

[![ci](https://github.com/remonenaidoo/swiftbets-originals-showcase/actions/workflows/ci.yml/badge.svg)](https://github.com/remonenaidoo/swiftbets-originals-showcase/actions/workflows/ci.yml)

SwiftBets Originals: games we design, build and own, played for the SwiftBets wallet. The studio is integrated
the way any outside provider is: it reaches the wallet only through the casino's signed seamless-wallet
API, as provider `swift-originals`. Restrictions, limits, idempotency and reconciliation therefore apply exactly as
for any provider.

One service hosts every slot. A game is data: a definition and a profile in `SwiftBets.Originals.Games`,
and a config folder in `web/games/`. The engine, the round handling and the client kit are shared.

| Game | | PAR sheet |
| --- | --- | --- |
| **Gilded Savanna** | 5×3, 20 lines; Protea Wild on reels 2–4; Baobab Scatter, 3+ = 10 free spins ×2, retriggerable. RTP 96.00%, exact. | [gilded-savanna.md](docs/par-sheet/gilded-savanna.md) |
| **Gold Reef Rush** | 5×3, 15 lines, 15-coin stakes; split symbols (Gold Bar to ×3, a line counts to 15); Miner Wild and Double Miner on reels 2–4; Dynamite Scatter, 3+ = 8 free spins where every split locks. RTP 96.00% (base exact, feature simulated). | [gold-reef-rush.md](docs/par-sheet/gold-reef-rush.md) |

Both cap a round (a paid spin and its feature) at 5,000× stake.

| Project | Purpose |
| --- | --- |
| `SwiftBets.Originals.Domain` | The slot engine, with no game in it: symbol table, strips, evaluator (split symbols by weight), feature rules, locks, the cap, the round engine, the definition hash. |
| `SwiftBets.Originals.Games` | The games: each a definition plus a profile (name, stated RTP, stakes), registered in `OriginalsGames.Catalog`. |
| `SwiftBets.Originals.Application` | Spin, free spins, state and recovery for any game; ports for the round store and the wallet. |
| `SwiftBets.Originals.Infrastructure` | Postgres round store, the signed wallet client, the recovery job. |
| `SwiftBets.Originals.Api` | The host: the games' API and their pages (served from `wwwroot`). |
| `SwiftBets.Originals.Migrator` | DbUp migrations for `sb_originals` (schema `originals`), each with a rollback. |
| `tools/SwiftBets.Originals.Maths` | RTP, PAR sheets and simulations, per game. |
| `web/packages/slot-kit` | The shared client: reels, HUD, speed levels, tap to stop, balance, autoplay, rules, history, sound, locks, art loaders. |
| `web/games/<id>` | One page per game: its art, colours and rules copy. |

Twenty-two games are live: nine slots (line, ways, hold-and-win and cascade engines) and thirteen instant and table games (Crash, Dice, Limbo, Mines, Towers, Keno, Peg Drop, Sunburst Wheel, Coin Flip, Lucky Scratch, Gem Hand, Baccarat, Higher or Lower). Instant games are provably fair: each result comes from HMAC-SHA256 over a committed server seed, the player's seed and a nonce, and the page can verify it. Each game's return rate is set in configuration.

## Adding a slot

1. `src/SwiftBets.Originals.Games/<Game>.cs`: symbols, reel counts, lines, pays, `FeatureRules`, and a `GameProfile`; add it to `OriginalsGames`.
2. Tune and generate its PAR sheet (below); add its id to `ParSheetTests`.
3. `web/games/<id>/index.html` and `main.ts`: `startSlot({ gameId, title, art, colours, rules })`. Use `placeholderArt` until the art exists, then `imageArt`.
4. A catalogue row in swiftbets-casino (`CatalogMigrator`), so the lobby offers it.

## The maths

Each reel is a fixed strip; a spin draws one stop per reel uniformly with `RandomNumberGenerator.GetInt32`. The
maths tool evaluates **every** combination of base stops with the production evaluator, summing wins as exact
integers, so the base RTP is an exact fraction. A stateless feature is exact in closed form (`w` per free spin,
`r` extra spins per spin, `N = n / (1 − r)`, value `w × N`). A feature whose spins depend on each other (locked
symbols) is valued by playing 100,000,000 whole features through the production engine, with a 99% confidence
interval. A 1,000,000,000-round simulation then checks the total and measures the cap.

```bash
M="dotnet run -c Release --project tools/SwiftBets.Originals.Maths --"
$M rtp --game gold-reef-rush                     # base exact; locking feature simulated (--features N)
$M simulate --game gold-reef-rush --spins 1000000000   # writes docs/par-sheet/<game>.simulation.json
$M par --game gold-reef-rush --with-simulation docs/par-sheet/gold-reef-rush.simulation.json
```

`ParSheetTests` fails if a game's strips, lines or pays change without its PAR sheet being regenerated (it compares
the definition hash recorded in the sheet).

## A spin

`POST /games/{gameId}/spin { session, stake, key }` (behind the gateway at `/originals`):

1. the stake must be one of the game's stakes (ZAR cents); the casino validates the session;
2. a repeated `key` returns the original round; a player with unfinished free spins in this game gets 409 `feature_in_progress`;
3. the result is drawn and stored (`pending`) **before** money moves;
4. `bet` for the stake (refused → `refused`, nothing moves; no clear answer → `rollback`, `voided`);
5. `win` for the win (no answer → the spin stays owed and the recovery job pays it under the same transaction id);
6. `settled`, or `feature` with its free spins.

`POST /games/{gameId}/feature/next { session, roundId }` plays one free spin inside one locked transaction (no
stake; paid as it lands, under the round's cap; a locking game's locked cells are read from the last spin under the
same lock). `GET /games/{gameId}/state?session=` returns the balance, an unfinished feature to resume, the last 50
rounds and the rules. Transaction ids are `{round}:bet`, `{round}:win:{seq}`, `{round}:rollback`.

The casino's wallet replies carry `playerRef`, an HMAC of the punter id under this provider's secret: stable, so a
feature resumes across sessions, without the studio learning who the player is.

## Client

PixiJS 8 (WebGL), opened by the casino's launch link `/originals/play?session=…&game=…`. The server decides every
outcome; the client spins the real strips and lands on the server's stops. `npm run dev` in `web/` serves every game
at `/originals/games/<id>/index.html?session=…` against the preview's API.

## Part of SwiftBets

SwiftBets is a sportsbook and casino platform built as independent .NET services. It runs live at [swiftbets.remonenaidoo.com](https://swiftbets.remonenaidoo.com). These repositories show a selection of it: [Settlement](https://github.com/remonenaidoo/swiftbets-settlement-showcase) · [Wallet](https://github.com/remonenaidoo/swiftbets-wallet-showcase) · [Steward](https://github.com/remonenaidoo/swiftbets-steward-showcase) · [Building blocks](https://github.com/remonenaidoo/swiftbets-building-blocks-showcase) · [Contracts](https://github.com/remonenaidoo/swiftbets-contracts-showcase).
