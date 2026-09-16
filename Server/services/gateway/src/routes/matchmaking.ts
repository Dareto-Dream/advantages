import { isMode, isRegion, MODES, REGIONS, relayUrlFor, type Mode, type Region } from '../config.js';
import * as matchmaker from '../matchmaking/matchmaker.js';
import * as queue from '../matchmaking/queue.js';
import { setPresence } from '../realtime/hub.js';
import * as parties from '../store/parties.js';
import * as servers from '../store/servers.js';
import { badRequest, forbidden, notFound, type Ctx, type Router } from '../util/http.js';

function parseModes(value: unknown): Mode[] {
  if (value == null) return [];
  if (!Array.isArray(value)) throw badRequest('modes must be an array');

  const out: Mode[] = [];
  for (const entry of value) {
    if (typeof entry !== 'string' || !isMode(entry)) throw badRequest(`unknown mode: ${entry}`);
    out.push(entry);
  }
  return out;
}

function parseRegions(value: unknown): Region[] {
  if (value == null) return [];
  if (!Array.isArray(value)) throw badRequest('regions must be an array');

  const out: Region[] = [];
  for (const entry of value) {
    if (typeof entry !== 'string' || !isRegion(entry)) throw badRequest(`unknown region: ${entry}`);
    if (!out.includes(entry)) out.push(entry);
  }
  return out;
}

function ticketView(ticket: queue.Ticket, forPlayer: string) {
  return {
    id: ticket.id,
    state: ticket.state,
    waitSeconds: queue.waitSeconds(ticket),
    players: ticket.playerIds.length,
    reason: ticket.reason ?? null,
    match:
      ticket.state === 'matched' && ticket.roomId
        ? {
            roomId: ticket.roomId,
            region: ticket.region,
            mode: ticket.mode,
            relayUrl: ticket.relayUrl,
            joinToken: ticket.joinTokens?.[forPlayer] ?? null,
          }
        : null,
  };
}

export function register(router: Router): void {

  router.get('/regions', async () => {
    const health = await servers.regionHealth();

    return {
      regions: REGIONS.map((region) => {
        const live = health.find((entry) => entry.region === region);
        return {
          id: region,
          relayUrl: live?.relayUrl ?? relayUrlFor(region),
          servers: live?.servers ?? 0,
          freeSlots: live?.freeSlots ?? 0,
          players: live?.players ?? 0,
          available: (live?.freeSlots ?? 0) > 0,
        };
      }),
      modes: MODES,
    };
  });

  router.post('/matchmaking/queue', async (ctx: Ctx) => {
    const modes = parseModes(ctx.body?.modes);
    const requested = parseRegions(ctx.body?.regions);

    const regions = requested.length > 0 ? requested : [...REGIONS];

    const party = await parties.partyOf(ctx.playerId);
    if (party && party.leaderId !== ctx.playerId) {
      throw forbidden('only the party leader can start a search');
    }

    const playerIds = party ? party.memberIds : [ctx.playerId];
    const ticket = await queue.enqueue({ ownerId: ctx.playerId, playerIds, modes, regions });

    for (const playerId of playerIds) await setPresence(playerId, 'queue');

    return { ticket: ticketView(ticket, ctx.playerId) };
  });

  router.get('/matchmaking/ticket/:id', async (ctx: Ctx) => {
    const ticket = await queue.get(ctx.params.id);
    if (!ticket) throw notFound('no such ticket');
    if (!ticket.playerIds.includes(ctx.playerId)) throw forbidden('not your ticket');

    return { ticket: ticketView(ticket, ctx.playerId) };
  });

  router.del('/matchmaking/ticket/:id', async (ctx: Ctx) => {
    const ticket = await queue.get(ctx.params.id);
    if (!ticket) return { ok: true };
    if (!ticket.playerIds.includes(ctx.playerId)) throw forbidden('not your ticket');

    await queue.cancel(ticket.id);
    for (const playerId of ticket.playerIds) await setPresence(playerId, 'menu');

    return { ok: true };
  });

  router.get('/matchmaking/current', async (ctx: Ctx) => {
    const ticket = await queue.activeTicketFor(ctx.playerId);
    return { ticket: ticket ? ticketView(ticket, ctx.playerId) : null };
  });

  router.post('/matchmaking/pump', async () => {
    await matchmaker.tick();
    return { ok: true };
  });
}
