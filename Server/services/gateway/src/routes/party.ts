import { sendTo, sendToMany } from '../realtime/hub.js';
import * as friends from '../store/friends.js';
import * as parties from '../store/parties.js';
import * as players from '../store/players.js';
import { badRequest, forbidden, notFound, requireString, type Ctx, type Router } from '../util/http.js';

async function describe(party: parties.Party | null) {
  if (!party) return null;

  const profiles = await players.byIds(party.memberIds);
  return {
    id: party.id,
    leaderId: party.leaderId,
    members: party.memberIds
      .map((id) => profiles.get(id))
      .filter((row): row is players.PlayerRow => row != null)
      .map(players.toPublic),
  };
}

async function broadcast(party: parties.Party | null, extraIds: string[] = []): Promise<void> {
  const payload = await describe(party);
  const targets = [...(party?.memberIds ?? []), ...extraIds];
  await sendToMany(targets, { t: 'party.updated', party: payload });
}

export function register(router: Router): void {
  router.get('/party', async (ctx: Ctx) => {
    return { party: await describe(await parties.partyOf(ctx.playerId)) };
  });

  router.post('/party/invite', async (ctx: Ctx) => {
    const targetId = requireString(ctx.body, 'playerId', 64);
    if (targetId === ctx.playerId) throw badRequest('you are already in your own party');

    if (!(await friends.areFriends(ctx.playerId, targetId))) {
      throw forbidden('you can only invite friends');
    }

    const party = await parties.ensure(ctx.playerId);
    const me = await players.byId(ctx.playerId);

    await sendTo(targetId, {
      t: 'party.invite',
      partyId: party.id,
      from: players.toPublic(me),
    });

    return { party: await describe(party) };
  });

  router.post('/party/join', async (ctx: Ctx) => {
    const partyId = requireString(ctx.body, 'partyId', 64);

    const target = await parties.get(partyId);
    if (!target) throw notFound('that party no longer exists');

    const invited = await Promise.all(target.memberIds.map((id) => friends.areFriends(ctx.playerId, id)));
    if (!invited.some(Boolean)) throw forbidden('you were not invited to that party');

    const party = await parties.join(partyId, ctx.playerId);
    await broadcast(party);
    return { party: await describe(party) };
  });

  router.post('/party/leave', async (ctx: Ctx) => {
    const before = await parties.partyOf(ctx.playerId);
    const party = await parties.leave(ctx.playerId);
    await broadcast(party, before ? [ctx.playerId] : []);
    return { party: null };
  });

  router.post('/party/kick', async (ctx: Ctx) => {
    const targetId = requireString(ctx.body, 'playerId', 64);

    const party = await parties.partyOf(ctx.playerId);
    if (!party) throw notFound('you are not in a party');

    const after = await parties.kick(party, ctx.playerId, targetId);
    await broadcast(after, [targetId]);
    return { party: await describe(after) };
  });
}
