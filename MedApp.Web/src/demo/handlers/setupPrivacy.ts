import type {Schemas} from '../../api/client';
import {addDays} from '../../utils/calendarSlots';
import {usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {teamName, TARGET_DAYS} from '../seed';
import {db} from '../store';
import {nowUtc, today} from '../time';
import {auditEntries, logActivity} from './adminLogs';
import {me, requirePermission} from './common';
import {practiceSummary} from './setup';

/**
 * A patient's privacy: the subject access export (read-only, from what the light demo holds), restriction and dispute,
 * the retention review (one record made out to be past its period, as the demo's own are all recent) and keeping a
 * record, and deleting a patient who has nothing. Anonymising is refused: it's the full version's. Every one is
 * recorded in the activity log, with the admin's reason as the only free text.
 */

type Patient = Schemas['PatientResponse'];

/** The appointments that aren't finished (`Requested` or `Booked`). */
const OPEN_APPOINTMENTS = ['Requested', 'Booked'];
const CLOSED_PATHWAYS = ['Completed', 'Cancelled', 'Archived'];

export function findPatient(patientId: string): Patient {
  const patient = db().patients.find((p) => p.id === patientId);
  if (!patient) refuse(notFound('The patient'));
  return patient;
}

/** A patient as the API answers one: with their practice. */
export const patientResponse = (patient: Patient): Patient => ({...patient, practice: practiceSummary(patient.practiceId)});

/** Refuses a change to a restricted or anonymised record (`PatientRules.EnsureCanChange`). */
export function ensureCanChange(patient: Patient) {
  if (patient.isAnonymised) refuse(problem(409, 'This record has been anonymised, so it can’t be changed.'));
  if (patient.isRestricted) refuse(problem(409, 'This record is restricted, so it can’t be changed. An admin can lift the restriction.'));
}

/** Everything held about a patient, as the subject access export gives it (`PatientExportResponse`). */
function exportOf(patient: Patient): Schemas['PatientExportResponse'] {
  const state = db();
  const pathways = state.pathways.filter((p) => p.patientId === patient.id);
  const pathwayIds = pathways.map((p) => p.id);
  const appointments = state.appointments.filter((a) => a.patientId === patient.id);
  const appointmentIds = appointments.map((a) => a.id);
  const referrals = state.referrals.filter((r) => r.patientId === patient.id);
  const practice = practiceSummary(patient.practiceId);
  const kept = state.retention.find((r) => r.patientId === patient.id);
  return {
    exportedAtUtc: nowUtc(),
    exportedBy: me().username,
    patient: {
      id: patient.id,
      firstName: patient.firstName,
      lastName: patient.lastName,
      dateOfBirth: patient.dateOfBirth,
      email: patient.email,
      nhsNumber: patient.nhsNumber,
      addressLines: patient.addressLines,
      postcode: patient.postcode,
      mobilePhone: patient.mobilePhone,
      reminderChannel: patient.reminderChannel,
      practiceName: practice?.name ?? null,
      practiceOdsCode: practice?.odsCode ?? null,
      unlistedPractice: patient.unlistedPractice,
      createdAt: pathways.map((p) => p.createdAt).sort()[0] ?? undefined,
      lastUpdated: undefined,
      restrictedAtUtc: patient.restrictedAtUtc ?? null,
      restrictionReason: patient.restrictionReason ?? null,
      disputedAtUtc: patient.disputedAtUtc ?? null,
      disputeReason: patient.disputeReason ?? null,
      anonymisedAtUtc: patient.anonymisedAtUtc ?? null,
      retentionKeptUntil: kept?.keptUntil ?? null,
      retentionKeptReason: kept?.keptReason ?? null,
    },
    pathways: pathways.map((p) => ({
      id: p.id,
      title: p.title,
      templateName: p.templateName,
      status: p.status,
      priority: p.priority,
      dueDate: p.dueDate,
      createdAt: p.createdAt,
      lastUpdated: p.lastUpdated,
      notes: p.notes.map((n) => ({
        id: n.id,
        author: usernameOf(n.authorUserId),
        text: n.text,
        createdAtUtc: n.createdAtUtc,
        lastEditedAtUtc: n.lastEditedAtUtc,
        version: n.version,
        deletedAtUtc: n.deleted?.atUtc ?? null,
        deletedBy: usernameOf(n.deleted?.byUserId),
        deletionReason: n.deleted?.reason ?? null,
        deletionDetail: n.deleted?.detail ?? null,
        isAboutAnotherPatient: false,
      })),
      assignedTo: p.assignments.map((a) => usernameOf(a.userId) ?? ''),
      stages: p.stages.map((s) => ({
        order: s.stageOrder,
        name: s.name,
        notes: s.notes,
        startedAtUtc: s.startedAtUtc,
        startedBy: usernameOf(s.startedByUserId),
        finishedAtUtc: s.completedAtUtc,
        finishedBy: usernameOf(s.completedByUserId),
        skippedAtUtc: s.skippedAtUtc,
        skippedBy: usernameOf(s.skippedByUserId),
        skipReason: s.skipReason,
        skipDetail: s.skipDetail,
        // The outcome recorded at a choice node (the export's `branch`, until the API renames it).
        branch: s.outcome,
      })),
    })),
    pathwayHistory: auditEntries()
      .filter((e) => e.patientId === patient.id)
      .reverse()
      .map((e) => ({atUtc: e.atUtc, action: e.action, username: e.username, pathwayId: e.patientPathwayId, pathwayTitle: e.pathwayTitle, stageName: e.stageName, details: e.details})),
    noteHistory: pathways.flatMap((p) =>
      p.notes.map((n) => ({noteId: n.id, pathwayId: p.id, version: n.version, text: n.text, username: usernameOf(n.authorUserId), atUtc: n.lastEditedAtUtc ?? n.createdAtUtc})),
    ),
    noteDeletionRequests: (state.noteDeletionRequests ?? [])
      .filter((r) => pathwayIds.includes(r.pathwayId))
      .map((r) => ({
        id: r.id,
        noteId: r.noteId,
        pathwayId: r.pathwayId,
        requestedBy: usernameOf(r.requestedByUserId),
        requestedAtUtc: r.requestedAtUtc,
        reason: r.reason,
        detail: r.detail,
        status: r.status,
        decidedBy: usernameOf(r.decidedByUserId),
        decidedAtUtc: r.decidedAtUtc,
      })),
    appointments: appointments.map((a) => ({
      id: a.id,
      pathwayId: a.pathwayId,
      stageName: a.stageName,
      status: a.status,
      staffUsername: a.staffUsername,
      startUtc: a.startUtc,
      endUtc: a.endUtc,
      notes: a.notes,
      cancelReason: a.cancelReason ?? null,
      isAutomatic: a.isAutomatic,
      isRecordedAfterEvent: a.isRecordedAfterEvent,
      createdAtUtc: a.createdAtUtc,
      createdBy: a.createdByUsername,
      bookedAtUtc: a.bookedAtUtc ?? null,
      bookedBy: a.bookedByUsername ?? null,
      outcomeAtUtc: a.outcomeAtUtc ?? null,
      outcomeBy: a.outcomeByUsername ?? null,
      dnaFollowUp: a.dnaFollowUp ?? null,
      dnaFollowUpDetail: a.dnaFollowUpDetail ?? null,
      dnaFollowUpAtUtc: a.dnaFollowUpAtUtc ?? null,
      dnaFollowUpBy: a.dnaFollowUpByUsername ?? null,
    })),
    appointmentHistory: appointments
      .flatMap((a) => {
        const base = {appointmentId: a.id, pathwayId: a.pathwayId, pathwayTitle: a.pathwayTitle, stageName: a.stageName, staffUsername: a.staffUsername, startUtc: a.startUtc, endUtc: a.endUtc, details: null};
        return [
          {...base, atUtc: a.createdAtUtc, action: a.isRecordedAfterEvent ? 'RecordedAfterEvent' : 'Requested', username: a.createdByUsername},
          ...(a.bookedAtUtc && !a.isRecordedAfterEvent ? [{...base, atUtc: a.bookedAtUtc, action: 'Booked', username: a.bookedByUsername}] : []),
          ...(a.outcomeAtUtc && !a.isRecordedAfterEvent ? [{...base, atUtc: a.outcomeAtUtc, action: a.status === 'DidNotAttend' ? 'DidNotAttend' : 'Attended', username: a.outcomeByUsername}] : []),
          ...(a.status === 'Cancelled' ? [{...base, atUtc: a.lastUpdated, action: 'Cancelled', username: null}] : []),
          ...(a.dnaFollowUpAtUtc ? [{...base, atUtc: a.dnaFollowUpAtUtc, action: 'DnaFollowUpRecorded', username: a.dnaFollowUpByUsername}] : []),
        ];
      })
      .sort((x, y) => (x.atUtc ?? '').localeCompare(y.atUtc ?? '')),
    messages: [],
    accessLog: state.activityLog
      .filter((e) => e.patientId === patient.id)
      .map((e) => ({atUtc: e.atUtc, action: e.action, username: e.username, pathwayId: e.pathwayId, details: e.details})),
    // About the patient's pathways and appointments; monitoring notifications are about staff, and left out.
    notifications: state.notifications
      .filter((n) => (n.pathwayId && pathwayIds.includes(n.pathwayId)) || (n.appointmentId && appointmentIds.includes(n.appointmentId)))
      .map((n) => ({
        createdAtUtc: n.createdAtUtc,
        kind: n.kind,
        username: usernameOf(n.userId),
        title: n.title,
        message: n.message,
        pathwayId: n.pathwayId ?? null,
        appointmentId: n.appointmentId ?? null,
      })),
    attachments: [],
    referrals: referrals.map((r) => {
      const targetDays = TARGET_DAYS[r.requestedPriority];
      return {
        id: r.id,
        teamName: teamName(r.teamId) ?? state.teams.find((s) => s.id === r.teamId)?.name ?? null,
        receivedOn: r.receivedOn,
        referrerPractice: r.referrerPractice,
        referrerClinician: r.referrerClinician,
        referrerOdsCode: null,
        requestedPriority: r.requestedPriority,
        reason: r.reason,
        status: r.status,
        registeredAtUtc: r.registeredAtUtc,
        registeredBy: usernameOf(r.registeredByUserId),
        decidedAtUtc: r.decidedAtUtc,
        decidedBy: usernameOf(r.decidedByUserId),
        rejectionReason: r.rejectionReason,
        decisionDetail: r.decisionDetail,
        redirectedTo: r.redirectedTo,
        redirectDetail: null,
        pathwayId: r.pathwayId,
        targetDays,
        waitStartOn: r.receivedOn,
        targetOn: addDays(r.receivedOn, targetDays),
        pausedDays: 0,
        stoppedOn: r.decidedAtUtc && r.status === 'Accepted' ? r.decidedAtUtc.slice(0, 10) : null,
        stopReason: r.status === 'Accepted' ? 'Accepted' : null,
        pauses: [],
      };
    }),
    referralHistory: referrals.flatMap((r) =>
      r.history.map((e) => ({atUtc: e.atUtc, action: e.action, username: e.username, referralId: r.id, pathwayId: e.pathwayId ?? null, details: null})),
    ),
    // The lab's, the letters', consent's, results' and the MDT's records aren't in the light demo's export.
    letters: [],
    specimens: [],
    consentRecords: [],
    results: [],
    mdtItems: [],
  };
}

/** Whether a patient has care under way: pathways not closed, open appointments, or referrals awaiting triage. */
function openCare(patientId: string) {
  const state = db();
  return {
    pathways: state.pathways.filter((p) => p.patientId === patientId && !CLOSED_PATHWAYS.includes(p.status)).length,
    appointments: state.appointments.filter((a) => a.patientId === patientId && OPEN_APPOINTMENTS.includes(a.status ?? '')).length,
    referrals: state.referrals.filter((r) => r.patientId === patientId && r.status === 'AwaitingTriage').length,
  };
}

const RETENTION_YEARS: Record<string, number> = {Adult: 8, Child: 25, Oncology: 30, Maternity: 25, MentalHealth: 20};

/** The date a record is due for review: its conclusion of treatment plus its class's years. */
const dueOn = (conclusion: string, retentionClass: string) =>
  `${Number(conclusion.slice(0, 4)) + (RETENTION_YEARS[retentionClass] ?? 8)}${conclusion.slice(4)}`;

export function registerSetupPrivacy() {
  // The deprecated list of every patient (for the Blazor client): one PatientsSearched entry, "all".
  on('get', '/api/patients', () => {
    requirePermission('ViewPatients');
    const patients = db().patients;
    logActivity('PatientsSearched', {details: JSON.stringify({filters: ['all'], resultCount: patients.length})});
    return patients.map(patientResponse);
  });

  on('delete', '/api/patients/{id}', ({params}) => {
    requirePermission('DeletePatients');
    const patient = findPatient(params.id);
    const state = db();
    if (state.pathways.some((p) => p.patientId === patient.id) || state.appointments.some((a) => a.patientId === patient.id))
      return problem(409, 'This patient has pathways or appointments, so they can’t be deleted.');
    if (state.referrals.some((r) => r.patientId === patient.id)) return problem(409, 'This patient has referrals, so they can’t be deleted.');
    state.patients = state.patients.filter((p) => p.id !== patient.id);
    logActivity('PatientDeleted', {patientId: patient.id});
    return undefined;
  });

  on('get', '/api/patients/{id}/export', ({params}) => {
    requirePermission('ExportPatientRecords');
    const patient = findPatient(params.id);
    logActivity('PatientExported', {patientId: patient.id});
    return exportOf(patient);
  });

  for (const [path, flag] of [
    ['/api/patients/{id}/restriction', 'restriction'],
    ['/api/patients/{id}/dispute', 'dispute'],
  ] as const)
    on('put', path, ({params, body}) => {
      requirePermission('ManagePrivacy');
      const patient = findPatient(params.id);
      if (patient.isAnonymised) return problem(409, 'This record has been anonymised.');
      const reason = body.reason?.trim() || null;
      if (body.set && !reason) return invalid({reason: ['Give a reason.']});
      if (reason && reason.length > 500) return invalid({reason: ['A reason is at most 500 characters.']});
      const isSet = flag === 'restriction' ? patient.isRestricted : patient.isDisputed;
      if (!body.set && !isSet) return patientResponse(patient);
      if (flag === 'restriction' && body.set && !body.confirmOpenPathways) {
        const open = openCare(patient.id!);
        if (open.pathways + open.appointments + open.referrals > 0)
          return invalid({
            confirmOpenPathways: [
              `This patient has ${open.pathways} pathway(s) under way, ${open.appointments} open appointment(s) and ${open.referrals} referral(s) awaiting triage. While restricted, their pathways can only be put on hold or cancelled, their appointments cancelled and their referrals withdrawn. Confirm to restrict the record.`,
            ],
          });
      }
      const now = nowUtc();
      if (flag === 'restriction')
        Object.assign(patient, {isRestricted: !!body.set, restrictedAtUtc: body.set ? now : null, restrictionReason: body.set ? reason : null});
      else Object.assign(patient, {isDisputed: !!body.set, disputedAtUtc: body.set ? now : null, disputeReason: body.set ? reason : null});
      const action =
        flag === 'restriction' ? (body.set ? 'PatientRestricted' : 'PatientRestrictionLifted') : body.set ? 'PatientDisputed' : 'PatientDisputeCleared';
      logActivity(action, {patientId: patient.id, details: JSON.stringify({reason})});
      return patientResponse(patient);
    });

  on('post', '/api/patients/{id}/anonymise', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const patient = findPatient(params.id);
    if (`${patient.firstName} ${patient.lastName}`.trim().toLowerCase() !== (body.confirmation ?? '').trim().toLowerCase())
      return invalid({confirmation: ['Type the patient’s full name to confirm.']});
    return problem(
      409,
      'Anonymising isn’t in the light demo.',
      'The full version replaces the record’s identifying details and free text with placeholders, across every table, keeping its structure.',
    );
  });

  on('get', '/api/patients/retention-review', ({query}) => {
    requirePermission('ManagePrivacy');
    const state = db();
    const includeKept = query.get('IncludeKept') === 'true';
    const items = state.retention
      .map((r) => ({r, patient: state.patients.find((p) => p.id === r.patientId)}))
      .filter(({r, patient}) => patient && !patient.isAnonymised && openCare(patient.id!).referrals === 0 && (includeKept || !r.keptUntil || r.keptUntil < today()))
      .map(({r, patient}) => ({
        patientId: r.patientId,
        firstName: patient!.firstName,
        lastName: patient!.lastName,
        dateOfBirth: patient!.dateOfBirth,
        conclusionOfTreatment: r.conclusionOfTreatment,
        retentionClass: r.retentionClass,
        dueOn: dueOn(r.conclusionOfTreatment, r.retentionClass),
        keptUntil: r.keptUntil,
        keptReason: r.keptReason,
      }))
      .sort((a, b) => a.dueOn.localeCompare(b.dueOn) || a.patientId.localeCompare(b.patientId));
    for (const item of items) logActivity('PatientListed', {patientId: item.patientId});
    const pageSize = Math.min(Math.max(Number(query.get('PageSize')) || 10, 1), 100);
    const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
    const page = Math.min(Math.max(Number(query.get('Page')) || 1, 1), totalPages);
    return {items: items.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: items.length, totalPages};
  });

  on('post', '/api/patients/{id}/retention/keep', ({params, body}) => {
    requirePermission('ManagePrivacy');
    const patient = findPatient(params.id);
    const record = db().retention.find((r) => r.patientId === patient.id);
    if (!record) return problem(409, 'This record isn’t past its retention period.');
    const reason = body.reason?.trim() ?? '';
    const errors: Record<string, string[]> = {};
    if (!body.keepUntil || body.keepUntil <= today() || body.keepUntil > addDays(today(), 731))
      errors.keepUntil = ['Choose a review date after today, at most two years ahead.'];
    if (!reason || reason.length > 500) errors.reason = ['Say why it’s kept (up to 500 characters, no patient details).'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    record.keptUntil = body.keepUntil!;
    record.keptReason = reason;
    logActivity('RetentionKept', {patientId: patient.id, details: JSON.stringify({keepUntil: record.keptUntil, reason})});
    return undefined;
  });
}

