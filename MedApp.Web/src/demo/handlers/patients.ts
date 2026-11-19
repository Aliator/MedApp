import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {invalid, notFound, on} from '../api/http';
import {newId} from '../ids';
import {teamName} from '../seed';
import {db} from '../store';
import {logActivity} from './adminLogs';
import {contains, int, list, me, paged, requirePermission, sorted} from './common';
import {isOverdue} from './pathways';
import {isHiddenFromSearch} from './hospitals';
import {ensureCanChange, patientResponse} from './setupPrivacy';
import {canSeePathway, canSeeRecord, isPickedTeam, myScope, otherTeamPathways, pickedTeam} from './teamWalls';

/**
 * Patients and their timeline. The timeline is read from the patient's pathways, appointments and referrals as they
 * are now (the full version reads it from the audit log and the appointments' history). No files, letters, exports
 * or demographics lookup.
 */

type Entry = Schemas['PatientTimelineEntryResponse'];

function findPatient(id: string) {
  return db().patients.find((p) => p.id === id);
}

/**
 * Every timeline entry for a patient the viewer may see, newest first: other teams' pathways, appointments and
 * referrals are left out, not summarised (the team walls; the overview has the summary lines).
 */
export function timelineOf(patientId: string): Entry[] {
  const state = db();
  const scope = myScope();
  const entries: Entry[] = [];
  const add = (entry: Entry & {id: string; atUtc: string}) => entries.push(entry);
  for (const p of state.pathways.filter((x) => x.patientId === patientId && canSeePathway(x, scope))) {
    const base = {pathwayId: p.id, pathwayTitle: p.title, isPathwayDeleted: false};
    add({
      ...base,
      id: `${p.id}:created`,
      kind: 'Created',
      action: 'Created',
      atUtc: p.createdAt,
      username: usernameOf(p.assignments[0]?.assignedByUserId) ?? 'admin',
      templateName: p.templateName,
      stageCount: p.stages.length,
      priority: p.priority,
      dueDate: p.dueDate,
    });
    for (const a of p.assignments)
      add({...base, id: `${p.id}:assigned:${a.userId}`, kind: 'Assignments', action: 'Assigned', atUtc: a.assignedAtUtc, username: usernameOf(a.assignedByUserId), assigneeUsername: usernameOf(a.userId)});
    for (const s of p.stages) {
      if (s.startedAtUtc)
        add({...base, id: `${s.id}:started`, kind: 'Stages', action: 'StageStarted', atUtc: s.startedAtUtc, username: usernameOf(s.startedByUserId), stageName: s.name});
      if (s.completedAtUtc)
        add({...base, id: `${s.id}:finished`, kind: 'Stages', action: 'StageFinished', atUtc: s.completedAtUtc, username: usernameOf(s.completedByUserId), stageName: s.name, finishedWithoutVisit: false});
    }
    for (const n of p.notes) add({...base, id: `${n.id}:note`, kind: 'Notes', action: 'NoteAdded', atUtc: n.createdAtUtc, username: usernameOf(n.authorUserId)});
    for (const [i, c] of p.statusChanges.entries())
      add({
        ...base,
        id: `${p.id}:status:${i}`,
        kind: c.to === 'Blocked' || c.from === 'Blocked' ? 'Blocks' : 'Status',
        action: 'StatusChanged',
        atUtc: c.atUtc,
        username: usernameOf(c.userId),
        fromStatus: c.from,
        toStatus: c.to,
      });
  }
  for (const a of state.appointments.filter((x) => x.patientId === patientId && canSeeRecord(x, scope))) {
    const base = {
      pathwayId: a.pathwayId,
      pathwayTitle: a.pathwayTitle,
      stageName: a.stageName,
      appointmentId: a.id,
      appointmentStartUtc: a.startUtc,
      appointmentEndUtc: a.endUtc,
      appointmentStaffUsername: a.staffUsername,
      visitRequirement: a.visitRequirement,
      kind: 'Appointments',
    };
    add({...base, id: `${a.id}:requested`, action: 'AppointmentRequested', atUtc: a.createdAtUtc!, username: a.createdByUsername});
    if (a.bookedAtUtc) add({...base, id: `${a.id}:booked`, action: 'AppointmentBooked', atUtc: a.bookedAtUtc, username: a.bookedByUsername});
    if (a.outcomeAtUtc)
      add({...base, id: `${a.id}:outcome`, action: a.status === 'DidNotAttend' ? 'AppointmentDidNotAttend' : 'AppointmentAttended', atUtc: a.outcomeAtUtc, username: a.outcomeByUsername});
    if (a.status === 'Cancelled') add({...base, id: `${a.id}:cancelled`, action: 'AppointmentCancelled', atUtc: a.lastUpdated!, username: null});
  }
  for (const r of state.referrals.filter((x) => x.patientId === patientId && canSeeRecord({teamId: x.teamId}, scope)))
    for (const event of r.history)
      add({
        id: `${event.id}:referral`,
        kind: 'Referrals',
        action: `Referral${event.action}`,
        atUtc: event.atUtc!,
        username: event.username,
        referralId: r.id,
        teamName: event.teamName ?? teamName(r.teamId),
        referrerPractice: r.referrerPractice,
        previousTeamName: event.previousTeamName,
        rejectionReason: event.rejectionReason,
        templateName: event.templateName,
        pathwayId: event.pathwayId,
        priority: event.priority ?? event.requestedPriority,
      });
  return entries.sort((a, b) => (b.atUtc ?? '').localeCompare(a.atUtc ?? '') || (b.id ?? '').localeCompare(a.id ?? ''));
}

