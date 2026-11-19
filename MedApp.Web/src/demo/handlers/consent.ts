import type {Schemas} from '../../api/client';
import {usernameOf} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoConsentForm, DemoConsentRecord} from '../slices/clinical';
import type {DemoPathway, DemoStage} from '../state';
import {db} from '../store';
import {daysFrom, nowUtc, today} from '../time';
import {can, me, requirePermission} from './common';
import {findPathway, pathwayToChange} from './pathways';
import {seeRecord} from './teamWalls';

/**
 * Consent forms (`api/consent-forms`, ManageSetup; archived, not deleted, once used) and consent records, simplified
 * from the API's `ConsentWorkflow`: recorded for a stage still to do on an open pathway, withdrawn, or marked entered in
 * error, each change from the version shown. **MedApp records the clinician's statement; it never assesses capacity or
 * validity.** No scans: attaching one needs a file.
 */

const SIGNED_BY: DemoConsentRecord['signedBy'][] = ['Patient', 'PersonWithParentalResponsibility', 'BestInterestsDecision'];
const OPEN: DemoPathway['status'][] = ['Unassigned', 'Assigned', 'InProgress', 'InReview', 'Blocked'];
const MAX_DETAIL = 500;
const MAX_SIGNED_DAYS_BACK = 365;
const OWN_CORRECTION_MS = 24 * 3600_000;

const STATUS_WORDS: Record<DemoConsentRecord['status'], string> = {Valid: 'valid', Withdrawn: 'withdrawn', EnteredInError: 'entered in error'};

/** The pathway statuses in words, for "Consent can't be recorded while the pathway is ...". */
const PATHWAY_STATUS_WORDS: Partial<Record<DemoPathway['status'], string>> = {
  Completed: 'completed',
  Cancelled: 'cancelled',
  Archived: 'archived',
};

/** Why consent can't be recorded for this stage now, or null (`ConsentWorkflow.WhyCantRecord`). */
function whyCantRecord(pathway: DemoPathway, stage: DemoStage): string | null {
  if (!OPEN.includes(pathway.status)) return `Consent can't be recorded while the pathway is ${PATHWAY_STATUS_WORDS[pathway.status] ?? 'closed'}.`;
  if (stage.skippedAtUtc) return "Consent can't be recorded for a skipped stage.";
  return stage.completedAtUtc ? "Consent can't be recorded for a stage that's already finished." : null;
}

/** Whether the signed-in person can record consent at this stage (the stage's `canRecordConsent`). */
export const canRecordConsentAt = (pathway: DemoPathway, stage: DemoStage) => can('RecordConsent') && whyCantRecord(pathway, stage) === null;

/** Whoever recorded it within 24 hours, or a holder of CorrectRecords at any time. */
function mayMarkEnteredInError(record: DemoConsentRecord, userId: string): boolean {
  return (
    can('CorrectRecords') ||
    (record.recordedByUserId === userId && can('RecordConsent') && Date.now() < new Date(record.recordedAtUtc).getTime() + OWN_CORRECTION_MS)
  );
}

/** A record as the API answers it, for the signed-in person. */
function recordResponse(record: DemoConsentRecord): Schemas['ConsentRecordResponse'] {
  const viewer = me();
  const stage = db()
    .pathways.find((p) => p.id === record.pathwayId)
    ?.stages.find((s) => s.id === record.stageId);
  return {
    id: record.id,
    patientId: record.patientId,
    pathwayId: record.pathwayId,
    stageId: record.stageId,
    stageName: stage?.name ?? null,
    consentFormId: record.consentFormId,
    formName: record.formName,
    formVersion: record.formVersion,
    signedOn: record.signedOn,
    signedBy: record.signedBy,
    recordedByUserId: record.recordedByUserId,
    recordedByUsername: usernameOf(record.recordedByUserId),
    recordedAtUtc: record.recordedAtUtc,
    scanAttachmentId: null,
    status: record.status,
    withdrawnOn: record.withdrawnOn,
    statusDetail: record.statusDetail,
    statusChangedByUsername: usernameOf(record.statusChangedByUserId),
    statusChangedAtUtc: record.statusChangedAtUtc,
    withdrawnAfterStageFinished: Boolean(
      record.status === 'Withdrawn' && stage?.completedAtUtc && record.statusChangedAtUtc && stage.completedAtUtc <= record.statusChangedAtUtc,
    ),
    version: record.version,
    canWithdraw: can('RecordConsent') && record.status === 'Valid',
    canMarkEnteredInError: record.status !== 'EnteredInError' && mayMarkEnteredInError(record, viewer.id),
    // Attaching a scan needs a file: none in the light demo.
    canAttachScan: false,
  };
}

