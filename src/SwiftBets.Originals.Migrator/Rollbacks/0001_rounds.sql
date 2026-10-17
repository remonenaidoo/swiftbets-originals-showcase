-- Rolls back 0001_rounds. Every round and its history goes; settle or refund anything open first.
DROP TABLE IF EXISTS originals.spins;
DROP TABLE IF EXISTS originals.rounds;
DROP SCHEMA IF EXISTS originals;
