import type {Consult} from '../api/consults';
import {mdtPatient} from './mdt';

/** Synthetic consults for tests: invented people and text, a 999 NHS number. */
export const CONSULT_ID = '00000000-0000-4000-8000-0000000000e1';

export const testConsult = (overrides: Partial<Consult> = {}): Consult => ({
  id: CONSULT_ID,
  status: 'Open',
  patient: mdtPatient,
  pathwayId: 'proc-1',
  pathwayTitle: 'Skin lesion',
  askingTeamId: '00000000-0000-4000-8000-0000000000b1',
  askingTeamName: 'Dermatology',
  askedTeamId: '00000000-0000-4000-8000-0000000000b6',
  askedTeamName: 'Lab',
  question: 'Synthetic question for the lab.',
  requestedByUsername: 'k.ashford',
  requestedAtUtc: '2026-11-18T10:00:00Z',
  reply: null,
  repliedByUsername: null,
  repliedAtUtc: null,
  closedByUsername: null,
  closedAtUtc: null,
  version: 1,
  canReply: false,
  canClose: false,
  canWithdraw: false,
  ...overrides,
});
