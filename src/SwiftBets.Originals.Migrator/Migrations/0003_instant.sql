-- Instant games (D209): a round that has taken its stake and is still being played (Mines picks, a Crash bet in
-- flight) is 'open'; each player's provably-fair seeds, the active server seed revealed only when rotated.
ALTER TABLE originals.rounds DROP CONSTRAINT rounds_status_check;
ALTER TABLE originals.rounds ADD CONSTRAINT rounds_status_check
    CHECK (status IN ('pending', 'refused', 'voided', 'win_pending', 'feature', 'settled', 'open'));
CREATE INDEX ix_rounds_open_instant ON originals.rounds (player_ref, game_id) WHERE status = 'open';

CREATE TABLE originals.player_seeds
(
    player_ref           text        PRIMARY KEY,
    server_seed          text        NOT NULL,   -- secret until rotated
    server_seed_hash     text        NOT NULL,   -- SHA-256 of server_seed, shown before play
    client_seed          text        NOT NULL,
    nonce                bigint      NOT NULL DEFAULT 0,  -- the next round's nonce
    previous_server_seed text        NULL,       -- revealed: every round under it can be checked
    previous_client_seed text        NULL,
    previous_nonce       bigint      NULL,       -- rounds 0 .. previous_nonce - 1 used the previous seeds
    rotated_at           timestamptz NOT NULL DEFAULT now()
);
