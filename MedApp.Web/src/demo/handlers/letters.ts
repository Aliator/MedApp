import type {Schemas} from '../../api/client';
import {personById, usernameOf, type DemoPerson} from '../accounts';
import {DemoReply, invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoLetter, DemoLetterRecipient, DemoLetterTemplate} from '../slices/clinical';
import {db} from '../store';
import {daysFrom, nowUtc, today, ukDateOf} from '../time';
import {can, contains, me, NO_FLAGS, paged, pathwayPatient, patientName, patientSortName, requirePermission, sortedBy} from './common';
import {findPathway} from './pathways';

/**
 * Letter templates and letters, with simplified rules from the API's `LetterWorkflow`: a draft is its author's alone,
 * submitted to someone else who approves letters (or approved by its author), returned with a comment, discarded,
 * withdrawn after approval and corrected. **Nothing is sent:** there's no email or post, so a letter is recorded as
 * sent by hand (`record-sent`), and secure email refuses. No PDFs: the preview and the approved letter's PDF aren't
 * made. Merge fields are the API's fixed list, identity and administrative only, never a clinical value.
 */

const LETTER_KINDS = ['ClinicLetter', 'DischargeSummary', 'ReferralResponse', 'InformationRequest', 'AppointmentLetter', 'Other'];
const REFERRAL_KINDS = ['ReferralResponse', 'InformationRequest'];
const RECORDED_METHODS = ['Post', 'HandedToPatient', 'EmailOutsideMedApp', 'Other'];
const RECIPIENT_KINDS: DemoLetterRecipient['kind'][] = ['GpPractice', 'Patient', 'Referrer', 'Other'];
const MAX_RECIPIENTS = 10;
const MAX_BODY = 20000;
const MAX_PLAIN = 1000;
const MAX_COMMENT = 500;
const MAX_DAYS_BACK = 7;

/** The API's merge fields (`LetterMergeFields`): identity and administrative data only. */
export const MERGE_FIELDS: Schemas['LetterMergeFieldResponse'][] = [
  {name: 'patient.name', label: "Patient's name", description: "The patient's full name, as on their record.", example: 'Sam Example'},
  {name: 'patient.dateOfBirth', label: "Patient's date of birth", description: "The patient's date of birth.", example: '1 January 1970'},
  {
    name: 'patient.nhsNumber',
    label: "Patient's NHS number",
    description: "The patient's NHS number, in groups of three, three and four.",
    example: '999 000 0018',
  },
  {
    name: 'patient.address',
    label: "Patient's address",
    description: "The patient's address and postcode, on separate lines.",
    example: '1 Example Street\nExampletown\nZZ1 1ZZ',
  },
  {name: 'practice.name', label: "GP practice's name", description: 'The name of the GP practice the patient is registered with.', example: 'Example Medical Practice'},
  {
    name: 'practice.address',
    label: "GP practice's address",
    description: "That practice's address and postcode, on separate lines.",
    example: '2 Sample Road\nExampletown\nZZ1 2ZZ',
  },
  {name: 'referral.receivedDate', label: 'Referral received', description: 'The date the referral the letter is about was received.', example: '3 March 2026'},
  {
    name: 'appointment.lastAttendedDate',
    label: 'Last appointment attended',
    description: "The date of the patient's last attended appointment on the pathway.",
    example: '10 March 2026',
  },
  {name: 'author.name', label: "Author's name", description: 'The full name of the member of staff who wrote the letter.', example: 'Alex Author'},
  {name: 'author.jobTitle', label: "Author's job title", description: 'Their job title.', example: 'Consultant Dermatologist'},
  {name: 'approver.name', label: "Approver's name", description: 'The full name of the member of staff who approved the letter.', example: 'Jo Approver'},
  {name: 'approver.jobTitle', label: "Approver's job title", description: 'Their job title.', example: 'Consultant Dermatologist'},
  {name: 'today', label: "Today's date", description: 'The date the letter is rendered.', example: '18 November 2026'},
  {name: 'site.name', label: "Site's name", description: 'The name of the site the letter is sent from.', example: 'Main site'},
];

const FIELD = /\{\{[ \t]*([A-Za-z0-9.]+)[ \t]*\}\}/g;

/** The merge fields some text uses, by name, in order, once each. */
function usedFields(...texts: (string | null | undefined)[]): string[] {
  const names = texts.flatMap((text) => [...(text ?? '').matchAll(FIELD)].map((match) => match[1]));
  return [...new Set(names)];
}

/** The API's words for unknown merge fields in some text, or null. */
function unknownFieldsMessage(text: string | null | undefined): string | null {
  const unknown = usedFields(text)
    .filter((name) => !MERGE_FIELDS.some((field) => field.name === name))
    .map((name) => `{{${name}}}`);
  if (unknown.length === 0) return null;
  return unknown.length === 1
    ? `${unknown[0]} isn't a merge field. Choose one from the list, or remove the double braces.`
    : `${unknown.join(', ')} aren't merge fields. Choose them from the list, or remove the double braces.`;
}

