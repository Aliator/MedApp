import type {Schemas} from '../../api/client';
import type {DemoPerson} from '../accounts';
import {invalid, notFound, problem, refuse} from '../api/http';
import {teamName} from '../seed';
import type {DemoAppointment, DemoPathway, DemoStage} from '../state';
import {db} from '../store';
import {nowUtc, ukDateOf} from '../time';
import {logActivity} from './adminLogs';
import {me, teamIdsOf} from './common';

/**
 * The team walls (decisions 183, 184 and 199 to 204 of `docs/multi-clinic-plan.md`): the light demo's copy of the
 * API's `TeamScope` and `TeamAccess`, the one place its rules live.
 * - **Patients** are centre-wide: anyone can find any patient.
 * - **A pathway** is seen in full by admins (`ViewAllTeams`, read only), and by a team with a live grant
 *   (`pathwayAccess`): its own team (`Owner`), and the lab once a sample is passed to it (`StageOwner`, kept after
 *   hand-back). A lab member invited to an MDT reads the pathways listed on it (`hasMdtGrant`, MC21), read only.
 *   Anyone else sees it only as a summary line on the patient (`otherTeamPathways`).
 * - **Its records** (appointments, letters, results, consent, specimens, MDT items) follow it; one without a
 *   pathway follows its own team. **A referral** is its current team's (and its pathway's, once accepted): redirected,
 *   the old team stops seeing it; registered for another team, the registrar doesn't keep it (MC5).
 * - **Only the pathway's own team changes it** (and a record's own team, the record), except a stage another team
 *   owns (the relay, MC7): that team (the lab) works it while it's current, and adds notes to the pathway meanwhile.
 *   The lab's specimen work is checked by its own handlers.
 * - **Across a wall is a 404**, never a 403, and records `TeamBoundaryRefused` once per target in the view window.
 * - **On a calendar**, another team's appointment is busy time with no patient (`busyTimeOf`), so clashes still count.
 * - **Counts and Insights** count only what the viewer's teams may see (admins: every team).
 */

/** Whose walls apply: the viewer's teams, and whether they see every team's (admins). */
export interface TeamScope {
  userId: string;
  teamIds: string[];
  seesAllTeams: boolean;
}

export const scopeOf = (person: DemoPerson): TeamScope => ({
  userId: person.id,
  teamIds: teamIdsOf(person),
  seesAllTeams: person.permissions.includes('ViewAllTeams'),
});

/** The signed-in person's scope (a 401 when nobody is). */
export const myScope = () => scopeOf(me());

/** Whether the viewer sees a pathway in full: every team (admins), a live grant for one of their teams, or an MDT's. */
export function canSeePathway(pathway: Pick<DemoPathway, 'id'>, scope: TeamScope = myScope()): boolean {
  return (
    scope.seesAllTeams ||
    db().pathwayAccess.some((a) => a.pathwayId === pathway.id && a.endedAtUtc === null && scope.teamIds.includes(a.teamId)) ||
    hasMdtGrant(pathway.id, scope)
  );
}

/**
 * An MDT grant (MC21; `PathwayAccessReason.Mdt`, decision 185, Q12): a lab member among an MDT's attendees reads each
 * pathway listed on it, read only, while its item is listed or deferred, and at least to the meeting's end; it ends
 * when the item is discussed or withdrawn. Worked out from the items here (the API keeps it as a `PathwayAccess` row by
 * user). MC26 widens it to every team's attendees.
 */
export function hasMdtGrant(pathwayId: string, scope: TeamScope): boolean {
  const lab = labTeamId();
  if (!lab || !scope.teamIds.includes(lab)) return false;
  const now = nowUtc();
  return db().mdtItems.some((item) => {
    if (item.pathwayId !== pathwayId || !item.meetingId) return false;
    const meeting = db().meetings.find((m) => m.id === item.meetingId);
    if (!meeting?.isMdt || !(meeting.attendees ?? []).some((a) => a.userId === scope.userId)) return false;
    return item.status === 'Listed' || item.status === 'Deferred' || (meeting.endUtc ?? '') > now;
  });
}

