import { createHmac, timingSafeEqual } from 'node:crypto';

function b64url(input: Buffer | string): string {
  return Buffer.from(input).toString('base64url');
}

export interface Claims {
  sub: string;
  iat: number;
  exp: number;
  [key: string]: unknown;
}

export function sign(payload: Record<string, unknown>, secret: string, ttlSeconds: number): string {
  const now = Math.floor(Date.now() / 1000);
  const body: Claims = { sub: '', ...payload, iat: now, exp: now + ttlSeconds } as Claims;
  const head = b64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const claims = b64url(JSON.stringify(body));
  const data = `${head}.${claims}`;
  const sig = createHmac('sha256', secret).update(data).digest('base64url');
  return `${data}.${sig}`;
}

export function verify(token: string, secret: string): Claims | null {
  const parts = token.split('.');
  if (parts.length !== 3) return null;

  const [head, claims, sig] = parts;
  const expected = createHmac('sha256', secret).update(`${head}.${claims}`).digest('base64url');

  const a = Buffer.from(sig);
  const b = Buffer.from(expected);
  if (a.length !== b.length || !timingSafeEqual(a, b)) return null;

  try {
    const parsed = JSON.parse(Buffer.from(claims, 'base64url').toString('utf8')) as Claims;
    if (typeof parsed.exp !== 'number' || parsed.exp < Math.floor(Date.now() / 1000)) return null;
    return parsed;
  } catch {
    return null;
  }
}
