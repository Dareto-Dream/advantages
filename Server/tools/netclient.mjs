#!/usr/bin/env node

import { WebSocket } from 'ws';

const GATEWAY = (process.env.GATEWAY_URL ?? 'https://gateway-production-a3c7.up.railway.app').replace(/\/+$/, '');
const RUN_SECONDS = Number(process.argv[2] ?? 25);
const PROTOCOL_VERSION = 1;

const ClientOp = { Hello: 1, Input: 2, HeroPick: 3, Ready: 4, RoomCommand: 5, Pong: 6, Leave: 7 };
const ServerOp = { Welcome: 1, Snapshot: 2, MatchState: 3, Roster: 4, Event: 5, Ping: 6, Reject: 7 };
const Phase = ['HeroSelect', 'Staging', 'Live', 'Overtime', 'RoundOver', 'MatchOver'];
const EntityFlag = { Alive: 1, Grounded: 2, Firing: 4, Aiming: 8, Reloading: 16, Bot: 32 };

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
  return { status: res.status, json: text ? JSON.parse(text) : null };
}

class Writer {
  constructor() {
    this.parts = [];
  }
  u8(v) { const b = Buffer.alloc(1); b.writeUInt8(v & 0xff); this.parts.push(b); return this; }
  i8(v) { const b = Buffer.alloc(1); b.writeInt8(Math.max(-128, Math.min(127, v))); this.parts.push(b); return this; }
  u16(v) { const b = Buffer.alloc(2); b.writeUInt16LE(v & 0xffff); this.parts.push(b); return this; }
  u32(v) { const b = Buffer.alloc(4); b.writeUInt32LE(v >>> 0); this.parts.push(b); return this; }
  f32(v) { const b = Buffer.alloc(4); b.writeFloatLE(v); this.parts.push(b); return this; }
  i64(v) {

    const b = Buffer.alloc(8);
    b.writeUInt32LE(Number(BigInt(v) & 0xffffffffn), 0);
    b.writeUInt32LE(Number((BigInt(v) >> 32n) & 0xffffffffn), 4);
    this.parts.push(b);
    return this;
  }
  str(s) {
    const bytes = Buffer.from(s ?? '', 'utf8');
    this.u16(bytes.length);
    this.parts.push(bytes);
    return this;
  }
  angle(deg) { return this.u16(Math.round(((deg % 360) + 360) % 360 * (65535 / 360))); }
  done() { return Buffer.concat(this.parts); }
}

class Reader {
  constructor(buffer) {
    this.b = buffer;
    this.o = 0;
  }
  u8() { return this.b.readUInt8(this.o++); }
  i8() { return this.b.readInt8(this.o++); }
  u16() { const v = this.b.readUInt16LE(this.o); this.o += 2; return v; }
  u32() { const v = this.b.readUInt32LE(this.o); this.o += 4; return v; }
  f32() { const v = this.b.readFloatLE(this.o); this.o += 4; return v; }
  i64() { const lo = this.u32(); const hi = this.u32(); return Number((BigInt(hi) << 32n) | BigInt(lo)); }
  str() { const n = this.u16(); const s = this.b.toString('utf8', this.o, this.o + n); this.o += n; return s; }
  vec3() { return { x: this.f32(), y: this.f32(), z: this.f32() }; }
  angle() { return this.u16() * (360 / 65535); }
  pool() { return this.u16() * 0.25; }
  get left() { return this.b.length - this.o; }
}

function inputPacket(tick, yaw, moveX, moveY, buttons, position) {
  const w = new Writer();
  w.u8(ClientOp.Input).u8(1);
  w.u32(tick).angle(yaw).angle(0 + 180);
  w.i8(Math.round(moveX * 127)).i8(Math.round(moveY * 127));
  w.u8(buttons).u8(0).u8(0);
  w.f32(position.x).f32(position.y).f32(position.z);
  return w.done();
}