/** Whether the viewer's team owns a pathway, so may change it (admins read only, the lab only reads it). */
export const canChangePathway = (pathway: Pick<DemoPathway, 'teamId'>, scope: TeamScope = myScope()) =>
  scope.teamIds.includes(pathway.teamId);

/** The team that works a stage: its owner (the lab, for its stages), else the pathway's own team (the relay, MC7). */
export const stageTeamOf = (pathway: Pick<DemoPathway, 'teamId'>, stage: Pick<DemoStage, 'ownerTeamId'>) => stage.ownerTeamId ?? pathway.teamId;

/** Whether the viewer's team works a stage (`TeamAccess.CanWorkStage`): it's their team's, by the stage's owner. */
export const canWorkStage = (pathway: Pick<DemoPathway, 'teamId'>, stage: Pick<DemoStage, 'ownerTeamId'>, scope: TeamScope = myScope()) =>
  scope.teamIds.includes(stageTeamOf(pathway, stage));

/** Whether the viewer sees a record that belongs to a team: its pathway visible, or its own team theirs. */
export function canSeeRecord(record: {teamId: string | null; pathwayId?: string | null}, scope: TeamScope = myScope()): boolean {
  if (scope.seesAllTeams || (record.teamId !== null && scope.teamIds.includes(record.teamId))) return true;
  const pathway = record.pathwayId ? db().pathways.find((p) => p.id === record.pathwayId) : undefined;
  return pathway !== undefined && canSeePathway(pathway, scope);
}

/** Refuses across a wall: a 404 as if it weren't there, recorded for the privacy officer (IDs only). */
export function refuseAcrossWall(targetKind: string, targetId: string, what: string): never {
  logActivity('TeamBoundaryRefused', {details: JSON.stringify({targetKind, targetId})});
  refuse(notFound(what));
}

/** The record if it exists and the viewer may see it; otherwise a 404 (recorded when it's behind a wall). */
export function seeRecord<T extends {id?: string; teamId: string | null; pathwayId?: string | null}>(
  record: T | undefined,
  targetKind: string,
  what: string,
): T {
  if (!record) refuse(notFound(what));
  if (!canSeeRecord(record)) refuseAcrossWall(targetKind, record.id ?? '', what);
  return record;
}

/** Why the viewer's team can read a pathway but not change it: it's another team's. */
export const notYourTeams = (pathway: Pick<DemoPathway, 'teamId'>) =>
  `This pathway is ${teamName(pathway.teamId) ?? 'another team'}’s: your team can read it, and works on it only while one of its stages is with your team.`;

/** Whether the viewer's team owns a record (an appointment, a referral), so may change it. */
export const canChangeRecord = (record: {teamId: string | null}, scope: TeamScope = myScope()) =>
  record.teamId !== null && scope.teamIds.includes(record.teamId);

/** Refuses a change to a record the viewer can read but their team doesn't own (an admin's, the lab's). */
export function ensureCanChangeRecord(record: {teamId: string | null}, what: string) {
  if (!canChangeRecord(record))
    refuse(problem(409, `${what} is ${teamName(record.teamId) ?? 'another team'}’s: your team can read it, and only its own team changes it.`));
}

/**
 * Another team's appointment on a calendar (decision 183): busy time only, with no patient, pathway, stage, notes or
 * appointment ID.
 */
export const busyTimeOf = (appointment: Pick<DemoAppointment, 'staffUserId' | 'staffUsername' | 'startUtc' | 'endUtc'>): Schemas['BusyTimeResponse'] => ({
  staffUserId: appointment.staffUserId ?? undefined,
  username: appointment.staffUsername ?? null,
  startUtc: appointment.startUtc ?? undefined,
  endUtc: appointment.endUtc ?? appointment.startUtc ?? undefined,
  what: 'Busy',
});

/**
 * The teams Insights counts (`InsightsScope.TeamIds`): an admin's every team, or the one picked; anyone else's own
 * teams, narrowed to the one picked (another team's picked counts nothing).
 */
