import { config } from '../config.js';
import * as players from '../store/players.js';
import { optionalString, requireString, type Ctx, type Router } from '../util/http.js';
import { sign } from '../util/jwt.js';

export function register(router: Router): void {
  router.post('/auth/guest', async (ctx: Ctx) => {
    const deviceId = requireString(ctx.body, 'deviceId', 128);
    const preferredName = optionalString(ctx.body, 'name', 32);

    const player = await players.loginWithDevice(deviceId, preferredName);
    const token = sign({ sub: player.id }, config.jwtSecret, config.sessionTtlSeconds);

    return {
      token,
      expiresIn: config.sessionTtlSeconds,
      player: players.toPublic(player),
    };
  });

  router.get('/me', async (ctx: Ctx) => {
    const player = await players.byId(ctx.playerId);
    return { player: players.toPublic(player) };
  });

  router.patch('/me', async (ctx: Ctx) => {
    const name = requireString(ctx.body, 'name', 32);
    const player = await players.rename(ctx.playerId, name);
    return { player: players.toPublic(player) };
  });
}