/** "Discharge summary", for messages (`LetterKinds.Describe`). */
const KIND_WORDS: Record<string, string> = {
  ClinicLetter: 'Clinic letter',
  DischargeSummary: 'Discharge summary',
  ReferralResponse: 'Referral response',
  InformationRequest: 'Request for more information',
  AppointmentLetter: 'Appointment letter',
};

/** A letter kind in a message, starting lower case: "discharge summary" (`LetterWorkflow.KindInMessage`). */
export const kindInMessage = (kind: string) => {
  const words = KIND_WORDS[kind] ?? 'Letter';
  return words.charAt(0).toLowerCase() + words.slice(1);
};

const article = (words: string) => ('aeiou'.includes(words.charAt(0)) ? 'an' : 'a');

const STATUS_WORDS: Record<DemoLetter['status'], string> = {
  Draft: 'a draft',
  AwaitingApproval: 'awaiting approval',
  Approved: 'approved',
  Withdrawn: 'withdrawn',
};

const notInStatus = (letter: DemoLetter, action: string) =>
  problem(409, `This letter is ${STATUS_WORDS[letter.status]}, so it can't be ${action}. Reload it to see where it is.`);

const CHANGED_MEANWHILE = 'Someone changed this letter while you had it open. Reload it to see the change, then try again.';
const NOT_AUTHOR = "Only the letter's author can change it while it's a draft.";
const NOT_APPROVER =
  'Only the person it was submitted to can approve or return this letter. Ask them, or ask its author to submit it to you.';
const NO_RECIPIENTS = 'Add at least one recipient before the letter is approved.';
/** Why nothing is sent from the light demo, for a recipient still to send. */
export const NO_SENDING = "Sending isn't available in the light demo: there's no email or post. Record it as sent instead.";

const forbidden = (detail: string) => new DemoReply(403, {type: 'about:blank', title: 'Forbidden', status: 403, detail});

const teamName = (teamId: string | null | undefined) =>
  teamId ? (db().teams.find((s) => s.id === teamId)?.name ?? null) : null;

/** A template as the API answers it: its team's name, the fields it uses, and whether it can be deleted. */
function templateResponse(template: DemoLetterTemplate): Schemas['LetterTemplateResponse'] {
  return {
    ...template,
    teamName: teamName(template.teamId),
    mergeFields: usedFields(template.letterhead, template.bodySkeleton, template.closing),
    canBeDeleted: !db().letters.some((l) => l.templateId === template.id),
  };
}

function findTemplate(id: string): DemoLetterTemplate {
  const template = db().letterTemplates.find((t) => t.id === id);
  if (!template) refuse(notFound('The letter template'));
  return template;
}

/** Each team's count of letter templates for it. */
function countTemplates() {
  for (const team of db().teams)
    team.letterTemplateCount = db().letterTemplates.filter((t) => t.teamId === team.id).length;
}

/** A template's text checked as the API does: required parts, lengths, known merge fields. */
function templateInput(body: Partial<Schemas['CreateLetterTemplateRequest']>) {
  const errors: Record<string, string[]> = {};
  const name = body.name?.trim() ?? '';
  if (!name) errors.name = ['Name is required.'];
  else if (name.length > 200) errors.name = ['Name must not exceed 200 characters.'];
  if (!LETTER_KINDS.includes(body.kind ?? '')) errors.kind = [`Kind must be one of: ${LETTER_KINDS.join(', ')}.`];
  if (body.teamId && !db().teams.some((s) => s.id === body.teamId))
    errors.teamId = ["Team id must be a team's, or left out for every team."];
  const parts = {letterhead: [body.letterhead, MAX_PLAIN], bodySkeleton: [body.bodySkeleton, MAX_BODY], closing: [body.closing, MAX_PLAIN]} as const;
  for (const [field, [text, max]] of Object.entries(parts)) {
    if ((text ?? '').length > max) errors[field] = [`It must not exceed ${max} characters.`];
    const unknown = unknownFieldsMessage(text);
    if (unknown) errors[field] = [unknown];
  }
  if (Object.keys(errors).length > 0) refuse(invalid(errors));
  return {
    name,
    kind: body.kind!,
    teamId: body.teamId || null,
    letterhead: (body.letterhead ?? '').trimEnd(),
    bodySkeleton: (body.bodySkeleton ?? '').trimEnd(),
    closing: (body.closing ?? '').trimEnd(),
  };
}

/** A 409 when another template for the same team (or for every team) has the name. */
function ensureNameFree(name: string, teamId: string | null, exceptId?: string) {
  const taken = db().letterTemplates.some(
    (t) => t.id !== exceptId && t.teamId === teamId && t.name.toLowerCase() === name.toLowerCase(),
  );
  if (!taken) return;
  const team = teamName(teamId);
  refuse(
    problem(409, team ? `A ${team} letter template is already called '${name}'.` : `A letter template for every team is already called '${name}'.`),
  );
}

function ensureTemplateVersion(template: DemoLetterTemplate, expectedVersion: number | undefined) {
  if (template.version !== expectedVersion)
    refuse(problem(409, 'Someone changed this letter template while you had it open. Reload to see the change, then try again.'));
}

/** The letter, or a 404. */
function findLetter(id: string): DemoLetter {
  const letter = db().letters.find((l) => l.id === id);
  if (!letter) refuse(notFound('The letter'));
  return letter;
}

