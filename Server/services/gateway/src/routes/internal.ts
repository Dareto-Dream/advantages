import { isRegion, type Region } from '../config.js';
import { sendToMany, setPresence } from '../realtime/hub.js';
import { query } from '../store/db.js';
import * as rooms from '../store/rooms.js';
import * as servers from '../store/servers.js';
import { badRequest, requireString, type Ctx, type Router } from '../util/http.js';
import { log } from '../util/log.js';

function parseRegion(value: unknown): Region {
  if (typeof value !== 'string' || !isRegion(value)) throw badRequest(`unknown region: ${String(value)}`);
  return value;
}

function intOr(value: unknown, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.floor(parsed) : fallback;
}

export function register(router: Router): void {
  router.post('/internal/servers/register', async (ctx: Ctx) => {
    const id = requireString(ctx.body, 'id', 128);
    const region = parseRegion(ctx.body?.region);
    const relayUrl = requireString(ctx.body, 'relayUrl', 300);

    const row = await servers.register({
      id,
      region,
      relayUrl,
      buildVersion: String(ctx.body?.buildVersion ?? 'dev').slice(0, 64),
      capacity: Math.max(1, intOr(ctx.body?.capacity, 1)),
      gamePort: intOr(ctx.body?.gamePort, 7777),
      queryPort: intOr(ctx.body?.queryPort, 7778),
      rconPort: intOr(ctx.body?.rconPort, 7779),
    });

    log.info('game server registered', { id, region, relayUrl, gamePort: row.game_port, queryPort: row.query_port, rconPort: row.rcon_port });
    return { server: { id: row.id, region: row.region, ports: { game: row.game_port, query: row.query_port, rcon: row.rcon_port } } };
  });

  router.post('/internal/servers/heartbeat', async (ctx: Ctx) => {
    const id = requireString(ctx.body, 'id', 128);
    const status = ctx.body?.status === 'draining' ? 'draining' : 'ready';

    const known = await servers.heartbeat(
      id,
      Math.max(0, intOr(ctx.body?.activeRooms, 0)),
      Math.max(0, intOr(ctx.body?.players, 0)),
      status,
    );

    return { known };
  });

  router.del('/internal/servers/:id', async (ctx: Ctx) => {
    await servers.unregister(ctx.params.id);
    return { ok: true };
  });

  router.get('/internal/rooms/:id', async (ctx: Ctx) => {
    const room = await rooms.byId(ctx.params.id);
    const members = await rooms.memberIds(room.id);

    return {
      room: {
        id: room.id,

        serverId: room.server_id,
        region: room.region,
        mode: room.mode,
        state: room.state,
        teamSize: room.team_size,
        botsEnabled: room.bots_enabled,
        hostId: room.host_id,
        visibility: room.visibility,
      },
      members,
    };
  });

  router.post('/internal/rooms/:id/state', async (ctx: Ctx) => {
    const state = ctx.body?.state;
    if (state !== 'lobby' && state !== 'live' && state !== 'over') throw badRequest('bad room state');

    if (state === 'over') {
      const members = await rooms.memberIds(ctx.params.id);
      await rooms.close(ctx.params.id);
      for (const playerId of members) await setPresence(playerId, 'menu');
      await sendToMany(members, { t: 'room.closed', roomId: ctx.params.id });
    } else {
      await rooms.setState(ctx.params.id, state);
    }

    return { ok: true };
  });

  router.post('/internal/rooms/:id/players', async (ctx: Ctx) => {
    const list = ctx.body?.players;
    if (!Array.isArray(list)) throw badRequest('players must be an array');

    const roomId = ctx.params.id;
    const seen: string[] = [];

    for (const entry of list) {
      const playerId = typeof entry?.id === 'string' ? entry.id : null;
      if (!playerId) continue;
      seen.push(playerId);
      await rooms.addMember(roomId, playerId, intOr(entry?.team, 0));
    }

    await query(
      'DELETE FROM room_members WHERE room_id = $1 AND NOT (player_id = ANY($2::uuid[]))',
      [roomId, seen],
    );

    await rooms.setBotsEnabled(roomId, ctx.body?.botsEnabled !== false);
    return { ok: true, players: seen.length };
  });

  router.post('/internal/rooms/:id/results', async (ctx: Ctx) => {
    const results = ctx.body?.results;
    if (!Array.isArray(results)) throw badRequest('results must be an array');

    const room = await rooms.byId(ctx.params.id);

    for (const entry of results) {
      if (typeof entry?.playerId !== 'string') continue;
      await query(
        `INSERT INTO match_results (room_id, player_id, mode, team, won, kills, deaths, assists, damage, healing, operative)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)`,
        [
          room.id,
          entry.playerId,
          room.mode,
          intOr(entry.team, 0),
          entry.won === true,
          intOr(entry.kills, 0),
          intOr(entry.deaths, 0),
          intOr(entry.assists, 0),
          intOr(entry.damage, 0),
          intOr(entry.healing, 0),
          typeof entry.operative === 'string' ? entry.operative.slice(0, 32) : null,
        ],
      );
    }

    return { ok: true, recorded: results.length };
  });
}
