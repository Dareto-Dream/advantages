import { Redis } from 'ioredis';
import { config } from '../config.js';
import { log } from '../util/log.js';

export type Listener = (channel: string, payload: any) => void;

export interface Bus {
  readonly distributed: boolean;
  publish(channel: string, payload: unknown): Promise<void>;
  subscribe(listener: Listener): void;

  zadd(key: string, score: number, member: string): Promise<void>;
  zrem(key: string, member: string): Promise<void>;
  zrange(key: string, limit: number): Promise<string[]>;
  hset(key: string, field: string, value: string, ttlSeconds?: number): Promise<void>;
  hget(key: string, field: string): Promise<string | null>;
  hgetall(key: string): Promise<Record<string, string>>;
  hdel(key: string, field: string): Promise<void>;

  lock(key: string, ttlMs: number): Promise<boolean>;
  unlock(key: string): Promise<void>;
}

const CHANNEL = 'advantage';

class RedisBus implements Bus {
  readonly distributed = true;
  private readonly listeners: Listener[] = [];
  private readonly token = Math.random().toString(36).slice(2);

  constructor(private readonly pub: Redis, private readonly sub: Redis) {
    this.sub.subscribe(CHANNEL).catch((err) => log.error('redis subscribe failed', { message: err.message }));
    this.sub.on('message', (_ch, raw) => {
      let parsed: { channel: string; payload: any };
      try {
        parsed = JSON.parse(raw);
      } catch {
        return;
      }
      for (const listener of this.listeners) listener(parsed.channel, parsed.payload);
    });
  }

  async publish(channel: string, payload: unknown): Promise<void> {
    await this.pub.publish(CHANNEL, JSON.stringify({ channel, payload }));
  }

  subscribe(listener: Listener): void {
    this.listeners.push(listener);
  }

  async zadd(key: string, score: number, member: string): Promise<void> {
    await this.pub.zadd(key, score, member);
  }

  async zrem(key: string, member: string): Promise<void> {
    await this.pub.zrem(key, member);
  }

  async zrange(key: string, limit: number): Promise<string[]> {
    return this.pub.zrange(key, 0, Math.max(0, limit - 1));
  }

  async hset(key: string, field: string, value: string, ttlSeconds?: number): Promise<void> {
    await this.pub.hset(key, field, value);
    if (ttlSeconds) await this.pub.expire(key, ttlSeconds);
  }

  async hget(key: string, field: string): Promise<string | null> {
    return this.pub.hget(key, field);
  }

  async hgetall(key: string): Promise<Record<string, string>> {
    return this.pub.hgetall(key);
  }

  async hdel(key: string, field: string): Promise<void> {
    await this.pub.hdel(key, field);
  }

  async lock(key: string, ttlMs: number): Promise<boolean> {
    const res = await this.pub.set(key, this.token, 'PX', ttlMs, 'NX');
    return res === 'OK';
  }

  async unlock(key: string): Promise<void> {
    const held = await this.pub.get(key);
    if (held === this.token) await this.pub.del(key);
  }
}

class MemoryBus implements Bus {
  readonly distributed = false;
  private readonly listeners: Listener[] = [];
  private readonly zsets = new Map<string, Map<string, number>>();
  private readonly hashes = new Map<string, Map<string, string>>();
  private readonly locks = new Map<string, number>();

  async publish(channel: string, payload: unknown): Promise<void> {

    const copy = JSON.parse(JSON.stringify(payload ?? null));
    for (const listener of this.listeners) listener(channel, copy);
  }

  subscribe(listener: Listener): void {
    this.listeners.push(listener);
  }

  private zset(key: string): Map<string, number> {
    let set = this.zsets.get(key);
    if (!set) {
      set = new Map();
      this.zsets.set(key, set);
    }
    return set;
  }

  private hash(key: string): Map<string, string> {
    let hash = this.hashes.get(key);
    if (!hash) {
      hash = new Map();
      this.hashes.set(key, hash);
    }
    return hash;
  }

  async zadd(key: string, score: number, member: string): Promise<void> {
    this.zset(key).set(member, score);
  }

  async zrem(key: string, member: string): Promise<void> {
    this.zset(key).delete(member);
  }

  async zrange(key: string, limit: number): Promise<string[]> {
    return [...this.zset(key).entries()]
      .sort((a, b) => a[1] - b[1])
      .slice(0, limit)
      .map((entry) => entry[0]);
  }

  async hset(key: string, field: string, value: string): Promise<void> {
    this.hash(key).set(field, value);
  }

  async hget(key: string, field: string): Promise<string | null> {
    return this.hash(key).get(field) ?? null;
  }

  async hgetall(key: string): Promise<Record<string, string>> {
    return Object.fromEntries(this.hash(key));
  }

  async hdel(key: string, field: string): Promise<void> {
    this.hash(key).delete(field);
  }

  async lock(key: string, ttlMs: number): Promise<boolean> {
    const now = Date.now();
    const held = this.locks.get(key);
    if (held && held > now) return false;
    this.locks.set(key, now + ttlMs);
    return true;
  }

  async unlock(key: string): Promise<void> {
    this.locks.delete(key);
  }
}

function createBus(): Bus {
  if (!config.redisUrl) {
    log.warn('REDIS_URL not set - running single-replica with an in-process bus');
    return new MemoryBus();
  }

  const options = { maxRetriesPerRequest: null, lazyConnect: false };
  const pub = new Redis(config.redisUrl, options);
  const sub = new Redis(config.redisUrl, options);

  pub.on('error', (err) => log.error('redis pub error', { message: err.message }));
  sub.on('error', (err) => log.error('redis sub error', { message: err.message }));

  log.info('redis bus configured');
  return new RedisBus(pub, sub);
}

export const bus: Bus = createBus();
