import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {id, newId, PATIENTS} from '../ids';
import {db} from '../store';
import {daysFrom, nowUtc, today, ukTime} from '../time';
import {can, contains, flag, me, paged, patientName, patientSortName, requirePermission, sortedBy} from './common';

/**
 * Patients' data requests (subject access). Simplified: no notifications, activity log or concurrency beyond the
 * version check; the review lists the patient's pathway notes only; and the pack, which the full version makes on its
 * server (a PDF report, the record as JSON and the files), isn't made: downloading it answers a 501 saying so.
 */

/** A data request as the light demo keeps it: the response without what depends on who's looking. */
export type DemoDataRequest = Omit<
  Schemas['DataRequestResponse'],
  'isOverdue' | 'canManage' | 'canReview' | 'canMakePack' | 'packBlockedReason' | 'patientName' | 'dueOn'
> & {id: string; patientId: string; version: number};

/** The request seeded for the admin: Tobias Lund's own, identity checked, due within the week. */
export const SEEDED_DATA_REQUEST = id(0xef, 1);

function seededRequest(): DemoDataRequest {
  const receivedOn = daysFrom(-26);
  const at = ukTime(receivedOn, 10);
  return {
    id: SEEDED_DATA_REQUEST,
    patientId: PATIENTS.tobiasLund,
    receivedOn,
    requestedByPatient: true,
    requesterName: null,
    requesterRelationship: null,
    identityCheckedOn: receivedOn,
    identityCheckedByUsername: 'admin',
    identityCheckMethod: 'InPerson',
    identityCheckDetail: null,
    firstDueOn: addMonths(receivedOn, 1),
    extensionMonths: 0,
    extensionReason: null,
    status: 'Open',
    refusalReason: null,
    sentOn: null,
    sendMethod: null,
    closedAtUtc: null,
    closedByUsername: null,
    reviewedAtUtc: null,
    reviewedByUsername: null,
    withheld: [],
    createdAtUtc: at,
    createdByUsername: 'admin',
    lastUpdatedUtc: at,
    version: 2,
  };
}

/**
 * The demo's requests, kept beside the seed's records: absent from the seed, so the first read makes the list with one
 * open request (synthetic), for the admin's Needs your attention.
 */
function requests(): DemoDataRequest[] {
  const state = db() as ReturnType<typeof db> & {dataRequests?: DemoDataRequest[]};
  state.dataRequests ??= [seededRequest()];
  return state.dataRequests;
}

/** A UK date some calendar months on: the same day, or the month's last day when it has no such day. */
export function addMonths(date: string, months: number): string {
  const [year, month, day] = date.split('-').map(Number);
  const last = new Date(Date.UTC(year, month - 1 + months + 1, 0)).getUTCDate();
  return new Date(Date.UTC(year, month - 1 + months, Math.min(day, last))).toISOString().slice(0, 10);
}

const dueOn = (request: DemoDataRequest) => addMonths(request.firstDueOn ?? '', request.extensionMonths ?? 0);
const isOpen = (request: DemoDataRequest) => request.status === 'Open';

/** Who may see data requests: admins only (ManagePrivacy), who log, review and answer them. */
function requireAccess() {
  requirePermission('ManagePrivacy');
}

function response(request: DemoDataRequest): Schemas['DataRequestResponse'] {
  const open = isOpen(request);
  const canManage = open && can('ManagePrivacy');
  const blocked = !request.identityCheckedOn
    ? 'Record the identity check first.'
    : !request.reviewedAtUtc
      ? 'Review the record first.'
      : null;
  return {
    ...request,
    patientName: patientName(request.patientId),
    dueOn: dueOn(request),
    isOverdue: open && dueOn(request) < today(),
    canManage,
    canReview: canManage,
    canMakePack: canManage && !blocked,
    packBlockedReason: canManage ? blocked : null,
  };
}

function summary(request: DemoDataRequest): Schemas['DataRequestSummaryResponse'] {
  const full = response(request);
  return {
    id: full.id,
    patientId: full.patientId,
    patientName: full.patientName,
    receivedOn: full.receivedOn,
    dueOn: full.dueOn,
    status: full.status,
    requestedByPatient: full.requestedByPatient,
    isIdentityChecked: Boolean(full.identityCheckedOn),
    isReviewed: Boolean(full.reviewedAtUtc),
    isOverdue: full.isOverdue,
  };
}

