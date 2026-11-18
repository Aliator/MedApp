import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {TARGET_DAYS} from '../seed';
import type {DemoReferral} from '../state';
import {db} from '../store';
import {daysFrom, nowUtc, today} from '../time';
import {can, contains, flag, list, me, NO_FLAGS, paged, pathwayPatient, patientName, requirePermission, sorted} from './common';
import {createPathway, pathwayResponse} from './pathways';

/**
 * Referrals and triage. Simplified: the waiting-time clock runs from the day received and stops when the referral is
 * accepted (the full version stops it at the template's clock-stop stage, and can pause it); no letters or files.
 */

const daysBetween = (from: string, to: string) => Math.round((Date.parse(to) - Date.parse(from)) / 86400_000);

/** The referral's waiting-time clock. */
export function referralSummaryClock(referral: DemoReferral): Schemas['WaitingClockResponse'] {
  const targetDays = TARGET_DAYS[referral.requestedPriority];
  const stoppedOn = referral.decidedAtUtc ? referral.decidedAtUtc.slice(0, 10) : null;
  const daysWaited = daysBetween(referral.receivedOn, stoppedOn ?? today());
  return {
    state: stoppedOn ? 'Stopped' : 'Running',
    targetDays,
    waitStartOn: referral.receivedOn,
    targetOn: daysFrom(targetDays, referral.receivedOn),
    daysWaited,
    pausedDays: 0,
    isBreached: daysWaited > targetDays,
    pausedSinceOn: null,
    pauseReviewOn: null,
    isReviewOverdue: false,
    stoppedOn,
    stopReason: stoppedOn ? (referral.status === 'Accepted' ? 'FirstSeen' : 'Closed') : null,
  };
}

const specialty = (id: string) => db().specialties.find((s) => s.id === id);

/** Why the signed-in person can't triage this referral, or null if they can. */
function whyCantTriage(referral: DemoReferral): string | null {
  if (referral.status !== 'AwaitingTriage') return 'This referral has been triaged.';
  if (!can('TriageReferrals')) return "Your roles don't allow triaging referrals.";
  if (!me().specialtyIds.includes(referral.specialtyId))
    return `Only members of ${specialty(referral.specialtyId)?.name ?? 'its specialty'} triage this referral.`;
  return null;
}

function referralResponse(referral: DemoReferral): Schemas['ReferralResponse'] {
  const why = whyCantTriage(referral);
  return {
    id: referral.id,
    patientId: referral.patientId,
    patient: pathwayPatient(referral.patientId),
    patientFlags: NO_FLAGS,
    specialtyId: referral.specialtyId,
    specialtyName: specialty(referral.specialtyId)?.name ?? null,
    receivedOn: referral.receivedOn,
    practiceId: null,
    referrerPractice: referral.referrerPractice,
    referrerClinician: referral.referrerClinician,
    referrerOdsCode: null,
    requestedPriority: referral.requestedPriority,
    reason: referral.reason,
    status: referral.status,
    isOpen: referral.status === 'AwaitingTriage' || referral.status === 'InformationRequested',
    registeredAtUtc: referral.registeredAtUtc,
    registeredByUsername: usernameOf(referral.registeredByUserId),
    decidedAtUtc: referral.decidedAtUtc,
    decidedByUsername: usernameOf(referral.decidedByUserId),
    rejectionReason: referral.rejectionReason,
    decisionDetail: referral.decisionDetail,
    redirectedTo: referral.redirectedTo,
    redirectDetail: null,
    pathwayId: referral.pathwayId,
    version: referral.version,
    lastUpdated: referral.lastUpdated,
    letterCount: 0,
    canEdit: false,
    canClose: false,
    canAttachFiles: false,
    readOnlyReason: null,
    canTriage: why === null,
    whyCantTriage: why,
    clock: referralSummaryClock(referral),
    pauses: [],
    canPauseClock: false,
    canResumeClock: false,
    history: [...referral.history].sort((a, b) => (b.atUtc ?? '').localeCompare(a.atUtc ?? '')),
  };
}

function referralSummary(referral: DemoReferral): Schemas['ReferralSummaryResponse'] {
  const patient = db().patients.find((p) => p.id === referral.patientId);
  const clock = referralSummaryClock(referral);
  return {
    id: referral.id,
    patientId: referral.patientId,
    patientName: patientName(referral.patientId),
    patientNhsNumber: patient?.nhsNumber ?? null,
    patientDateOfBirth: patient?.dateOfBirth,
    specialtyId: referral.specialtyId,
    specialtyName: specialty(referral.specialtyId)?.name ?? null,
    receivedOn: referral.receivedOn,
    daysSinceReceived: daysBetween(referral.receivedOn, today()),
    requestedPriority: referral.requestedPriority,
    referrerPractice: referral.referrerPractice,
    status: referral.status,
    hasLetter: false,
    registeredAtUtc: referral.registeredAtUtc,
    decidedAtUtc: referral.decidedAtUtc,
    clock,
  };
}

function findReferral(id: string): DemoReferral {
  const referral = db().referrals.find((r) => r.id === id);
  if (!referral) refuse(notFound('The referral'));
  return referral;
}

/** A triage decision can be made: the person may triage this referral, and it hasn't changed since they read it. */
function forTriage(id: string, expectedVersion: number | undefined): DemoReferral {
  requirePermission('TriageReferrals');
  const referral = findReferral(id);
  const why = whyCantTriage(referral);
  if (why) refuse(problem(409, why));
  if (expectedVersion !== undefined && expectedVersion !== referral.version)
    refuse(problem(409, 'This referral changed meanwhile. Read it again before deciding.'));
  return referral;
}

