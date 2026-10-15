-- A voided round whose rollback the casino has not yet confirmed; RoundRecovery retries it until it has.
ALTER TABLE originals.rounds ADD COLUMN rollback_pending boolean NOT NULL DEFAULT false;
CREATE INDEX ix_rounds_recovery ON originals.rounds (created_at) WHERE status = 'pending' OR rollback_pending;