export function registerPatients() {
  on('get', '/api/patients/search', ({query}) => {
    me();
    // The clinic picker (MC6): patients with a pathway, referral or appointment in the team picked.
    const picked = pickedTeam(query);
    const inPicked = (patientId: string | null | undefined) =>
      !picked ||
      db().pathways.some((x) => x.patientId === patientId && isPickedTeam(x.teamId, picked)) ||
      db().referrals.some((x) => x.patientId === patientId && isPickedTeam(x.teamId, picked)) ||
      db().appointments.some((x) => x.patientId === patientId && isPickedTeam(x.teamId, picked));
    // A patient whose only link is a hospital case is the lab's to find (MC18).
    const scope = myScope();
    const items = db().patients.filter(
      (p) =>
        !isHiddenFromSearch(p.id, scope) &&
        inPicked(p.id) &&
        contains(p.firstName, query.get('FirstName')) &&
        contains(p.lastName, query.get('LastName')) &&
        contains(p.email, query.get('Email')) &&
        contains(p.nhsNumber, query.get('NhsNumber')?.replace(/\s/g, '') ?? null) &&
        (!query.get('DateOfBirthFrom') || (p.dateOfBirth ?? '') >= query.get('DateOfBirthFrom')!) &&
        (!query.get('DateOfBirthTo') || (p.dateOfBirth ?? '') <= query.get('DateOfBirthTo')!),
    );
    const page = paged(
      sorted(
        items.map(patientResponse),
        query,
        {
          name: (p) => `${p.lastName} ${p.firstName}`.toLowerCase(),
          dateOfBirth: (p) => p.dateOfBirth ?? '',
          email: (p) => p.email ?? '',
        },
        'name',
      ),
      query,
    );
    for (const p of page.items) logActivity('PatientListed', {patientId: p.id});
    return page;
  });

  on('get', '/api/patients/labels', ({query}) => {
    me();
    const ids = list(query, 'ids');
    return db()
      .patients.filter((p) => ids.includes(p.id ?? ''))
      .map((p) => ({id: p.id, name: `${p.firstName} ${p.lastName}`}));
  });

  on('get', '/api/patients/{id}', ({params}) => {
    me();
    const patient = findPatient(params.id);
    if (!patient) return notFound('The patient');
    logActivity('PatientViewed', {patientId: patient.id});
    // Centre-wide (decision 183): anyone sees the patient; other teams' pathways only as summary lines.
    return {...patientResponse(patient), otherTeamPathways: otherTeamPathways(params.id)};
  });

  on('post', '/api/patients', ({body}) => {
    requirePermission('EditPatients');
    const errors: Record<string, string[]> = {};
    if (!body.firstName?.trim()) errors.firstName = ['Enter a first name.'];
    if (!body.lastName?.trim()) errors.lastName = ['Enter a last name.'];
    if (!body.dateOfBirth) errors.dateOfBirth = ['Enter a date of birth.'];
    if (body.unlistedPractice && body.practiceId) errors.unlistedPractice = [BOTH_PRACTICES];
    if (body.unlistedPractice && !body.unlistedPractice.name?.trim()) errors.unlistedPracticeName = ['Enter the practice’s name.'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const patient: Schemas['PatientResponse'] = {
      id: newId(),
      firstName: body.firstName!.trim(),
      lastName: body.lastName!.trim(),
      dateOfBirth: body.dateOfBirth,
      email: body.email ?? null,
      nhsNumber: body.nhsNumber?.replace(/\s/g, '') || null,
      addressLines: body.addressLines ?? [],
      postcode: body.postcode ?? null,
      mobilePhone: body.mobilePhone ?? null,
      reminderChannel: body.reminderChannel ?? 'None',
      practiceId: body.unlistedPractice ? null : (practiceChoice(body.practiceId) ?? null),
      unlistedPractice: body.unlistedPractice ? unlistedPractice(body.unlistedPractice) : undefined,
      isRestricted: false,
      isDisputed: false,
      isAnonymised: false,
    };
    db().patients.push(patient);
    logActivity('PatientCreated', {patientId: patient.id});
    return patientResponse(patient);
  });

  on('patch', '/api/patients/{id}', ({params, body}) => {
    requirePermission('EditPatients');
    const patient = findPatient(params.id);
    if (!patient) return notFound('The patient');
    ensureCanChange(patient);
    const changed: string[] = [];
    for (const key of ['firstName', 'lastName', 'dateOfBirth', 'email', 'nhsNumber', 'addressLines', 'postcode', 'mobilePhone', 'reminderChannel'] as const)
      if (body[key] !== undefined && body[key] !== null && JSON.stringify(body[key]) !== JSON.stringify(patient[key])) {
        (patient as Record<string, unknown>)[key] = body[key];
        changed.push(key);
      }
    const unlisted = body.unlistedPractice;
    if (unlisted && body.practiceId && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(body.practiceId)) return invalid({unlistedPractice: [BOTH_PRACTICES]});
    if (unlisted && !unlisted.name?.trim()) return invalid({unlistedPracticeName: ['Enter the practice’s name.']});
    const practiceId = practiceChoice(body.practiceId);
    if (practiceId !== undefined && practiceId !== patient.practiceId) {
      patient.practiceId = practiceId;
      changed.push('practice');
    }
    // A directory practice (or none) replaces an unlisted one, and an unlisted one replaces a directory one: never both.
    if (practiceId !== undefined && !unlisted && patient.unlistedPractice) {
      patient.unlistedPractice = undefined;
      changed.push('unlistedPractice');
    }
    if (unlisted) {
      if (patient.practiceId) {
        patient.practiceId = null;
        if (!changed.includes('practice')) changed.push('practice');
      }
      const next = unlistedPractice(unlisted);
      if (JSON.stringify(next) !== JSON.stringify(patient.unlistedPractice)) {
        patient.unlistedPractice = next;
        if (!changed.includes('unlistedPractice')) changed.push('unlistedPractice');
      }
    }
    // Field names only, never their values.
    logActivity('PatientUpdated', {patientId: patient.id, details: JSON.stringify({fields: changed})});
    return patientResponse(patient);
  });

  on('get', '/api/patients/{id}/timeline', ({params, query}) => {
    me();
    logActivity('PatientViewed', {patientId: params.id});
    const kinds = list(query, 'Kinds');
    const pathwayIds = list(query, 'PathwayIds');
    const all = timelineOf(params.id).filter(
      (e) => (kinds.length === 0 || kinds.includes(e.kind ?? '')) && (pathwayIds.length === 0 || pathwayIds.includes(e.pathwayId ?? '')),
    );
    const before = query.get('Before');
    const start = before ? all.findIndex((e) => e.id === before) + 1 : 0;
    const size = int(query, 'PageSize', 20);
    const items = all.slice(start, start + size);
    return {items, nextCursor: start + size < all.length ? (items.at(-1)?.id ?? null) : null};
  });

  on('get', '/api/patients/{id}/timeline/overview', ({params}) => {
    me();
    const state = db();
    const scope = myScope();
    const pathways = state.pathways.filter((p) => p.patientId === params.id && canSeePathway(p, scope));
    const open = pathways.filter((p) => !['Completed', 'Cancelled', 'Archived'].includes(p.status));
    return {
      upcoming: [...open]
        .sort((a, b) => a.dueDate.localeCompare(b.dueDate))
        .map((p) => ({pathwayId: p.id, title: p.title, status: p.status, priority: p.priority, dueDate: p.dueDate, isOverdue: isOverdue(p)})),
      openCount: open.length,
      pathways: pathways.map((p) => ({id: p.id, title: p.title, isDeleted: false, createdOn: p.createdAt.slice(0, 10), deletedOn: null})),
      awaitingReferrals: state.referrals
        .filter((r) => r.patientId === params.id && r.status === 'AwaitingTriage' && canSeeRecord({teamId: r.teamId}, scope))
        .map((r) => ({referralId: r.id, teamName: teamName(r.teamId), receivedOn: r.receivedOn, requestedPriority: r.requestedPriority})),
      otherTeamPathways: otherTeamPathways(params.id, scope),
    };
  });

  // No files in the light demo: the list is empty.
  on('get', '/api/attachments', () => ({items: [], totalCount: 0, maxFileBytes: 0, allowedExtensions: []}));
}

const BOTH_PRACTICES = 'Choose a practice from the directory or enter one that isn’t listed, not both.';

/** A GP practice that isn't in the directory, kept on the patient only (trimmed; blank lines and fields left out). */
function unlistedPractice(practice: Schemas['UnlistedPracticeRequest']): Schemas['UnlistedPracticeResponse'] {
  return {
    name: (practice.name ?? '').trim(),
    addressLines: (practice.addressLines ?? []).map((line) => line.trim()).filter(Boolean),
    postcode: practice.postcode?.trim().toUpperCase() || null,
    phone: practice.phone?.trim() || null,
  };
}

/** A practice chosen on the patient form: its ID, null to remove it (`Guid.Empty`), undefined to leave it. */
function practiceChoice(practiceId: string | null | undefined): string | null | undefined {
  if (practiceId === undefined || practiceId === null) return undefined;
  if (/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(practiceId)) return null;
  return db().practices.some((p) => p.id === practiceId) ? practiceId : undefined;
}