export function insightsTeams(teamId: string | null, scope: TeamScope = myScope()): (id: string) => boolean {
  if (scope.seesAllTeams) return (id) => !teamId || id === teamId;
  return (id) => scope.teamIds.includes(id) && (!teamId || id === teamId);
}

/**
 * The clinic picker's team (MC6: `TeamId` on the grids' searches), or null for all clinics. An admin may pick any team;
 * anyone else only one of their own: another is a 400, as the API.
 */
export function pickedTeam(query: URLSearchParams, scope: TeamScope = myScope()): string | null {
  const teamId = query.get('TeamId')?.trim().toLowerCase() || null;
  if (teamId && !scope.seesAllTeams && !scope.teamIds.some((id) => id.toLowerCase() === teamId))
    refuse(invalid({teamId: ['Choose one of your own teams, or all of them.']}));
  return teamId;
}

/** Whether a record's team is the one picked (always, for all clinics). */
export const isPickedTeam = (teamId: string | null | undefined, picked: string | null) => !picked || teamId?.toLowerCase() === picked;

/** Refuses a change to a pathway the viewer's team can read but doesn't own (the lab's, after hand-back). */
export function ensureCanChangePathway(pathway: DemoPathway) {
  if (!canChangePathway(pathway)) refuse(problem(409, notYourTeams(pathway)));
}

/** Refuses work on a stage another team works (the relay): the lab's stage is the lab's, the clinic's the clinic's. */
export function ensureCanWorkStage(pathway: DemoPathway, stage: DemoStage) {
  if (!canWorkStage(pathway, stage))
    refuse(problem(409, `The '${stage.name}' stage is ${teamName(stageTeamOf(pathway, stage)) ?? 'another team'}’s to work: it comes back when they finish it.`));
}

/** The lab team's ID (the one team of the lab kind). */
export const labTeamId = () => db().teams.find((t) => t.kind === 'Lab')?.id ?? null;

/** Passes a pathway to the lab (a sample recorded on it): the lab sees it in full from then on, and keeps it. */
export function passToLab(pathway: DemoPathway) {
  const lab = labTeamId();
  if (lab) grantStageOwner(pathway, lab);
}

/**
 * Gives a team that owns one of a pathway's stages its grant to see the pathway (`StageOwner`), when the stage is first
 * reached (the relay) or the lab is passed a sample; never ended (decision 184). Its own team needs none.
 */
export function grantStageOwner(pathway: DemoPathway, teamId: string) {
  if (teamId === pathway.teamId) return;
  const access = db().pathwayAccess;
  if (access.some((a) => a.pathwayId === pathway.id && a.teamId === teamId)) return;
  access.push({pathwayId: pathway.id, patientId: pathway.patientId, teamId, reason: 'StageOwner', startedAtUtc: nowUtc(), endedAtUtc: null});
}

const NOT_SUMMARISED: DemoPathway['status'][] = ['Cancelled', 'Archived'];

/**
 * A patient's pathways the viewer can't see, as summary lines only: the team, in progress or completed, and since
 * when (no title, stages, notes, files, ID or link). Cancelled and archived ones aren't summarised. Newest first.
 */
export function otherTeamPathways(patientId: string, scope: TeamScope = myScope()): Schemas['OtherTeamPathwaySummaryResponse'][] {
  if (scope.seesAllTeams) return [];
  return db()
    .pathways.filter((p) => p.patientId === patientId && !NOT_SUMMARISED.includes(p.status) && !canSeePathway(p, scope))
    .map((p) => {
      const completed = p.status === 'Completed';
      const completedAt = [...p.statusChanges].reverse().find((c) => c.to === 'Completed')?.atUtc ?? p.lastUpdated;
      return {teamName: teamName(p.teamId) ?? db().teams.find((t) => t.id === p.teamId)?.name ?? null, state: completed ? 'Completed' : 'InProgress', since: ukDateOf(completed ? completedAt : p.createdAt)};
    })
    .sort((a, b) => (b.since ?? '').localeCompare(a.since ?? '') || (a.teamName ?? '').localeCompare(b.teamName ?? ''));
}