/** The request to change: it exists, it's open, and it's the version the page showed. */
function changing(id: string, expectedVersion: number | undefined): DemoDataRequest {
  const request = requests().find((r) => r.id === id) ?? refuse(notFound('The data request'));
  if (!isOpen(request)) refuse(problem(409, 'This data request is closed, so it can’t be changed.'));
  if (expectedVersion !== request.version)
    refuse(problem(409, 'Someone else changed this data request while you were working on it. Reload it, then try again.'));
  return request;
}

/** Marks a change made: by whom, when, and a new version. */
function changed(request: DemoDataRequest) {
  request.lastUpdatedUtc = nowUtc();
  request.version += 1;
  return response(request);
}

/** Closes a request: sent, refused or withdrawn. */
function close(request: DemoDataRequest, status: 'Sent' | 'Refused' | 'Withdrawn') {
  request.status = status;
  request.closedAtUtc = nowUtc();
  request.closedByUsername = me().username;
}

const reason = (text: string | null | undefined) => (text ?? '').trim();

export function registerDataRequests() {
  // The home page's counts: open, and open ones due within 7 days or overdue. They name no patient, so nothing is logged.
  on('get', '/api/data-requests/count', () => {
    requireAccess();
    const open = requests().filter(isOpen);
    const dueBy = daysFrom(7);
    return {open: open.length, dueSoon: open.filter((r) => dueOn(r) <= dueBy).length};
  });

  on('get', '/api/data-requests/search', ({query}) => {
    requireAccess();
    const status = query.get('Status');
    const patientId = query.get('PatientId');
    const awaitingReview = flag(query, 'AwaitingReview') === true;
    const search = query.get('Search')?.trim() ?? '';
    if (search.length > 200) return invalid({search: ['Search must not exceed 200 characters.']});
    const matching = requests()
      .filter((r) => (!status || r.status === status) && (!patientId || r.patientId === patientId) && contains(patientName(r.patientId), search))
      .filter((r) => !awaitingReview || (isOpen(r) && !r.reviewedAtUtc));
    // Open ones first by due date, then the rest, newest received first.
    const ordered = [
      ...matching.filter(isOpen).sort((a, b) => dueOn(a).localeCompare(dueOn(b)) || a.id.localeCompare(b.id)),
      ...matching.filter((r) => !isOpen(r)).sort((a, b) => (b.receivedOn ?? '').localeCompare(a.receivedOn ?? '') || a.id.localeCompare(b.id)),
    ];
    const sorted = sortedBy(ordered, query.get('Sort'), query.get('Ascending') !== 'false', {
      patient: (r) => patientSortName(r.patientId),
      received: (r) => r.receivedOn ?? '',
      due: dueOn,
      status: (r) => ['Open', 'Sent', 'Refused', 'Withdrawn'].indexOf(r.status ?? ''),
    });
    return paged((sorted ?? ordered).map(summary), query);
  });

  on('post', '/api/data-requests', ({body}) => {
    requirePermission('ManagePrivacy');
    const patient = db().patients.find((p) => p.id === body.patientId) ?? refuse(notFound('The patient'));
    const receivedOn = body.receivedOn ?? '';
    if (!receivedOn || receivedOn > today()) refuse(invalid({receivedOn: ['Give the day it was received: not in the future.']}));
    if (!body.requestedByPatient && (!reason(body.requesterName) || !reason(body.requesterRelationship)))
      refuse(invalid({requesterName: ['Give who asked, and their relationship to the patient.']}));
    const now = nowUtc();
    const request: DemoDataRequest = {
      id: newId(),
      patientId: patient.id ?? '',
      receivedOn,
      requestedByPatient: body.requestedByPatient === true,
      requesterName: body.requestedByPatient ? null : reason(body.requesterName),
      requesterRelationship: body.requestedByPatient ? null : reason(body.requesterRelationship),
      identityCheckedOn: null,
      identityCheckedByUsername: null,
      identityCheckMethod: null,
      identityCheckDetail: null,
      firstDueOn: addMonths(receivedOn, 1),
      extensionMonths: 0,
      extensionReason: null,
      status: 'Open',
      refusalReason: null,
      sentOn: null,
      sendMethod: null,
      closedAtUtc: null,
      closedByUsername: null,
      reviewedAtUtc: null,
      reviewedByUsername: null,
      withheld: [],
      createdAtUtc: now,
      createdByUsername: me().username,
      lastUpdatedUtc: now,
      version: 1,
    };
    requests().push(request);
    return new DemoReply(201, response(request));
  });

  on('get', '/api/data-requests/{id}', ({params}) => {
    requireAccess();
    return response(requests().find((r) => r.id === params.id) ?? refuse(notFound('The data request')));
  });

  // The patient's pathway notes, as the review lists them (the full version adds files, letters, results, MDT items
  // and referrals).
  on('get', '/api/data-requests/{id}/review-items', ({params}) => {
    requireAccess();
    const request = requests().find((r) => r.id === params.id) ?? refuse(notFound('The data request'));
    return db()
      .pathways.filter((p) => p.patientId === request.patientId)
      .flatMap((pathway) =>
        pathway.notes.map((note): Schemas['DataRequestReviewItemResponse'] => {
          const withheld = request.withheld?.find((w) => w.kind === 'Note' && w.itemId === note.id);
          return {
            kind: 'Note',
            itemId: note.id,
            atUtc: note.createdAtUtc,
            title: pathway.title,
            text: note.text,
            by: usernameOf(note.authorUserId),
            isDeleted: false,
            isWithheld: Boolean(withheld),
            withheldReason: withheld?.reason ?? null,
          };
        }),
      );
  });

  on('put', '/api/data-requests/{id}/review', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const request = changing(params.id, body.expectedVersion);
    const withheld = (body.withheld ?? []).map((w) => ({kind: w.kind ?? '', itemId: w.itemId ?? '', reason: reason(w.reason)}));
    if (withheld.some((w) => !w.reason)) refuse(invalid({reason: ['Give a reason for each item withheld.']}));
    request.withheld = withheld;
    request.reviewedAtUtc = nowUtc();
    request.reviewedByUsername = me().username;
    return changed(request);
  });

  on('post', '/api/data-requests/{id}/identity-check', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const request = changing(params.id, body.expectedVersion);
    if (!body.checkedOn || !body.method) refuse(invalid({method: ['Say when and how the identity was checked.']}));
    if (body.method === 'Other' && !reason(body.detail)) refuse(invalid({detail: ['Say how it was checked.']}));
    request.identityCheckedOn = body.checkedOn;
    request.identityCheckMethod = body.method;
    request.identityCheckDetail = reason(body.detail) || null;
    request.identityCheckedByUsername = me().username;
    return changed(request);
  });

  on('post', '/api/data-requests/{id}/extension', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const request = changing(params.id, body.expectedVersion);
    if (body.months !== 1 && body.months !== 2) refuse(invalid({months: ['Extend by one or two months in all.']}));
    if (!reason(body.reason)) refuse(invalid({reason: ['Give the reason the requester is told.']}));
    request.extensionMonths = body.months;
    request.extensionReason = reason(body.reason);
    return changed(request);
  });

  on('post', '/api/data-requests/{id}/sent', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const request = changing(params.id, body.expectedVersion);
    if (!body.sentOn || !body.method) refuse(invalid({method: ['Say when and how it was sent.']}));
    request.sentOn = body.sentOn;
    request.sendMethod = body.method;
    close(request, 'Sent');
    return changed(request);
  });

  on('post', '/api/data-requests/{id}/refuse', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const request = changing(params.id, body.expectedVersion);
    if (!reason(body.reason)) refuse(invalid({reason: ['Give the reason the requester is told.']}));
    request.refusalReason = reason(body.reason);
    close(request, 'Refused');
    return changed(request);
  });

  on('post', '/api/data-requests/{id}/withdraw', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const request = changing(params.id, body.expectedVersion);
    close(request, 'Withdrawn');
    return changed(request);
  });

  on('get', '/api/data-requests/{id}/pack', ({params}) => {
    requirePermission('ManagePrivacy');
    if (!requests().some((r) => r.id === params.id)) refuse(notFound('The data request'));
    return problem(501, 'The data-request pack isn’t in the light demo.', 'The full version makes it on its server: a PDF report, the record as JSON and the files.');
  });
}