function formResponse(form: DemoConsentForm): Schemas['ConsentFormResponse'] {
  return {...form, canBeDeleted: !db().consentRecords.some((r) => r.consentFormId === form.id)};
}

/** The record, or a 404 (also when its pathway is behind the viewer's team wall); changed only by the pathway's team. */
function findRecord(id: string): DemoConsentRecord {
  const record = seeRecord(db().consentRecords.find((r) => r.id === id), 'ConsentRecord', 'The consent record');
  pathwayToChange(record.pathwayId);
  return record;
}

function findForm(id: string): DemoConsentForm {
  const form = db().consentForms.find((f) => f.id === id);
  if (!form) refuse(notFound('The consent form'));
  return form;
}

function ensureVersion(record: DemoConsentRecord, expectedVersion: number | undefined) {
  if (record.version !== expectedVersion)
    refuse(problem(409, 'Someone else changed this consent record while you had it open. Reload the pathway to see their change, then try again.'));
}

/** "Say what happened." when the detail is missing or too long. */
function requiredDetail(detail: string | null | undefined): string {
  const text = detail?.trim() ?? '';
  if (!text) refuse(invalid({detail: ['Say what happened.']}));
  if (text.length > MAX_DETAIL) refuse(invalid({detail: [`The detail must be at most ${MAX_DETAIL} characters.`]}));
  return text;
}

/** A form's name and version, checked, and free (no other form has both). */
function formInput(body: Partial<Schemas['ConsentFormRequest']>, exceptId?: string) {
  const name = body.name?.trim() ?? '';
  const formVersion = body.formVersion?.trim() ?? '';
  const errors: Record<string, string[]> = {};
  if (!name) errors.name = ['Give it a name.'];
  else if (name.length > 200) errors.name = ['The name must be at most 200 characters.'];
  if (!formVersion) errors.formVersion = ['Give the form’s version, as printed on it.'];
  else if (formVersion.length > 50) errors.formVersion = ['The version must be at most 50 characters.'];
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  const taken = db().consentForms.some(
    (f) => f.id !== exceptId && f.name.toLowerCase() === name.toLowerCase() && f.formVersion.toLowerCase() === formVersion.toLowerCase(),
  );
  if (taken) refuse(problem(409, `There's already a consent form called '${name}', version ${formVersion}.`));
  return {name, formVersion};
}

