import { presenceOfMany, sendTo } from '../realtime/hub.js';
import * as friends from '../store/friends.js';
import * as players from '../store/players.js';
import { badRequest, notFound, requireString, type Ctx, type Router } from '../util/http.js';

async function withPresence(entries: friends.FriendEntry[]): Promise<unknown[]> {
  const presence = await presenceOfMany(entries.map((entry) => entry.player.id));

  return entries.map((entry) => ({
    ...entry,
    presence: presence.get(entry.player.id)?.status ?? 'offline',
    roomId: presence.get(entry.player.id)?.roomId ?? null,
  }));
}

export function register(router: Router): void {
  router.get('/friends', async (ctx: Ctx) => {
    const list = await friends.list(ctx.playerId);
    return {
      friends: await withPresence(list.friends),
      incoming: await withPresence(list.incoming),
      outgoing: await withPresence(list.outgoing),
      blocked: await withPresence(list.blocked),
    };
  });

  router.get('/players/lookup', async (ctx: Ctx) => {
    const handle = ctx.query.get('handle');
    if (!handle) throw badRequest('handle is required');

    const found = await players.byHandle(handle);
    if (!found) throw notFound('no player with that handle');
    return { player: players.toPublic(found) };
  });

  router.post('/friends/request', async (ctx: Ctx) => {
    const handle = requireString(ctx.body, 'handle', 32);
    const target = await players.byHandle(handle);
    if (!target) throw notFound('no player with that handle');

    const outcome = await friends.request(ctx.playerId, target);
    const me = await players.byId(ctx.playerId);

    if (outcome.result === 'accepted') {
      await sendTo(target.id, { t: 'friend.accepted', player: players.toPublic(me) });
    } else {
      await sendTo(target.id, { t: 'friend.request', player: players.toPublic(me) });
    }

    return { result: outcome.result, player: players.toPublic(target) };
  });

  router.post('/friends/:id/accept', async (ctx: Ctx) => {
    await friends.accept(ctx.playerId, ctx.params.id);
    const me = await players.byId(ctx.playerId);
    await sendTo(ctx.params.id, { t: 'friend.accepted', player: players.toPublic(me) });
    return { ok: true };
  });

  router.del('/friends/:id', async (ctx: Ctx) => {
    await friends.remove(ctx.playerId, ctx.params.id);
    await sendTo(ctx.params.id, { t: 'friend.removed', playerId: ctx.playerId });
    return { ok: true };
  });

  router.post('/friends/:id/block', async (ctx: Ctx) => {
    await friends.block(ctx.playerId, ctx.params.id);

    await sendTo(ctx.params.id, { t: 'friend.removed', playerId: ctx.playerId });
    return { ok: true };
  });

  router.del('/friends/:id/block', async (ctx: Ctx) => {
    await friends.unblock(ctx.playerId, ctx.params.id);
    return { ok: true };
  });
}
