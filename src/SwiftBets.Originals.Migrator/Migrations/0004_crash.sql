-- Crash (D209): the shared rounds, each fixed by its seed (secret until it has crashed); a bet is a round row
-- pointing at its shared round, with its auto cash-out target. Seeds remember when a nonce was last taken, so a
-- rotation never reveals the seed of a round still being started.
CREATE TABLE originals.crash_rounds
(
    id            uuid        PRIMARY KEY,
    seq           bigint      NOT NULL UNIQUE,
    seed          text        NOT NULL,
    seed_hash     text        NOT NULL,
    point_bp      bigint      NOT NULL CHECK (point_bp >= 10000),
    phase         text        NOT NULL CHECK (phase IN ('betting', 'flying', 'crashed', 'void')),
    betting_until timestamptz NOT NULL,   -- the flight starts here
    crashed_at    timestamptz NULL
);
CREATE INDEX ix_crash_rounds_unfinished ON originals.crash_rounds (seq) WHERE phase IN ('betting', 'flying');

ALTER TABLE originals.rounds ADD COLUMN table_round uuid NULL REFERENCES originals.crash_rounds (id);
ALTER TABLE originals.rounds ADD COLUMN auto_bp bigint NULL;
CREATE INDEX ix_rounds_table ON originals.rounds (table_round) WHERE table_round IS NOT NULL;

ALTER TABLE originals.player_seeds ADD COLUMN last_taken_at timestamptz NOT NULL DEFAULT '-infinity';