function ensureVersion(letter: DemoLetter, expectedVersion: number | undefined) {
  if (letter.version !== expectedVersion) refuse(problem(409, CHANGED_MEANWHILE));
}

/** Marks a change: a new version, and when. */
function changed(letter: DemoLetter) {
  letter.version++;
  letter.lastUpdated = nowUtc();
}

const isAuthorsDraft = (letter: DemoLetter, viewer: DemoPerson) =>
  letter.status === 'Draft' && letter.authorUserId === viewer.id && can('WriteLetters');

const canApprove = (letter: DemoLetter, viewer: DemoPerson) =>
  can('ApproveLetters') &&
  (letter.status === 'AwaitingApproval' ? letter.approverUserId === viewer.id : letter.status === 'Draft' && letter.authorUserId === viewer.id);

const canWithdraw = (letter: DemoLetter, viewer: DemoPerson) =>
  letter.status === 'Approved' && ((letter.approverUserId === viewer.id && can('ApproveLetters')) || can('CorrectRecords'));

const correctionOf = (letter: DemoLetter) => db().letters.find((l) => l.replacesLetterId === letter.id);

const canBeCorrected = (letter: DemoLetter) => letter.status === 'Withdrawn' && letter.approvedAtUtc !== null && !correctionOf(letter);

const isSentToEveryone = (letter: DemoLetter) => letter.recipients.length > 0 && letter.recipients.every((r) => r.dispatchStatus === 'Sent');

/** A letter in a list (`LetterSummaryResponse`). */
/** The statuses in the API's stored order, which its status sort follows. */
const LETTER_STATUS_ORDER: DemoLetter['status'][] = ['Draft', 'AwaitingApproval', 'Approved', 'Withdrawn'];

function letterSummary(letter: DemoLetter): Schemas['LetterSummaryResponse'] {
  const pathway = letter.pathwayId ? db().pathways.find((p) => p.id === letter.pathwayId) : undefined;
  return {
    id: letter.id,
    patientId: letter.patientId,
    patientName: patientName(letter.patientId),
    pathwayId: letter.pathwayId,
    pathwayTitle: pathway?.title ?? null,
    referralId: letter.referralId,
    kind: letter.kind,
    templateName: letter.templateName,
    status: letter.status,
    isReturned: letter.status === 'Draft' && letter.returnComment !== null,
    authorUserId: letter.authorUserId,
    authorUsername: usernameOf(letter.authorUserId) ?? '',
    approverUserId: letter.approverUserId,
    approverUsername: usernameOf(letter.approverUserId),
    createdAt: letter.createdAt,
    lastUpdated: letter.lastUpdated,
    submittedAtUtc: letter.submittedAtUtc,
    approvedAtUtc: letter.approvedAtUtc,
    withdrawnAtUtc: letter.withdrawnAtUtc,
    recipientCount: letter.recipients.length,
    unsentCount: letter.recipients.filter((r) => r.dispatchStatus !== 'Sent').length,
  };
}

/** A letter as the API answers it, for the signed-in person (`LetterMapper.ToResponse`). */
function letterResponse(letter: DemoLetter): Schemas['LetterResponse'] {
  const viewer = me();
  const authorsDraft = isAuthorsDraft(letter, viewer);
  const canSend = can('SendLetters');
  return {
    ...letterSummary(letter),
    patient: pathwayPatient(letter.patientId),
    patientFlags: NO_FLAGS,
    letterhead: letter.letterhead,
    body: letter.body,
    closing: letter.closing,
    largePrint: letter.largePrint,
    returnComment: letter.returnComment,
    returnedAtUtc: letter.returnedAtUtc,
    returnedByUsername: usernameOf(letter.returnedByUserId),
    withdrawnByUsername: usernameOf(letter.withdrawnByUserId),
    withdrawnReason: letter.withdrawnReason,
    // No PDFs in the light demo: the approved letter isn't rendered, and drafts have no preview.
    pdfAttachmentId: null,
    replacesLetterId: letter.replacesLetterId,
    replacedByLetterId: correctionOf(letter)?.id ?? null,
    missingMergeFields: [],
    version: letter.version,
    canEdit: authorsDraft,
    canSubmit: authorsDraft,
    canDiscard: authorsDraft,
    canApprove: canApprove(letter, viewer),
    canReturn: letter.status === 'AwaitingApproval' && letter.approverUserId === viewer.id && can('ApproveLetters'),
    canWithdraw: canWithdraw(letter, viewer),
    canWriteCorrection: can('WriteLetters') && canBeCorrected(letter),
    canPreview: false,
    readOnlyReason: null,
    secureEmailAvailable: false,
    recipients: letter.recipients.map((recipient) => {
      const awaits = letter.status === 'Approved' && recipient.dispatchStatus !== 'Sent';
      return {
        id: recipient.id,
        kind: recipient.kind,
        practiceId: recipient.practiceId,
        name: recipient.name,
        addressLines: recipient.addressLines,
        postcode: recipient.postcode,
        email: recipient.email,
        dispatchStatus: recipient.dispatchStatus,
        method: recipient.method,
        sentOn: recipient.sentOn,
        sentAtUtc: recipient.sentAtUtc,
        sentByUsername: usernameOf(recipient.sentByUserId),
        canSendSecureEmail: false,
        whyNoSecureEmail: awaits ? NO_SENDING : null,
        canRecordSent: canSend && awaits,
      };
    }),
  };
}