async function main() {
  console.log(`Advantage net client harness\ngateway: ${GATEWAY}\n`);

  const login = await api('/auth/guest', {
    method: 'POST',
    body: { deviceId: `harness-${Date.now()}`, name: 'Harness' },
  });
  const token = login.json.token;
  console.log(`signed in as ${login.json.player.handle}`);

  const queued = await api('/matchmaking/queue', { method: 'POST', token, body: { modes: [], regions: [] } });
  const ticketId = queued.json?.ticket?.id;
  if (!ticketId) throw new Error(`could not queue: ${JSON.stringify(queued.json)}`);

  let match = null;
  for (let i = 0; i < 30; i++) {
    await api('/matchmaking/pump', { method: 'POST', token });
    const status = await api(`/matchmaking/ticket/${ticketId}`, { token });
    const ticket = status.json?.ticket;
    if (ticket?.state === 'matched') { match = ticket.match; break; }
    if (ticket?.state === 'failed') throw new Error(`matchmaking failed: ${ticket.reason}`);
    await new Promise((r) => setTimeout(r, 800));
  }
  if (!match) throw new Error('no match formed - is a game server registered?');

  console.log(`matched into ${match.region} / ${match.mode}, room ${match.roomId}\n`);

  const stats = {
    snapshots: 0,
    entities: 0,
    bots: 0,
    players: 0,
    phases: new Set(),
    roster: [],
    pings: [],
    lastPositions: new Map(),
    moved: 0,
    rejected: null,
    welcome: null,
  };

  await new Promise((resolve, reject) => {
    const socket = new WebSocket(`${match.relayUrl}/play?token=${encodeURIComponent(match.joinToken)}`);
    let myEntity = 0;
    let tick = 0;
    let inputTimer = null;
    const finishAt = Date.now() + RUN_SECONDS * 1000;

    socket.on('open', () => {
      socket.send(new Writer().u8(ClientOp.Hello).u8(PROTOCOL_VERSION).str(match.joinToken).done());
    });

    socket.on('message', (data) => {
      const r = new Reader(data);
      const op = r.u8();

      if (op === ServerOp.Welcome) {
        const version = r.u8();
        myEntity = r.u16();
        r.u32();
        const tickRate = r.u8();
        const mode = r.u8();
        const teamSize = r.u8();
        const team = r.u8();
        const host = r.u8();
        const roomId = r.str();

        stats.welcome = { version, myEntity, tickRate, mode, teamSize, team, host: !!host, roomId };
        console.log(`welcomed: entity ${myEntity}, team ${team}, ${tickRate} Hz, team size ${teamSize}, host ${!!host}`);

        socket.send(new Writer().u8(ClientOp.HeroPick).u8(3).done());
        socket.send(new Writer().u8(ClientOp.Ready).done());

        inputTimer = setInterval(() => {
          tick++;
          const angle = (tick / 60) * 90;
          const packet = inputPacket(tick, angle, Math.sin(tick / 30), Math.cos(tick / 30), 0, { x: 0, y: 0, z: 0 });
          if (socket.readyState === socket.OPEN) socket.send(packet);
        }, 1000 / 60);
        return;
      }

      if (op === ServerOp.Snapshot) {
        r.u32();
        const count = r.u8();
        let bots = 0;
        let players = 0;

        for (let i = 0; i < count; i++) {
          const id = r.u16();
          const flags = r.u8();
          r.u8();
          r.u8();
          const pos = r.vec3();
          r.vec3();
          r.angle();
          r.angle();
          r.pool();
          r.pool();

          if (flags & EntityFlag.Bot) bots++; else players++;

          const previous = stats.lastPositions.get(id);
          if (previous) {
            const d = Math.hypot(pos.x - previous.x, pos.y - previous.y, pos.z - previous.z);
            if (d > 0.02) stats.moved++;
          }
          stats.lastPositions.set(id, pos);
        }

        stats.snapshots++;
        stats.entities = Math.max(stats.entities, count);
        stats.bots = Math.max(stats.bots, bots);
        stats.players = Math.max(stats.players, players);
        return;
      }

      if (op === ServerOp.MatchState) {
        const phase = r.u8();
        stats.phases.add(Phase[phase] ?? `phase${phase}`);
        return;
      }

      if (op === ServerOp.Roster) {
        const count = r.u8();
        const entries = [];
        for (let i = 0; i < count; i++) {
          const entityId = r.u16();
          r.str();
          const name = r.str();
          const team = r.u8();
          const operative = r.u8();
          const bot = r.u8();
          entries.push({ entityId, name, team, operative, bot: !!bot });
        }
        stats.roster = entries;
        return;
      }

      if (op === ServerOp.Ping) {
        const serverTime = r.i64();
        if (r.left >= 2) stats.pings.push(r.u16());
        socket.send(new Writer().u8(ClientOp.Pong).i64(serverTime).done());
        return;
      }

      if (op === ServerOp.Reject) {
        stats.rejected = r.str();
        socket.close();
      }
    });

    socket.on('error', (err) => {
      clearInterval(inputTimer);
      reject(err);
    });

    socket.on('close', () => {
      clearInterval(inputTimer);
      resolve();
    });

    const poll = setInterval(() => {
      if (Date.now() >= finishAt) {
        clearInterval(poll);
        clearInterval(inputTimer);
        if (socket.readyState === socket.OPEN) {
          socket.send(new Writer().u8(ClientOp.Leave).done());
          socket.close();
        }
        resolve();
      }
    }, 500);
  });

  console.log('\nresults');
  if (stats.rejected) {
    console.log(`  rejected: ${stats.rejected}`);
    process.exit(1);
  }

  const expectedRate = (stats.welcome?.tickRate ?? 30) * RUN_SECONDS;
  const lines = [
    ['handshake completed', !!stats.welcome, stats.welcome ? `entity ${stats.welcome.myEntity}` : ''],
    ['snapshots streaming', stats.snapshots > RUN_SECONDS * 5, `${stats.snapshots} in ${RUN_SECONDS}s (~${(stats.snapshots / RUN_SECONDS).toFixed(1)}/s of ${stats.welcome?.tickRate ?? '?'})`],
    ['our avatar exists', stats.players >= 1, `${stats.players} player entity(s)`],
    ['server is running bots', stats.bots > 0, `${stats.bots} bot(s)`],
    ['entities are being simulated', stats.moved > 0, `${stats.moved} position changes observed`],
    ['match advanced past hero select', stats.phases.size > 0, [...stats.phases].join(' -> ')],
    ['roster replicated', stats.roster.length > 0, `${stats.roster.length} entries: ${stats.roster.slice(0, 4).map((e) => `${e.name}${e.bot ? '(bot)' : ''}`).join(', ')}`],
    ['round trip measured', stats.pings.length > 0, stats.pings.length ? `${Math.min(...stats.pings)}-${Math.max(...stats.pings)} ms` : ''],
  ];

  let failures = 0;
  for (const [label, condition, detail] of lines) {
    if (!condition) failures++;
    console.log(`  ${condition ? 'PASS' : 'FAIL'}  ${label}${detail ? ` — ${detail}` : ''}`);
  }

  console.log(`\n${lines.length - failures} passed, ${failures} failed`);
  process.exit(failures === 0 ? 0 : 1);
}

main().catch((err) => {
  console.error('\nharness failed:', err.message);
  process.exit(1);
});
