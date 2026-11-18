import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {invalid, notFound, on} from '../api/http';
import {newId} from '../ids';
import {specialtyName} from '../seed';
import {db} from '../store';
import {contains, int, list, me, paged, requirePermission, sorted} from './common';
import {isOverdue} from './pathways';

/**
 * Patients and their timeline. The timeline is read from the patient's pathways, appointments and referrals as they
 * are now (the full version reads it from the audit log and the appointments' history). No files, letters, exports
 * or demographics lookup.
 */

type Entry = Schemas['PatientTimelineEntryResponse'];

function findPatient(id: string) {
  return db().patients.find((p) => p.id === id);
}

/** Every timeline entry for a patient, newest first. */
export function timelineOf(patientId: string): Entry[] {
  const state = db();
  const entries: Entry[] = [];
  const add = (entry: Entry & {id: string; atUtc: string}) => entries.push(entry);
  for (const p of state.pathways.filter((x) => x.patientId === patientId)) {
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
  for (const a of state.appointments.filter((x) => x.patientId === patientId)) {
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
  for (const r of state.referrals.filter((x) => x.patientId === patientId))
    for (const event of r.history)
      add({
        id: `${event.id}:referral`,
        kind: 'Referrals',
        action: `Referral${event.action}`,
        atUtc: event.atUtc!,
        username: event.username,
        referralId: r.id,
        specialtyName: event.specialtyName ?? specialtyName(r.specialtyId),
        referrerPractice: r.referrerPractice,
        previousSpecialtyName: event.previousSpecialtyName,
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
    const items = db().patients.filter(
      (p) =>
        contains(p.firstName, query.get('FirstName')) &&
        contains(p.lastName, query.get('LastName')) &&
        contains(p.email, query.get('Email')) &&
        contains(p.nhsNumber, query.get('NhsNumber')?.replace(/\s/g, '') ?? null) &&
        (!query.get('DateOfBirthFrom') || (p.dateOfBirth ?? '') >= query.get('DateOfBirthFrom')!) &&
        (!query.get('DateOfBirthTo') || (p.dateOfBirth ?? '') <= query.get('DateOfBirthTo')!),
    );
    return paged(
      sorted(
        items,
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
    return findPatient(params.id) ?? notFound('The patient');
  });

  on('post', '/api/patients', ({body}) => {
    requirePermission('EditPatients');
    const errors: Record<string, string[]> = {};
    if (!body.firstName?.trim()) errors.firstName = ['Enter a first name.'];
    if (!body.lastName?.trim()) errors.lastName = ['Enter a last name.'];
    if (!body.dateOfBirth) errors.dateOfBirth = ['Enter a date of birth.'];
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
      practiceId: null,
      isRestricted: false,
      isDisputed: false,
      isAnonymised: false,
    };
    db().patients.push(patient);
    return patient;
  });

  on('patch', '/api/patients/{id}', ({params, body}) => {
    requirePermission('EditPatients');
    const patient = findPatient(params.id);
    if (!patient) return notFound('The patient');
    for (const key of ['firstName', 'lastName', 'dateOfBirth', 'email', 'nhsNumber', 'addressLines', 'postcode', 'mobilePhone', 'reminderChannel'] as const)
      if (body[key] !== undefined) (patient as Record<string, unknown>)[key] = body[key];
    return patient;
  });

  on('get', '/api/patients/{id}/timeline', ({params, query}) => {
    me();
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
    const pathways = state.pathways.filter((p) => p.patientId === params.id);
    const open = pathways.filter((p) => !['Completed', 'Cancelled', 'Archived'].includes(p.status));
    return {
      upcoming: [...open]
        .sort((a, b) => a.dueDate.localeCompare(b.dueDate))
        .map((p) => ({pathwayId: p.id, title: p.title, status: p.status, priority: p.priority, dueDate: p.dueDate, isOverdue: isOverdue(p)})),
      openCount: open.length,
      pathways: pathways.map((p) => ({id: p.id, title: p.title, isDeleted: false, createdOn: p.createdAt.slice(0, 10), deletedOn: null})),
      awaitingReferrals: state.referrals
        .filter((r) => r.patientId === params.id && r.status === 'AwaitingTriage')
        .map((r) => ({referralId: r.id, specialtyName: specialtyName(r.specialtyId), receivedOn: r.receivedOn, requestedPriority: r.requestedPriority})),
    };
  });

  // No files, letters or practices in the light demo: their lists are empty.
  on('get', '/api/attachments', () => ({items: [], totalCount: 0, maxFileBytes: 0, allowedExtensions: []}));
  on('get', '/api/letters/count', () => ({toApprove: 0, toSend: 0}));
  on('get', '/api/letters/search', ({query}) => paged([], query));
  on('get', '/api/practices', () => []);

  on('get', '/api/specialties', () => {
    me();
    return db().specialties;
  });
}
