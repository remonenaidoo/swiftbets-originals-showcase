-- Rolls back 0002_rollback_pending. Unconfirmed rollbacks are no longer retried; confirm them with the casino first.
DROP INDEX IF EXISTS originals.ix_rounds_recovery;
ALTER TABLE originals.rounds DROP COLUMN IF EXISTS rollback_pending;
