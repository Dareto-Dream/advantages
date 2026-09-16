import { createServer, type IncomingMessage } from 'node:http';
import { randomUUID, createHmac, timingSafeEqual } from 'node:crypto';
import type { Duplex } from 'node:stream';
import { WebSocketServer, type WebSocket, type RawData } from 'ws';

const PORT = Number(process.env.PORT ?? 8080);
const REGION = process.env.REGION ?? 'unknown';
const INTERNAL_SECRET = process.env.INTERNAL_SECRET ?? 'dev-insecure-internal-secret';
const GATEWAY_URL = (process.env.GATEWAY_URL ?? '').replace(/\/+$/, '');

const ATTACH_TIMEOUT_MS = Number(process.env.ATTACH_TIMEOUT_MS ?? 8000);

const ROOM_CACHE_MS = Number(process.env.ROOM_CACHE_MS ?? 60_000);

function log(level: string, msg: string, extra?: unknown): void {
  const line = { t: new Date().toISOString(), level, msg, region: REGION, ...(extra ? { data: extra } : {}) };
  (level === 'error' || level === 'warn' ? console.error : console.log)(JSON.stringify(line));
}

function secretOk(candidate: string | null): boolean {
  if (!candidate) return false;
  const a = Buffer.from(candidate);
  const b = Buffer.from(INTERNAL_SECRET);
  return a.length === b.length && timingSafeEqual(a, b);
}

function readJoinToken(token: string): { room: string; sub: string } | null {
  const parts = token.split('.');
  if (parts.length !== 3) return null;

  const expected = createHmac('sha256', INTERNAL_SECRET).update(`${parts[0]}.${parts[1]}`).digest('base64url');
  const a = Buffer.from(parts[2]);
  const b = Buffer.from(expected);
  if (a.length !== b.length || !timingSafeEqual(a, b)) return null;

  try {
    const claims = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
    if (typeof claims.exp === 'number' && claims.exp < Math.floor(Date.now() / 1000)) return null;
    if (typeof claims.room !== 'string' || typeof claims.sub !== 'string') return null;
    return { room: claims.room, sub: claims.sub };
  } catch {
    return null;
  }
}

interface GameServerLink {
  id: string;
  socket: WebSocket;
}

const gameServers = new Map<string, GameServerLink>();

interface PendingAttach {
  client: WebSocket;
  roomId: string;
  playerId: string;
  timer: NodeJS.Timeout;

  early: Array<{ data: Buffer; isBinary: boolean }>;
  collect: (data: RawData, isBinary: boolean) => void;
}

const MAX_EARLY_FRAMES = 64;
const pending = new Map<string, PendingAttach>();

const roomCache = new Map<string, { serverId: string; at: number }>();

async function serverIdForRoom(roomId: string): Promise<string | null> {
  const cached = roomCache.get(roomId);
  if (cached && Date.now() - cached.at < ROOM_CACHE_MS) return cached.serverId;

  if (!GATEWAY_URL) return null;

  try {
    const res = await fetch(`${GATEWAY_URL}/internal/rooms/${encodeURIComponent(roomId)}`, {
      headers: { authorization: `Bearer ${INTERNAL_SECRET}` },
    });
    if (!res.ok) return null;

    const body = (await res.json()) as { room?: { serverId?: string | null } };
    const serverId = body?.room?.serverId;
    if (typeof serverId !== 'string' || serverId.length === 0) return null;

    roomCache.set(roomId, { serverId, at: Date.now() });
    return serverId;
  } catch (err) {
    log('warn', 'room lookup failed', { roomId, message: (err as Error).message });
    return null;
  }
}

const wss = new WebSocketServer({ noServer: true });

function pipe(a: WebSocket, b: WebSocket): void {
  const forward = (from: WebSocket, to: WebSocket) => {
    from.on('message', (data: RawData, isBinary: boolean) => {
      if (to.readyState === to.OPEN) to.send(data as Buffer, { binary: isBinary });
    });
    from.on('close', () => {
      if (to.readyState === to.OPEN) to.close();
    });
    from.on('error', () => to.terminate());
  };

  forward(a, b);
  forward(b, a);
}

function rejectSocket(socket: Duplex, status: number, reason: string): void {
  socket.write(`HTTP/1.1 ${status} ${reason}\r\n\r\n`);
  socket.destroy();
}

function onServerLink(id: string, socket: WebSocket): void {
  const existing = gameServers.get(id);
  if (existing && existing.socket !== socket) existing.socket.terminate();

  gameServers.set(id, { id, socket });
  log('info', 'game server linked', { id, servers: gameServers.size });

  socket.on('message', (raw) => {

    let msg: { t?: string; rooms?: string[] };
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return;
    }

    if (msg.t === 'claim' && Array.isArray(msg.rooms)) {
      for (const roomId of msg.rooms) roomCache.set(roomId, { serverId: id, at: Date.now() });
    }

    if (msg.t === 'release' && Array.isArray(msg.rooms)) {
      for (const roomId of msg.rooms) roomCache.delete(roomId);
    }
  });

  const keepalive = setInterval(() => {
    if (socket.readyState === socket.OPEN) socket.ping();
  }, 15_000);

  socket.on('close', () => {
    clearInterval(keepalive);
    if (gameServers.get(id)?.socket === socket) gameServers.delete(id);
    for (const [roomId, entry] of roomCache) {
      if (entry.serverId === id) roomCache.delete(roomId);
    }
    log('warn', 'game server unlinked', { id, servers: gameServers.size });
  });

  socket.on('error', () => socket.terminate());
  socket.send(JSON.stringify({ t: 'linked', region: REGION }));
}