export function registerConsent() {
  // A pathway's consent records.
  on('get', '/api/pathways/{id}/consent-records', ({params}) => {
    requirePermission('ViewPatients');
    const pathway = findPathway(params.id);
    return db()
      .consentRecords.filter((r) => r.pathwayId === pathway.id)
      .sort((a, b) => a.recordedAtUtc.localeCompare(b.recordedAtUtc) || a.id.localeCompare(b.id))
      .map(recordResponse);
  });
  on('post', '/api/pathways/{id}/consent-records', ({params, body}) => {
    requirePermission('RecordConsent');
    const viewer = me();
    const errors: Record<string, string[]> = {};
    if (!body.stageId) errors.stageId = ['Choose the stage consented to.'];
    if (!body.consentFormId) errors.consentFormId = ['Choose the consent form.'];
    if (!body.signedOn) errors.signedOn = ['Give the date it was signed.'];
    if (!SIGNED_BY.includes(body.signedBy as DemoConsentRecord['signedBy']))
      errors.signedBy = ['Say who signed: the patient, someone with parental responsibility, or a best-interests decision.'];
    if (body.scanAttachmentId) errors.scanAttachmentId = ['No files in the light demo: leave the scan out.'];
    if (Object.keys(errors).length > 0) return invalid(errors);
    const pathway = pathwayToChange(params.id);
    const stage = pathway.stages.find((s) => s.id === body.stageId);
    if (!stage) return notFound('The stage');
    const why = whyCantRecord(pathway, stage);
    if (why) return problem(409, why);
    const form = db().consentForms.find((f) => f.id === body.consentFormId);
    if (!form || form.isArchived) return invalid({consentFormId: ["Choose a consent form that's in use (not archived)."]});
    const signedOn = body.signedOn!;
    if (signedOn > today()) return invalid({signedOn: ["The date it was signed can't be in the future."]});
    if (signedOn < daysFrom(-MAX_SIGNED_DAYS_BACK)) return invalid({signedOn: ['The date it was signed can be at most a year ago.']});
    const record: DemoConsentRecord = {
      id: newId(),
      patientId: pathway.patientId,
      pathwayId: pathway.id,
      teamId: pathway.teamId,
      stageId: stage.id,
      consentFormId: form.id,
      formName: form.name,
      formVersion: form.formVersion,
      signedOn,
      signedBy: body.signedBy as DemoConsentRecord['signedBy'],
      recordedByUserId: viewer.id,
      recordedAtUtc: nowUtc(),
      status: 'Valid',
      withdrawnOn: null,
      statusDetail: null,
      statusChangedByUserId: null,
      statusChangedAtUtc: null,
      version: 1,
    };
    db().consentRecords.push(record);
    return new DemoReply(201, recordResponse(record));
  });
  on('post', '/api/consent-records/{id}/withdraw', ({params, body}) => {
    requirePermission('RecordConsent');
    const viewer = me();
    if (!body.withdrawnOn) return invalid({withdrawnOn: ['Give the date it was withdrawn.']});
    const detail = requiredDetail(body.detail);
    const record = findRecord(params.id);
    ensureVersion(record, body.expectedVersion);
    if (record.status !== 'Valid') return problem(409, `This consent has been ${STATUS_WORDS[record.status]}, so it can't be withdrawn.`);
    if (body.withdrawnOn > today()) return invalid({withdrawnOn: ["The date it was withdrawn can't be in the future."]});
    if (body.withdrawnOn < record.signedOn) return invalid({withdrawnOn: ["The date it was withdrawn can't be before the date it was signed."]});
    Object.assign(record, {
      status: 'Withdrawn',
      withdrawnOn: body.withdrawnOn,
      statusDetail: detail,
      statusChangedByUserId: viewer.id,
      statusChangedAtUtc: nowUtc(),
      version: record.version + 1,
    });
    return recordResponse(record);
  });
  on('post', '/api/consent-records/{id}/entered-in-error', ({params, body}) => {
    const viewer = me();
    const detail = requiredDetail(body.detail);
    const record = findRecord(params.id);
    ensureVersion(record, body.expectedVersion);
    if (record.status === 'EnteredInError') return problem(409, 'This consent record has already been marked entered in error.');
    if (!mayMarkEnteredInError(record, viewer.id))
      return new DemoReply(403, {
        type: 'about:blank',
        title: 'Forbidden',
        status: 403,
        detail: 'Only whoever recorded this consent can mark it entered in error, within 24 hours of recording it. After that, ask an admin.',
      });
    Object.assign(record, {
      status: 'EnteredInError',
      statusDetail: detail,
      statusChangedByUserId: viewer.id,
      statusChangedAtUtc: nowUtc(),
      version: record.version + 1,
    });
    return recordResponse(record);
  });

  // Consent forms.
  on('get', '/api/consent-forms', ({query}) => {
    me();
    const includeArchived = query.get('includeArchived') === 'true';
    return db()
      .consentForms.filter((f) => includeArchived || !f.isArchived)
      .sort((a, b) => a.name.localeCompare(b.name) || a.formVersion.localeCompare(b.formVersion) || a.id.localeCompare(b.id))
      .map(formResponse);
  });
  on('post', '/api/consent-forms', ({body}) => {
    requirePermission('ManageSetup');
    const input = formInput(body);
    const now = nowUtc();
    const form: DemoConsentForm = {id: newId(), ...input, isArchived: false, createdAt: now, lastUpdated: now};
    db().consentForms.push(form);
    return new DemoReply(201, formResponse(form));
  });
  on('put', '/api/consent-forms/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const form = findForm(params.id);
    Object.assign(form, formInput(body, form.id), {lastUpdated: nowUtc()});
    return formResponse(form);
  });
  on('delete', '/api/consent-forms/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const form = findForm(params.id);
    if (db().consentRecords.some((r) => r.consentFormId === form.id))
      return problem(409, `Consent has been recorded on '${form.name}', so it can't be deleted. Archive it instead.`);
    db().consentForms = db().consentForms.filter((f) => f.id !== form.id);
    return undefined;
  });
  const setArchived =
    (isArchived: boolean) =>
    ({params}: {params: Record<string, string>}) => {
      requirePermission('ManageSetup');
      const form = findForm(params.id);
      if (form.isArchived !== isArchived) Object.assign(form, {isArchived, lastUpdated: nowUtc()});
      return formResponse(form);
    };
  on('post', '/api/consent-forms/{id}/archive', setArchived(true));
  on('post', '/api/consent-forms/{id}/restore', setArchived(false));
}

