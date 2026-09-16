import type { Mode, Region } from '../config.js';
import { conflict, notFound } from '../util/http.js';
import { joinCode, uuid } from '../util/ids.js';
import { one, query } from './db.js';
import { pickForRegion, type ServerRow } from './servers.js';

export type RoomState = 'lobby' | 'live' | 'over';
export type RoomVisibility = 'public' | 'private';

export interface RoomRow {
  id: string;
  server_id: string | null;
  region: Region;
  mode: Mode;
  state: RoomState;
  visibility: RoomVisibility;
  join_code: string | null;
  team_size: number;
  bots_enabled: boolean;
  host_id: string | null;
  created_at: Date;
  closed_at: Date | null;
}

export interface CreateRoomInput {
  region: Region;
  mode: Mode;
  visibility: RoomVisibility;
  teamSize: number;
  botsEnabled: boolean;
  hostId: string | null;
}

export interface CreatedRoom {
  room: RoomRow;
  server: ServerRow;
}

export async function create(input: CreateRoomInput): Promise<CreatedRoom> {
  const server = await pickForRegion(input.region);
  if (!server) throw conflict(`no game server available in ${input.region}`);

  for (let attempt = 0; attempt < 8; attempt++) {
    const code = input.visibility === 'private' ? joinCode() : null;
    const row = await one<RoomRow>(
      `INSERT INTO rooms (id, server_id, region, mode, state, visibility, join_code, team_size, bots_enabled, host_id)
       VALUES ($1, $2, $3, $4, 'lobby', $5, $6, $7, $8, $9)
       ON CONFLICT (join_code) DO NOTHING
       RETURNING *`,
      [
        uuid(),
        server.id,
        input.region,
        input.mode,
        input.visibility,
        code,
        input.teamSize,
        input.botsEnabled,
        input.hostId,
      ],
    );

    if (row) {
      await query('UPDATE game_servers SET active_rooms = active_rooms + 1 WHERE id = $1', [server.id]);
      return { room: row, server };
    }
  }

  throw conflict('could not allocate a join code');
}

export async function byId(id: string): Promise<RoomRow> {
  const row = await one<RoomRow>('SELECT * FROM rooms WHERE id = $1', [id]);
  if (!row) throw notFound('room not found');
  return row;
}

export async function byCode(code: string): Promise<RoomRow> {
  const row = await one<RoomRow>(
    `SELECT * FROM rooms WHERE join_code = upper($1) AND closed_at IS NULL AND state <> 'over'`,
    [code],
  );
  if (!row) throw notFound('no open room with that code');
  return row;
}

export async function addMember(roomId: string, playerId: string, team: number): Promise<void> {
  await query(
    `INSERT INTO room_members (room_id, player_id, team) VALUES ($1, $2, $3)
     ON CONFLICT (room_id, player_id) DO UPDATE SET team = EXCLUDED.team`,
    [roomId, playerId, team],
  );
}

export async function removeMember(roomId: string, playerId: string): Promise<void> {
  await query('DELETE FROM room_members WHERE room_id = $1 AND player_id = $2', [roomId, playerId]);
}

export async function memberIds(roomId: string): Promise<string[]> {
  const rows = await query<{ player_id: string }>(
    'SELECT player_id FROM room_members WHERE room_id = $1',
    [roomId],
  );
  return rows.map((row) => row.player_id);
}

export async function memberCount(roomId: string): Promise<number> {
  const row = await one<{ count: string }>(
    'SELECT count(*) AS count FROM room_members WHERE room_id = $1',
    [roomId],
  );
  return Number(row?.count ?? 0);
}

export async function currentRoomOf(playerId: string): Promise<RoomRow | null> {
  return one<RoomRow>(
    `SELECT r.* FROM rooms r
     JOIN room_members m ON m.room_id = r.id
     WHERE m.player_id = $1 AND r.closed_at IS NULL AND r.state <> 'over'
     ORDER BY m.joined_at DESC
     LIMIT 1`,
    [playerId],
  );
}

export async function setState(roomId: string, state: RoomState): Promise<void> {
  await query('UPDATE rooms SET state = $2, updated_at = now() WHERE id = $1', [roomId, state]);
}

export async function setBotsEnabled(roomId: string, enabled: boolean): Promise<void> {
  await query('UPDATE rooms SET bots_enabled = $2, updated_at = now() WHERE id = $1', [roomId, enabled]);
}

export async function close(roomId: string): Promise<void> {
  const row = await one<{ server_id: string | null }>(
    `UPDATE rooms SET state = 'over', closed_at = now(), updated_at = now()
     WHERE id = $1 AND closed_at IS NULL
     RETURNING server_id`,
    [roomId],
  );

  if (row?.server_id) {
    await query(
      'UPDATE game_servers SET active_rooms = greatest(active_rooms - 1, 0) WHERE id = $1',
      [row.server_id],
    );
  }
}

export async function findJoinable(region: Region, mode: Mode, maxPlayers: number): Promise<RoomRow | null> {
  return one<RoomRow>(
    `SELECT r.* FROM rooms r
     WHERE r.region = $1 AND r.mode = $2 AND r.visibility = 'public'
       AND r.closed_at IS NULL AND r.state = 'lobby'
       AND (SELECT count(*) FROM room_members m WHERE m.room_id = r.id) < $3
     ORDER BY r.created_at ASC
     LIMIT 1`,
    [region, mode, maxPlayers],
  );
}
