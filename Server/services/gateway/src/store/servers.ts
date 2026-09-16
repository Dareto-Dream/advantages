import { config, type Region } from '../config.js';
import { log } from '../util/log.js';
import { one, query } from './db.js';

export interface ServerRow {
  id: string;
  region: Region;
  relay_url: string;
  build_version: string;
  capacity: number;
  active_rooms: number;
  players: number;
  status: 'ready' | 'draining' | 'offline';
  heartbeat_at: Date;
  game_port: number;
  query_port: number;
  rcon_port: number;
}

export interface RegisterInput {
  id: string;
  region: Region;
  relayUrl: string;
  buildVersion: string;
  capacity: number;
  gamePort?: number;
  queryPort?: number;
  rconPort?: number;
}

export async function register(input: RegisterInput): Promise<ServerRow> {
  const row = await one<ServerRow>(
    `INSERT INTO game_servers (id, region, relay_url, build_version, capacity, game_port, query_port, rcon_port, status, registered_at, heartbeat_at)
     VALUES ($1, $2, $3, $4, $5, $6, $7, $8, 'ready', now(), now())
     ON CONFLICT (id) DO UPDATE SET
       region = EXCLUDED.region,
       relay_url = EXCLUDED.relay_url,
       build_version = EXCLUDED.build_version,
       capacity = EXCLUDED.capacity,
       game_port = EXCLUDED.game_port,
       query_port = EXCLUDED.query_port,
       rcon_port = EXCLUDED.rcon_port,
       status = 'ready',
       active_rooms = 0,
       players = 0,
       heartbeat_at = now()
     RETURNING *`,
    [input.id, input.region, input.relayUrl, input.buildVersion, input.capacity, input.gamePort ?? 7777, input.queryPort ?? 7778, input.rconPort ?? 7779],
  );
  return row as ServerRow;
}

export async function heartbeat(
  id: string,
  activeRooms: number,
  players: number,
  status: ServerRow['status'],
): Promise<boolean> {
  const row = await one(
    `UPDATE game_servers
     SET active_rooms = $2, players = $3, status = $4, heartbeat_at = now()
     WHERE id = $1
     RETURNING id`,
    [id, activeRooms, players, status],
  );
  return row != null;
}

export async function unregister(id: string): Promise<void> {
  await query('DELETE FROM game_servers WHERE id = $1', [id]);
}

export async function byId(id: string): Promise<ServerRow | null> {
  return one<ServerRow>('SELECT * FROM game_servers WHERE id = $1', [id]);
}

export async function liveById(id: string): Promise<ServerRow | null> {
  return one<ServerRow>(
    `SELECT * FROM game_servers
     WHERE id = $1 AND status = 'ready' AND heartbeat_at > now() - ($2 || ' seconds')::interval`,
    [id, config.serverStaleSeconds],
  );
}

export async function pickForRegion(region: Region): Promise<ServerRow | null> {
  return one<ServerRow>(
    `SELECT * FROM game_servers
     WHERE region = $1
       AND status = 'ready'
       AND active_rooms < capacity
       AND heartbeat_at > now() - ($2 || ' seconds')::interval
     ORDER BY active_rooms ASC, players ASC, heartbeat_at DESC
     LIMIT 1`,
    [region, config.serverStaleSeconds],
  );
}

export interface RegionHealth {
  region: Region;
  relayUrl: string | null;
  servers: number;
  freeSlots: number;
  players: number;
}

export async function regionHealth(): Promise<RegionHealth[]> {
  const rows = await query<{
    region: Region;
    relay_url: string;
    servers: string;
    free_slots: string;
    players: string;
  }>(
    `SELECT region,
            min(relay_url) AS relay_url,
            count(*) AS servers,
            sum(greatest(capacity - active_rooms, 0)) AS free_slots,
            sum(players) AS players
     FROM game_servers
     WHERE status = 'ready' AND heartbeat_at > now() - ($1 || ' seconds')::interval
     GROUP BY region`,
    [config.serverStaleSeconds],
  );

  return rows.map((row) => ({
    region: row.region,
    relayUrl: row.relay_url,
    servers: Number(row.servers),
    freeSlots: Number(row.free_slots),
    players: Number(row.players),
  }));
}

export async function sweepStale(): Promise<number> {
  const dead = await query<{ id: string }>(
    `DELETE FROM game_servers
     WHERE heartbeat_at < now() - ($1 || ' seconds')::interval
     RETURNING id`,
    [config.serverStaleSeconds],
  );

  if (dead.length === 0) return 0;

  const ids = dead.map((row) => row.id);
  await query(
    `UPDATE rooms SET state = 'over', closed_at = now(), updated_at = now()
     WHERE server_id = ANY($1::text[]) AND closed_at IS NULL`,
    [ids],
  );

  log.warn('swept stale game servers', { ids });
  return dead.length;
}
