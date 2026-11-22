import {APPOINTMENTS, id, PATHWAYS, PATIENTS, REFERRALS, TEAMS, USERS} from '../demo/ids';
import {ownerAccess} from '../demo/seed';
import type {DemoState} from '../demo/state';

/**
 * A third clinic, for the team walls' tests only: beside the seeded Dermatology, Eye clinic and lab, a test that needs
 * another clinic's records on Dermatology's own patients (and Tom Hallworth in two clinics) adds them with
 * `addOtherClinic`. Its IDs are its own, apart from the Eye clinic's. Synthetic, like the seed.
 */
export const OTHER_CLINIC = {
  teamId: id(0xd1, 0x7e),
  name: 'Other clinic (test)',
  /** Priya Holloway's pathway in the other clinic, beside her Dermatology one. */
  pathwayId: id(0xd5, 0x7e),
  /** A referral of Priya Holloway's to the other clinic, awaiting triage. */
  referralId: id(0xd6, 0x7e),
  /** Its first visit, booked with Tom Hallworth (who also works in the other clinic). */
  appointmentId: id(0xd7, 0x7e),
} as const;

/** Adds the other clinic to the state: the team, a pathway and a referral of Priya Holloway's, and a visit with Tom Hallworth. */
export function addOtherClinic(state: DemoState) {
  const dermatology = state.teams.find((t) => t.id === TEAMS.dermatology)!;
  state.teams.push({...dermatology, id: OTHER_CLINIC.teamId, name: OTHER_CLINIC.name, templateCount: 0, pathwayCount: 1, memberCount: 1, referralCount: 1});

  // A copy of Tobias Lund's pathway (at Triage), made Priya's in the other clinic.
  const pathway = structuredClone(state.pathways.find((p) => p.id === PATHWAYS.unassigned)!);
  const stageIds = new Map(pathway.stages.map((s, i) => [s.id, id(0x7e, i + 1)]));
  Object.assign(pathway, {
    id: OTHER_CLINIC.pathwayId,
    patientId: PATIENTS.priyaHolloway,
    teamId: OTHER_CLINIC.teamId,
    title: 'A pathway in the other clinic',
    status: 'InProgress',
    handovers: [],
    notes: [],
    assignments: [],
  });
  for (const stage of pathway.stages) stage.id = stageIds.get(stage.id)!;
  pathway.links = pathway.links.map((l) => ({fromStageId: stageIds.get(l.fromStageId)!, toStageId: stageIds.get(l.toStageId)!}));
  state.pathways.push(pathway);
  state.pathwayAccess.push(ownerAccess(pathway));

  const referral = structuredClone(state.referrals.find((r) => r.id === REFERRALS.dermatologyToTriage)!);
  Object.assign(referral, {id: OTHER_CLINIC.referralId, patientId: PATIENTS.priyaHolloway, teamId: OTHER_CLINIC.teamId});
  referral.history = referral.history.map((event, i) => ({...event, id: id(0xde, 0x7e0 + i), teamName: OTHER_CLINIC.name}));
  state.referrals.push(referral);

  // Tom Hallworth works in both clinics, so Dermatology sees the other clinic's visit on his calendar as busy time.
  state.memberships[USERS.hallworth] = [...(state.memberships[USERS.hallworth] ?? []), {teamId: OTHER_CLINIC.teamId, role: 'Member'}];
  const visit = structuredClone(state.appointments.find((a) => a.id === APPOINTMENTS.ilonaFollowUp)!);
  Object.assign(visit, {
    id: OTHER_CLINIC.appointmentId,
    teamId: OTHER_CLINIC.teamId,
    patientId: PATIENTS.priyaHolloway,
    patientName: 'Priya Holloway',
    pathwayId: pathway.id,
    pathwayTitle: pathway.title,
    stageId: pathway.stages[0].id,
    stageName: pathway.stages[0].name,
    staffUserId: USERS.hallworth,
    staffUsername: 't.hallworth',
  });
  state.appointments.push(visit);
}