/** The consent form whose valid record on a pathway is its photo consent (MC27), by name, any version. */
export const CLINICAL_PHOTOGRAPHY_FORM = 'Clinical photography';

/** A stage that takes clinical photographs (the Dermatology templates' Take photographs): its gate needs photo consent. */
export const isPhotoStage = (stage: Pick<DemoStage, 'name'>) => /photograph/i.test(stage.name);

/** A record on the "Clinical photography" form (any version). */
export const isPhotoConsent = (record: Pick<DemoConsentRecord, 'formName'>) =>
  record.formName.trim().toLowerCase() === CLINICAL_PHOTOGRAPHY_FORM.toLowerCase();

/**
 * The consent gate (`StageGates.WhyConsentMissing`): a stage that needs consent can't be finished without a Valid
 * record for it. A photographs stage needs the patient's consent to clinical photography: a Valid record on the
 * "Clinical photography" form anywhere on the pathway (MC27, the photo consent a clinical photo needs). Null when it
 * can. No override.
 */
export function whyConsentMissing(stage: DemoStage, pathwayId?: string): string | null {
  if (!stage.needsConsent) return null;
  if (isPhotoStage(stage)) {
    const photo = db().consentRecords.filter((r) => (r.stageId === stage.id || r.pathwayId === pathwayId) && isPhotoConsent(r));
    if (photo.some((r) => r.status === 'Valid')) return null;
    const message = `This stage needs the patient's consent to clinical photography before it can be finished: record it on the ${CLINICAL_PHOTOGRAPHY_FORM} form from the pathway's Consent card.`;
    return photo.some((r) => r.status === 'Withdrawn') ? `${message} The photo consent recorded has been withdrawn.` : message;
  }
  const forStage = db().consentRecords.filter((r) => r.stageId === stage.id);
  if (forStage.some((r) => r.status === 'Valid')) return null;
  const message = "This stage needs the patient's consent recorded before it can be finished: record it from the pathway's Consent card.";
  return forStage.some((r) => r.status === 'Withdrawn') ? `${message} The consent recorded for it has been withdrawn.` : message;
}
