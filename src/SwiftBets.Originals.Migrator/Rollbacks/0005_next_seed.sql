-- Rolls back 0005_next_seed: rotation goes back to drawing the new seed at the moment of rotation.
ALTER TABLE originals.player_seeds DROP COLUMN IF EXISTS next_server_seed_hash;
ALTER TABLE originals.player_seeds DROP COLUMN IF EXISTS next_server_seed;
