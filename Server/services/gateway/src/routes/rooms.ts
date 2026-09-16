import { config, isMode, isRegion, MODES, type Mode, type Region } from '../config.js';
import { mintJoinToken } from '../matchmaking/matchmaker.js';
import { sendTo, setPresence } from '../realtime/hub.js';
import * as friends from '../store/friends.js';
import * as players from '../store/players.js';
import * as rooms from '../store/rooms.js';
import * as servers from '../store/servers.js';
import { badRequest, conflict, forbidden, notFound, requireString, type Ctx, type Router } from '../util/http.js';

function parseMode(value: unknown, fallback: Mode): Mode {
  if (value == null) return fallback;
  if (typeof value !== 'string' || !isMode(value)) throw badRequest(`unknown mode: ${String(value)}`);
  return value;
}

function parseRegion(value: unknown): Region {
  if (typeof value !== 'string' || !isRegion(value)) throw badRequest(`unknown region: ${String(value)}`);
  return value;
}

async function relayFor(room: rooms.RoomRow): Promise<string> {
  if (!room.server_id) throw conflict('that room has no server');
  const server = await servers.liveById(room.server_id);
  if (!server) throw conflict('that room is no longer running');
  return server.relay_url;
}

async function joinRoom(room: rooms.RoomRow, playerId: string, host: boolean) {
  const profile = await players.byId(playerId);
  const relayUrl = await relayFor(room);

  const members = await rooms.memberIds(room.id);
  const team = members.length % 2;

  await rooms.addMember(room.id, playerId, team);
  await setPresence(playerId, 'match', room.id);

  return {
    room: {
      id: room.id,
      region: room.region,
      mode: room.mode,
      state: room.state,
      joinCode: room.join_code,
      teamSize: room.team_size,
      botsEnabled: room.bots_enabled,
      hostId: room.host_id,
    },
    relayUrl,
    team,
    joinToken: mintJoinToken({
      playerId,
      roomId: room.id,
      team,
      name: profile.display_name,
      handle: `${profile.display_name}#${profile.tag}`,
      host,
    }),
  };
}

export function register(router: Router): void {
  router.post('/rooms', async (ctx: Ctx) => {
    const region = parseRegion(ctx.body?.region);
    const mode = parseMode(ctx.body?.mode, MODES[0]);
    const teamSize = Number(ctx.body?.teamSize ?? config.matchTeamSize);
    const botsEnabled = ctx.body?.botsEnabled !== false;

    if (!Number.isInteger(teamSize) || teamSize < 1 || teamSize > 8) {
      throw badRequest('teamSize must be between 1 and 8');
    }

    const created = await rooms.create({
      region,
      mode,
      visibility: 'private',
      teamSize,
      botsEnabled,
      hostId: ctx.playerId,
    });

    return joinRoom(created.room, ctx.playerId, true);
  });

  router.post('/rooms/join', async (ctx: Ctx) => {
    const code = requireString(ctx.body, 'code', 12).toUpperCase();
    const room = await rooms.byCode(code);

    const count = await rooms.memberCount(room.id);
    if (count >= room.team_size * 2) throw conflict('that room is full');

    return joinRoom(room, ctx.playerId, room.host_id === ctx.playerId);
  });

  router.post('/rooms/:id/rejoin', async (ctx: Ctx) => {
    const room = await rooms.byId(ctx.params.id);
    if (room.closed_at) throw conflict('that match has ended');

    const members = await rooms.memberIds(room.id);
    if (!members.includes(ctx.playerId)) throw forbidden('you are not in that room');

    return joinRoom(room, ctx.playerId, room.host_id === ctx.playerId);
  });

  router.post('/rooms/:id/leave', async (ctx: Ctx) => {
    await rooms.removeMember(ctx.params.id, ctx.playerId);
    await setPresence(ctx.playerId, 'menu');
    return { ok: true };
  });

  router.post('/rooms/:id/invite', async (ctx: Ctx) => {
    const targetId = requireString(ctx.body, 'playerId', 64);
    const room = await rooms.byId(ctx.params.id);

    if (!room.join_code) throw badRequest('only private rooms can be invited to');
    if (!(await friends.areFriends(ctx.playerId, targetId))) throw forbidden('you can only invite friends');

    const me = await players.byId(ctx.playerId);
    await sendTo(targetId, {
      t: 'room.invite',
      roomId: room.id,
      code: room.join_code,
      mode: room.mode,
      region: room.region,
      from: players.toPublic(me),
    });

    return { ok: true };
  });

  router.get('/rooms/current', async (ctx: Ctx) => {
    const room = await rooms.currentRoomOf(ctx.playerId);
    if (!room) return { room: null };

    return {
      room: {
        id: room.id,
        region: room.region,
        mode: room.mode,
        state: room.state,
        joinCode: room.join_code,
      },
    };
  });

  router.get('/rooms/:id', async (ctx: Ctx) => {
    const room = await rooms.byId(ctx.params.id);
    const members = await rooms.memberIds(room.id);
    const profiles = await players.byIds(members);

    return {
      room: {
        id: room.id,
        region: room.region,
        mode: room.mode,
        state: room.state,
        joinCode: room.join_code,
        teamSize: room.team_size,
        botsEnabled: room.bots_enabled,
        hostId: room.host_id,
      },
      members: members
        .map((id) => profiles.get(id))
        .filter((row): row is players.PlayerRow => row != null)
        .map(players.toPublic),
    };
  });
}