function decide(referral: DemoReferral, status: DemoReferral['status'], event: Schemas['ReferralEventResponse']) {
  const viewer = me();
  const now = nowUtc();
  referral.status = status;
  referral.decidedAtUtc = now;
  referral.decidedByUserId = viewer.id;
  referral.version++;
  referral.lastUpdated = now;
  referral.history.push({id: newId(), atUtc: now, username: viewer.username, toStatus: status, ...event});
  if (referral.registeredByUserId !== viewer.id)
    db().notifications.push({
      id: newId(),
      userId: referral.registeredByUserId,
      kind: 'ReferralTriaged',
      title: 'Referral triaged',
      message: `${viewer.username} triaged a referral you registered.`,
      referralId: referral.id,
      createdAtUtc: now,
      isRead: false,
    });
}

export function registerReferrals() {
  on('get', '/api/referrals/search', ({query}) => {
    const viewer = me();
    const statuses = list(query, 'Statuses');
    const specialtyIds = list(query, 'SpecialtyIds');
    const mine = flag(query, 'Mine') === true;
    const search = query.get('Search');
    const items = db().referrals.filter(
      (r) =>
        (statuses.length === 0 || statuses.includes(r.status)) &&
        (specialtyIds.length === 0 || specialtyIds.includes(r.specialtyId)) &&
        (!mine || viewer.specialtyIds.includes(r.specialtyId)) &&
        (!query.get('PatientId') || r.patientId === query.get('PatientId')) &&
        (flag(query, 'BreachedOnly') !== true || referralSummaryClock(r).isBreached === true) &&
        (contains(patientName(r.patientId), search) || contains(db().patients.find((p) => p.id === r.patientId)?.nhsNumber, search)),
    );
    const order = {Low: 0, Medium: 1, High: 2, Urgent: 3};
    return paged(
      sorted(
        items.map(referralSummary),
        query,
        {
          daysWaited: (r) => r.clock?.daysWaited ?? 0,
          receivedOn: (r) => r.receivedOn ?? '',
          targetOn: (r) => r.clock?.targetOn ?? '',
          patient: (r) => (r.patientName ?? '').toLowerCase(),
          specialty: (r) => r.specialtyName ?? '',
          priority: (r) => order[(r.requestedPriority ?? 'Low') as keyof typeof order],
          status: (r) => r.status ?? '',
        },
        'receivedOn',
      ),
      query,
    );
  });

  on('get', '/api/referrals/awaiting-triage/count', () => {
    const viewer = me();
    const awaiting = db().referrals.filter((r) => r.status === 'AwaitingTriage');
    const inMine = viewer.specialtyIds.length > 0;
    return {
      count: (inMine ? awaiting.filter((r) => viewer.specialtyIds.includes(r.specialtyId)) : awaiting).length,
      inYourSpecialties: inMine,
    };
  });

  on('get', '/api/referrals/{id}', ({params}) => {
    me();
    return referralResponse(findReferral(params.id));
  });

  on('post', '/api/referrals/{id}/accept', ({params, body}) => {
    const referral = forTriage(params.id, body.expectedVersion);
    if (!body.templateId || !body.priority)
      return problem(400, 'Choose a template and a priority.');
    const template = db().templates.find((t) => t.id === body.templateId);
    if (!template || template.specialtyId !== referral.specialtyId)
      return problem(409, 'Choose one of this specialty’s templates.');
    const pathway = createPathway({
      patientId: referral.patientId,
      templateId: template.id!,
      title: body.title,
      priority: body.priority,
      notes: body.notes,
      assigneeUserId: body.assigneeUserId || null,
      referralId: referral.id,
      dueDate: daysFrom(TARGET_DAYS[referral.requestedPriority], referral.receivedOn),
    });
    referral.pathwayId = pathway.id;
    decide(referral, 'Accepted', {action: 'Accepted', templateName: template.name, pathwayId: pathway.id, priority: body.priority});
    return pathwayResponse(pathway);
  });

  on('post', '/api/referrals/{id}/reject', ({params, body}) => {
    const referral = forTriage(params.id, body.expectedVersion);
    if (!body.reason) return problem(400, 'Choose a reason.');
    referral.rejectionReason = body.reason;
    referral.decisionDetail = body.detail ?? null;
    decide(referral, 'Rejected', {action: 'Rejected', rejectionReason: body.reason});
    return referralResponse(referral);
  });

  on('post', '/api/referrals/{id}/redirect', ({params, body}) => {
    const referral = forTriage(params.id, body.expectedVersion);
    if (body.specialtyId) {
      const to = specialty(body.specialtyId);
      if (!to) return notFound('The specialty');
      const viewer = me();
      const now = nowUtc();
      referral.history.push({
        id: newId(),
        action: 'SpecialtyChanged',
        atUtc: now,
        username: viewer.username,
        toStatus: 'AwaitingTriage',
        specialtyName: to.name,
        previousSpecialtyName: specialty(referral.specialtyId)?.name ?? null,
      });
      referral.specialtyId = body.specialtyId;
      referral.version++;
      referral.lastUpdated = now;
      return referralResponse(referral);
    }
    referral.redirectedTo = body.elsewhere ?? 'Elsewhere';
    referral.decisionDetail = body.detail ?? null;
    decide(referral, 'RedirectedElsewhere', {action: 'RedirectedElsewhere'});
    return referralResponse(referral);
  });
}
