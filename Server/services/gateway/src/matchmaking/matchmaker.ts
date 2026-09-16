import { config, MODES, REGIONS, type Mode, type Region } from '../config.js';
import { sendTo, setPresence } from '../realtime/hub.js';
import { bus } from '../store/bus.js';
import * as players from '../store/players.js';
import * as rooms from '../store/rooms.js';
import * as servers from '../store/servers.js';
import { sign } from '../util/jwt.js';
import { log } from '../util/log.js';
import * as queue from './queue.js';

const LOCK_KEY = 'mm:lock';

export interface Placement {
  roomId: string;
  region: Region;
  mode: Mode;
  relayUrl: string;
  team: number;
  joinToken: string;
}

export function mintJoinToken(input: {
  playerId: string;
  roomId: string;
  team: number;
  name: string;
  handle: string;
  host: boolean;
}): string {
  return sign(
    {
      sub: input.playerId,
      room: input.roomId,
      team: input.team,
      name: input.name,
      handle: input.handle,
      host: input.host,
    },
    config.internalSecret,
    config.joinTokenTtlSeconds,
  );
}

function intersectModes(a: Mode[], b: Mode[]): Mode[] {
  if (a.length === 0) return b;
  if (b.length === 0) return a;
  return a.filter((mode) => b.includes(mode));
}

interface Lobby {
  tickets: queue.Ticket[];
  modes: Mode[];
  players: number;
}

function formLobby(tickets: queue.Ticket[], capacity: number): Lobby | null {
  const first = tickets[0];
  if (!first) return null;

  const lobby: Lobby = { tickets: [first], modes: first.modes, players: first.playerIds.length };

  for (const ticket of tickets.slice(1)) {
    if (lobby.players >= capacity) break;
    if (lobby.players + ticket.playerIds.length > capacity) continue;

    const modes = intersectModes(lobby.modes, ticket.modes);
    if (modes.length === 0 && (lobby.modes.length > 0 || ticket.modes.length > 0)) continue;

    lobby.tickets.push(ticket);
    lobby.modes = modes;
    lobby.players += ticket.playerIds.length;
  }

  return lobby;
}

function pickMode(lobby: Lobby): Mode {
  const options = lobby.modes.length > 0 ? lobby.modes : [...MODES];
  return options[Math.floor(Math.random() * options.length)];
}

function assignTeams(tickets: queue.Ticket[]): Map<string, number> {
  const teams = new Map<string, number>();
  const counts = [0, 0];

  for (const ticket of tickets) {
    const team = counts[0] <= counts[1] ? 0 : 1;
    counts[team] += ticket.playerIds.length;
    for (const playerId of ticket.playerIds) teams.set(playerId, team);
  }

  return teams;
}

async function targetRoom(region: Region, mode: Mode, incoming: number) {
  const capacity = config.matchTeamSize * 2;
  const open = await rooms.findJoinable(region, mode, capacity - incoming + 1);

  if (open?.server_id) {
    const server = await servers.liveById(open.server_id);

    if (server) return { room: open, relayUrl: server.relay_url };
  }

  const created = await rooms.create({
    region,
    mode,
    visibility: 'public',
    teamSize: config.matchTeamSize,
    botsEnabled: true,
    hostId: null,
  });

  return { room: created.room, relayUrl: created.server.relay_url };
}

async function placeLobby(lobby: Lobby, region: Region): Promise<void> {
  const mode = pickMode(lobby);
  const { room, relayUrl } = await targetRoom(region, mode, lobby.players);

  const teams = assignTeams(lobby.tickets);
  const allIds = lobby.tickets.flatMap((ticket) => ticket.playerIds);
  const profiles = await players.byIds(allIds);

  for (const ticket of lobby.tickets) {
    const joinTokens: Record<string, string> = {};

    for (const playerId of ticket.playerIds) {
      const profile = profiles.get(playerId);
      const team = teams.get(playerId) ?? 0;
      await rooms.addMember(room.id, playerId, team);

      joinTokens[playerId] = mintJoinToken({
        playerId,
        roomId: room.id,
        team,
        name: profile?.display_name ?? 'Player',
        handle: profile ? `${profile.display_name}#${profile.tag}` : 'Player#0000',
        host: false,
      });
    }

    ticket.state = 'matched';
    ticket.roomId = room.id;
    ticket.region = region;
    ticket.mode = mode;
    ticket.relayUrl = relayUrl;
    ticket.joinTokens = joinTokens;

    await queue.save(ticket);
    await queue.detach(ticket);

    for (const playerId of ticket.playerIds) {
      await setPresence(playerId, 'match', room.id);
      await sendTo(playerId, {
        t: 'match.found',
        ticketId: ticket.id,
        roomId: room.id,
        region,
        mode,
        relayUrl,
        team: teams.get(playerId) ?? 0,
        joinToken: joinTokens[playerId],
      });
    }
  }

  log.info('match formed', { roomId: room.id, region, mode, players: lobby.players });
}

async function runRegion(region: Region): Promise<void> {
  const capacity = config.matchTeamSize * 2;
  const tickets = await queue.pending(region);
  if (tickets.length === 0) return;

  const lobby = formLobby(tickets, capacity);
  if (!lobby) return;

  const oldestWait = queue.waitSeconds(lobby.tickets[0]);
  const full = lobby.players >= capacity;
  const botFill = oldestWait >= config.botFillAfterSeconds;

  if (!full && !botFill) return;

  try {
    await placeLobby(lobby, region);
  } catch (err) {
    const message = (err as Error).message;

    log.warn('could not place lobby', { region, message });

    for (const ticket of lobby.tickets) {
      if (queue.waitSeconds(ticket) >= config.ticketTimeoutSeconds) {
        await queue.fail(ticket, message);
        for (const playerId of ticket.playerIds) {
          await setPresence(playerId, 'menu');
          await sendTo(playerId, { t: 'match.failed', ticketId: ticket.id, reason: message });
        }
      }
    }
  }
}

export async function tick(): Promise<void> {
  const held = await bus.lock(LOCK_KEY, config.matchmakerIntervalMs * 3);
  if (!held) return;

  try {
    for (const region of REGIONS) {
      await runRegion(region);
    }
  } finally {
    await bus.unlock(LOCK_KEY);
  }
}

export function start(): NodeJS.Timeout {
  return setInterval(() => {
    void tick().catch((err) => log.error('matchmaker pass failed', { message: (err as Error).message }));
  }, config.matchmakerIntervalMs);
}