async function chooseServer(roomId: string): Promise<GameServerLink | null> {
  const serverId = await serverIdForRoom(roomId);

  if (serverId) {
    const link = gameServers.get(serverId);
    if (link) return link;

    log('warn', 'allocated server is not linked to this relay', { roomId, serverId });
    roomCache.delete(roomId);
    return null;
  }

  const links = [...gameServers.values()];
  return links.length > 0 ? links[0] : null;
}

async function onClient(socket: WebSocket, roomId: string, playerId: string, token: string): Promise<void> {
  const link = await chooseServer(roomId);

  if (!link) {
    log('warn', 'no game server for room', { roomId });
    socket.close(4503, 'no game server available in this region');
    return;
  }

  if (socket.readyState !== socket.OPEN) return;

  const session = randomUUID();
  const timer = setTimeout(() => {
    if (pending.delete(session)) {
      log('warn', 'attach timed out', { roomId, playerId, serverId: link.id });
      socket.close(4504, 'game server did not accept the connection');
    }
  }, ATTACH_TIMEOUT_MS);

  const early: Array<{ data: Buffer; isBinary: boolean }> = [];
  const collect = (data: RawData, isBinary: boolean) => {
    if (early.length < MAX_EARLY_FRAMES) early.push({ data: data as Buffer, isBinary });
  };

  socket.on('message', collect);
  pending.set(session, { client: socket, roomId, playerId, timer, early, collect });

  socket.on('close', () => {
    const entry = pending.get(session);
    if (entry) {
      clearTimeout(entry.timer);
      pending.delete(session);
    }
  });

  link.socket.send(JSON.stringify({ t: 'attach', session, room: roomId, player: playerId, token }));
}

function onAttach(socket: WebSocket, session: string): void {
  const entry = pending.get(session);

  if (!entry) {
    socket.close(4404, 'unknown session');
    return;
  }

  clearTimeout(entry.timer);
  pending.delete(session);
  entry.client.off('message', entry.collect);

  if (entry.client.readyState !== entry.client.OPEN) {
    socket.close(4410, 'player already gone');
    return;
  }

  pipe(entry.client, socket);

  for (const frame of entry.early) {
    socket.send(frame.data, { binary: frame.isBinary });
  }

  log('info', 'player attached', {
    roomId: entry.roomId,
    playerId: entry.playerId,
    buffered: entry.early.length,
  });
}

const server = createServer((req, res) => {
  const url = new URL(req.url ?? '/', 'http://localhost');

  if (url.pathname === '/ping') {
    res.writeHead(200, { 'content-type': 'text/plain', 'access-control-allow-origin': '*' });
    res.end('pong');
    return;
  }

  if (url.pathname === '/health') {
    res.writeHead(200, { 'content-type': 'application/json', 'access-control-allow-origin': '*' });
    res.end(
      JSON.stringify({
        ok: true,
        service: 'advantage-relay',
        region: REGION,
        servers: gameServers.size,
        rooms: roomCache.size,
        pending: pending.size,
      }),
    );
    return;
  }

  res.writeHead(404, { 'content-type': 'application/json' });
  res.end(JSON.stringify({ error: 'not_found' }));
});

server.on('upgrade', (req: IncomingMessage, socket: Duplex, head: Buffer) => {
  const url = new URL(req.url ?? '/', 'http://localhost');

  if (url.pathname === '/server') {
    const id = url.searchParams.get('id');
    if (!secretOk(url.searchParams.get('secret')) || !id) return rejectSocket(socket, 401, 'Unauthorized');
    wss.handleUpgrade(req, socket, head, (ws) => onServerLink(id, ws));
    return;
  }

  if (url.pathname === '/attach') {
    const session = url.searchParams.get('session');
    if (!secretOk(url.searchParams.get('secret')) || !session) return rejectSocket(socket, 401, 'Unauthorized');
    wss.handleUpgrade(req, socket, head, (ws) => onAttach(ws, session));
    return;
  }

  if (url.pathname === '/play') {
    const token = url.searchParams.get('token') ?? '';
    const claims = readJoinToken(token);
    if (!claims) return rejectSocket(socket, 401, 'Unauthorized');
    wss.handleUpgrade(req, socket, head, (ws) => {
      void onClient(ws, claims.room, claims.sub, token).catch((err) => {
        log('error', 'client attach failed', { message: (err as Error).message });
        ws.close(4500, 'relay error');
      });
    });
    return;
  }

  rejectSocket(socket, 404, 'Not Found');
});

server.listen(PORT, () => log('info', 'relay listening', { port: PORT, region: REGION }));

for (const signal of ['SIGTERM', 'SIGINT'] as const) {
  process.on(signal, () => {
    log('info', 'relay shutting down');
    server.close(() => process.exit(0));
    setTimeout(() => process.exit(0), 3000).unref();
  });
}