/** The patient as a recipient: their name and address copied from their record. */
function patientRecipient(patientId: string): DemoLetterRecipient {
  const patient = db().patients.find((p) => p.id === patientId);
  return blankRecipient({
    kind: 'Patient',
    name: patient ? `${patient.firstName} ${patient.lastName}` : null,
    addressLines: (patient?.addressLines ?? []).join('\n') || null,
    postcode: patient?.postcode ?? null,
  });
}

function blankRecipient(parts: Partial<DemoLetterRecipient> & Pick<DemoLetterRecipient, 'kind'>): DemoLetterRecipient {
  return {
    id: newId(),
    practiceId: null,
    name: null,
    addressLines: null,
    postcode: null,
    email: null,
    dispatchStatus: 'NotSent',
    method: null,
    sentOn: null,
    sentAtUtc: null,
    sentByUserId: null,
    ...parts,
  };
}

/** The referral a letter is about, as its referrer (the clinician and practice the referral names). */
function referrerRecipient(referralId: string, typed?: {addressLines?: string | null; postcode?: string | null}): DemoLetterRecipient {
  const referral = db().referrals.find((r) => r.id === referralId);
  return blankRecipient({
    kind: 'Referrer',
    name: referral ? `${referral.referrerClinician}, ${referral.referrerPractice}` : null,
    addressLines: typed?.addressLines?.trim() || referral?.referrerPractice || null,
    postcode: typed?.postcode?.trim() || null,
  });
}

/** The demo's GP practices, if the setup slice keeps any (it may not: the directory can be empty). */
const practices = () => (db() as unknown as {practices?: Schemas['PracticeResponse'][]}).practices ?? [];

/** A draft's recipients from the editor, checked as the API does, the patient's and practices' details copied. */
function recipientsFrom(letter: DemoLetter, requests: Schemas['LetterRecipientRequest'][]): DemoLetterRecipient[] {
  if (requests.length > MAX_RECIPIENTS) refuse(invalid({recipients: [`A letter has at most ${MAX_RECIPIENTS} recipients.`]}));
  if (requests.filter((r) => r.kind === 'Patient').length > 1) refuse(invalid({recipients: ['The patient can be a recipient only once.']}));
  return requests.map((request) => {
    const kind = request.kind as DemoLetterRecipient['kind'];
    if (!RECIPIENT_KINDS.includes(kind)) refuse(invalid({kind: [`Kind must be one of: ${RECIPIENT_KINDS.join(', ')}.`]}));
    switch (kind) {
      case 'Patient':
        return patientRecipient(letter.patientId);
      case 'Referrer':
        if (!letter.referralId) refuse(invalid({kind: ['The referrer is a recipient only of a letter about a referral.']}));
        return referrerRecipient(letter.referralId, request);
      case 'GpPractice': {
        const practiceId = request.practiceId ?? null;
        // A practice already on the letter (the seed's are copied, with no directory entry) is kept as it is.
        const kept = letter.recipients.find((r) => r.kind === 'GpPractice' && r.practiceId === practiceId);
        if (kept) return {...kept, id: newId()};
        // No practice ID: the patient's own practice that isn't in the directory, copied from the patient.
        if (!practiceId) {
          const unlisted = db().patients.find((p) => p.id === letter.patientId)?.unlistedPractice;
          if (!unlisted)
            refuse(invalid({recipients: ['This patient has no GP practice outside the directory recorded. Choose the practice from the directory.']}));
          return blankRecipient({
            kind,
            name: unlisted.name ?? null,
            addressLines: (unlisted.addressLines ?? []).join('\n') || null,
            postcode: unlisted.postcode ?? null,
          });
        }
        const practice = practices().find((p) => p.id === practiceId);
        if (!practice) refuse(invalid({practiceId: ['Choose the GP practice from the directory.']}));
        return blankRecipient({
          kind,
          practiceId,
          name: practice.name ?? null,
          addressLines: (practice.addressLines ?? []).join('\n') || null,
          postcode: practice.postcode ?? null,
          email: practice.secureEmail ?? null,
        });
      }
      default: {
        const errors: Record<string, string[]> = {};
        if (!request.name?.trim()) errors.name = ['Name is required for another recipient.'];
        if (!request.addressLines?.trim()) errors.addressLines = ['Address is required for another recipient.'];
        if (Object.keys(errors).length > 0) refuse(invalid(errors));
        return blankRecipient({
          kind,
          name: request.name!.trim(),
          addressLines: request.addressLines!.trim(),
          postcode: request.postcode?.trim() || null,
          email: request.email?.trim() || null,
        });
      }
    }
  });
}

/** Tells someone about a letter (a fixed title; the message holds a username and the letter's kind only). */
function tell(userId: string, kind: string, title: string, message: string, letter: DemoLetter) {
  db().notifications.push({
    id: newId(),
    userId,
    kind,
    title,
    message,
    subjectId: letter.id,
    createdAtUtc: nowUtc(),
    isRead: false,
  });
}

