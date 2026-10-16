-- Rolls back 0003_instant. Settle or refund every open instant round first: the old status check rejects 'open'.
DROP TABLE IF EXISTS originals.player_seeds;
DROP INDEX IF EXISTS originals.ix_rounds_open_instant;
ALTER TABLE originals.rounds DROP CONSTRAINT rounds_status_check;
ALTER TABLE originals.rounds ADD CONSTRAINT rounds_status_check
    CHECK (status IN ('pending', 'refused', 'voided', 'win_pending', 'feature', 'settled'));
