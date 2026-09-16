#!/usr/bin/env node

import { WebSocket } from 'ws';

const GATEWAY = (process.argv[2] ?? process.env.GATEWAY_URL ?? 'https://gateway-production-a3c7.up.railway.app')
  .replace(/\/+$/, '');

const PROTOCOL_VERSION = 1;

let passed = 0;
let failed = 0;

function ok(label, detail = '') {
  passed++;
  console.log(`  PASS  ${label}${detail ? ` — ${detail}` : ''}`);
}

function bad(label, detail = '') {
  failed++;
  console.log(`  FAIL  ${label}${detail ? ` — ${detail}` : ''}`);
}

function section(title) {
  console.log(`\n${title}`);
}

async function api(path, { method = 'GET', token = null, body = null } = {}) {
  const res = await fetch(GATEWAY + path, {
    method,
    headers: {
      ...(body ? { 'content-type': 'application/json' } : {}),
      ...(token ? { authorization: `Bearer ${token}` } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });

  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {

  }

  return { status: res.status, json, text };
}

function expect(label, condition, detail = '') {
  if (condition) ok(label, detail);
  else bad(label, detail);
  return condition;
}

function helloPacket(token) {
  const tokenBytes = Buffer.from(token, 'utf8');
  const buffer = Buffer.alloc(1 + 1 + 2 + tokenBytes.length);

  let offset = 0;
  buffer.writeUInt8(1, offset++);
  buffer.writeUInt8(PROTOCOL_VERSION, offset++);
  buffer.writeUInt16LE(tokenBytes.length, offset);
  offset += 2;
  tokenBytes.copy(buffer, offset);

  return buffer;
}

async function main() {
  console.log(`Advantage backend smoke test\ngateway: ${GATEWAY}`);

  section('health');
  const health = await api('/health');
  expect('gateway responds', health.status === 200, `status ${health.status}`);
  expect('reports itself', health.json?.service === 'advantage-gateway', health.json?.service ?? health.text.slice(0, 80));

  section('auth');
  const deviceA = `smoke-a-${Date.now()}`;
  const deviceB = `smoke-b-${Date.now()}`;

  const loginA = await api('/auth/guest', { method: 'POST', body: { deviceId: deviceA, name: 'SmokeAlpha' } });
  expect('guest login issues a token', !!loginA.json?.token, loginA.json?.player?.handle ?? loginA.text.slice(0, 80));

  const tokenA = loginA.json?.token;
  const playerA = loginA.json?.player;

  const loginB = await api('/auth/guest', { method: 'POST', body: { deviceId: deviceB, name: 'SmokeBravo' } });
  const tokenB = loginB.json?.token;
  const playerB = loginB.json?.player;
  expect('second identity created', !!tokenB && playerB?.id !== playerA?.id);

  const again = await api('/auth/guest', { method: 'POST', body: { deviceId: deviceA } });
  expect('same device returns the same player', again.json?.player?.id === playerA?.id);

  const me = await api('/me', { token: tokenA });
  expect('session token resolves', me.json?.player?.id === playerA?.id);

  const anon = await api('/me');
  expect('no token is rejected', anon.status === 401, `status ${anon.status}`);

  const renamed = await api('/me', { method: 'PATCH', token: tokenA, body: { name: 'SmokeRenamed' } });
  expect('rename works', renamed.json?.player?.name === 'SmokeRenamed', renamed.json?.player?.handle);

  section('friends');
  const lookup = await api(`/players/lookup?handle=${encodeURIComponent(playerB.handle)}`, { token: tokenA });
  expect('lookup by handle', lookup.json?.player?.id === playerB.id);

  const request = await api('/friends/request', { method: 'POST', token: tokenA, body: { handle: playerB.handle } });
  expect('friend request sent', request.json?.result === 'requested', request.json?.result ?? request.text.slice(0, 80));

  const incoming = await api('/friends', { token: tokenB });
  expect('request shows as incoming for B', incoming.json?.incoming?.length === 1, `incoming ${incoming.json?.incoming?.length}`);

  const outgoing = await api('/friends', { token: tokenA });
  expect('request shows as outgoing for A', outgoing.json?.outgoing?.length === 1);

  const accept = await api(`/friends/${playerA.id}/accept`, { method: 'POST', token: tokenB });
  expect('request accepted', accept.status === 200, `status ${accept.status}`);

  const friendsA = await api('/friends', { token: tokenA });
  expect('A now has B as a friend', friendsA.json?.friends?.length === 1, friendsA.json?.friends?.[0]?.player?.handle);
  expect('presence is reported', typeof friendsA.json?.friends?.[0]?.presence === 'string', friendsA.json?.friends?.[0]?.presence);

  const currentHandleA = renamed.json?.player?.handle ?? playerA.handle;
  const selfAdd = await api('/friends/request', { method: 'POST', token: tokenA, body: { handle: currentHandleA } });
  expect('cannot add yourself', selfAdd.status === 400, `status ${selfAdd.status} ${selfAdd.json?.message ?? ''}`);

  const duplicate = await api('/friends/request', { method: 'POST', token: tokenA, body: { handle: playerB.handle } });
  expect('duplicate request is a conflict', duplicate.status === 409, `status ${duplicate.status}`);

  section('party');
  const invite = await api('/party/invite', { method: 'POST', token: tokenA, body: { playerId: playerB.id } });
  expect('friend can be invited to a party', invite.status === 200, invite.json?.party?.id ?? invite.text.slice(0, 80));

  const partyId = invite.json?.party?.id;
  const join = await api('/party/join', { method: 'POST', token: tokenB, body: { partyId } });
  expect('invitee joins', join.json?.party?.members?.length === 2, `members ${join.json?.party?.members?.length}`);

  const leave = await api('/party/leave', { method: 'POST', token: tokenB });
  expect('member can leave', leave.status === 200);

  section('regions');
  const regions = await api('/regions');
  const regionIds = (regions.json?.regions ?? []).map((r) => r.id);
  expect('four regions offered', regionIds.length === 4, regionIds.join(', '));
  expect('every region has a relay url', (regions.json?.regions ?? []).every((r) => !!r.relayUrl));

  const live = (regions.json?.regions ?? []).filter((r) => r.servers > 0);
  console.log(`  info  ${live.length} region(s) with a live game server: ${live.map((r) => r.id).join(', ') || 'none'}`);

  for (const region of regions.json?.regions ?? []) {
    if (!region.relayUrl) {
      bad(`relay ${region.id} answers /ping`, 'no relay url configured for this region');
      continue;
    }

    const relayHttp = region.relayUrl.replace(/^wss:/, 'https:').replace(/^ws:/, 'http:');
    const started = Date.now();
    try {
      const res = await fetch(`${relayHttp}/ping`, { signal: AbortSignal.timeout(8000) });
      const elapsed = Date.now() - started;
      expect(`relay ${region.id} answers /ping`, res.ok, `${elapsed} ms`);
    } catch (err) {
      bad(`relay ${region.id} answers /ping`, err.message);
    }
  }

  section('matchmaking');
  const queued = await api('/matchmaking/queue', {
    method: 'POST',
    token: tokenA,
    body: { modes: ['Convergence'], regions: regionIds },
  });

  const ticketId = queued.json?.ticket?.id;
  expect('ticket created', !!ticketId, queued.json?.ticket?.state ?? queued.text.slice(0, 120));

  let matched = null;
  for (let attempt = 0; attempt < 20 && ticketId; attempt++) {
    await api('/matchmaking/pump', { method: 'POST', token: tokenA });
    const status = await api(`/matchmaking/ticket/${ticketId}`, { token: tokenA });
    const ticket = status.json?.ticket;

    if (ticket?.state === 'matched') {
      matched = ticket;
      break;
    }

    if (ticket?.state === 'failed') {
      console.log(`  info  matchmaking gave up: ${ticket.reason}`);
      break;
    }

    await new Promise((resolve) => setTimeout(resolve, 1000));
  }

  if (matched) {
    ok('match formed', `${matched.match.region} / ${matched.match.mode}`);
    expect('join token issued', !!matched.match.joinToken);
    expect('relay url issued', !!matched.match.relayUrl, matched.match.relayUrl);

    section('game server handshake');
    await new Promise((resolve) => {
      const url = `${matched.match.relayUrl}/play?token=${encodeURIComponent(matched.match.joinToken)}`;
      const socket = new WebSocket(url);
      const timer = setTimeout(() => {
        bad('server sends Welcome', 'timed out after 15s');
        socket.terminate();
        resolve();
      }, 15000);

      socket.on('open', () => {
        ok('relay accepted the join token');
        socket.send(helloPacket(matched.match.joinToken));
      });

      socket.on('message', (data) => {
        const op = data.readUInt8(0);
        if (op === 1) {
          const version = data.readUInt8(1);
          const entityId = data.readUInt16LE(2);
          ok('server sends Welcome', `protocol ${version}, entity ${entityId}`);
        } else if (op === 7) {
          const length = data.readUInt16LE(1);
          bad('server sends Welcome', `rejected: ${data.toString('utf8', 3, 3 + length)}`);
        } else {
          return;
        }

        clearTimeout(timer);
        socket.close();
        resolve();
      });

      socket.on('error', (err) => {
        bad('relay accepted the join token', err.message);
        clearTimeout(timer);
        resolve();
      });
    });
  } else {
    console.log('  info  no game server is registered, so the match path stops at the queue.');
    console.log('  info  start one (Advantage > Net > Run As Dedicated Server, or deploy the image) and re-run.');
    await api(`/matchmaking/ticket/${ticketId}`, { method: 'DELETE', token: tokenA });
  }

  section('cleanup');
  const removed = await api(`/friends/${playerB.id}`, { method: 'DELETE', token: tokenA });
  expect('friend removed', removed.status === 200);

  console.log(`\n${passed} passed, ${failed} failed`);
  process.exit(failed === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error('\nsmoke test crashed:', err);
  process.exit(1);
});
