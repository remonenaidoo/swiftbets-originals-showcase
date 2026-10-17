-- Gilded Savanna rounds: a base spin and the free spins it triggered, with every spin's stops and result kept for replay.
CREATE SCHEMA IF NOT EXISTS originals;

CREATE TABLE originals.rounds
(
    round_id        uuid        PRIMARY KEY,
    player_ref      text        NOT NULL,   -- opaque per-provider reference from the casino's balance reply
    session_token   text        NOT NULL,   -- the casino session the round was played in; needed to pay a delayed win
    game_id         text        NOT NULL,
    stake           bigint      NOT NULL CHECK (stake > 0),
    idempotency_key text        NOT NULL,
    status          text        NOT NULL CHECK (status IN ('pending', 'refused', 'voided', 'win_pending', 'feature', 'settled')),
    free_spins_left int         NOT NULL DEFAULT 0,
    paid_units      bigint      NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL,
    settled_at      timestamptz NULL,
    UNIQUE (player_ref, idempotency_key)
);
CREATE INDEX ix_rounds_open ON originals.rounds (player_ref) WHERE status IN ('feature', 'win_pending');
CREATE INDEX ix_rounds_recent ON originals.rounds (player_ref, created_at DESC);

CREATE TABLE originals.spins
(
    round_id      uuid    NOT NULL REFERENCES originals.rounds (round_id),
    seq           int     NOT NULL,          -- 0 = base spin, 1.. = free spins
    stops         int[]   NOT NULL,
    outcome       jsonb   NOT NULL,          -- lines, scatters and units: enough to replay the spin
    win           bigint  NOT NULL,          -- minor units owed for this spin, after the cap
    wallet_bet_tx text    NULL,
    wallet_win_tx text    NULL,
    win_paid      boolean NOT NULL DEFAULT false,
    PRIMARY KEY (round_id, seq)
);
CREATE INDEX ix_spins_unpaid ON originals.spins (round_id) WHERE win > 0 AND NOT win_paid;
