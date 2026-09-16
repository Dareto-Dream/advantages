import { conflict, notFound } from '../util/http.js';
import { randomName, randomTag, uuid } from '../util/ids.js';
import { one, query } from './db.js';

export interface PlayerRow {
  id: string;
  display_name: string;
  tag: string;
  device_id: string | null;
  email: string | null;
  created_at: Date;
  last_seen_at: Date;
}

export interface PublicPlayer {
  id: string;
  name: string;
  tag: string;

  handle: string;

  guest: boolean;
}

export function toPublic(row: PlayerRow): PublicPlayer {
  return {
    id: row.id,
    name: row.display_name,
    tag: row.tag,
    handle: `${row.display_name}#${row.tag}`,
    guest: row.email == null,
  };
}

const NAME_PATTERN = /^[A-Za-z0-9 _.-]{3,16}$/;

export function validateName(name: string): string {
  const trimmed = name.trim();
  if (!NAME_PATTERN.test(trimmed)) {
    throw conflict('names are 3-16 characters, letters/digits/space/_/./- only');
  }
  return trimmed;
}

export async function loginWithDevice(deviceId: string, preferredName: string | null): Promise<PlayerRow> {
  const existing = await one<PlayerRow>('SELECT * FROM players WHERE device_id = $1', [deviceId]);
  if (existing) {
    await query('UPDATE players SET last_seen_at = now() WHERE id = $1', [existing.id]);
    return existing;
  }

  const name = preferredName ? validateName(preferredName) : randomName();

  for (let attempt = 0; attempt < 12; attempt++) {
    try {
      const row = await one<PlayerRow>(
        `INSERT INTO players (id, display_name, tag, device_id)
         VALUES ($1, $2, $3, $4)
         ON CONFLICT (display_name, tag) DO NOTHING
         RETURNING *`,
        [uuid(), name, randomTag(), deviceId],
      );
      if (row) return row;
    } catch (err) {

      if ((err as { code?: string }).code !== UNIQUE_VIOLATION) throw err;
      const raced = await one<PlayerRow>('SELECT * FROM players WHERE device_id = $1', [deviceId]);
      if (raced) return raced;
    }
  }

  throw conflict('could not allocate a tag for that name, try another');
}

export async function byId(id: string): Promise<PlayerRow> {
  const row = await one<PlayerRow>('SELECT * FROM players WHERE id = $1', [id]);
  if (!row) throw notFound('player not found');
  return row;
}

export async function byIds(ids: string[]): Promise<Map<string, PlayerRow>> {
  if (ids.length === 0) return new Map();
  const rows = await query<PlayerRow>('SELECT * FROM players WHERE id = ANY($1::uuid[])', [ids]);
  return new Map(rows.map((row) => [row.id, row]));
}

export async function byHandle(handle: string): Promise<PlayerRow | null> {
  const hash = handle.lastIndexOf('#');
  if (hash <= 0) return null;

  const name = handle.slice(0, hash).trim();
  const tag = handle.slice(hash + 1).trim();
  if (!/^\d{4}$/.test(tag)) return null;

  return one<PlayerRow>('SELECT * FROM players WHERE lower(display_name) = lower($1) AND tag = $2', [name, tag]);
}

const UNIQUE_VIOLATION = '23505';

export async function rename(id: string, name: string): Promise<PlayerRow> {
  const clean = validateName(name);
  const current = await byId(id);

  for (let attempt = 0; attempt < 12; attempt++) {
    const tag = attempt === 0 ? current.tag : randomTag();
    try {
      const row = await one<PlayerRow>(
        'UPDATE players SET display_name = $2, tag = $3 WHERE id = $1 RETURNING *',
        [id, clean, tag],
      );
      if (row) return row;
    } catch (err) {
      if ((err as { code?: string }).code !== UNIQUE_VIOLATION) throw err;
    }
  }

  throw conflict('could not allocate a tag for that name, try another');
}

export async function touch(id: string): Promise<void> {
  await query('UPDATE players SET last_seen_at = now() WHERE id = $1', [id]);
}
