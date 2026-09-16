import { readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import pg from 'pg';
import { config } from '../config.js';
import { log } from '../util/log.js';

const { Pool } = pg;

export const pool = new Pool({
  connectionString: config.databaseUrl,
  ssl: config.databaseUrl.includes('railway') ? { rejectUnauthorized: false } : undefined,
  max: 10,
  idleTimeoutMillis: 30_000,
  connectionTimeoutMillis: 10_000,
});

pool.on('error', (err) => log.error('postgres pool error', { message: err.message }));

export async function query<T = any>(text: string, params: unknown[] = []): Promise<T[]> {
  const result = await pool.query(text, params as any[]);
  return result.rows as T[];
}

export async function one<T = any>(text: string, params: unknown[] = []): Promise<T | null> {
  const rows = await query<T>(text, params);
  return rows.length > 0 ? rows[0] : null;
}

export async function transact<T>(fn: (q: typeof query) => Promise<T>): Promise<T> {
  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    const scoped = async (text: string, params: unknown[] = []) => {
      const result = await client.query(text, params as any[]);
      return result.rows as any[];
    };
    const out = await fn(scoped as typeof query);
    await client.query('COMMIT');
    return out;
  } catch (err) {
    await client.query('ROLLBACK').catch(() => {});
    throw err;
  } finally {
    client.release();
  }
}

const MIGRATION_LOCK = 1145130561;

export async function migrate(): Promise<void> {
  await query(`CREATE TABLE IF NOT EXISTS schema_migrations (
    name text PRIMARY KEY,
    applied_at timestamptz NOT NULL DEFAULT now()
  )`);

  await query('SELECT pg_advisory_lock($1)', [MIGRATION_LOCK]);
  try {
    const here = dirname(fileURLToPath(import.meta.url));
    const files = ['001_init.sql', '002_add_server_ports.sql'];

    for (const name of files) {
      const done = await one('SELECT name FROM schema_migrations WHERE name = $1', [name]);
      if (done) continue;

      const sql = await readFile(join(here, '..', '..', 'migrations', name), 'utf8');
      await query(sql);
      await query('INSERT INTO schema_migrations (name) VALUES ($1)', [name]);
      log.info('migration applied', { name });
    }
  } finally {
    await query('SELECT pg_advisory_unlock($1)', [MIGRATION_LOCK]);
  }
}
