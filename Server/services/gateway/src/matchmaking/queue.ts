import { config, type Mode, type Region } from '../config.js';
import { bus } from '../store/bus.js';
import { uuid } from '../util/ids.js';

export type TicketState = 'searching' | 'matched' | 'failed' | 'cancelled';

export interface Ticket {
  id: string;

  ownerId: string;

  playerIds: string[];

  modes: Mode[];

  regions: Region[];
  createdAt: number;
  state: TicketState;

  roomId?: string;
  region?: Region;
  mode?: Mode;
  relayUrl?: string;

  joinTokens?: Record<string, string>;
  reason?: string;
}

const TICKETS = 'tickets';

function queueKey(region: Region): string {
  return `mmq:${region}`;
}

export async function save(ticket: Ticket): Promise<void> {
  await bus.hset(TICKETS, ticket.id, JSON.stringify(ticket));
}

export async function get(id: string): Promise<Ticket | null> {
  const raw = await bus.hget(TICKETS, id);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Ticket;
  } catch {
    return null;
  }
}

export interface EnqueueInput {
  ownerId: string;
  playerIds: string[];
  modes: Mode[];
  regions: Region[];
}

export async function enqueue(input: EnqueueInput): Promise<Ticket> {

  for (const playerId of input.playerIds) {
    const existing = await activeTicketFor(playerId);
    if (existing) await cancel(existing.id);
  }

  const ticket: Ticket = {
    id: uuid(),
    ownerId: input.ownerId,
    playerIds: input.playerIds,
    modes: input.modes,
    regions: input.regions,
    createdAt: Date.now(),
    state: 'searching',
  };

  await save(ticket);
  for (const playerId of ticket.playerIds) {
    await bus.hset('ticketByPlayer', playerId, ticket.id);
  }

  for (const region of ticket.regions) {
    await bus.zadd(queueKey(region), ticket.createdAt, ticket.id);
  }

  return ticket;
}

export async function activeTicketFor(playerId: string): Promise<Ticket | null> {
  const id = await bus.hget('ticketByPlayer', playerId);
  if (!id) return null;

  const ticket = await get(id);
  if (!ticket) return null;
  if (ticket.state === 'cancelled' || ticket.state === 'failed') return null;
  return ticket;
}

export async function detach(ticket: Ticket): Promise<void> {
  for (const region of ticket.regions) {
    await bus.zrem(queueKey(region), ticket.id);
  }
  for (const playerId of ticket.playerIds) {
    const owned = await bus.hget('ticketByPlayer', playerId);
    if (owned === ticket.id) await bus.hdel('ticketByPlayer', playerId);
  }
}

export async function cancel(id: string): Promise<void> {
  const ticket = await get(id);
  if (!ticket) return;

  ticket.state = 'cancelled';
  await save(ticket);
  await detach(ticket);
}

export async function fail(ticket: Ticket, reason: string): Promise<void> {
  ticket.state = 'failed';
  ticket.reason = reason;
  await save(ticket);
  await detach(ticket);
}

export async function pending(region: Region, limit = 64): Promise<Ticket[]> {
  const ids = await bus.zrange(queueKey(region), limit);
  const out: Ticket[] = [];

  for (const id of ids) {
    const ticket = await get(id);

    if (!ticket || ticket.state !== 'searching') {
      await bus.zrem(queueKey(region), id);
      continue;
    }

    if (Date.now() - ticket.createdAt > config.ticketTimeoutSeconds * 1000) {
      await fail(ticket, 'timed out waiting for a match');
      continue;
    }

    out.push(ticket);
  }

  return out;
}

export function waitSeconds(ticket: Ticket): number {
  return Math.floor((Date.now() - ticket.createdAt) / 1000);
}
