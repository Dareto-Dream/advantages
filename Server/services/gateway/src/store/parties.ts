import { badRequest, conflict, notFound } from '../util/http.js';
import { uuid } from '../util/ids.js';
import { bus } from './bus.js';

export interface Party {
  id: string;
  leaderId: string;
  memberIds: string[];
  createdAt: number;
}

const PARTIES = 'parties';
const BY_PLAYER = 'partyByPlayer';

export const MAX_PARTY = 4;

export async function get(id: string): Promise<Party | null> {
  const raw = await bus.hget(PARTIES, id);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Party;
  } catch {
    return null;
  }
}

async function save(party: Party): Promise<void> {
  await bus.hset(PARTIES, party.id, JSON.stringify(party));
}

export async function partyOf(playerId: string): Promise<Party | null> {
  const id = await bus.hget(BY_PLAYER, playerId);
  if (!id) return null;

  const party = await get(id);
  if (!party || !party.memberIds.includes(playerId)) {
    await bus.hdel(BY_PLAYER, playerId);
    return null;
  }
  return party;
}

export async function ensure(playerId: string): Promise<Party> {
  const existing = await partyOf(playerId);
  if (existing) return existing;

  const party: Party = { id: uuid(), leaderId: playerId, memberIds: [playerId], createdAt: Date.now() };
  await save(party);
  await bus.hset(BY_PLAYER, playerId, party.id);
  return party;
}

export async function join(partyId: string, playerId: string): Promise<Party> {
  const party = await get(partyId);
  if (!party) throw notFound('that party no longer exists');
  if (party.memberIds.includes(playerId)) return party;
  if (party.memberIds.length >= MAX_PARTY) throw conflict('that party is full');

  await leave(playerId);

  party.memberIds.push(playerId);
  await save(party);
  await bus.hset(BY_PLAYER, playerId, party.id);
  return party;
}

export async function leave(playerId: string): Promise<Party | null> {
  const party = await partyOf(playerId);
  if (!party) return null;

  party.memberIds = party.memberIds.filter((id) => id !== playerId);
  await bus.hdel(BY_PLAYER, playerId);

  if (party.memberIds.length === 0) {
    await bus.hdel(PARTIES, party.id);
    return null;
  }

  if (party.leaderId === playerId) party.leaderId = party.memberIds[0];
  await save(party);
  return party;
}

export async function kick(party: Party, actorId: string, targetId: string): Promise<Party | null> {
  if (party.leaderId !== actorId) throw badRequest('only the party leader can remove members');
  if (targetId === actorId) throw badRequest('use leave instead');
  return leave(targetId);
}