/** A recipient in words, for messages. */
const recipientWords = (recipient: DemoLetterRecipient) =>
  recipient.kind === 'Patient' ? 'The patient' : (recipient.name ?? (recipient.kind === 'GpPractice' ? 'The GP practice' : 'A recipient'));

export function registerLetters() {
  // Letter templates.
  on('get', '/api/letter-templates', ({query}) => {
    me();
    const includeArchived = query.get('includeArchived') === 'true';
    return db()
      .letterTemplates.filter((t) => includeArchived || !t.isArchived)
      .map(templateResponse)
      .sort(
        (a, b) =>
          Number(a.teamId !== null) - Number(b.teamId !== null) ||
          (a.teamName ?? '').localeCompare(b.teamName ?? '') ||
          (a.name ?? '').localeCompare(b.name ?? '') ||
          (a.id ?? '').localeCompare(b.id ?? ''),
      );
  });
  on('get', '/api/letter-templates/merge-fields', () => {
    me();
    return MERGE_FIELDS;
  });
  on('get', '/api/letter-templates/{id}', ({params}) => {
    me();
    return templateResponse(findTemplate(params.id));
  });
  on('post', '/api/letter-templates', ({body}) => {
    requirePermission('ManageSetup');
    const input = templateInput(body);
    ensureNameFree(input.name, input.teamId);
    const now = nowUtc();
    const template: DemoLetterTemplate = {id: newId(), ...input, isArchived: false, version: 1, createdAt: now, lastUpdated: now};
    db().letterTemplates.push(template);
    countTemplates();
    return new DemoReply(201, templateResponse(template));
  });
  on('put', '/api/letter-templates/{id}', ({params, body}) => {
    requirePermission('ManageSetup');
    const template = findTemplate(params.id);
    const input = templateInput(body);
    ensureTemplateVersion(template, body.expectedVersion);
    ensureNameFree(input.name, input.teamId, template.id);
    Object.assign(template, input, {version: template.version + 1, lastUpdated: nowUtc()});
    countTemplates();
    return templateResponse(template);
  });
  const setArchived =
    (isArchived: boolean) =>
    ({params, body}: {params: Record<string, string>; body: {expectedVersion?: number}}) => {
      requirePermission('ManageSetup');
      const template = findTemplate(params.id);
      ensureTemplateVersion(template, body.expectedVersion);
      if (template.isArchived !== isArchived) Object.assign(template, {isArchived, version: template.version + 1, lastUpdated: nowUtc()});
      return templateResponse(template);
    };
  on('post', '/api/letter-templates/{id}/archive', setArchived(true));
  on('post', '/api/letter-templates/{id}/restore', setArchived(false));
  on('delete', '/api/letter-templates/{id}', ({params}) => {
    requirePermission('ManageSetup');
    const template = findTemplate(params.id);
    if (db().letters.some((l) => l.templateId === template.id))
      return problem(409, `Letters have been written from the '${template.name}' template, so it can't be deleted. Archive it instead.`);
    db().letterTemplates = db().letterTemplates.filter((t) => t.id !== template.id);
    countTemplates();
    return undefined;
  });

  // Letters: lists and the side nav's count.
  on('get', '/api/letters/search', ({query}) => {
    requirePermission('ViewPatients');
    const viewer = me();
    const view = query.get('View');
    const patientId = query.get('PatientId');
    const pathwayId = query.get('PathwayId');
    const referralId = query.get('ReferralId');
    const views = ['MyDrafts', 'AwaitingMyApproval', 'ReturnedToMe', 'ToSend', 'Sent', 'All'];
    if (view && !views.includes(view)) return invalid({view: [`View must be one of: ${views.join(', ')}.`]});
    if (!view && !patientId && !pathwayId && !referralId) return invalid({view: ['Choose a view, or a patient, pathway or referral.']});
    const search = query.get('Search')?.trim() ?? '';
    if (search.length > 200) return invalid({search: ['Search must not exceed 200 characters.']});
    let letters = db().letters.filter(
      (l) =>
        (!patientId || l.patientId === patientId) &&
        (!pathwayId || l.pathwayId === pathwayId) &&
        (!referralId || l.referralId === referralId) &&
        contains(patientName(l.patientId), search),
    );
    const by = (key: (l: DemoLetter) => string | null, descending = false) => (a: DemoLetter, b: DemoLetter) =>
      ((key(a) ?? '').localeCompare(key(b) ?? '') || a.id.localeCompare(b.id)) * (descending ? -1 : 1);
    switch (view) {
      case 'MyDrafts':
        letters = letters.filter((l) => l.status === 'Draft' && l.authorUserId === viewer.id && l.returnComment === null).sort(by((l) => l.lastUpdated, true));
        break;
      case 'ReturnedToMe':
        letters = letters.filter((l) => l.status === 'Draft' && l.authorUserId === viewer.id && l.returnComment !== null).sort(by((l) => l.returnedAtUtc));
        break;
      case 'AwaitingMyApproval':
        letters = letters.filter((l) => l.status === 'AwaitingApproval' && l.approverUserId === viewer.id).sort(by((l) => l.submittedAtUtc));
        break;
      case 'ToSend':
        letters = letters.filter((l) => l.status === 'Approved' && !isSentToEveryone(l)).sort(by((l) => l.approvedAtUtc));
        break;
      case 'Sent':
        letters = letters.filter((l) => l.status === 'Approved' && isSentToEveryone(l)).sort(by((l) => l.approvedAtUtc, true));
        break;
      default:
        letters = letters.sort(by((l) => l.createdAt, true));
    }
    const sorted = sortedBy(letters, query.get('Sort'), query.get('Ascending') !== 'false', {
      patient: (l) => patientSortName(l.patientId),
      letter: (l) => `${l.kind} ${l.templateName.toLowerCase()}`,
      status: (l) => LETTER_STATUS_ORDER.indexOf(l.status),
      latest: (l) => l.withdrawnAtUtc ?? l.approvedAtUtc ?? (l.status === 'AwaitingApproval' ? l.submittedAtUtc : null) ?? l.lastUpdated,
    });
    return paged((sorted ?? letters).map(letterSummary), query);
  });
  on('get', '/api/letters/count', () => {
    const viewer = me();
    return {
      toApprove: db().letters.filter((l) => l.status === 'AwaitingApproval' && l.approverUserId === viewer.id).length,
      toSend: db().letters.filter((l) => l.status === 'Approved' && !isSentToEveryone(l)).length,
    };
  });
  on('get', '/api/letters/{id}', ({params}) => {
    requirePermission('ViewPatients');
    return letterResponse(findLetter(params.id));
  });

  // Writing and editing a draft.
  on('post', '/api/letters', ({body}) => {
    requirePermission('WriteLetters');
    const viewer = me();
    if (!body.templateId) return invalid({templateId: ['Choose a template.']});
    if (Boolean(body.pathwayId) === Boolean(body.referralId))
      return invalid({pathwayId: ['A letter is about one pathway or one referral: give exactly one.']});
    const template = findTemplate(body.templateId);
    if (template.isArchived) return problem(409, `The '${template.name}' template is archived, so letters can't be started from it. Choose another.`);
    const pathway = body.pathwayId ? findPathway(body.pathwayId) : null;
    const referral = body.referralId ? db().referrals.find((r) => r.id === body.referralId) : null;
    if (body.referralId && !referral) return notFound('The referral');
    const teamId = pathway?.teamId ?? referral!.teamId;
    if (template.teamId && template.teamId !== teamId)
      return invalid({
        templateId: [`The '${template.name}' template is for another team. Choose one of this ${pathway ? 'pathway' : 'referral'}'s team's.`],
      });
    if (referral && !REFERRAL_KINDS.includes(template.kind))
      return invalid({
        templateId: ['From a referral, write a referral response or a request for more information. Write other letters from the pathway.'],
      });
    if (referral?.status === 'EnteredInError') return problem(409, 'This referral was entered in error, so letters can\'t be written about it.');
    const now = nowUtc();
    const patientId = pathway?.patientId ?? referral!.patientId;
    // Recipients start as the patient (and, from a referral, its referrer): the demo's patients have no registered practice.
    const recipients = referral ? [referrerRecipient(referral.id), patientRecipient(patientId)] : [patientRecipient(patientId)];
    const letter: DemoLetter = {
      id: newId(),
      patientId,
      pathwayId: pathway?.id ?? null,
      referralId: referral?.id ?? null,
      templateId: template.id,
      templateName: template.name,
      kind: template.kind,
      letterhead: template.letterhead,
      body: template.bodySkeleton,
      closing: template.closing,
      largePrint: false,
      status: 'Draft',
      authorUserId: viewer.id,
      approverUserId: null,
      createdAt: now,
      lastUpdated: now,
      submittedAtUtc: null,
      approvedAtUtc: null,
      withdrawnAtUtc: null,
      withdrawnByUserId: null,
      withdrawnReason: null,
      returnComment: null,
      returnedAtUtc: null,
      returnedByUserId: null,
      replacesLetterId: null,
      version: 1,
      recipients,
    };
    db().letters.push(letter);
    return new DemoReply(201, letterResponse(letter));
  });
  on('put', '/api/letters/{id}', ({params, body}) => {
    requirePermission('WriteLetters');
    const viewer = me();
    const letter = findLetter(params.id);
    if (letter.authorUserId !== viewer.id) return forbidden(NOT_AUTHOR);
    if (letter.status !== 'Draft') return notInStatus(letter, 'changed');
    const errors: Record<string, string[]> = {};
    const parts = {letterhead: [body.letterhead, MAX_PLAIN], body: [body.body, MAX_BODY], closing: [body.closing, MAX_PLAIN]} as const;
    for (const [field, [text, max]] of Object.entries(parts)) {
      if ((text ?? '').length > max) errors[field] = [`It must not exceed ${max} characters.`];
      const unknown = unknownFieldsMessage(text);
      if (unknown) errors[field] = [unknown];
    }
    if (Object.keys(errors).length > 0) return invalid(errors);
    const recipients = recipientsFrom(letter, body.recipients ?? []);
    ensureVersion(letter, body.expectedVersion);
    Object.assign(letter, {
      letterhead: body.letterhead ?? '',
      body: body.body ?? '',
      closing: body.closing ?? '',
      largePrint: body.largePrint ?? false,
      recipients,
    });
    changed(letter);
    return letterResponse(letter);
  });

  // Approval.
  on('post', '/api/letters/{id}/submit', ({params, body}) => {
    requirePermission('WriteLetters');
    const viewer = me();
    const letter = findLetter(params.id);
    if (!body.approverUserId) return invalid({approverUserId: ['Choose who should approve the letter.']});
    if (letter.authorUserId !== viewer.id) return forbidden(NOT_AUTHOR);
    if (letter.status !== 'Draft') return notInStatus(letter, 'submitted');
    ensureVersion(letter, body.expectedVersion);
    if (letter.recipients.length === 0) return problem(409, NO_RECIPIENTS);
    if (body.approverUserId === viewer.id)
      return invalid({approverUserId: ['Submit the letter to someone else, or approve it yourself if you can approve letters.']});
    if (!personById(body.approverUserId)?.permissions.includes('ApproveLetters'))
      return invalid({approverUserId: ['Choose someone who can approve letters.']});
    Object.assign(letter, {
      status: 'AwaitingApproval',
      approverUserId: body.approverUserId,
      submittedAtUtc: nowUtc(),
      returnComment: null,
      returnedAtUtc: null,
      returnedByUserId: null,
    });
    changed(letter);
    const kind = kindInMessage(letter.kind);
    tell(letter.approverUserId!, 'LetterAwaitingApproval', 'Letter to approve', `${viewer.username} asked you to approve ${article(kind)} ${kind}.`, letter);
    return letterResponse(letter);
  });
  on('post', '/api/letters/{id}/approve', ({params, body}) => {
    const viewer = me();
    const letter = findLetter(params.id);
    if (letter.status === 'AwaitingApproval') {
      if (letter.approverUserId !== viewer.id || !can('ApproveLetters')) return forbidden(NOT_APPROVER);
    } else if (letter.status === 'Draft') {
      if (letter.authorUserId !== viewer.id) return forbidden(NOT_AUTHOR);
      if (!can('ApproveLetters'))
        return forbidden("You can't approve letters (your roles don't give you the Approve letters permission). Submit it to someone who can.");
    } else return notInStatus(letter, 'approved');
    ensureVersion(letter, body.expectedVersion);
    if (letter.recipients.length === 0) return problem(409, NO_RECIPIENTS);
    const noAddress = letter.recipients.find((r) => r.kind !== 'Patient' && !r.addressLines?.trim());
    if (noAddress) return problem(409, `${recipientWords(noAddress)} has no address. Add one, or remove them, before the letter is approved.`);
    const selfApproved = letter.authorUserId === viewer.id;
    Object.assign(letter, {status: 'Approved', approverUserId: viewer.id, approvedAtUtc: nowUtc(), returnComment: null});
    changed(letter);
    if (!selfApproved) tell(letter.authorUserId, 'LetterApproved', 'Letter approved', `${viewer.username} approved your ${kindInMessage(letter.kind)}.`, letter);
    return letterResponse(letter);
  });
  on('post', '/api/letters/{id}/return', ({params, body}) => {
    const viewer = me();
    const letter = findLetter(params.id);
    const comment = body.comment?.trim() ?? '';
    if (!comment) return invalid({comment: ['Comment is required.']});
    if (comment.length > MAX_COMMENT) return invalid({comment: [`Comment must not exceed ${MAX_COMMENT} characters.`]});
    if (letter.status !== 'AwaitingApproval') return notInStatus(letter, 'returned');
    if (letter.approverUserId !== viewer.id || !can('ApproveLetters')) return forbidden(NOT_APPROVER);
    ensureVersion(letter, body.expectedVersion);
    Object.assign(letter, {status: 'Draft', returnComment: comment, returnedAtUtc: nowUtc(), returnedByUserId: viewer.id});
    changed(letter);
    tell(letter.authorUserId, 'LetterReturned', 'Letter returned', `${viewer.username} returned your ${kindInMessage(letter.kind)} with a comment.`, letter);
    return letterResponse(letter);
  });

  // Discarding, withdrawing and correcting.
  on('post', '/api/letters/{id}/discard', ({params, body}) => {
    const viewer = me();
    const letter = findLetter(params.id);
    if (letter.authorUserId !== viewer.id) return forbidden(NOT_AUTHOR);
    if (letter.status !== 'Draft') return notInStatus(letter, 'discarded');
    ensureVersion(letter, body.expectedVersion);
    Object.assign(letter, {status: 'Withdrawn', withdrawnAtUtc: nowUtc(), withdrawnByUserId: viewer.id, withdrawnReason: null});
    changed(letter);
    return letterResponse(letter);
  });
  on('post', '/api/letters/{id}/withdraw', ({params, body}) => {
    const viewer = me();
    const letter = findLetter(params.id);
    const reason = body.reason?.trim() ?? '';
    if (!reason) return invalid({reason: ['Reason is required.']});
    if (reason.length > MAX_COMMENT) return invalid({reason: [`Reason must not exceed ${MAX_COMMENT} characters.`]});
    if (letter.status !== 'Approved')
      return letter.status === 'Withdrawn'
        ? notInStatus(letter, 'withdrawn again')
        : problem(409, 'Only an approved letter can be withdrawn. Its author can discard a draft.');
    if (!canWithdraw(letter, viewer)) return forbidden('Only the person who approved this letter, or someone who can correct records, can withdraw it.');
    ensureVersion(letter, body.expectedVersion);
    Object.assign(letter, {status: 'Withdrawn', withdrawnAtUtc: nowUtc(), withdrawnByUserId: viewer.id, withdrawnReason: reason});
    changed(letter);
    return letterResponse(letter);
  });
  on('post', '/api/letters/{id}/correction', ({params}) => {
    requirePermission('WriteLetters');
    const viewer = me();
    const old = findLetter(params.id);
    if (!canBeCorrected(old))
      return problem(
        409,
        correctionOf(old)
          ? 'A correction of this letter has been written already. Open it from the letter’s page.'
          : 'Only a letter withdrawn after it was approved can be corrected.',
      );
    const now = nowUtc();
    const letter: DemoLetter = {
      ...old,
      id: newId(),
      status: 'Draft',
      authorUserId: viewer.id,
      approverUserId: null,
      createdAt: now,
      lastUpdated: now,
      submittedAtUtc: null,
      approvedAtUtc: null,
      withdrawnAtUtc: null,
      withdrawnByUserId: null,
      withdrawnReason: null,
      returnComment: null,
      returnedAtUtc: null,
      returnedByUserId: null,
      replacesLetterId: old.id,
      version: 1,
      recipients: old.recipients.map((r) => blankRecipient({...r, id: newId(), dispatchStatus: 'NotSent', method: null, sentOn: null, sentAtUtc: null, sentByUserId: null})),
    };
    db().letters.push(letter);
    return new DemoReply(201, letterResponse(letter));
  });

  // Sending: nothing leaves the light demo, so each recipient is recorded as sent by hand.
  on('post', '/api/letters/{id}/recipients/{recipientId}/send', () => {
    requirePermission('SendLetters');
    return problem(409, NO_SENDING);
  });
  on('post', '/api/letters/{id}/recipients/{recipientId}/record-sent', ({params, body}) => {
    requirePermission('SendLetters');
    const viewer = me();
    const letter = findLetter(params.id);
    const recipient = letter.recipients.find((r) => r.id === params.recipientId);
    if (!recipient) return notFound('The recipient');
    if (!RECORDED_METHODS.includes(body.method ?? '')) return invalid({method: [`Method must be one of: ${RECORDED_METHODS.join(', ')}.`]});
    if (body.method === 'HandedToPatient' && recipient.kind !== 'Patient')
      return invalid({method: ['Only the patient can be handed the letter. Choose how it reached this recipient.']});
    if (body.method === 'EmailOutsideMedApp' && recipient.kind === 'Patient')
      return invalid({method: ["Letters aren't emailed to patients: their address isn't verified and the letter would travel unencrypted. Post it or hand it over."]});
    const sentOn = body.sentOn ?? '';
    if (!sentOn) return invalid({sentOn: ['Give the date it was sent.']});
    if (sentOn > today()) return invalid({sentOn: ["The date it was sent can't be in the future."]});
    if (sentOn < daysFrom(-MAX_DAYS_BACK)) return invalid({sentOn: [`The date it was sent must be within the last ${MAX_DAYS_BACK} days.`]});
    if (letter.status !== 'Approved') return notInStatus(letter, 'sent');
    if (letter.approvedAtUtc && sentOn < ukDateOf(letter.approvedAtUtc)) return invalid({sentOn: ["The date it was sent can't be before the letter was approved."]});
    if (recipient.dispatchStatus === 'Sent') return problem(409, `${recipientWords(recipient)} has been sent this letter already. Reload to see when and how.`);
    ensureVersion(letter, body.expectedVersion);
    Object.assign(recipient, {dispatchStatus: 'Sent', method: body.method, sentOn, sentAtUtc: nowUtc(), sentByUserId: viewer.id});
    changed(letter);
    return letterResponse(letter);
  });
}

/**
 * The letter gate (`StageGates.WhyLetterMissing`): a stage that needs a letter of a kind can't be finished until one on
 * the pathway is approved and sent to every recipient. Null when it can.
 */
export function whyLetterMissing(pathwayId: string, needsLetterKind: string | null): string | null {
  if (!needsLetterKind) return null;
  const ofKind = db().letters.filter((l) => l.pathwayId === pathwayId && l.kind === needsLetterKind && l.status !== 'Withdrawn');
  if (ofKind.some((l) => l.status === 'Approved' && isSentToEveryone(l))) return null;
  const what = kindInMessage(needsLetterKind);
  const state =
    ofKind.length === 0
      ? "None has been written yet: write one from the pathway's Letters card."
      : ofKind.some((l) => l.status === 'Approved')
        ? "It's approved, but not yet sent to every recipient: send it, or record how it was sent."
        : ofKind.some((l) => l.status === 'AwaitingApproval')
          ? "It's awaiting approval."
          : "It's still a draft.";
  return `This stage needs ${article(what)} ${what}, approved and sent to every recipient, before it can be finished. ${state}`;
}
