import {STAFF} from '../accounts';
import {PATHWAYS, TEAMS, id} from '../ids';
import type {BaseState, DemoPathwayAccess} from '../state';
import {daysFrom, ukTime} from '../time';

/**
 * The light demo's consults (MC25 of `docs/multi-clinic-plan.md`): a pathway's team asks another team (a clinic or the
 * lab) a question. The asked team reads the pathway while it's open (a `Consult` grant in `pathwayAccess`), replies on
 * it, and the asking team closes it (or withdraws it before a reply), which ends the grant. Handlers:
 * `handlers/consults.ts`. The question and reply are free text, kept here only: never in the audit log or a notification.
 */

/** `ConsultStatus` by name (stored numbers: Open 0, Replied 1, Closed 2, Withdrawn 3). */
export type ConsultStatus = 'Open' | 'Replied' | 'Closed' | 'Withdrawn';

export interface DemoConsult {
  id: string;
  pathwayId: string;
  patientId: string;
  /** The pathway's team, which asked. */
  askingTeamId: string;
  /** The team asked: a clinic or the lab. */
  askedTeamId: string;
  question: string;
  status: ConsultStatus;
  requestedByUserId: string;
  requestedAtUtc: string;
  reply: string | null;
  repliedByUserId: string | null;
  repliedAtUtc: string | null;
  /** Who closed or withdrew it, and when. */
  closedByUserId: string | null;
  closedAtUtc: string | null;
  version: number;
}

export interface ConsultSlice {
  consults: DemoConsult[];
}

export const CONSULTS = {
  /**
   * Felix Moreton's suspicious lesion: Dermatology asks the lab (a pathologist's opinion) before choosing at Dermoscopy
   * assessment. Open, so the lab reads his pathway, which it otherwise couldn't (no sample has been passed to it).
   */
  felixToLab: id(0xb5, 1),
} as const;

/** The grant an open consult gives the asked team: read only, until it closes. */
export const consultAccess = (consult: DemoConsult): DemoPathwayAccess => ({
  pathwayId: consult.pathwayId,
  patientId: consult.patientId,
  teamId: consult.askedTeamId,
  reason: 'Consult',
  consultId: consult.id,
  startedAtUtc: consult.requestedAtUtc,
  endedAtUtc: consult.closedAtUtc,
});

/** The seeded consults, and their grants (added to `pathwayAccess` by the seed). */
export function seedConsults(base: BaseState): ConsultSlice {
  const felix = base.pathways.find((p) => p.id === PATHWAYS.branchToChoose);
  if (!felix) return {consults: []};
  return {
    consults: [
      {
        id: CONSULTS.felixToLab,
        pathwayId: felix.id,
        patientId: felix.patientId,
        askingTeamId: TEAMS.dermatology,
        askedTeamId: TEAMS.lab,
        question:
          'If we biopsy this 9 mm lesion on the shin, would the lab rather have a punch biopsy or a full excision for a reliable report? (Synthetic question.)',
        status: 'Open',
        requestedByUserId: STAFF.id,
        requestedAtUtc: ukTime(daysFrom(-1), 11, 15),
        reply: null,
        repliedByUserId: null,
        repliedAtUtc: null,
        closedByUserId: null,
        closedAtUtc: null,
        version: 1,
      },
    ],
  };
}
