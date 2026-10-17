-- The next server seed is fixed, and its hash shown, before the player chooses the client seed it will be used
-- with: on rotation it becomes the active seed. A seed chosen after the client seed could be picked to suit it.
ALTER TABLE originals.player_seeds ADD COLUMN next_server_seed text NULL;
ALTER TABLE originals.player_seeds ADD COLUMN next_server_seed_hash text NULL;
