import type {Schemas} from '../../api/client';
import {personById, usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import type {DemoResult, ResultStatus} from '../slices/lab';
import {db} from '../store';
import {nowUtc} from '../time';
import {can, contains, me, paged, patientName, patientSortName, requirePermission, sortedBy} from './common';
import {addDaysUtc, ensureVersion, notify, optionalText, patientBanner, patientFlags, wholeDaysBetween} from './labCommon';

/**
 * Results: a specimen's report, or a report from elsewhere, awaiting sign-off by a named clinician (a holder of
 * SignOffResults), a simplified copy of the API's `ResultWorkflow`. **MedApp never reads, interprets or flags a
 * result**: lists are ordered by dates only, the clinician chooses what was done from a fixed list with none
 * pre-selected, and sign-off records who and when. Simplified: no files, so a result is never recorded from an
 * attached file in the light demo (the seed has one from elsewhere).
 */

const AWAITING: ResultStatus[] = ['AwaitingSignOff', 'Seen'];
export const isAwaitingResult = (result: DemoResult) => AWAITING.includes(result.status);

const DESCRIBE: Record<ResultStatus, string> = {
  AwaitingSignOff: 'awaiting sign-off',
  Seen: 'seen',
  SignedOff: 'signed off',
  EnteredInError: 'entered in error',
};

const ACTIONS = ['NoFurtherAction', 'PatientInformed', 'AppointmentArranged', 'ForMdtDiscussion', 'ReferredOn', 'Other'];
const CHANGED_MEANWHILE = 'Someone else changed this result while you had it open. Reload it to see their change, then try again.';
const ANONYMISED = "This patient's record has been anonymised, so it can't be changed.";

/** A result whose responsible clinician no longer holds SignOffResults (or has gone) needs a clinician. */
const needsClinician = (result: DemoResult) =>
  isAwaitingResult(result) && !(personById(result.responsibleUserId)?.permissions.includes('SignOffResults') ?? false);

const specimenOf = (result: DemoResult) => (result.specimenId ? db().specimens.find((s) => s.id === result.specimenId) : undefined);
const pathwayOf = (result: DemoResult) => (result.pathwayId ? db().pathways.find((p) => p.id === result.pathwayId) : undefined);

/** `ResultWorkflow.WhyCantProgress`: only a result awaiting sign-off moves on (a restricted record's still can). */
function whyCantProgress(result: DemoResult): string | null {
  if (patientFlags(result.patientId).isAnonymised) return ANONYMISED;
  return isAwaitingResult(result)
    ? null
    : `This result has been ${DESCRIBE[result.status]}, so it can't be changed. Anything later goes in a pathway note.`;
}

/** A holder of CorrectRecords at any time, or whoever reported it within 24 hours, while they hold what let them. */
const mayMarkEnteredInError = (result: DemoResult) =>
  can('CorrectRecords') ||
  (result.reportedByUserId === me().id &&
    can(result.source === 'Specimen' ? 'ProcessSpecimens' : 'WorkPathways') &&
    Date.parse(nowUtc()) < Date.parse(addDaysUtc(result.reportedAtUtc, 1)));

function resultSummary(result: DemoResult): Schemas['ResultSummaryResponse'] {
  const patient = db().patients.find((p) => p.id === result.patientId);
  return {
    id: result.id,
    patientId: result.patientId,
    patientName: patientName(result.patientId),
    patientNhsNumber: patient?.nhsNumber ?? null,
    patientDateOfBirth: patient?.dateOfBirth,
    pathwayId: result.pathwayId,
    pathwayTitle: pathwayOf(result)?.title ?? null,
    source: result.source,
    title: result.title,
    specimenNumber: specimenOf(result)?.number ?? null,
    reportedAtUtc: result.reportedAtUtc,
    ageDays: wholeDaysBetween(result.reportedAtUtc, nowUtc()),
    responsibleUsername: usernameOf(result.responsibleUserId),
    needsClinician: needsClinician(result),
    status: result.status,
    isAwaitingSignOff: isAwaitingResult(result),
    signedOffAtUtc: result.signedOffAtUtc,
    signedOffByUsername: usernameOf(result.signedOffByUserId),
    action: result.action,
  };
}

function resultResponse(result: DemoResult): Schemas['ResultResponse'] {
  const flags = patientFlags(result.patientId);
  const signs = can('SignOffResults');
  const moves = whyCantProgress(result) === null;
  const specimen = specimenOf(result);
  return {
    id: result.id,
    patientId: result.patientId,
    patient: patientBanner(result.patientId),
    patientFlags: flags,
    pathwayId: result.pathwayId,
    pathwayTitle: pathwayOf(result)?.title ?? null,
    specimenId: result.specimenId,
    specimenNumber: specimen?.number ?? null,
    specimenTypeName: specimen ? (db().specimenTypes.find((t) => t.id === specimen.specimenTypeId)?.name ?? null) : null,
    source: result.source,
    title: result.title,
    reportAttachmentId: result.reportAttachmentId,
    reportFileName: result.reportFileName,
    reportHasThumbnail: false,
    reportedAtUtc: result.reportedAtUtc,
    reportedByUsername: usernameOf(result.reportedByUserId),
    ageDays: wholeDaysBetween(result.reportedAtUtc, nowUtc()),
    responsibleUserId: result.responsibleUserId,
    responsibleUsername: usernameOf(result.responsibleUserId),
    needsClinician: needsClinician(result),
    status: result.status,
    isAwaitingSignOff: isAwaitingResult(result),
    seenAtUtc: result.seenAtUtc,
    seenByUsername: usernameOf(result.seenByUserId),
    signedOffAtUtc: result.signedOffAtUtc,
    signedOffByUsername: usernameOf(result.signedOffByUserId),
    action: result.action,
    actionDetail: result.actionDetail,
    enteredInErrorAtUtc: result.enteredInErrorAtUtc,
    enteredInErrorByUsername: usernameOf(result.enteredInErrorByUserId),
    errorDetail: result.errorDetail,
    version: result.version,
    canMarkSeen: signs && moves && result.status === 'AwaitingSignOff',
    canSignOff: signs && moves,
    canReassign: signs && moves,
    canMarkEnteredInError: result.status !== 'EnteredInError' && !flags.isAnonymised && mayMarkEnteredInError(result),
    readOnlyReason: whyCantProgress(result),
  };
}

function findResult(id: string): DemoResult {
  const result = db().results.find((r) => r.id === id);
  if (!result) refuse(notFound('The result'));
  return result;
}

/** A result that can move on (seen, signed off, reassigned): from the version the page showed, and awaiting sign-off. */
function forProgress(id: string, expectedVersion: number | undefined): DemoResult {
  requirePermission('SignOffResults');
  const result = findResult(id);
  ensureVersion(expectedVersion, result.version, CHANGED_MEANWHILE);
  const why = whyCantProgress(result);
  if (why) refuse(problem(409, why));
  return result;
}

/** The statuses in the API's stored order, which its status sort follows. */
const RESULT_STATUS_ORDER: ResultStatus[] = ['AwaitingSignOff', 'Seen', 'SignedOff', 'EnteredInError'];

/** Each view's results and order: dates only, never anything in the report. */
const VIEWS: Record<string, {match: (r: DemoResult, viewerId: string) => boolean; key: (r: DemoResult) => string; newestFirst?: boolean}> = {
  AwaitingMine: {match: (r, viewerId) => isAwaitingResult(r) && r.responsibleUserId === viewerId, key: (r) => r.reportedAtUtc},
  Awaiting: {match: isAwaitingResult, key: (r) => r.reportedAtUtc},
  NeedsClinician: {match: needsClinician, key: (r) => r.reportedAtUtc},
  SignedOff: {match: (r) => r.status === 'SignedOff', key: (r) => r.signedOffAtUtc ?? '', newestFirst: true},
  All: {match: () => true, key: (r) => r.reportedAtUtc, newestFirst: true},
};

export function registerResults() {
  on('get', '/api/results/search', ({query}) => {
    const viewer = me();
    requirePermission('ViewPatients');
    const view = VIEWS[query.get('View') ?? ''] ?? VIEWS.All;
    const teamId = query.get('TeamId');
    const pathwayId = query.get('PathwayId');
    const patientId = query.get('PatientId');
    // The patient's name only, as the API: a title can hold a specimen number, which stays out of URLs.
    const search = query.get('Search')?.trim() ?? '';
    if (search.length > 200) return invalid({search: ['Search must not exceed 200 characters.']});
    const items = db()
      .results.filter(
        (r) =>
          view.match(r, viewer.id) &&
          (!teamId || pathwayOf(r)?.teamId === teamId) &&
          (!pathwayId || r.pathwayId === pathwayId) &&
          (!patientId || r.patientId === patientId) &&
          contains(patientName(r.patientId), search),
      )
      .sort((a, b) => {
        const order = view.key(a).localeCompare(view.key(b)) || a.id.localeCompare(b.id);
        return view.newestFirst ? -order : order;
      });
    const sorted = sortedBy(items, query.get('Sort'), query.get('Ascending') !== 'false', {
      patient: (r) => patientSortName(r.patientId),
      title: (r) => r.title.toLowerCase(),
      pathway: (r) => pathwayOf(r)?.title.toLowerCase() ?? '',
      reported: (r) => r.reportedAtUtc,
      status: (r) => `${RESULT_STATUS_ORDER.indexOf(r.status)} ${r.signedOffAtUtc ?? ''}`,
    });
    return paged((sorted ?? items).map(resultSummary), query);
  });

  on('get', '/api/results/count', () => {
    const viewer = me();
    return {awaitingMine: db().results.filter((r) => isAwaitingResult(r) && r.responsibleUserId === viewer.id).length};
  });

  on('get', '/api/results/{id}', ({params}) => {
    requirePermission('ViewPatients');
    return resultResponse(findResult(params.id));
  });

  on('post', '/api/results/{id}/seen', ({params, body}) => {
    const viewer = me();
    const result = forProgress(params.id, body.expectedVersion);
    if (result.status === 'Seen') return problem(409, 'This result has already been marked seen.');
    result.status = 'Seen';
    result.seenAtUtc = nowUtc();
    result.seenByUserId = viewer.id;
    result.version++;
    return resultResponse(result);
  });

  on('post', '/api/results/{id}/sign-off', ({params, body}) => {
    const viewer = me();
    const result = forProgress(params.id, body.expectedVersion);
    if (!ACTIONS.includes(body.action ?? '')) return invalid({action: ['Choose what was done.']});
    const detail = optionalText(body.detail, 1000, 'detail', 'The detail');
    if (body.action === 'Other' && !detail) return invalid({detail: ['Say what was done.']});
    result.status = 'SignedOff';
    result.signedOffAtUtc = nowUtc();
    result.signedOffByUserId = viewer.id;
    result.action = body.action!;
    result.actionDetail = detail;
    result.version++;
    notify([result.responsibleUserId], viewer.id, {
      kind: 'ResultSignedOffByOther',
      title: 'Result signed off',
      message: `${viewer.username} signed off a result you're responsible for.`,
      pathwayId: result.pathwayId,
      subjectId: result.id,
    });
    return resultResponse(result);
  });

  on('post', '/api/results/{id}/reassign', ({params, body}) => {
    const viewer = me();
    const result = forProgress(params.id, body.expectedVersion);
    const to = personById(body.responsibleUserId);
    if (!to || !to.permissions.includes('SignOffResults'))
      return invalid({responsibleUserId: ['Choose a clinician whose roles allow signing off results.']});
    if (to.id === result.responsibleUserId) return invalid({responsibleUserId: ["They're already responsible for this result."]});
    result.responsibleUserId = to.id;
    result.version++;
    notify([to.id], viewer.id, {
      kind: 'ResultReassigned',
      title: 'Result reassigned to you',
      message: `${viewer.username} made you responsible for a result.`,
      pathwayId: result.pathwayId,
      subjectId: result.id,
    });
    return resultResponse(result);
  });

  on('post', '/api/results/{id}/entered-in-error', ({params, body}) => {
    const viewer = me();
    requirePermission('ViewPatients');
    const result = findResult(params.id);
    if (!mayMarkEnteredInError(result))
      return problem(403, 'Only whoever reported this result can mark it entered in error, within 24 hours of reporting it. After that, ask an admin.');
    ensureVersion(body.expectedVersion, result.version, CHANGED_MEANWHILE);
    if (patientFlags(result.patientId).isAnonymised) return problem(409, ANONYMISED);
    if (result.status === 'EnteredInError') return problem(409, 'This result has already been marked entered in error.');
    const detail = optionalText(body.detail, 500, 'detail', 'The detail');
    if (!detail) return invalid({detail: ['Say what was wrong.']});
    const now = nowUtc();
    result.status = 'EnteredInError';
    result.enteredInErrorAtUtc = now;
    result.enteredInErrorByUserId = viewer.id;
    result.errorDetail = detail;
    // The report's file is deleted, and a specimen goes back to the lab (received) to be reported again.
    result.reportFileName = null;
    result.version++;
    const specimen = specimenOf(result);
    if (specimen) {
      specimen.status = 'Received';
      specimen.reportedAtUtc = null;
      specimen.reportedByUserId = null;
      specimen.reportAttachmentId = null;
      specimen.version++;
    }
    return resultResponse(result);
  });
}
