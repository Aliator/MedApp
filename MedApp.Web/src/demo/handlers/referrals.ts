import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {TARGET_DAYS} from '../seed';
import type {DemoClockPause, DemoReferralExtra} from '../slices/lab';
import type {DemoReferral} from '../state';
import {db} from '../store';
import {daysFrom, nowUtc, today, ukDateOf} from '../time';
import {can, contains, flag, list, me, paged, patientName, requirePermission, sorted, teamIdsOf} from './common';
import {ensureVersion, optionalText, patientBanner, patientFlags, whyRecordCantChange} from './labCommon';
import {createPathway, pathwayResponse} from './pathways';

/**
 * Referrals, triage and the waiting-time clock, a simplified copy of the API's `ReferralWorkflow` and `WaitingClock`.
 * The clock runs from the day received (plus any pauses) against the team's target for the priority (the
 * triager's, once accepted), and stops for good when the template's clock-stop stage is finished, the pathway is
 * discharged or cancelled, or the referral is closed without a pathway. Pauses are patient-initiated, only once
 * accepted, and move the pathway's target date. Nothing is scored, suggested or ordered by clinical content; the
 * history holds no free text. Simplified: no letters or files, and the clock's stop isn't written to the history.
 */

type Status = DemoReferral['status'];
type Priority = DemoReferral['requestedPriority'];

const OPEN: Status[] = ['AwaitingTriage', 'InformationRequested'];
const isOpenReferral = (referral: DemoReferral) => OPEN.includes(referral.status);
const PRIORITIES: Priority[] = ['Low', 'Medium', 'High', 'Urgent'];
const PAUSE_REASONS = ['PatientChoseToWait', 'PatientUnavailable', 'Other'];

const DESCRIBE: Record<Status, string> = {
  AwaitingTriage: 'awaiting triage',
  InformationRequested: 'waiting for more information',
  Accepted: 'accepted',
  Rejected: 'rejected',
  RedirectedElsewhere: 'redirected elsewhere',
  Withdrawn: 'withdrawn',
  EnteredInError: 'entered in error',
};

