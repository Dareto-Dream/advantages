ALTER TABLE game_servers
ADD COLUMN game_port int DEFAULT 7777,
ADD COLUMN query_port int DEFAULT 7778,
ADD COLUMN rcon_port int DEFAULT 7779;
