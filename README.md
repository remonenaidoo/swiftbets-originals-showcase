# swiftbets-originals

SwiftBets Originals: games we design, build and own, played for the SwiftBets wallet. The studio is integrated
the way any outside provider is (D204): it reaches the wallet only through the casino's signed seamless-wallet
API, as provider `swift-originals`. Restrictions, limits, idempotency and reconciliation therefore apply exactly as
for any provider.

The first game is **Gilded Savanna**, a 5-reel, 3-row, 20-line slot: Protea Wild on reels 2–4, Baobab Scatter
(3+ = 10 free spins, every win ×2, retriggerable), max win 5,000× stake, **RTP 96.00%** proven by full
enumeration. See the [PAR sheet](docs/par-sheet/gilded-savanna.md).

| Project | Purpose |
| --- | --- |
| `SwiftBets.Originals.Domain` | The game maths: definition, evaluator, the cap, the round engine. No references. |
| `SwiftBets.Originals.Application` | Spin, free spins, state; ports for the round store and the wallet. |
| `SwiftBets.Originals.Infrastructure` | Postgres round store, the signed wallet client, the win-retry job. |
| `SwiftBets.Originals.Api` | The host: the game API and the web client (served from `wwwroot`). |
| `SwiftBets.Originals.Migrator` | DbUp migrations for `sb_originals` (schema `originals`), each with a rollback. |
| `tools/SwiftBets.Originals.Maths` | Exact RTP, the PAR sheet, the simulation. |
| `web/` | The PixiJS client (Vite + TypeScript). |

## The maths

Each reel is a fixed 36-stop strip; a spin draws one stop per reel uniformly with
`RandomNumberGenerator.GetInt32`. The maths tool evaluates **every** combination (36⁵ = 60,466,176) with the
production evaluator, summing wins as exact integers, so the base RTP is an exact fraction. Free spins are exact in
closed form: from the free-spin strips, `w` (expected win per free spin) and `r` (expected extra spins per free
spin) give `N = 10 / (1 − r)` spins and a feature worth `w × N`. The cap's small effect and the per-round
volatility come from a 1,000,000,000-round simulation through the same round engine the service uses.

```bash
dotnet run -c Release --project tools/SwiftBets.Originals.Maths -- rtp        # exact RTP, seconds
dotnet run -c Release --project tools/SwiftBets.Originals.Maths -- simulate --spins 1000000000   # ~1.5 h on 12 cores
dotnet run -c Release --project tools/SwiftBets.Originals.Maths -- par --with-simulation docs/par-sheet/simulation.json
```

`ParSheetTests` fails if the strips, lines or pays change without the PAR sheet being regenerated (it compares the
definition hash recorded in the sheet).

## A spin

`POST /slot/spin { session, stake, key }` (behind the gateway at `/originals`):

1. the stake must be one of 100, 200, 500, 1000, 2000, 5000 (ZAR cents); the casino validates the session;
2. a repeated `key` returns the original round; a player with unfinished free spins gets 409 `feature_in_progress`;
3. the result is drawn and stored (`pending`) **before** money moves;
4. `bet` for the stake (refused → `refused`, nothing moves; no answer → `rollback`, `voided`);
5. `win` for the win (no answer → the spin stays owed and `WinRetryJob` pays it under the same transaction id);
6. `settled`, or `feature` with 10 free spins.

`POST /slot/feature/next { session, roundId }` plays one free spin (no stake; win ×2, paid as it lands, under the
round's 5,000× cap). `GET /slot/state?session=` returns the balance, an unfinished feature to resume, the last 50
rounds and the rules. Transaction ids are `{round}:bet`, `{round}:win:{seq}`, `{round}:rollback`.

The casino's wallet replies carry `playerRef`, an HMAC of the punter id under this provider's secret: stable, so a
feature resumes across sessions, without the studio learning who the player is.

## Client

PixiJS 8 (WebGL), served at `/originals/play?session=`. The server decides every outcome; the client spins the real
strips and lands on the server's stops. Win lines, big/mega/epic wins, free spins with a ×2 banner and resume, turbo,
autoplay with a mandatory loss limit, sound (synthesised, mutable), session clock, rules with the RTP and max win,
and the last 50 rounds. Artwork: `web/art` (Flux, see its README).