const CHANGED_MEANWHILE = 'Someone else changed this referral while you had it open. Reload it to see their change, then try again.';
const LONG_DATE = new Intl.DateTimeFormat('en-GB', {day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC'});
const longDate = (date: string) => LONG_DATE.format(new Date(`${date}T00:00:00Z`));

const daysBetween = (from: string, to: string) => Math.round((Date.parse(to) - Date.parse(from)) / 86400_000);

const team = (id: string) => db().teams.find((s) => s.id === id);
const extraOf = (referral: DemoReferral): DemoReferralExtra =>
  db().referralExtras[referral.id] ?? {practiceId: null, referrerOdsCode: null, redirectDetail: null};
const pausesOf = (referralId: string) =>
  db()
    .clockPauses.filter((p) => p.referralId === referralId)
    .sort((a, b) => a.pausedAtUtc.localeCompare(b.pausedAtUtc));
const pathwayOf = (referral: DemoReferral) => (referral.pathwayId ? db().pathways.find((p) => p.id === referral.pathwayId) : undefined);

/** When and why the clock stopped for good, or null while it runs or is paused. */
function stopOf(referral: DemoReferral): {on: string; reason: string} | null {
  if (['Rejected', 'RedirectedElsewhere', 'Withdrawn', 'EnteredInError'].includes(referral.status))
    return {on: ukDateOf(referral.decidedAtUtc ?? referral.lastUpdated), reason: referral.status};
  const pathway = pathwayOf(referral);
  if (referral.status !== 'Accepted' || !pathway) return null;
  const stops: {on: string; reason: string}[] = [];
  const stopOrder = db().templates.find((t) => t.id === pathway.templateId)?.clockStopStageOrder;
  const stopStage = pathway.stages.find((s) => s.stageOrder === stopOrder);
  if (stopStage?.completedAtUtc) stops.push({on: ukDateOf(stopStage.completedAtUtc), reason: 'StopStageFinished'});
  for (const change of pathway.statusChanges)
    if (change.to === 'Completed' || change.to === 'Cancelled')
      stops.push({on: ukDateOf(change.atUtc), reason: change.to === 'Completed' ? 'PathwayDischarged' : 'PathwayCancelled'});
  return stops.sort((a, b) => a.on.localeCompare(b.on))[0] ?? null;
}

/** The referral's waiting-time clock (`WaitingClock`): whole UK days, against the team's target for the priority. */
export function referralSummaryClock(referral: DemoReferral): Schemas['WaitingClockResponse'] {
  const pathway = pathwayOf(referral);
  const priority = referral.status === 'Accepted' && pathway ? pathway.priority : referral.requestedPriority;
  const targetDays = TARGET_DAYS[priority];
  const stop = stopOf(referral);
  const pauses = pausesOf(referral.id);
  // A pause still open when the clock stopped ends with it.
  const ended = (pause: DemoClockPause) => pause.resumedOn ?? stop?.on ?? null;
  const pausedDays = pauses.reduce((sum, p) => sum + (ended(p) ? Math.max(0, daysBetween(p.pausedOn, ended(p)!)) : 0), 0);
  const open = stop ? undefined : pauses.find((p) => !p.resumedOn);
  const waitStartOn = daysFrom(pausedDays, referral.receivedOn);
  const targetOn = daysFrom(targetDays, waitStartOn);
  const endDay = stop?.on ?? open?.pausedOn ?? today();
  return {
    state: stop ? 'Stopped' : open ? 'Paused' : 'Running',
    targetDays,
    waitStartOn,
    targetOn,
    daysWaited: Math.max(0, daysBetween(waitStartOn, endDay)),
    pausedDays,
    isBreached: endDay > targetOn,
    pausedSinceOn: open?.pausedOn ?? null,
    pauseReviewOn: open?.reviewOn ?? null,
    isReviewOverdue: open ? open.reviewOn < today() : false,
    stoppedOn: stop?.on ?? null,
    stopReason: stop?.reason ?? null,
  };
}

function pauseResponses(referralId: string): Schemas['ClockPauseResponse'][] {
  return pausesOf(referralId).map((p) => ({
    id: p.id,
    pausedOn: p.pausedOn,
    pausedAtUtc: p.pausedAtUtc,
    pausedByUsername: usernameOf(p.pausedByUserId),
    reason: p.reason,
    detail: p.detail,
    reviewOn: p.reviewOn,
    resumedOn: p.resumedOn,
    resumedByUsername: usernameOf(p.resumedByUserId),
    days: Math.max(0, daysBetween(p.pausedOn, p.resumedOn ?? today())),
  }));
}

/** Pausing needs WorkPathways, an accepted referral and a running clock; never once anonymised. */
const canPause = (referral: DemoReferral) =>
  can('WorkPathways') &&
  referral.status === 'Accepted' &&
  referralSummaryClock(referral).state === 'Running' &&
  !patientFlags(referral.patientId).isAnonymised;

/** Resuming needs WorkPathways and a paused clock, and is refused while the record is restricted. */
const canResume = (referral: DemoReferral) =>
  can('WorkPathways') && referralSummaryClock(referral).state === 'Paused' && whyRecordCantChange(referral.patientId) === null;

/**
 * The pathway's referral and its waiting-time card (`PathwayReferralResponse`): what the pathway page shows, and what
 * pausing and resuming return. `pathwayResponse` should use it for its `referral`.
 */
export function pathwayReferralResponse(referral: DemoReferral): Schemas['PathwayReferralResponse'] {
  return {
    id: referral.id,
    receivedOn: referral.receivedOn,
    teamName: team(referral.teamId)?.name ?? null,
    version: referral.version,
    clock: referralSummaryClock(referral),
    pauses: pauseResponses(referral.id),
    canPauseClock: canPause(referral),
    canResumeClock: canResume(referral),
  };
}

const HOW: Partial<Record<Status, string>> = {
  Accepted: 'accepted it into a pathway',
  Rejected: 'rejected it',
  RedirectedElsewhere: 'redirected it elsewhere',
  Withdrawn: 'withdrew it',
  EnteredInError: 'marked it entered in error',
};

/** Why the signed-in person can't triage this referral, or null if they can (`ReferralWorkflow.WhyCantTriage`). */
function whyCantTriage(referral: DemoReferral): string | null {
  if (!isOpenReferral(referral)) {
    const when = referral.decidedAtUtc ? ` on ${longDate(ukDateOf(referral.decidedAtUtc))}` : '';
    return `This referral can't be triaged now: ${usernameOf(referral.decidedByUserId) ?? 'someone'} ${HOW[referral.status] ?? `marked it ${DESCRIBE[referral.status]}`}${when}. Reload it to see the decision.`;
  }
  if (!can('TriageReferrals'))
    return 'Only clinicians who can triage referrals (their roles give them the Triage referrals permission) can accept, reject or redirect a referral.';
  const name = team(referral.teamId)?.name ?? 'its team';
  if (!teamIdsOf(me()).includes(referral.teamId))
    return `Only members of ${name} can triage this referral. If you should, ask an admin to add you to ${name}.`;
  return whyRecordCantChange(referral.patientId);
}

/** Why it can't be edited, withdrawn or marked entered in error now: closed, or the record can't change. */
function whyCantEdit(referral: DemoReferral): string | null {
  if (!isOpenReferral(referral))
    return `This referral has been ${DESCRIBE[referral.status]}, so it can't be changed. Only an open referral (awaiting triage, or waiting for more information) can be edited, withdrawn or marked entered in error.`;
  return whyRecordCantChange(referral.patientId);
}

/** Withdrawing and entered in error stay allowed while the record is restricted (so care can be stopped safely). */
function whyCantClose(referral: DemoReferral): string | null {
  if (!isOpenReferral(referral)) return whyCantEdit(referral);
  return patientFlags(referral.patientId).isAnonymised ? "This patient's record has been anonymised, so it can't be changed." : null;
}

function referralResponse(referral: DemoReferral): Schemas['ReferralResponse'] {
  const why = whyCantTriage(referral);
  const extra = extraOf(referral);
  const card = pathwayReferralResponse(referral);
  return {
    id: referral.id,
    patientId: referral.patientId,
    patient: patientBanner(referral.patientId),
    patientFlags: patientFlags(referral.patientId),
    teamId: referral.teamId,
    teamName: team(referral.teamId)?.name ?? null,
    receivedOn: referral.receivedOn,
    practiceId: extra.practiceId,
    referrerPractice: referral.referrerPractice,
    referrerClinician: referral.referrerClinician,
    referrerOdsCode: extra.referrerOdsCode,
    requestedPriority: referral.requestedPriority,
    reason: referral.reason,
    status: referral.status,
    isOpen: isOpenReferral(referral),
    registeredAtUtc: referral.registeredAtUtc,
    registeredByUsername: usernameOf(referral.registeredByUserId),
    decidedAtUtc: referral.decidedAtUtc,
    decidedByUsername: usernameOf(referral.decidedByUserId),
    rejectionReason: referral.rejectionReason,
    decisionDetail: referral.decisionDetail,
    redirectedTo: referral.redirectedTo,
    redirectDetail: extra.redirectDetail,
    pathwayId: referral.pathwayId,
    version: referral.version,
    lastUpdated: referral.lastUpdated,
    letterCount: 0,
    canEdit: can('RegisterReferrals') && whyCantEdit(referral) === null,
    canClose: can('RegisterReferrals') && whyCantClose(referral) === null,
    canAttachFiles: false,
    readOnlyReason: whyCantEdit(referral),
    canTriage: why === null,
    whyCantTriage: why,
    clock: card.clock,
    pauses: card.pauses,
    canPauseClock: card.canPauseClock,
    canResumeClock: card.canResumeClock,
    history: [...referral.history].sort((a, b) => (b.atUtc ?? '').localeCompare(a.atUtc ?? '')),
  };
}

function referralSummary(referral: DemoReferral): Schemas['ReferralSummaryResponse'] {
  const patient = db().patients.find((p) => p.id === referral.patientId);
  return {
    id: referral.id,
    patientId: referral.patientId,
    patientName: patientName(referral.patientId),
    patientNhsNumber: patient?.nhsNumber ?? null,
    patientDateOfBirth: patient?.dateOfBirth,
    teamId: referral.teamId,
    teamName: team(referral.teamId)?.name ?? null,
    receivedOn: referral.receivedOn,
    daysSinceReceived: daysBetween(referral.receivedOn, today()),
    requestedPriority: referral.requestedPriority,
    referrerPractice: referral.referrerPractice,
    status: referral.status,
    hasLetter: false,
    registeredAtUtc: referral.registeredAtUtc,
    decidedAtUtc: referral.decidedAtUtc,
    clock: referralSummaryClock(referral),
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
  ensureVersion(expectedVersion, referral.version, CHANGED_MEANWHILE);
  return referral;
}

/** A change to the referral: its version moves on and its history gains an entry (structured values only). */
function record(referral: DemoReferral, event: Omit<Schemas['ReferralEventResponse'], 'id' | 'atUtc' | 'username'>) {
  const now = nowUtc();
  referral.version++;
  referral.lastUpdated = now;
  referral.history.push({id: newId(), atUtc: now, username: me().username, toStatus: referral.status, ...event});
}

function decide(referral: DemoReferral, status: Status, event: Schemas['ReferralEventResponse'], notifies = true) {
  const viewer = me();
  const now = nowUtc();
  referral.status = status;
  referral.decidedAtUtc = now;
  referral.decidedByUserId = viewer.id;
  record(referral, {...event, toStatus: status});
  if (notifies && referral.registeredByUserId !== viewer.id)
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

type DetailsBody = {
  teamId?: string;
  receivedOn?: string;
  practiceId?: string | null;
  referrerPractice?: string | null;
  referrerClinician?: string | null;
  referrerOdsCode?: string | null;
  requestedPriority?: string | null;
  reason?: string | null;
};

/** What's typed from the letter, checked as `ReferralRules` does: a 400 naming each field that's wrong. */
function checkDetails(body: DetailsBody) {
  const errors: Record<string, string[]> = {};
  const to = body.teamId ? team(body.teamId) : undefined;
  if (!to || to.isArchived) errors.teamId = ['Choose a team that isn’t archived.'];
  const receivedOn = body.receivedOn?.slice(0, 10) ?? '';
  if (!/^\d{4}-\d{2}-\d{2}$/.test(receivedOn)) errors.receivedOn = ['Give the date the referral was received.'];
  else if (receivedOn > today()) errors.receivedOn = ["The date received can't be in the future."];
  else if (receivedOn < daysFrom(-365)) errors.receivedOn = ['The date received can be at most 365 days ago.'];
  const practice = body.referrerPractice?.trim() ?? '';
  if (!practice && !body.practiceId) errors.referrerPractice = ['Give the referring practice.'];
  else if (practice.length > 200) errors.referrerPractice = ['The practice must be at most 200 characters.'];
  if ((body.referrerClinician?.trim().length ?? 0) > 200) errors.referrerClinician = ['The clinician must be at most 200 characters.'];
  const ods = body.referrerOdsCode?.replace(/\s/g, '').toUpperCase() || null;
  if (ods && !/^[A-Z0-9]{3,10}$/.test(ods)) errors.referrerOdsCode = ['An ODS code is 3 to 10 letters and digits.'];
  if (!PRIORITIES.includes(body.requestedPriority as Priority)) errors.requestedPriority = ['Choose the priority asked for.'];
  const reason = body.reason?.trim() ?? '';
  if (!reason) errors.reason = ['Give the reason for the referral.'];
  else if (reason.length > 2000) errors.reason = ['The reason must be at most 2,000 characters.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  return {
    teamId: to!.id!,
    receivedOn,
    practiceId: body.practiceId || null,
    referrerPractice: practice,
    referrerClinician: body.referrerClinician?.trim() || '',
    referrerOdsCode: ods,
    requestedPriority: body.requestedPriority as Priority,
    reason,
  };
}

/** Withdrawing or marking entered in error: open only, a detail required, never once anonymised. */
function closeReferral(id: string, body: {detail?: string | null; expectedVersion?: number}, status: 'Withdrawn' | 'EnteredInError') {
  requirePermission('RegisterReferrals');
  const referral = findReferral(id);
  const why = whyCantClose(referral);
  if (why) refuse(problem(409, why));
  ensureVersion(body.expectedVersion, referral.version, CHANGED_MEANWHILE);
  const detail = optionalText(body.detail, 500, 'detail', 'The detail');
  if (!detail) refuse(invalid({detail: [status === 'Withdrawn' ? 'Say why it was withdrawn.' : 'Say what was wrong.']}));
  referral.decisionDetail = detail;
  decide(referral, status, {action: status}, false);
  return referralResponse(referral);
}

export function registerReferrals() {
  on('get', '/api/referrals/search', ({query}) => {
    const viewer = me();
    const statuses = list(query, 'Statuses');
    const teamIds = list(query, 'TeamIds');
    const mine = flag(query, 'Mine') === true;
    const paused = flag(query, 'Paused');
    const stopped = flag(query, 'ClockStopped');
    const search = query.get('Search');
    const items = db().referrals.filter((r) => {
      const clock = referralSummaryClock(r);
      return (
        (statuses.length === 0 || statuses.includes(r.status)) &&
        (teamIds.length === 0 || teamIds.includes(r.teamId)) &&
        (!mine || teamIdsOf(viewer).length === 0 || teamIdsOf(viewer).includes(r.teamId)) &&
        (!query.get('PatientId') || r.patientId === query.get('PatientId')) &&
        (flag(query, 'BreachedOnly') !== true || clock.isBreached === true) &&
        (paused === undefined || paused === (clock.state === 'Paused')) &&
        (stopped === undefined || stopped === (clock.state === 'Stopped')) &&
        (contains(patientName(r.patientId), search) || contains(db().patients.find((p) => p.id === r.patientId)?.nhsNumber, search))
      );
    });
    const order = {Low: 0, Medium: 1, High: 2, Urgent: 3};
    return paged(
      sorted(
        items.map(referralSummary),
        query,
        {
          daysWaited: (r) => r.clock?.daysWaited ?? 0,
          receivedOn: (r) => r.receivedOn ?? '',
          targetOn: (r) => r.clock?.targetOn ?? '',
          // Paused ones by review date; the rest last.
          reviewOn: (r) => r.clock?.pauseReviewOn ?? '9999-12-31',
          registered: (r) => r.registeredAtUtc ?? '',
          patient: (r) => (r.patientName ?? '').toLowerCase(),
          team: (r) => r.teamName ?? '',
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
    const mine = teamIdsOf(viewer);
    const inMine = mine.length > 0;
    return {
      count: (inMine ? awaiting.filter((r) => mine.includes(r.teamId)) : awaiting).length,
      inYourTeams: inMine,
    };
  });

  on('get', '/api/referrals/{id}', ({params}) => {
    me();
    return referralResponse(findReferral(params.id));
  });

  on('post', '/api/referrals', ({body}) => {
    const viewer = me();
    requirePermission('RegisterReferrals');
    const patient = db().patients.find((p) => p.id === body.patientId);
    if (!patient) return notFound('The patient');
    const details = checkDetails(body);
    if (patient.isAnonymised)
      return problem(
        409,
        "This patient's record has been anonymised, so a referral can't be registered for them. Check you have the right patient; if you have, give the referral letter to an admin, so it isn't lost.",
      );
    if (patient.isRestricted)
      return problem(
        409,
        "This patient's record is restricted, so a referral can't be registered for them until an admin lifts the restriction. Give the referral letter to an admin now, so it isn't lost.",
      );
    // Duplicates are shown, never merged: the patient's open referrals to the same team, by date and status only.
    const open = db()
      .referrals.filter(
        (r) =>
          r.patientId === patient.id &&
          r.teamId === details.teamId &&
          (isOpenReferral(r) || (r.status === 'Accepted' && referralSummaryClock(r).state !== 'Stopped')),
      )
      .sort((a, b) => a.receivedOn.localeCompare(b.receivedOn));
    if (open.length > 0 && body.confirmPossibleDuplicate !== true) {
      const name = team(details.teamId)?.name ?? 'team';
      const count = open.length === 1 ? `an open ${name} referral` : `${open.length} open ${name} referrals`;
      const each = open.map((r) => `received ${longDate(r.receivedOn)} (${DESCRIBE[r.status]})`).join('; ');
      return invalid({
        confirmPossibleDuplicate: [`This patient already has ${count}: ${each}. If this is a different referral, confirm it to register it anyway.`],
      });
    }
    const now = nowUtc();
    const referral: DemoReferral = {
      id: newId(),
      patientId: patient.id!,
      teamId: details.teamId,
      receivedOn: details.receivedOn,
      referrerPractice: details.referrerPractice,
      referrerClinician: details.referrerClinician,
      requestedPriority: details.requestedPriority,
      reason: details.reason,
      status: 'AwaitingTriage',
      registeredAtUtc: now,
      registeredByUserId: viewer.id,
      decidedAtUtc: null,
      decidedByUserId: null,
      rejectionReason: null,
      decisionDetail: null,
      redirectedTo: null,
      pathwayId: null,
      version: 1,
      lastUpdated: now,
      history: [
        {
          id: newId(),
          action: 'Registered',
          atUtc: now,
          username: viewer.username,
          toStatus: 'AwaitingTriage',
          teamName: team(details.teamId)?.name ?? null,
          requestedPriority: details.requestedPriority,
        },
      ],
    };
    db().referrals.push(referral);
    db().referralExtras[referral.id] = {practiceId: details.practiceId, referrerOdsCode: details.referrerOdsCode, redirectDetail: null};
    const to = team(details.teamId);
    if (to) to.referralCount = (to.referralCount ?? 0) + 1;
    return referralResponse(referral);
  });

  on('put', '/api/referrals/{id}', ({params, body}) => {
    requirePermission('RegisterReferrals');
    const referral = findReferral(params.id);
    const why = whyCantEdit(referral);
    if (why) return problem(409, why);
    ensureVersion(body.expectedVersion, referral.version, CHANGED_MEANWHILE);
    const details = checkDetails(body);
    const extra = extraOf(referral);
    const changed: string[] = [];
    const previousTeam = team(referral.teamId)?.name ?? null;
    if (details.teamId !== referral.teamId) changed.push('specialty');
    if (details.receivedOn !== referral.receivedOn) changed.push('receivedOn');
    if (details.referrerPractice !== referral.referrerPractice) changed.push('referrerPractice');
    if (details.practiceId !== extra.practiceId) changed.push('directoryPractice');
    if (details.referrerClinician !== referral.referrerClinician) changed.push('referrerClinician');
    if (details.referrerOdsCode !== extra.referrerOdsCode) changed.push('referrerOdsCode');
    if (details.requestedPriority !== referral.requestedPriority) changed.push('requestedPriority');
    if (details.reason !== referral.reason) changed.push('reason');
    if (changed.length === 0) return referralResponse(referral);
    Object.assign(referral, {
      teamId: details.teamId,
      receivedOn: details.receivedOn,
      referrerPractice: details.referrerPractice,
      referrerClinician: details.referrerClinician,
      requestedPriority: details.requestedPriority,
      reason: details.reason,
    });
    db().referralExtras[referral.id] = {...extra, practiceId: details.practiceId, referrerOdsCode: details.referrerOdsCode};
    record(referral, {
      action: 'DetailsEdited',
      changedFields: changed,
      ...(changed.includes('specialty') ? {teamName: team(details.teamId)?.name ?? null, previousTeamName: previousTeam} : {}),
    });
    return referralResponse(referral);
  });

  on('post', '/api/referrals/{id}/withdraw', ({params, body}) => closeReferral(params.id, body, 'Withdrawn'));
  on('post', '/api/referrals/{id}/entered-in-error', ({params, body}) => closeReferral(params.id, body, 'EnteredInError'));

  on('post', '/api/referrals/{id}/ask-for-information', ({params, body}) => {
    const referral = forTriage(params.id, body.expectedVersion);
    if (referral.status === 'InformationRequested')
      return problem(409, 'More information has been asked for already. When it comes, record that it was received.');
    referral.status = 'InformationRequested';
    record(referral, {action: 'InformationRequested'});
    return referralResponse(referral);
  });

  on('post', '/api/referrals/{id}/information-received', ({params, body}) => {
    const referral = forTriage(params.id, body.expectedVersion);
    if (referral.status !== 'InformationRequested')
      return problem(409, "No information was asked for on this referral, so there's nothing to record as received.");
    referral.status = 'AwaitingTriage';
    record(referral, {action: 'InformationReceived'});
    return referralResponse(referral);
  });

  on('post', '/api/referrals/{id}/accept', ({params, body}) => {
    const referral = forTriage(params.id, body.expectedVersion);
    if (!body.templateId || !body.priority) return problem(400, 'Choose a template and a priority.');
    const template = db().templates.find((t) => t.id === body.templateId);
    const name = team(referral.teamId)?.name ?? 'this team';
    if (!template || template.teamId !== referral.teamId)
      return invalid({
        templateId: [
          `The '${template?.name ?? 'chosen'}' template isn't one of ${name}'s. To use another team's template, redirect the referral to that team first.`,
        ],
      });
    const priority = PRIORITIES.includes(body.priority as Priority) ? (body.priority as Priority) : referral.requestedPriority;
    const pathway = createPathway({
      patientId: referral.patientId,
      templateId: template.id!,
      title: body.title,
      priority,
      notes: body.notes,
      assigneeUserId: body.assigneeUserId || null,
      referralId: referral.id,
      dueDate: daysFrom(TARGET_DAYS[priority], daysFrom(referralSummaryClock(referral).pausedDays ?? 0, referral.receivedOn)),
    });
    referral.pathwayId = pathway.id;
    decide(referral, 'Accepted', {action: 'Accepted', templateName: template.name, pathwayId: pathway.id, priority});
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
    if (body.teamId) {
      const to = team(body.teamId);
      if (!to) return notFound('The team');
      if (to.id === referral.teamId) return invalid({teamId: ['The referral is already in that team. Choose another one.']});
      if (to.isArchived) return problem(409, `${to.name} is archived, so referrals can't be redirected to it.`);
      const previous = team(referral.teamId)?.name ?? null;
      referral.teamId = body.teamId;
      db().referralExtras[referral.id] = {...extraOf(referral), redirectDetail: body.detail?.trim() || null};
      record(referral, {action: 'TeamChanged', teamName: to.name, previousTeamName: previous});
      return referralResponse(referral);
    }
    referral.redirectedTo = body.elsewhere ?? 'Elsewhere';
    referral.decisionDetail = body.detail ?? null;
    decide(referral, 'RedirectedElsewhere', {action: 'RedirectedElsewhere'});
    return referralResponse(referral);
  });

  on('post', '/api/referrals/{id}/clock/pause', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const referral = findReferral(params.id);
    ensureVersion(body.expectedVersion, referral.version, CHANGED_MEANWHILE);
    if (patientFlags(referral.patientId).isAnonymised) return problem(409, "This patient's record has been anonymised, so it can't be changed.");
    if (referral.status !== 'Accepted')
      return problem(409, 'The waiting-time clock can only be paused once the referral has been accepted: before triage, it always runs.');
    const clock = referralSummaryClock(referral);
    if (clock.state === 'Stopped') return problem(409, "The waiting-time clock has stopped, so it can't be paused or resumed.");
    if (clock.state === 'Paused')
      return problem(409, 'The waiting-time clock is already paused. Resume it first, then pause it again with the new review date.');
    if (!PAUSE_REASONS.includes(body.reason ?? '')) return invalid({reason: ['Choose why the patient’s wait is paused.']});
    const detail = optionalText(body.detail, 500, 'detail', 'The detail');
    if (body.reason === 'Other' && !detail) return invalid({detail: ['Say why.']});
    const reviewOn = body.reviewOn?.slice(0, 10) ?? '';
    if (!reviewOn || reviewOn <= today() || reviewOn > daysFrom(90))
      return invalid({reviewOn: ['The review date must be from tomorrow to 90 days ahead.']});
    db().clockPauses.push({
      id: newId(),
      referralId: referral.id,
      pausedOn: today(),
      pausedAtUtc: nowUtc(),
      pausedByUserId: viewer.id,
      reason: body.reason!,
      detail,
      reviewOn,
      resumedOn: null,
      resumedByUserId: null,
    });
    record(referral, {action: 'ClockPaused', pauseReason: body.reason, reviewOn});
    // While paused, the pathway's target date is the pause's review date.
    const pathway = pathwayOf(referral);
    if (pathway) {
      pathway.dueDate = reviewOn;
      pathway.lastUpdated = nowUtc();
    }
    return pathwayReferralResponse(referral);
  });

  on('post', '/api/referrals/{id}/clock/resume', ({params, body}) => {
    const viewer = me();
    requirePermission('WorkPathways');
    const referral = findReferral(params.id);
    ensureVersion(body.expectedVersion, referral.version, CHANGED_MEANWHILE);
    const why = whyRecordCantChange(referral.patientId);
    if (why) return problem(409, why);
    const open = referralSummaryClock(referral).state === 'Paused' ? pausesOf(referral.id).find((p) => !p.resumedOn) : undefined;
    if (!open) return problem(409, "The waiting-time clock isn't paused.");
    open.resumedOn = today();
    open.resumedByUserId = viewer.id;
    record(referral, {action: 'ClockResumed'});
    // Running again, the pathway's target date is the clock's.
    const pathway = pathwayOf(referral);
    if (pathway) {
      pathway.dueDate = referralSummaryClock(referral).targetOn ?? pathway.dueDate;
      pathway.lastUpdated = nowUtc();
    }
    return pathwayReferralResponse(referral);
  });
}
