import { badRequest, conflict, forbidden, notFound } from '../util/http.js';
import { one, query } from './db.js';
import { byIds, type PublicPlayer, toPublic, type PlayerRow } from './players.js';

export type FriendStatus = 'pending' | 'accepted' | 'blocked';

interface FriendRow {
  low_id: string;
  high_id: string;
  status: FriendStatus;
  actor_id: string;
  created_at: Date;
  updated_at: Date;
}

function pair(a: string, b: string): [string, string] {
  return a < b ? [a, b] : [b, a];
}

export interface FriendEntry {
  player: PublicPlayer;
  status: FriendStatus;

  incoming: boolean;
  since: string;
}

export interface FriendList {
  friends: FriendEntry[];
  incoming: FriendEntry[];
  outgoing: FriendEntry[];
  blocked: FriendEntry[];
}

async function rowFor(a: string, b: string): Promise<FriendRow | null> {
  const [low, high] = pair(a, b);
  return one<FriendRow>('SELECT * FROM friendships WHERE low_id = $1 AND high_id = $2', [low, high]);
}

export async function areFriends(a: string, b: string): Promise<boolean> {
  const row = await rowFor(a, b);
  return row?.status === 'accepted';
}

export async function friendIdsOf(playerId: string): Promise<string[]> {
  const rows = await query<{ other: string }>(
    `SELECT CASE WHEN low_id = $1 THEN high_id ELSE low_id END AS other
     FROM friendships
     WHERE status = 'accepted' AND (low_id = $1 OR high_id = $1)`,
    [playerId],
  );
  return rows.map((row) => row.other);
}

export async function list(playerId: string): Promise<FriendList> {
  const rows = await query<FriendRow>(
    'SELECT * FROM friendships WHERE low_id = $1 OR high_id = $1 ORDER BY updated_at DESC',
    [playerId],
  );

  const otherIds = rows.map((row) => (row.low_id === playerId ? row.high_id : row.low_id));
  const players = await byIds(otherIds);

  const out: FriendList = { friends: [], incoming: [], outgoing: [], blocked: [] };

  for (const row of rows) {
    const otherId = row.low_id === playerId ? row.high_id : row.low_id;
    const other = players.get(otherId);
    if (!other) continue;

    const entry: FriendEntry = {
      player: toPublic(other),
      status: row.status,
      incoming: row.actor_id !== playerId,
      since: row.updated_at.toISOString(),
    };

    if (row.status === 'accepted') out.friends.push(entry);
    else if (row.status === 'blocked') {

      if (row.actor_id === playerId) out.blocked.push(entry);
    } else if (entry.incoming) out.incoming.push(entry);
    else out.outgoing.push(entry);
  }

  return out;
}

export interface RequestOutcome {

  result: 'requested' | 'accepted';
  other: PlayerRow;
}

export async function request(me: string, other: PlayerRow): Promise<RequestOutcome> {
  if (me === other.id) throw badRequest('you cannot add yourself');

  const existing = await rowFor(me, other.id);
  const [low, high] = pair(me, other.id);

  if (existing) {
    if (existing.status === 'blocked') {

      throw forbidden('cannot send a request to that player');
    }
    if (existing.status === 'accepted') throw conflict('already friends');

    if (existing.actor_id !== me) {
      await query(
        `UPDATE friendships SET status = 'accepted', updated_at = now()
         WHERE low_id = $1 AND high_id = $2`,
        [low, high],
      );
      return { result: 'accepted', other };
    }

    return { result: 'requested', other };
  }

  await query(
    `INSERT INTO friendships (low_id, high_id, status, actor_id)
     VALUES ($1, $2, 'pending', $3)
     ON CONFLICT (low_id, high_id) DO NOTHING`,
    [low, high, me],
  );

  return { result: 'requested', other };
}

export async function accept(me: string, otherId: string): Promise<void> {
  const [low, high] = pair(me, otherId);
  const updated = await one(
    `UPDATE friendships SET status = 'accepted', updated_at = now()
     WHERE low_id = $1 AND high_id = $2 AND status = 'pending' AND actor_id <> $3
     RETURNING low_id`,
    [low, high, me],
  );
  if (!updated) throw notFound('no pending request from that player');
}

export async function remove(me: string, otherId: string): Promise<void> {
  const [low, high] = pair(me, otherId);
  await query(
    `DELETE FROM friendships
     WHERE low_id = $1 AND high_id = $2 AND (status <> 'blocked' OR actor_id = $3)`,
    [low, high, me],
  );
}

export async function block(me: string, otherId: string): Promise<void> {
  if (me === otherId) throw badRequest('you cannot block yourself');
  const [low, high] = pair(me, otherId);

  await query(
    `INSERT INTO friendships (low_id, high_id, status, actor_id)
     VALUES ($1, $2, 'blocked', $3)
     ON CONFLICT (low_id, high_id)
     DO UPDATE SET status = 'blocked', actor_id = $3, updated_at = now()`,
    [low, high, me],
  );
}

export async function unblock(me: string, otherId: string): Promise<void> {
  const [low, high] = pair(me, otherId);
  await query(
    `DELETE FROM friendships WHERE low_id = $1 AND high_id = $2 AND status = 'blocked' AND actor_id = $3`,
    [low, high, me],
  );
}
