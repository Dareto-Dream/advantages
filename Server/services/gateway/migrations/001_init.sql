CREATE TABLE IF NOT EXISTS players (
  id            uuid PRIMARY KEY,
  display_name  text        NOT NULL,
  tag           char(4)     NOT NULL,
  device_id     text        UNIQUE,
  email         text        UNIQUE,
  password_hash text,
  provider      text,
  provider_uid  text,
  created_at    timestamptz NOT NULL DEFAULT now(),
  last_seen_at  timestamptz NOT NULL DEFAULT now(),
  lifetime      jsonb       NOT NULL DEFAULT '{}'::jsonb,
  UNIQUE (display_name, tag),
  UNIQUE (provider, provider_uid)
);

CREATE INDEX IF NOT EXISTS players_last_seen_idx ON players (last_seen_at DESC);

CREATE TABLE IF NOT EXISTS friendships (
  low_id     uuid        NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  high_id    uuid        NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  status     text        NOT NULL,

  actor_id   uuid        NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (low_id, high_id),
  CHECK (low_id < high_id)
);

CREATE INDEX IF NOT EXISTS friendships_high_idx ON friendships (high_id);

CREATE TABLE IF NOT EXISTS game_servers (
  id             text        PRIMARY KEY,
  region         text        NOT NULL,
  relay_url      text        NOT NULL,
  build_version  text        NOT NULL DEFAULT 'dev',
  capacity       int         NOT NULL DEFAULT 1,
  active_rooms   int         NOT NULL DEFAULT 0,
  players        int         NOT NULL DEFAULT 0,
  status         text        NOT NULL DEFAULT 'ready',
  registered_at  timestamptz NOT NULL DEFAULT now(),
  heartbeat_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS game_servers_region_idx ON game_servers (region, status, heartbeat_at DESC);

CREATE TABLE IF NOT EXISTS rooms (
  id           uuid        PRIMARY KEY,
  server_id    text        REFERENCES game_servers(id) ON DELETE SET NULL,
  region       text        NOT NULL,
  mode         text        NOT NULL,
  state        text        NOT NULL DEFAULT 'lobby',
  visibility   text        NOT NULL DEFAULT 'public',
  join_code    text        UNIQUE,
  team_size    int         NOT NULL DEFAULT 4,
  bots_enabled boolean     NOT NULL DEFAULT true,
  host_id      uuid        REFERENCES players(id) ON DELETE SET NULL,
  created_at   timestamptz NOT NULL DEFAULT now(),
  updated_at   timestamptz NOT NULL DEFAULT now(),
  closed_at    timestamptz
);

CREATE INDEX IF NOT EXISTS rooms_open_idx ON rooms (state, region) WHERE closed_at IS NULL;

CREATE TABLE IF NOT EXISTS room_members (
  room_id   uuid        NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
  player_id uuid        NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  team      int         NOT NULL DEFAULT 0,
  joined_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (room_id, player_id)
);

CREATE INDEX IF NOT EXISTS room_members_player_idx ON room_members (player_id);

CREATE TABLE IF NOT EXISTS match_results (
  id         bigserial   PRIMARY KEY,
  room_id    uuid        NOT NULL,
  player_id  uuid        NOT NULL REFERENCES players(id) ON DELETE CASCADE,
  mode       text        NOT NULL,
  team       int         NOT NULL,
  won        boolean     NOT NULL,
  kills      int         NOT NULL DEFAULT 0,
  deaths     int         NOT NULL DEFAULT 0,
  assists    int         NOT NULL DEFAULT 0,
  damage     int         NOT NULL DEFAULT 0,
  healing    int         NOT NULL DEFAULT 0,
  operative  text,
  ended_at   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS match_results_player_idx ON match_results (player_id, ended_at DESC);
