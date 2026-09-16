import type { IncomingMessage } from 'node:http';
import type { Duplex } from 'node:stream';
import { WebSocketServer, type WebSocket } from 'ws';
import { config } from '../config.js';
import { bus } from '../store/bus.js';
import * as friends from '../store/friends.js';
import * as players from '../store/players.js';
import { verify } from '../util/jwt.js';
import { log } from '../util/log.js';

export type PresenceStatus = 'offline' | 'menu' | 'queue' | 'match';

export interface Presence {
  status: PresenceStatus;
  roomId?: string;
  at: number;
}

const PRESENCE_KEY = 'presence';
const DIRECT_CHANNEL = 'direct';
const PRESENCE_CHANNEL = 'presence';

const local = new Map<string, Set<WebSocket>>();

export const wss = new WebSocketServer({ noServer: true });

function deliverLocal(playerId: string, message: unknown): boolean {
  const sockets = local.get(playerId);
  if (!sockets || sockets.size === 0) return false;

  const raw = JSON.stringify(message);
  for (const socket of sockets) {
    if (socket.readyState === socket.OPEN) socket.send(raw);
  }
  return true;
}

export async function sendTo(playerId: string, message: unknown): Promise<void> {
  if (bus.distributed) {
    await bus.publish(DIRECT_CHANNEL, { to: playerId, message });
    return;
  }
  deliverLocal(playerId, message);
}

export async function sendToMany(playerIds: string[], message: unknown): Promise<void> {
  await Promise.all(playerIds.map((id) => sendTo(id, message)));
}

export function isOnline(playerId: string): boolean {
  return (local.get(playerId)?.size ?? 0) > 0;
}

export async function presenceOf(playerId: string): Promise<Presence> {
  const raw = await bus.hget(PRESENCE_KEY, playerId);
  if (!raw) return { status: 'offline', at: 0 };

  try {
    const parsed = JSON.parse(raw) as Presence;

    if (Date.now() - parsed.at > config.presenceTtlSeconds * 1000) return { status: 'offline', at: parsed.at };
    return parsed;
  } catch {
    return { status: 'offline', at: 0 };
  }
}

export async function presenceOfMany(playerIds: string[]): Promise<Map<string, Presence>> {
  const out = new Map<string, Presence>();
  await Promise.all(playerIds.map(async (id) => out.set(id, await presenceOf(id))));
  return out;
}

export async function setPresence(playerId: string, status: PresenceStatus, roomId?: string): Promise<void> {
  const presence: Presence = { status, at: Date.now(), ...(roomId ? { roomId } : {}) };

  if (status === 'offline') await bus.hdel(PRESENCE_KEY, playerId);
  else await bus.hset(PRESENCE_KEY, playerId, JSON.stringify(presence), config.presenceTtlSeconds);

  await bus.publish(PRESENCE_CHANNEL, { playerId, presence });
}

async function fanOutPresence(playerId: string, presence: Presence): Promise<void> {
  const ids = await friends.friendIdsOf(playerId);
  if (ids.length === 0) return;

  const message = { t: 'friend.presence', playerId, status: presence.status, roomId: presence.roomId ?? null };
  for (const id of ids) {
    if (deliverLocal(id, message)) continue;
  }
}

bus.subscribe((channel, payload) => {
  if (channel === DIRECT_CHANNEL) {
    deliverLocal(payload.to, payload.message);
    return;
  }

  if (channel === PRESENCE_CHANNEL) {
    void fanOutPresence(payload.playerId, payload.presence).catch((err) =>
      log.error('presence fan-out failed', { message: (err as Error).message }),
    );
  }
});

function attach(playerId: string, socket: WebSocket): void {
  let sockets = local.get(playerId);
  if (!sockets) {
    sockets = new Set();
    local.set(playerId, sockets);
  }
  sockets.add(socket);
}

function detach(playerId: string, socket: WebSocket): boolean {
  const sockets = local.get(playerId);
  if (!sockets) return true;

  sockets.delete(socket);
  if (sockets.size > 0) return false;

  local.delete(playerId);
  return true;
}

export function handleUpgrade(req: IncomingMessage, socket: Duplex, head: Buffer): void {
  const url = new URL(req.url ?? '/', 'http://localhost');
  const token = url.searchParams.get('token') ?? '';
  const claims = verify(token, config.jwtSecret);

  if (!claims?.sub) {
    socket.write('HTTP/1.1 401 Unauthorized\r\n\r\n');
    socket.destroy();
    return;
  }

  const playerId = claims.sub;

  wss.handleUpgrade(req, socket, head, (ws) => {
    void onConnected(playerId, ws).catch((err) =>
      log.error('socket setup failed', { playerId, message: (err as Error).message }),
    );
  });
}

async function onConnected(playerId: string, socket: WebSocket): Promise<void> {
  attach(playerId, socket);
  await setPresence(playerId, 'menu');
  await players.touch(playerId);

  let alive = true;
  socket.on('pong', () => {
    alive = true;
  });

  const heartbeat = setInterval(() => {
    if (!alive) {
      socket.terminate();
      return;
    }
    alive = false;
    socket.ping();

    void bus.hget(PRESENCE_KEY, playerId).then((raw) => {
      if (raw) void bus.hset(PRESENCE_KEY, playerId, raw, config.presenceTtlSeconds);
    });
  }, 20_000);

  socket.on('message', (raw) => {
    void onMessage(playerId, socket, raw.toString()).catch((err) =>
      log.warn('bad socket message', { playerId, message: (err as Error).message }),
    );
  });

  socket.on('close', () => {
    clearInterval(heartbeat);
    if (detach(playerId, socket)) void setPresence(playerId, 'offline');
  });

  socket.on('error', () => socket.terminate());

  const player = await players.byId(playerId);
  socket.send(JSON.stringify({ t: 'ready', player: players.toPublic(player) }));
}

async function onMessage(playerId: string, socket: WebSocket, raw: string): Promise<void> {
  let msg: { t?: string; status?: string; roomId?: string };
  try {
    msg = JSON.parse(raw);
  } catch {
    return;
  }

  switch (msg.t) {
    case 'ping':
      socket.send(JSON.stringify({ t: 'pong', now: Date.now() }));
      return;

    case 'presence': {
      const status = msg.status;
      if (status === 'menu' || status === 'queue' || status === 'match') {
        await setPresence(playerId, status, msg.roomId);
      }
      return;
    }

    default:
      return;
  }
}
