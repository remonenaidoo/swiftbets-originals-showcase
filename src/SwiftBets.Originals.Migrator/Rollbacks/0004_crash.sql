-- Rolls back 0004_crash. Settle or refund every open Crash bet first; the shared rounds' seeds and history go.
ALTER TABLE originals.player_seeds DROP COLUMN IF EXISTS last_taken_at;
DROP INDEX IF EXISTS originals.ix_rounds_table;
ALTER TABLE originals.rounds DROP COLUMN IF EXISTS auto_bp;
ALTER TABLE originals.rounds DROP COLUMN IF EXISTS table_round;
DROP TABLE IF EXISTS originals.crash_rounds;
