import type {Schemas} from '../../api/client';
import {personById, usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import {specimenNumber, type DemoSpecimen, type DemoSpecimenType, type SpecimenStatus} from '../slices/lab';
import {teamName} from '../seed';
import type {DemoPathway, DemoStage} from '../state';
import {db} from '../store';
import {nowUtc, today} from '../time';
import {can, contains, me, paged, patientName, patientSortName, requirePermission, sortedBy} from './common';
import {
  addDaysUtc,
  ensureVersion,
  notify,
  optionalText,
  patientBanner,
  patientFlags,
  stepTime,
  ukShortDate,
} from './labCommon';
import {canOpenCaseFor, caseOfSample, clockOf, openCaseOnReceipt, registerLabCases} from './labCases';
import {isAwaitingResult, registerResults} from './labResults';
import {registerLabTests} from './labTests';
import {findPathway, isCurrentStage} from './pathways';
import {canSeeRecord, labTeamId, myScope, passToLab, refuseAcrossWall, type TeamScope} from './teamWalls';

/**
 * Specimens, the samples page and specimen types (lab tests: `labTests.ts`; results: `labResults.ts`), a simplified copy of the API's
 * `SpecimenWorkflow`: statuses only move forward (Taken → Sent → Received → Reported) or close early (rejected by the
 * lab, lost, entered in error), every change carries `expectedVersion`, and the last stage can't be finished while a
 * specimen is open. MedApp tracks specimens; it never reads or interprets a report. Simplified: no report uploads (no
 * files in the light demo, so a specimen is reported only in the seed) and no QR code on the label.
 */

const OPEN: SpecimenStatus[] = ['Taken', 'Sent', 'Received'];
export const isOpenSpecimen = (specimen: DemoSpecimen) => OPEN.includes(specimen.status);

const DESCRIBE: Record<SpecimenStatus, string> = {
  Taken: 'taken',
  Sent: 'sent',
  Received: 'received',
  Reported: 'reported',
  RejectedByLab: 'rejected by the lab',
  Lost: 'lost',
  EnteredInError: 'entered in error',
};

const REJECTION_REASONS = ['Unlabelled', 'Mislabelled', 'Leaked', 'InsufficientSample', 'WrongContainer', 'Other'];

const CHANGED_MEANWHILE = 'Someone else changed this sample while you had it open. Reload it to see their change, then try again.';
const RESTRICTED_TAKE =
  "This patient's record is restricted, so a sample can't be recorded for them until an admin lifts the restriction. Ask an admin what to do with the sample.";
const RESTRICTED_EDIT =
  "This patient's record is restricted: the sample can still go on to the lab and be reported, but its details can't be changed.";
const EIE_NOT_ALLOWED =
  'Only whoever recorded this sample can mark it entered in error, within 24 hours of recording it. After that, ask an admin.';
const MAX_NAME = 100;

const typeOf = (specimen: DemoSpecimen) => db().specimenTypes.find((t) => t.id === specimen.specimenTypeId);
/** The clock of the lab case a sample is in (MC19), which its due date follows once the lab has it. */
const caseClock = (specimen: DemoSpecimen) => {
  const labCase = caseOfSample(specimen);
  return labCase ? clockOf(labCase) : undefined;
};
/** When it's due: its case's due date (MC19), else the type's expected days from when it was taken. */
const expectedBy = (specimen: DemoSpecimen) =>
  caseClock(specimen)?.dueAtUtc ?? addDaysUtc(specimen.takenAtUtc, typeOf(specimen)?.expectedTurnaroundDays ?? 0);
const isOverdueAtLab = (specimen: DemoSpecimen) => isOpenSpecimen(specimen) && Date.parse(nowUtc()) > Date.parse(expectedBy(specimen));
const stageOf = (specimen: DemoSpecimen) =>
  db().pathways.find((p) => p.id === specimen.pathwayId)?.stages.find((s) => s.id === specimen.stageId);
const labTestOf = (specimen: DemoSpecimen) => db().labTests.find((t) => t.id === specimen.labTestId);
const hospitalName = (hospitalId: string | null) => (hospitalId ? (db().hospitals.find((h) => h.id === hospitalId)?.name ?? 'The hospital') : null);
/** Where a sample came from, in words: its clinic's name, or its hospital's. */
const sourceName = (specimen: DemoSpecimen) =>
  specimen.source === 'Hospital' ? hospitalName(specimen.hospitalId) : teamName(specimen.requestingTeamId);

/** How many days before it's expected a sample with no case yet counts as due soon (a case's test sets its own). */
const DUE_SOON_DAYS = 2;

const DUE_STATES: Record<string, string> = {Overdue: 'Breached', DueSoon: 'AtRisk', NotDue: 'OnTime'};

/**
 * Whether a sample's due date matches the samples page's Due filter: open ones only, by its case's clock (MC19: due
 * soon is at risk), else by `expectedBy`.
 */
function matchesDue(specimen: DemoSpecimen, due: string | null | undefined): boolean {
  if (!due) return true;
  if (!isOpenSpecimen(specimen)) return false;
  const clock = caseClock(specimen);
  if (clock && DUE_STATES[due]) return clock.state === DUE_STATES[due];
  const left = Date.parse(expectedBy(specimen)) - Date.parse(nowUtc());
  if (due === 'Overdue') return left < 0;
  if (due === 'DueSoon') return left >= 0 && left <= DUE_SOON_DAYS * 86400_000;
  if (due === 'NotDue') return left > DUE_SOON_DAYS * 86400_000;
  return true;
}

/**
 * Whether the viewer sees a sample (decision 200, MC14): the lab sees every one, a clinic its own team's (or a pathway
 * it can see), and a hospital's only the lab (and admins, who see all).
 */
export function canSeeSample(specimen: DemoSpecimen, scope: TeamScope = myScope()): boolean {
  const lab = labTeamId();
  if (scope.seesAllTeams || (lab !== null && scope.teamIds.includes(lab))) return true;
  return specimen.source === 'Clinic' && canSeeRecord(specimen, scope);
}

const PATHWAY_STATUS_WORDS: Record<string, string> = {Blocked: 'on hold', Completed: 'discharged', Cancelled: 'cancelled', Archived: 'archived'};

/** `SpecimenWorkflow.WhyCantTake`: the stage must be editable (the current one, or one unlocked) and the pathway open. */
function whyCantTake(pathway: DemoPathway, stage: DemoStage): string | null {
  if (!['Unassigned', 'Assigned', 'InProgress', 'InReview'].includes(pathway.status))
    return `A sample can't be recorded while the pathway is ${PATHWAY_STATUS_WORDS[pathway.status] ?? pathway.status}.`;
  const editable = isCurrentStage(pathway, stage) || (stage.completedAtUtc !== null && stage.unlocked);
  return editable ? null : 'A sample can only be recorded at a stage being worked on. Finish the stages before it first.';
}

/** Whether the signed-in person can record a specimen at this stage (the stage's `canRecordSpecimen`). */
export function canRecordSpecimen(pathway: DemoPathway, stage: DemoStage): boolean {
  const flags = patientFlags(pathway.patientId);
  return can('RecordSpecimens') && !flags.isRestricted && !flags.isAnonymised && whyCantTake(pathway, stage) === null;
}

/**
 * The lab's stage gates (`StageGates.WhyCantFinish` and `WhyResultsAwaiting`), called by finishing a stage: why
 * `stage` can't be finished yet, or null. `isLast`: finishing it leaves no stage to do. No override.
 */
export function labGate(pathway: DemoPathway, stage: DemoStage, isLast: boolean): string | null {
  if (isLast) {
    const open = db()
      .specimens.filter((s) => s.pathwayId === pathway.id && isOpenSpecimen(s))
      .sort((a, b) => a.takenAtUtc.localeCompare(b.takenAtUtc) || a.number.localeCompare(b.number));
    if (open.length > 0) {
      const each = open.map((s) => `${s.number} (${typeOf(s)?.name ?? 'sample'}, ${DESCRIBE[s.status]})`).join('; ');
      const count = open.length === 1 ? 'a sample is' : `${open.length} samples are`;
      return `The pathway's last stage can't be finished while ${count} still with the lab: ${each}. Record each one's report, or mark it rejected, lost or entered in error.`;
    }
  }
  if (!isLast && !stage.waitsForResults) return null;
  const awaiting = db()
    .results.filter((r) => r.pathwayId === pathway.id && isAwaitingResult(r))
    .sort((a, b) => a.reportedAtUtc.localeCompare(b.reportedAtUtc) || a.id.localeCompare(b.id));
  if (awaiting.length === 0) return null;
  const count = awaiting.length === 1 ? 'a result awaits' : `${awaiting.length} results await`;
  const which = isLast ? "The pathway's last stage" : 'This stage waits for results: it';
  return `${which} can't be finished while ${count} sign-off (reported ${awaiting.map((r) => ukShortDate(r.reportedAtUtc)).join(', ')}). Sign each one off on the Results page, or mark it entered in error.`;
}

/** Whoever recorded it may mark it entered in error within 24 hours, while they hold RecordSpecimens; CorrectRecords any time. */
const mayMarkEnteredInError = (specimen: DemoSpecimen) =>
  can('CorrectRecords') ||
  (specimen.recordedByUserId === me().id && can('RecordSpecimens') && Date.parse(nowUtc()) < Date.parse(addDaysUtc(specimen.recordedAtUtc, 1)));

/** Where it came from and what it's for, as both responses carry them. */
const sourceFields = (specimen: DemoSpecimen) => ({
  source: specimen.source,
  requestingTeamId: specimen.requestingTeamId,
  requestingTeamName: teamName(specimen.requestingTeamId),
  hospitalId: specimen.hospitalId,
  hospitalName: hospitalName(specimen.hospitalId),
  hospitalRequestId: specimen.hospitalRequestId,
  labTestId: specimen.labTestId,
  labTestName: labTestOf(specimen)?.name ?? null,
  labCaseId: specimen.labCaseId,
});

export function specimenSummary(specimen: DemoSpecimen): Schemas['SpecimenSummaryResponse'] {
  const patient = db().patients.find((p) => p.id === specimen.patientId);
  return {
    id: specimen.id,
    number: specimen.number,
    patientId: specimen.patientId,
    patientName: patientName(specimen.patientId),
    patientNhsNumber: patient?.nhsNumber ?? null,
    patientDateOfBirth: patient?.dateOfBirth,
    ...sourceFields(specimen),
    pathwayId: specimen.pathwayId,
    stageId: specimen.stageId,
    specimenTypeName: typeOf(specimen)?.name ?? null,
    status: specimen.status,
    isOpen: isOpenSpecimen(specimen),
    takenAtUtc: specimen.takenAtUtc,
    sentAtUtc: specimen.sentAtUtc,
    receivedAtUtc: specimen.receivedAtUtc,
    reportedAtUtc: specimen.reportedAtUtc,
    closedAtUtc: specimen.closedAtUtc,
    expectedByUtc: expectedBy(specimen),
    isOverdueAtLab: isOverdueAtLab(specimen),
    clock: caseClock(specimen),
  };
}

function specimenResponse(specimen: DemoSpecimen): Schemas['SpecimenResponse'] {
  const flags = patientFlags(specimen.patientId);
  const open = isOpenSpecimen(specimen);
  const records = can('RecordSpecimens');
  const processes = can('ProcessSpecimens');
  const pathway = db().pathways.find((p) => p.id === specimen.pathwayId);
  const readOnlyReason = !open
    ? `This sample has been ${DESCRIBE[specimen.status]}, so it can't be changed.`
    : flags.isAnonymised
      ? "This patient's record has been anonymised, so it can't be changed."
      : flags.isRestricted
        ? RESTRICTED_EDIT
        : null;
  return {
    id: specimen.id,
    number: specimen.number,
    patientId: specimen.patientId,
    patient: patientBanner(specimen.patientId),
    patientFlags: flags,
    ...sourceFields(specimen),
    pathwayId: specimen.pathwayId,
    pathwayTitle: pathway?.title ?? null,
    stageId: specimen.stageId,
    stageName: stageOf(specimen)?.name ?? null,
    specimenTypeId: specimen.specimenTypeId,
    specimenTypeName: typeOf(specimen)?.name ?? null,
    expectedTurnaroundDays: typeOf(specimen)?.expectedTurnaroundDays ?? 0,
    site: specimen.site,
    requestNotes: specimen.requestNotes,
    requestingUserId: specimen.requestingUserId,
    requestingUsername: usernameOf(specimen.requestingUserId),
    status: specimen.status,
    isOpen: open,
    takenAtUtc: specimen.takenAtUtc,
    takenByUsername: usernameOf(specimen.takenByUserId),
    sentAtUtc: specimen.sentAtUtc,
    sentByUsername: usernameOf(specimen.sentByUserId),
    courierReference: specimen.courierReference,
    receivedAtUtc: specimen.receivedAtUtc,
    receivedByUsername: usernameOf(specimen.receivedByUserId),
    labReference: specimen.labReference,
    reportedAtUtc: specimen.reportedAtUtc,
    reportedByUsername: usernameOf(specimen.reportedByUserId),
    reportAttachmentId: specimen.reportAttachmentId,
    rejectionReason: specimen.rejectionReason,
    closedDetail: specimen.closedDetail,
    closedAtUtc: specimen.closedAtUtc,
    closedByUsername: usernameOf(specimen.closedByUserId),
    expectedByUtc: expectedBy(specimen),
    isOverdueAtLab: isOverdueAtLab(specimen),
    clock: caseClock(specimen),
    version: specimen.version,
    canEdit: specimen.status === 'Taken' && records && !flags.isRestricted && !flags.isAnonymised,
    canMarkSent: specimen.status === 'Taken' && records && !flags.isAnonymised,
    canMarkReceived: specimen.status === 'Sent' && processes && !flags.isAnonymised,
    canReject: (specimen.status === 'Sent' || specimen.status === 'Received') && processes && !flags.isAnonymised,
    // A report is a file, and the light demo has no file uploads.
    canReport: false,
    canMarkLost: open && (records || processes) && !flags.isAnonymised,
    canMarkEnteredInError: open && mayMarkEnteredInError(specimen) && !flags.isAnonymised,
    readOnlyReason,
    labCaseNumber: caseOfSample(specimen)?.number ?? null,
    canOpenLabCase: canOpenCaseFor(specimen),
    // The full version draws the label's QR code on the server; the light demo shows the number as text only.
    label: {qrPath: null, size: 0},
    otherSpecimensFromStage: db()
      .specimens.filter((s) => specimen.stageId !== null && s.stageId === specimen.stageId && s.id !== specimen.id)
      .sort((a, b) => a.takenAtUtc.localeCompare(b.takenAtUtc))
      .map(specimenSummary),
  };
}

/** The sample, or a 404: also when it's behind the viewer's team wall (`canSeeSample`; the lab sees every one). */
function findSpecimen(id: string): DemoSpecimen {
  return seeSample(db().specimens.find((s) => s.id === id));
}

function seeSample(specimen: DemoSpecimen | undefined): DemoSpecimen {
  if (!specimen) refuse(notFound('The sample'));
  if (!canSeeSample(specimen)) refuseAcrossWall('Specimen', specimen.id, 'The sample');
  return specimen;
}

/** A step that can be taken now: from the version the page showed, and from the status it needs (`WhyCantMove`). */
function forStep(id: string, expectedVersion: number | undefined, to: SpecimenStatus): DemoSpecimen {
  const specimen = findSpecimen(id);
  if (patientFlags(specimen.patientId).isAnonymised) refuse(problem(409, "This patient's record has been anonymised, so it can't be changed."));
  ensureVersion(expectedVersion, specimen.version, CHANGED_MEANWHILE);
  const from = specimen.status;
  const why =
    !isOpenSpecimen(specimen)
      ? `This sample has been ${DESCRIBE[from]}, so it can't be changed.`
      : to === 'Sent' && from !== 'Taken'
        ? `Only a sample not yet sent can be sent; this one has been ${DESCRIBE[from]}.`
        : to === 'Received' && from !== 'Sent'
          ? from === 'Taken'
            ? 'Record the sample as sent before the lab receives it.'
            : `This sample has already been ${DESCRIBE[from]}.`
          : to === 'RejectedByLab' && from === 'Taken'
            ? 'Only a sample sent to the lab can be rejected by it.'
            : null;
  if (why) refuse(problem(409, why));
  return specimen;
}

function moved(specimen: DemoSpecimen) {
  specimen.version++;
}

/** The type and requesting clinician chosen for a specimen (`SpecimenChoices`): a 400 on the field otherwise. */
function checkChoices(
  body: {specimenTypeId?: string; requestingUserId?: string; site?: string | null; requestNotes?: string | null; labTestId?: string | null},
  existing?: DemoSpecimen,
) {
  const errors: Record<string, string[]> = {};
  // The test is optional and never chosen for the clinician (decision 191); an archived one only if it already had it.
  const labTestId = body.labTestId || null;
  const test = labTestId ? db().labTests.find((t) => t.id === labTestId) : undefined;
  if (labTestId && (!test || (test.isArchived && test.id !== existing?.labTestId))) errors.labTestId = ["Choose a lab test that isn't archived, or none."];
  const type = db().specimenTypes.find((t) => t.id === body.specimenTypeId);
  if (!type || (type.isArchived && type.id !== existing?.specimenTypeId)) errors.specimenTypeId = ["Choose a sample type that isn't archived."];
  const requesting = personById(body.requestingUserId);
  if (!requesting || (!requesting.permissions.includes('SignOffResults') && requesting.id !== existing?.requestingUserId))
    errors.requestingUserId = ["Choose a clinician whose roles allow signing off results: they'll get the result."];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  return {
    site: optionalText(body.site, 200, 'site', 'The site'),
    requestNotes: optionalText(body.requestNotes, 1000, 'requestNotes', 'The request notes'),
    labTestId,
  };
}

const VIEW_ORDER: Record<string, {match: (s: DemoSpecimen) => boolean; key: (s: DemoSpecimen) => string; newestFirst?: boolean}> = {
  // MC14's first tab: not yet at the lab (taken, or on its way), oldest taken first.
  ToReceive: {match: (s) => s.status === 'Taken' || s.status === 'Sent', key: (s) => s.takenAtUtc},
  ToSend: {match: (s) => s.status === 'Taken', key: (s) => s.takenAtUtc},
  Expected: {match: (s) => s.status === 'Sent', key: (s) => s.sentAtUtc ?? ''},
  InLab: {match: (s) => s.status === 'Received', key: (s) => s.receivedAtUtc ?? ''},
  Overdue: {match: isOverdueAtLab, key: expectedBy},
  RejectedAndLost: {
    match: (s) => (s.status === 'RejectedByLab' || s.status === 'Lost') && Date.parse(s.closedAtUtc ?? '') > Date.now() - 30 * 86400_000,
    key: (s) => s.closedAtUtc ?? '',
  },
  Reported: {match: (s) => s.status === 'Reported' && Date.parse(s.reportedAtUtc ?? '') > Date.now() - 7 * 86400_000, key: (s) => s.reportedAtUtc ?? ''},
  All: {match: () => true, key: (s) => s.takenAtUtc, newestFirst: true},
};

/** What a sample search asks for: a view, a pathway, a patient, text, the samples page's filters and a column to sort by. */
interface SpecimenListSearch {
  view?: string | null;
  pathwayId?: string | null;
  patientId?: string | null;
  search?: string | null;
  sort?: string | null;
  ascending?: boolean;
  source?: string | null;
  teamId?: string | null;
  labTestId?: string | null;
  status?: string | null;
  due?: string | null;
}

const same = (a: string | null | undefined, b: string | null | undefined) => !b || a?.toLowerCase() === b.toLowerCase();

/** The specimens a search finds, in the view's order or the column asked for (as `SpecimenSearch` in the API). */
function specimenList({view: viewName, pathwayId, patientId, search, sort, ascending = true, source, teamId, labTestId, status, due}: SpecimenListSearch) {
  const view = VIEW_ORDER[viewName ?? ''] ?? VIEW_ORDER.All;
  const term = search?.trim() ?? '';
  const compact = term.replace(/\s/g, '');
  const scope = myScope();
  const items = db()
    .specimens.filter(
      (s) =>
        canSeeSample(s, scope) &&
        view.match(s) &&
        same(s.source, source) &&
        same(s.requestingTeamId, teamId) &&
        same(s.labTestId, labTestId) &&
        same(s.status, status) &&
        matchesDue(s, due) &&
        (!pathwayId || s.pathwayId === pathwayId) &&
        (!patientId || s.patientId === patientId) &&
        (!term ||
          contains(s.number, compact) ||
          contains(patientName(s.patientId), term) ||
          contains(db().patients.find((p) => p.id === s.patientId)?.nhsNumber, compact) ||
          contains(typeOf(s)?.name, term) ||
          contains(labTestOf(s)?.name, term) ||
          contains(sourceName(s), term)),
    )
    .sort((a, b) => {
      const order = view.key(a).localeCompare(view.key(b)) || a.id.localeCompare(b.id);
      return view.newestFirst ? -order : order;
    });
  return (
    sortedBy(items, sort, ascending, {
      number: (s) => s.number,
      patient: (s) => patientSortName(s.patientId),
      status: (s) => STATUS_ORDER.indexOf(s.status),
      source: (s) => sourceName(s) ?? '',
      test: (s) => labTestOf(s)?.name ?? '',
      due: expectedBy,
      time: view.key,
    }) ?? items
  );
}

/** The statuses in the API's stored order, which its status sort follows. */
const STATUS_ORDER: SpecimenStatus[] = ['Taken', 'Sent', 'Received', 'Reported', 'RejectedByLab', 'Lost', 'EnteredInError'];

/** A setup list's name: required, at most 100 characters, and not another's (a 409). */
export function setupName(name: string | null | undefined, others: {id: string; name: string}[], id: string | undefined, taken: (name: string) => string) {
  const trimmed = name?.trim() ?? '';
  if (!trimmed) refuse(invalid({name: ['Give it a name.']}));
  if (trimmed.length > MAX_NAME) refuse(invalid({name: [`The name must be at most ${MAX_NAME} characters.`]}));
  if (others.some((o) => o.id !== id && o.name.toLowerCase() === trimmed.toLowerCase())) refuse(problem(409, taken(trimmed)));
  return trimmed;
}

function typeResponse(type: DemoSpecimenType): Schemas['SpecimenTypeResponse'] {
  return {...type, canBeDeleted: !db().specimens.some((s) => s.specimenTypeId === type.id)};
}

/** A SNOMED CT code, as the API checks it: optional, 6 to 18 digits (a concept ID), never looked up. */
export function snomedCode(value: string | null | undefined, field = 'snomedCode'): string | null {
  const code = value?.trim() ?? '';
  if (!code) return null;
  if (!/^\d{6,18}$/.test(code)) refuse(invalid({[field]: ['A SNOMED CT code is 6 to 18 digits.']}));
  return code;
}

export const byName = <T extends {name: string}>(a: T, b: T) => a.name.localeCompare(b.name);

function registerSetupLists() {
  const findType = (id: string) => db().specimenTypes.find((t) => t.id === id) ?? refuse(notFound(`Sample type '${id}'`));
  const turnaround = (days: number | undefined) => {
    if (!Number.isInteger(days) || days! < 1 || days! > 90) refuse(invalid({expectedTurnaroundDays: ['The expected turnaround must be 1 to 90 days.']}));
    return days!;
  };
  const typeTaken = (name: string) => `There's already a sample type called '${name}'.`;

  on('get', '/api/specimen-types', ({query}) => {
    me();
    const includeArchived = query.get('includeArchived') === 'true';
    return db().specimenTypes.filter((t) => includeArchived || !t.isArchived).sort(byName).map(typeResponse);
  });
  on('post', '/api/specimen-types', ({body}) => {
    requirePermission('ManageSetup');
    const type: DemoSpecimenType = {
      id: newId(),
      name: setupName(body.name, db().specimenTypes, undefined, typeTaken),
      expectedTurnaroundDays: turnaround(body.expectedTurnaroundDays),
      snomedCode: snomedCode(body.snomedCode),
      isArchived: false,
    };
    db().specimenTypes.push(type);
    return typeResponse(type);
  });
  on('put', '/api/specimen-types/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const type = findType(params.id);
    type.name = setupName(body.name, db().specimenTypes, type.id, typeTaken);
    type.expectedTurnaroundDays = turnaround(body.expectedTurnaroundDays);
    type.snomedCode = snomedCode(body.snomedCode);
    return typeResponse(type);
  });
  on('delete', '/api/specimen-types/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const type = findType(params.id);
    if (!typeResponse(type).canBeDeleted)
      return problem(409, `Samples of type '${type.name}' have been recorded, so it can't be deleted. Archive it instead.`);
    db().specimenTypes = db().specimenTypes.filter((t) => t.id !== type.id);
  });
  on('post', '/api/specimen-types/{id}/archive', ({params}) => {
    requirePermission('ManageSetup');
    const type = findType(params.id);
    type.isArchived = true;
    return typeResponse(type);
  });
  on('post', '/api/specimen-types/{id}/restore', ({params}) => {
    requirePermission('ManageSetup');
    const type = findType(params.id);
    type.isArchived = false;
    return typeResponse(type);
  });
}

