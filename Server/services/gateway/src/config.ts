function num(name: string, fallback: number): number {
  const raw = process.env[name];
  if (!raw) return fallback;
  const parsed = Number(raw);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function str(name: string, fallback: string): string {
  return process.env[name] ?? fallback;
}

export const REGIONS = ['us-west', 'us-east', 'eu-west', 'ap-southeast'] as const;
export type Region = (typeof REGIONS)[number];

export function isRegion(value: string): value is Region {
  return (REGIONS as readonly string[]).includes(value);
}

function relayEnvKey(region: Region): string {
  return `RELAY_URL_${region.toUpperCase().replace(/-/g, '_')}`;
}

export function relayUrlFor(region: Region): string | null {
  return process.env[relayEnvKey(region)] ?? null;
}

export const config = {
  port: num('PORT', 8080),
  databaseUrl: str('DATABASE_URL', ''),
  redisUrl: process.env.REDIS_URL ?? process.env.REDIS_PRIVATE_URL ?? '',

  jwtSecret: str('JWT_SECRET', 'dev-insecure-player-secret'),

  internalSecret: str('INTERNAL_SECRET', 'dev-insecure-internal-secret'),

  sessionTtlSeconds: num('SESSION_TTL_SECONDS', 60 * 60 * 24 * 30),
  joinTokenTtlSeconds: num('JOIN_TOKEN_TTL_SECONDS', 120),

  serverStaleSeconds: num('SERVER_STALE_SECONDS', 20),

  sweepIntervalMs: num('SWEEP_INTERVAL_MS', 5000),

  matchmakerIntervalMs: num('MATCHMAKER_INTERVAL_MS', 1000),
  matchTeamSize: num('MATCH_TEAM_SIZE', 4),

  botFillAfterSeconds: num('BOT_FILL_AFTER_SECONDS', 12),

  ticketTimeoutSeconds: num('TICKET_TIMEOUT_SECONDS', 180),

  presenceTtlSeconds: num('PRESENCE_TTL_SECONDS', 60),

  logLevel: str('LOG_LEVEL', 'info'),
} as const;

export const MODES = ['Convergence', 'Extraction', 'Breach', 'Dominion'] as const;
export type Mode = (typeof MODES)[number];

export function isMode(value: string): value is Mode {
  return (MODES as readonly string[]).includes(value);
}
