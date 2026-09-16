import { randomUUID, randomInt } from 'node:crypto';

export const uuid = randomUUID;

export function randomTag(): string {
  return randomInt(0, 10000).toString().padStart(4, '0');
}

const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

export function joinCode(length = 6): string {
  let out = '';
  for (let i = 0; i < length; i++) out += CODE_ALPHABET[randomInt(0, CODE_ALPHABET.length)];
  return out;
}

const ADJECTIVES = ['Swift', 'Iron', 'Quiet', 'Vivid', 'Solar', 'Grim', 'Neon', 'Cobalt', 'Rapid', 'Lunar'];
const NOUNS = ['Vector', 'Cipher', 'Relay', 'Warden', 'Spectre', 'Anchor', 'Falcon', 'Ember', 'Drift', 'Signal'];

export function randomName(): string {
  return `${ADJECTIVES[randomInt(0, ADJECTIVES.length)]}${NOUNS[randomInt(0, NOUNS.length)]}`;
}
