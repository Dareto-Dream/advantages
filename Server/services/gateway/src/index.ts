import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import { config } from './config.js';
import * as matchmaker from './matchmaking/matchmaker.js';
import { handleUpgrade } from './realtime/hub.js';
import * as authRoutes from './routes/auth.js';
import * as friendRoutes from './routes/friends.js';
import * as internalRoutes from './routes/internal.js';
import * as matchmakingRoutes from './routes/matchmaking.js';
import * as partyRoutes from './routes/party.js';
import * as roomRoutes from './routes/rooms.js';
import { migrate } from './store/db.js';
import * as servers from './store/servers.js';
import {
  applyCors,
  bearer,
  HttpError,
  readJsonBody,
  Router,
  sendJson,
  unauthorized,
  type Ctx,
} from './util/http.js';
import { verify } from './util/jwt.js';
import { log } from './util/log.js';

const router = new Router();

authRoutes.register(router);
friendRoutes.register(router);
partyRoutes.register(router);
matchmakingRoutes.register(router);
roomRoutes.register(router);
internalRoutes.register(router);

router.get('/health', async () => ({
  ok: true,
  service: 'advantage-gateway',
  regions: await servers.regionHealth(),
}));

const PUBLIC_PATHS = new Set(['/health', '/auth/guest', '/regions']);

async function authenticate(ctx: Ctx, pathname: string): Promise<void> {
  const token = bearer(ctx.req);

  if (pathname.startsWith('/internal/')) {

    if (!token || token !== config.internalSecret) throw unauthorized('bad server credentials');
    ctx.serverId = 'fleet';
    return;
  }

  if (PUBLIC_PATHS.has(pathname)) return;

  if (!token) throw unauthorized();
  const claims = verify(token, config.jwtSecret);
  if (!claims?.sub) throw unauthorized('session expired');

  ctx.playerId = claims.sub;
}

async function handle(req: IncomingMessage, res: ServerResponse): Promise<void> {
  applyCors(res);

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  const url = new URL(req.url ?? '/', 'http://localhost');
  const route = router.match(req.method ?? 'GET', url.pathname);

  if (!route) {
    sendJson(res, 404, { error: 'not_found', message: `no route for ${req.method} ${url.pathname}` });
    return;
  }

  const ctx: Ctx = {
    req,
    res,
    params: route.params,
    query: url.searchParams,
    body: {},
    playerId: '',
    serverId: '',
  };

  try {
    if (req.method === 'POST' || req.method === 'PATCH' || req.method === 'DELETE') {
      ctx.body = await readJsonBody(req);
    }

    await authenticate(ctx, url.pathname);
    const payload = await route.handler(ctx);
    sendJson(res, 200, payload ?? { ok: true });
  } catch (err) {
    if (err instanceof HttpError) {
      sendJson(res, err.status, { error: err.code, message: err.message });
      return;
    }

    log.error('unhandled request error', {
      path: url.pathname,
      message: (err as Error).message,
      stack: (err as Error).stack,
    });
    sendJson(res, 500, { error: 'internal', message: 'something went wrong' });
  }
}

const server = createServer((req, res) => {
  void handle(req, res).catch((err) => {
    log.error('request handler threw', { message: (err as Error).message });
    if (!res.headersSent) sendJson(res, 500, { error: 'internal', message: 'something went wrong' });
  });
});

server.on('upgrade', (req, socket, head) => {
  const url = new URL(req.url ?? '/', 'http://localhost');
  if (url.pathname !== '/ws') {
    socket.write('HTTP/1.1 404 Not Found\r\n\r\n');
    socket.destroy();
    return;
  }
  handleUpgrade(req, socket, head);
});

async function main(): Promise<void> {
  if (!config.databaseUrl) {
    log.error('DATABASE_URL is not set');
    process.exit(1);
  }

  await migrate();

  const matchmakerTimer = matchmaker.start();
  const sweeper = setInterval(() => {
    void servers.sweepStale().catch((err) => log.error('sweep failed', { message: (err as Error).message }));
  }, config.sweepIntervalMs);

  server.listen(config.port, () => log.info('gateway listening', { port: config.port }));

  const shutdown = () => {
    log.info('shutting down');
    clearInterval(matchmakerTimer);
    clearInterval(sweeper);
    server.close(() => process.exit(0));
    setTimeout(() => process.exit(0), 5000).unref();
  };

  process.on('SIGTERM', shutdown);
  process.on('SIGINT', shutdown);
}

void main().catch((err) => {
  log.error('gateway failed to start', { message: (err as Error).message, stack: (err as Error).stack });
  process.exit(1);
});