/** Specimens, the samples page, specimen types, lab tests (`labTests.ts`) and results. */
export function registerLab() {
  registerSetupLists();
  registerLabTests();
  registerResults();
  registerLabCases();

  on('get', '/api/specimens/search', ({query}) => {
    requirePermission('ViewPatients');
    const items = specimenList({
      view: query.get('View'),
      pathwayId: query.get('PathwayId'),
      patientId: query.get('PatientId'),
    });
    return paged(items.map(specimenSummary), query);
  });

  // The Lab page's worklist: searched (the number, the patient, the type) and sorted, the search in the body.
  on('post', '/api/specimens/search', ({body}) => {
    requirePermission('ViewPatients');
    if ((body.search ?? '').length > 200) return invalid({search: ['Search must not exceed 200 characters.']});
    const items = specimenList(body);
    const pages = new URLSearchParams({Page: String(body.page ?? 1), PageSize: String(body.pageSize ?? 10)});
    return paged(items.map(specimenSummary), pages);
  });

  on('get', '/api/specimens/{id}', ({params}) => {
    requirePermission('ViewPatients');
    return specimenResponse(findSpecimen(params.id));
  });

  on('post', '/api/specimens/find', ({body}) => {
    requirePermission('ViewPatients');
    const number = (body.number ?? '').replace(/\s/g, '').toUpperCase();
    if (!number || number.length > 20) return invalid({number: ['Type or scan the sample number (at most 20 characters).']});
    // A clinic can't look up another team's sample, or the hospital's: a 404, as one that doesn't exist.
    const specimen = db().specimens.find((s) => s.number.toUpperCase() === number);
    return specimenResponse(seeSample(specimen));
  });

  on('post', '/api/pathways/{id}/specimens', ({params, body}) => {
    const viewer = me();
    requirePermission('RecordSpecimens');
    const pathway = findPathway(params.id);
    if (!body.stageId) return invalid({stageId: ['Choose the stage that took the sample.']});
    const stage = pathway.stages.find((s) => s.id === body.stageId);
    if (!stage) return notFound('The stage');
    const {site, requestNotes, labTestId} = checkChoices(body);
    if (patientFlags(pathway.patientId).isRestricted || patientFlags(pathway.patientId).isAnonymised) return problem(409, RESTRICTED_TAKE);
    const why = whyCantTake(pathway, stage);
    if (why) return problem(409, why);
    const now = nowUtc();
    const sequence = ++db().specimenSequence;
    const specimen: DemoSpecimen = {
      id: newId(),
      number: specimenNumber(sequence, today()),
      patientId: pathway.patientId,
      source: 'Clinic',
      requestingTeamId: pathway.teamId,
      hospitalId: null,
      hospitalRequestId: null,
      labTestId,
      labCaseId: null,
      pathwayId: pathway.id,
      teamId: pathway.teamId,
      stageId: stage.id,
      specimenTypeId: body.specimenTypeId!,
      site,
      requestNotes,
      requestingUserId: body.requestingUserId!,
      status: 'Taken',
      recordedAtUtc: now,
      recordedByUserId: viewer.id,
      takenAtUtc: stepTime(body.takenAtUtc, null, 'taken', 'takenAtUtc'),
      takenByUserId: viewer.id,
      sentAtUtc: null,
      sentByUserId: null,
      courierReference: null,
      receivedAtUtc: null,
      receivedByUserId: null,
      labReference: null,
      reportedAtUtc: null,
      reportedByUserId: null,
      reportAttachmentId: null,
      rejectionReason: null,
      closedDetail: null,
      closedAtUtc: null,
      closedByUserId: null,
      version: 1,
    };
    db().specimens.push(specimen);
    // Passed to the lab: it sees the whole pathway from now on, and keeps it after hand-back (decision 184).
    passToLab(pathway);
    return specimenResponse(specimen);
  });

  on('put', '/api/specimens/{id}', ({params, body}) => {
    requirePermission('RecordSpecimens');
    const specimen = findSpecimen(params.id);
    ensureVersion(body.expectedVersion, specimen.version, CHANGED_MEANWHILE);
    if (specimen.status !== 'Taken')
      return problem(409, `A sample's details can only be changed before it's sent; this one has been ${DESCRIBE[specimen.status]}.`);
    if (patientFlags(specimen.patientId).isRestricted || patientFlags(specimen.patientId).isAnonymised) return problem(409, RESTRICTED_EDIT);
    const {site, requestNotes, labTestId} = checkChoices(body, specimen);
    if (!body.takenAtUtc) return invalid({takenAtUtc: ['Give the time it was taken.']});
    const takenAtUtc = Date.parse(body.takenAtUtc) === Date.parse(specimen.takenAtUtc) ? specimen.takenAtUtc : stepTime(body.takenAtUtc, null, 'taken', 'takenAtUtc');
    Object.assign(specimen, {
      specimenTypeId: body.specimenTypeId,
      requestingUserId: body.requestingUserId,
      site,
      requestNotes,
      labTestId,
      takenAtUtc,
    });
    moved(specimen);
    return specimenResponse(specimen);
  });

  on('post', '/api/specimens/{id}/sent', ({params, body}) => {
    const viewer = me();
    requirePermission('RecordSpecimens');
    const specimen = forStep(params.id, body.expectedVersion, 'Sent');
    specimen.sentAtUtc = stepTime(body.atUtc, specimen.takenAtUtc, 'sent', 'atUtc');
    specimen.sentByUserId = viewer.id;
    specimen.courierReference = optionalText(body.reference, 50, 'reference', 'The reference');
    specimen.status = 'Sent';
    moved(specimen);
    return specimenResponse(specimen);
  });

  on('post', '/api/specimens/{id}/received', ({params, body}) => {
    const viewer = me();
    requirePermission('ProcessSpecimens');
    const specimen = forStep(params.id, body.expectedVersion, 'Received');
    specimen.receivedAtUtc = stepTime(body.atUtc, specimen.sentAtUtc, 'received', 'atUtc');
    specimen.receivedByUserId = viewer.id;
    specimen.labReference = optionalText(body.reference, 50, 'reference', 'The reference');
    specimen.status = 'Received';
    moved(specimen);
    // With its test known, its lab case opens (or it joins its stage's open one); without, the lab opens one by hand.
    openCaseOnReceipt(specimen, viewer.id);
    return specimenResponse(specimen);
  });

  on('post', '/api/specimens/{id}/rejected', ({params, body}) => {
    const viewer = me();
    requirePermission('ProcessSpecimens');
    const specimen = forStep(params.id, body.expectedVersion, 'RejectedByLab');
    if (!REJECTION_REASONS.includes(body.reason ?? '')) return invalid({reason: [`The reason must be one of: ${REJECTION_REASONS.join(', ')}.`]});
    const detail = optionalText(body.detail, 500, 'detail', 'The detail');
    if (body.reason === 'Other' && !detail) return invalid({detail: ['Say why the lab rejected it.']});
    close(specimen, 'RejectedByLab', viewer.id, detail);
    specimen.rejectionReason = body.reason!;
    notify([specimen.requestingUserId, specimen.takenByUserId], viewer.id, {
      kind: 'SpecimenRejectedByLab',
      title: 'Sample rejected',
      message: `The lab rejected a sample (${typeOf(specimen)?.name ?? 'no type'}).`,
      pathwayId: specimen.pathwayId,
      subjectId: specimen.id,
    });
    return specimenResponse(specimen);
  });

  on('post', '/api/specimens/{id}/lost', ({params, body}) => {
    const viewer = me();
    requirePermission('ViewPatients');
    if (!can('RecordSpecimens') && !can('ProcessSpecimens'))
      return problem(403, "Your roles don't allow recording samples, so you can't mark one lost.");
    const specimen = forStep(params.id, body.expectedVersion, 'Lost');
    const detail = closingDetail(body.detail);
    close(specimen, 'Lost', viewer.id, detail);
    notify([specimen.requestingUserId, specimen.takenByUserId], viewer.id, {
      kind: 'SpecimenLost',
      title: 'Sample lost',
      message: `${viewer.username} marked a sample lost (${typeOf(specimen)?.name ?? 'no type'}).`,
      pathwayId: specimen.pathwayId,
      subjectId: specimen.id,
    });
    return specimenResponse(specimen);
  });

  on('post', '/api/specimens/{id}/entered-in-error', ({params, body}) => {
    const viewer = me();
    requirePermission('ViewPatients');
    const specimen = forStep(params.id, body.expectedVersion, 'EnteredInError');
    if (!mayMarkEnteredInError(specimen)) return problem(403, EIE_NOT_ALLOWED);
    close(specimen, 'EnteredInError', viewer.id, closingDetail(body.detail));
    return specimenResponse(specimen);
  });
}

/** A lost or entered-in-error specimen's detail: required, at most 500 characters. */
function closingDetail(value: string | null | undefined): string {
  const detail = optionalText(value, 500, 'detail', 'The detail');
  if (!detail) refuse(invalid({detail: ['Say what happened.']}));
  return detail;
}

function close(specimen: DemoSpecimen, status: SpecimenStatus, userId: string, detail: string | null) {
  specimen.status = status;
  specimen.closedAtUtc = nowUtc();
  specimen.closedByUserId = userId;
  specimen.closedDetail = detail;
  moved(specimen);
}
