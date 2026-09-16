import type { IncomingMessage, ServerResponse } from 'node:http';

export class HttpError extends Error {
  constructor(readonly status: number, message: string, readonly code = 'error') {
    super(message);
  }
}

export const badRequest = (m: string) => new HttpError(400, m, 'bad_request');
export const unauthorized = (m = 'not signed in') => new HttpError(401, m, 'unauthorized');
export const forbidden = (m = 'not allowed') => new HttpError(403, m, 'forbidden');
export const notFound = (m = 'not found') => new HttpError(404, m, 'not_found');
export const conflict = (m: string) => new HttpError(409, m, 'conflict');

export interface Ctx {
  req: IncomingMessage;
  res: ServerResponse;
  params: Record<string, string>;
  query: URLSearchParams;
  body: any;

  playerId: string;

  serverId: string;
}

type Handler = (ctx: Ctx) => Promise<unknown> | unknown;

interface Route {
  method: string;
  segments: string[];
  handler: Handler;
}

const MAX_BODY_BYTES = 256 * 1024;

export class Router {
  private readonly routes: Route[] = [];

  add(method: string, path: string, handler: Handler): void {
    this.routes.push({ method, segments: path.split('/').filter(Boolean), handler });
  }

  get(path: string, handler: Handler) { this.add('GET', path, handler); }
  post(path: string, handler: Handler) { this.add('POST', path, handler); }
  patch(path: string, handler: Handler) { this.add('PATCH', path, handler); }
  del(path: string, handler: Handler) { this.add('DELETE', path, handler); }

  match(method: string, pathname: string): { handler: Handler; params: Record<string, string> } | null {
    const parts = pathname.split('/').filter(Boolean);

    for (const route of this.routes) {
      if (route.method !== method || route.segments.length !== parts.length) continue;

      const params: Record<string, string> = {};
      let ok = true;

      for (let i = 0; i < route.segments.length; i++) {
        const seg = route.segments[i];
        if (seg.startsWith(':')) {
          params[seg.slice(1)] = decodeURIComponent(parts[i]);
        } else if (seg !== parts[i]) {
          ok = false;
          break;
        }
      }

      if (ok) return { handler: route.handler, params };
    }

    return null;
  }
}

export async function readJsonBody(req: IncomingMessage): Promise<any> {
  const chunks: Buffer[] = [];
  let size = 0;

  for await (const chunk of req) {
    size += (chunk as Buffer).length;
    if (size > MAX_BODY_BYTES) throw badRequest('body too large');
    chunks.push(chunk as Buffer);
  }

  if (chunks.length === 0) return {};

  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch {
    throw badRequest('body is not valid JSON');
  }
}

export function sendJson(res: ServerResponse, status: number, payload: unknown): void {
  const body = JSON.stringify(payload);
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': Buffer.byteLength(body),
    'access-control-allow-origin': '*',
  });
  res.end(body);
}

export function applyCors(res: ServerResponse): void {
  res.setHeader('access-control-allow-origin', '*');
  res.setHeader('access-control-allow-headers', 'authorization, content-type');
  res.setHeader('access-control-allow-methods', 'GET, POST, PATCH, DELETE, OPTIONS');
  res.setHeader('access-control-max-age', '86400');
}

export function bearer(req: IncomingMessage): string | null {
  const header = req.headers.authorization;
  if (!header || !header.toLowerCase().startsWith('bearer ')) return null;
  return header.slice(7).trim() || null;
}

export function requireString(body: any, field: string, max = 200): string {
  const value = body?.[field];
  if (typeof value !== 'string' || value.trim().length === 0) throw badRequest(`${field} is required`);
  if (value.length > max) throw badRequest(`${field} is too long`);
  return value.trim();
}

export function optionalString(body: any, field: string, max = 200): string | null {
  const value = body?.[field];
  if (value == null) return null;
  if (typeof value !== 'string') throw badRequest(`${field} must be a string`);
  if (value.length > max) throw badRequest(`${field} is too long`);
  return value.trim();
}
