import type {Schemas} from '../../api/client';
import {NO_PATHWAY} from '../../api/attachments';
import {usernameOf} from '../accounts';
import {invalid, notFound, on, problem, refuse} from '../api/http';
import {newId} from '../ids';
import type {DemoHospitalRequest} from '../slices/hospitals';
import {specimenNumber, type DemoLabTest, type DemoResult, type DemoSpecimen} from '../slices/lab';
import {LAB_CHOICES, labStageAfter, type DemoLabCase, type DemoLabCaseStep, type LabChoice, type LabOutcome} from '../slices/labCases';
import {teamName} from '../seed';
import type {DemoPathway, DemoStage} from '../state';
import {db} from '../store';
import {nowUtc} from '../time';
import {logActivity} from './adminLogs';
import {can, contains, me, paged, patientName, patientSortName, requirePermission, sortedBy} from './common';
import {specimenSummary} from './lab';
import {finishLabStage, pickUpLabStage, reopenSampleStage} from './pathways';
import {addDaysUtc, ensureVersion, notify, optionalText, patientBanner, patientFlags, stepTime} from './labCommon';
import {canSeeRecord, labTeamId, myScope, refuseAcrossWall, type TeamScope} from './teamWalls';

/**
 * Lab cases (MC15 of `docs/multi-clinic-plan.md`), a simplified copy of the API's `LabCaseChange`: the lab's unit of
 * work, one request's samples, its steps and one report, for a clinic's samples and the hospital's alike.
 * - **Opened** when the lab receives a clinic sample with a test (its stage's other samples join), by hand for one
 *   without (the lab chooses the test), or with a hospital request (MC18).
 * - **Worked** in order: Accessioned, the choice "Sample adequate?", the test's required steps, then the choice "Needs
 *   more work?" (each extra outcome adds the test's optional steps of its kind, then asks again). The lab member
 *   records each outcome: nothing is chosen or suggested for them (decision 191). Only the lab team works a case
 *   (`ProcessSpecimens`), from the version shown.
 * - **Reported**: a holder of `AuthoriseLabReports` enters the results and authorises them; the samples are reported,
 *   and a clinic case's result goes to the requesting clinician for sign-off. The light demo keeps the results as
 *   text (the full version's report is a file).
 * - **Insufficient**: the case closes as `RepeatRequested`, its samples rejected (insufficient), and the clinic is told
 *   (`RepeatSampleRequested`). On a pathway (MC16) the stage that took the sample is reopened for the repeat, and the
 *   lab stage stays with the lab: the repeat's receipt opens the stage's next case.
 * - **A pathway's lab stage** (MC16): one case per stage. Reaching the stage opens it (waiting for its sample, its test
 *   from the samples taken for it, else chosen by the lab), and receiving the first sample links it and picks the stage
 *   up; a sample received first opens it. Authorising the report finishes the stage with the results in the same
 *   change and hands the pathway back; while the case is open the stage can't be finished by hand.
 * - **Seen** by the lab (all), admins (read only) and a clinic case's own clinic (through its pathway); anyone else gets
 *   a 404. Viewing one logs `LabCaseViewed`; the audit log gets its entries from the cases (`labCaseAuditEntries`),
 *   holding IDs, names and outcomes, never the results or a detail typed.
 */

type Case = Schemas['LabCaseResponse'];

const MAX_DETAIL = 500;
const MAX_WHERE = 200;
const MAX_RESULTS = 4000;

const CHANGED_MEANWHILE = 'Someone else changed this lab case while you had it open. Reload it to see their change, then try again.';

const STATUS_WORDS: Record<string, string> = {
  Reported: 'reported',
  RepeatRequested: 'closed with a repeat sample requested',
  EnteredInError: 'marked entered in error',
  Closed: 'closed',
  AwaitingSurgeryDetails: 'reported',
};

/** An outcome in words, as the page shows it. */
export const OUTCOME_WORDS: Record<LabOutcome, string> = {
  Adequate: 'Adequate',
  Insufficient: 'Insufficient: ask for a repeat',
  NoMoreWork: 'No more work',
  ExtraTests: 'Extra tests (special stains)',
  SecondOpinion: 'A second opinion',
};

/** What a case needs next: its sample (a lab stage's case before it's received), a step, a choice's outcome, or its results. */
export type NextWork =
  | {kind: 'Receive'; name: string}
  | {kind: 'Step'; name: string; testStepId: string | null; stepKind: string | null}
  | {kind: 'Choice'; name: string; choice: LabChoice; outcomes: LabOutcome[]}
  | {kind: 'Report'; name: string};

const testOf = (c: Pick<DemoLabCase, 'labTestId'>) => db().labTests.find((t) => t.id === c.labTestId);
const liveSteps = (c: DemoLabCase) => c.steps.filter((s) => !s.enteredInError);
const samplesOf = (c: DemoLabCase) => db().specimens.filter((s) => s.labCaseId === c.id);

/** The test's optional steps an extra outcome adds: its extra tests, or the second opinion's two. */
function extraSteps(test: DemoLabTest | undefined, outcome: LabOutcome | null): DemoLabTest['steps'] {
  const kinds = outcome === 'ExtraTests' ? ['ExtraTests'] : outcome === 'SecondOpinion' ? ['SentForSecondOpinion', 'SecondOpinionReturned'] : [];
  return (test?.steps ?? []).filter((s) => s.isOptional && kinds.includes(s.kind));
}

/** A choice, with the outcomes its test allows (more work only when the test has steps for it), in a fixed order. */
function choiceOf(choice: LabChoice, test: DemoLabTest | undefined): NextWork {
  const outcomes = LAB_CHOICES[choice].outcomes.filter((o) => (o !== 'ExtraTests' && o !== 'SecondOpinion') || extraSteps(test, o).length > 0);
  return {kind: 'Choice', name: LAB_CHOICES[choice].question, choice, outcomes};
}

/** What an open case needs next (`LabCaseRules.Next`), or null once it's closed. */
export function nextWork(c: DemoLabCase): NextWork | null {
  if (c.status !== 'Open') return null;
  const steps = liveSteps(c);
  const test = testOf(c);
  if (!steps.some((s) => s.kind === 'Received')) return {kind: 'Receive', name: 'Receive the sample'};
  if (!steps.some((s) => s.kind === 'Accessioned')) return {kind: 'Step', name: 'Accessioned', testStepId: null, stepKind: null};
  if (!steps.some((s) => s.choice === 'SampleAdequate')) return choiceOf('SampleAdequate', test);
  for (const t of (test?.steps ?? []).filter((s) => !s.isOptional))
    if (!steps.some((s) => s.testStepId === t.id)) return {kind: 'Step', name: t.name, testStepId: t.id, stepKind: t.kind};
  const at = steps.findLastIndex((s) => s.choice === 'NeedsMoreWork');
  if (at < 0) return choiceOf('NeedsMoreWork', test);
  const last = steps[at];
  if (last.outcome === 'NoMoreWork') return {kind: 'Report', name: 'Results and report'};
  const after = steps.slice(at + 1);
  for (const t of extraSteps(test, last.outcome))
    if (!after.some((s) => s.testStepId === t.id)) return {kind: 'Step', name: t.name, testStepId: t.id, stepKind: t.kind};
  return choiceOf('NeedsMoreWork', test);
}

const isLabMember = (scope: TeamScope) => {
  const lab = labTeamId();
  return lab !== null && scope.teamIds.includes(lab);
};

/** Whether the viewer sees a case: the lab and admins every one, a clinic its own clinic's (through the pathway). */
export function canSeeCase(c: DemoLabCase, scope: TeamScope = myScope()): boolean {
  if (scope.seesAllTeams || isLabMember(scope)) return true;
  return c.source === 'Clinic' && canSeeRecord({teamId: c.requestingTeamId, pathwayId: c.pathwayId}, scope);
}

/** Whether the viewer works lab cases: the lab team, with `ProcessSpecimens` (admins read only). */
const worksCases = (scope: TeamScope = myScope()) => can('ProcessSpecimens') && !scope.seesAllTeams && isLabMember(scope);

function readOnlyReason(c: DemoLabCase): string | null {
  if (c.status !== 'Open') return `This case has been ${STATUS_WORDS[c.status] ?? c.status}, so it can't be changed.`;
  if (patientFlags(c.patientId).isAnonymised) return "This patient's record has been anonymised, so the case can't be changed.";
  if (!worksCases()) return 'Lab cases are worked by the lab team.';
  return null;
}

const mayMarkCaseEnteredInError = (c: DemoLabCase) =>
  c.status === 'Open' &&
  !patientFlags(c.patientId).isAnonymised &&
  (can('CorrectRecords') ||
    (c.openedByUserId === me().id && worksCases() && Date.parse(nowUtc()) < Date.parse(addDaysUtc(c.openedAtUtc, 1))));

function stepName(c: DemoLabCase, s: DemoLabCaseStep): string {
  if (s.kind === 'Received') return 'Received';
  if (s.kind === 'Accessioned') return 'Accessioned';
  if (s.kind === 'Outcome') return s.choice ? LAB_CHOICES[s.choice].question : 'Outcome';
  return testOf(c)?.steps.find((t) => t.id === s.testStepId)?.name ?? 'Step';
}

/** The latest step still standing, which is the only one that can be marked entered in error (not Received). */
const latestLive = (c: DemoLabCase) => liveSteps(c).at(-1);

function stepResponse(c: DemoLabCase, s: DemoLabCaseStep, canWork: boolean): Schemas['LabCaseStepResponse'] {
  const test = testOf(c);
  return {
    id: s.id,
    kind: s.kind,
    name: stepName(c, s),
    testStepId: s.testStepId,
    stepKind: test?.steps.find((t) => t.id === s.testStepId)?.kind ?? null,
    choice: s.choice,
    outcome: s.outcome,
    detail: s.detail,
    atUtc: s.atUtc,
    byUsername: usernameOf(s.byUserId),
    isEnteredInError: s.enteredInError !== null,
    enteredInErrorAtUtc: s.enteredInError?.atUtc ?? null,
    enteredInErrorByUsername: usernameOf(s.enteredInError?.byUserId),
    errorDetail: s.enteredInError?.detail ?? null,
    canMarkEnteredInError: canWork && s.kind !== 'Received' && !s.enteredInError && latestLive(c)?.id === s.id && s.byUserId === me().id,
  };
}

const hospitalNameOf = (hospitalId: string | null) => (hospitalId ? (db().hospitals.find((h) => h.id === hospitalId)?.name ?? 'The hospital') : null);
const sourceName = (c: DemoLabCase) => (c.source === 'Hospital' ? hospitalNameOf(c.hospitalId) : teamName(c.requestingTeamId));

export function caseResponse(c: DemoLabCase): Case {
  const test = testOf(c);
  const pathway = db().pathways.find((p) => p.id === c.pathwayId);
  const next = nextWork(c);
  const reason = readOnlyReason(c);
  const canWork = reason === null;
  return {
    id: c.id,
    number: c.number,
    status: c.status,
    source: c.source,
    labTestId: c.labTestId,
    labTestName: test?.name ?? null,
    labTestKind: test?.kind ?? null,
    patientId: c.patientId,
    patient: patientBanner(c.patientId),
    patientFlags: patientFlags(c.patientId),
    requestingTeamId: c.requestingTeamId,
    requestingTeamName: teamName(c.requestingTeamId),
    hospitalId: c.hospitalId,
    hospitalName: hospitalNameOf(c.hospitalId),
    hospitalRequestId: c.hospitalRequestId,
    pathwayId: c.pathwayId,
    pathwayTitle: pathway?.title ?? null,
    stageId: c.stageId,
    stageName: pathway?.stages.find((s) => s.id === c.stageId)?.name ?? null,
    openedAtUtc: c.openedAtUtc,
    openedByUsername: usernameOf(c.openedByUserId),
    receivedAtUtc: c.receivedAtUtc,
    steps: c.steps.map((s) => stepResponse(c, s, canWork)),
    next: next
      ? {
          kind: next.kind,
          name: next.name,
          testStepId: next.kind === 'Step' ? next.testStepId : null,
          stepKind: next.kind === 'Step' ? next.stepKind : null,
          choice: next.kind === 'Choice' ? {choice: next.choice, question: next.name, outcomes: next.outcomes} : undefined,
        }
      : undefined,
    samples: samplesOf(c)
      .sort((a, b) => a.takenAtUtc.localeCompare(b.takenAtUtc) || a.number.localeCompare(b.number))
      .map(specimenSummary),
    reportText: c.report?.text ?? null,
    reportedAtUtc: c.report?.atUtc ?? null,
    reportedByUsername: usernameOf(c.report?.byUserId),
    resultId: c.report?.resultId ?? null,
    repeatDetail: c.repeat?.detail ?? null,
    repeatRequestedAtUtc: c.repeat?.atUtc ?? null,
    repeatRequestedByUsername: usernameOf(c.repeat?.byUserId),
    errorDetail: c.enteredInError?.detail ?? null,
    enteredInErrorAtUtc: c.enteredInError?.atUtc ?? null,
    enteredInErrorByUsername: usernameOf(c.enteredInError?.byUserId),
    version: c.version,
    canRecord: canWork && next !== null && next.kind !== 'Report' && next.kind !== 'Receive',
    canAuthorise: canWork && next?.kind === 'Report' && can('AuthoriseLabReports'),
    canMarkEnteredInError: mayMarkCaseEnteredInError(c),
    readOnlyReason: reason,
  };
}

function caseSummary(c: DemoLabCase): Schemas['LabCaseSummaryResponse'] {
  const next = nextWork(c);
  return {
    id: c.id,
    number: c.number,
    status: c.status,
    source: c.source,
    sourceName: sourceName(c),
    patientId: c.patientId,
    patientName: patientName(c.patientId),
    patientNhsNumber: db().patients.find((p) => p.id === c.patientId)?.nhsNumber ?? null,
    labTestName: testOf(c)?.name ?? null,
    nextName: next?.name ?? null,
    nextKind: next?.kind ?? null,
    openedAtUtc: c.openedAtUtc,
    reportedAtUtc: c.report?.atUtc ?? null,
  };
}

/** The case, or a 404: also behind the viewer's team wall (recorded). */
function findCase(id: string): DemoLabCase {
  const c = (db().labCases ?? []).find((x) => x.id === id);
  if (!c) refuse(notFound('The lab case'));
  if (!canSeeCase(c)) refuseAcrossWall('LabCase', c.id, 'The lab case');
  return c;
}

/** Refuses work on cases to anyone but the lab team (admins read only). */
function requireLabWork() {
  requirePermission('ProcessSpecimens');
  if (!worksCases()) refuse(problem(409, 'Lab cases are worked by the lab team.'));
}

/** An open case to work on, from the version the page showed. */
function forWork(id: string, expectedVersion: number | undefined): DemoLabCase {
  requireLabWork();
  const c = findCase(id);
  if (patientFlags(c.patientId).isAnonymised) refuse(problem(409, "This patient's record has been anonymised, so the case can't be changed."));
  ensureVersion(expectedVersion, c.version, CHANGED_MEANWHILE);
  if (c.status !== 'Open') refuse(problem(409, `This case has been ${STATUS_WORDS[c.status] ?? c.status}, so it can't be changed.`));
  return c;
}

const notNext = (next: NextWork | null) =>
  next?.kind === 'Choice'
    ? `Record the outcome of “${next.name}” first.`
    : next?.kind === 'Report'
      ? 'Every step is done: enter the results and authorise the report.'
      : `The next step is ${next?.name ?? 'none'}.`;

function moved(c: DemoLabCase) {
  c.version++;
}

const addStep = (c: DemoLabCase, step: Omit<DemoLabCaseStep, 'id' | 'enteredInError'>) => c.steps.push({id: newId(), enteredInError: null, ...step});

/** A new case for samples the lab has (one sequence for every case and sample, H-73), with its Received step. */
function newCase(samples: DemoSpecimen[], labTestId: string, userId: string, number: string, stageId = samples[0].stageId): DemoLabCase {
  const first = samples[0];
  const now = nowUtc();
  const receivedAtUtc = first.receivedAtUtc ?? now;
  const c: DemoLabCase = {
    id: newId(),
    number,
    labTestId,
    patientId: first.patientId,
    teamId: labTeamId() ?? first.teamId,
    source: first.source,
    requestingTeamId: first.requestingTeamId,
    hospitalId: first.hospitalId,
    hospitalRequestId: first.hospitalRequestId,
    pathwayId: first.pathwayId,
    stageId,
    status: 'Open',
    openedAtUtc: now,
    openedByUserId: userId,
    receivedAtUtc,
    steps: [{id: newId(), kind: 'Received', testStepId: null, choice: null, outcome: null, detail: null, atUtc: receivedAtUtc, byUserId: first.receivedByUserId ?? userId, enteredInError: null}],
    report: null,
    repeat: null,
    enteredInError: null,
    version: 1,
  };
  for (const s of samples) s.labCaseId = c.id;
  db().labCases.push(c);
  return c;
}

/** The case a hospital request opens (MC18), numbered as the request's sample. */
export function openHospitalCase(request: DemoHospitalRequest, sample: DemoSpecimen, userId: string): DemoLabCase {
  const c = newCase([sample], request.labTestId, userId, request.labCaseNumber);
  // The request already named its case's ID: keep it.
  c.id = request.labCaseId;
  sample.labCaseId = c.id;
  return c;
}

/** The stage's other received samples with no case, which join one opened there. */
const fromSameStage = (sample: DemoSpecimen) =>
  db().specimens.filter((s) => s.id !== sample.id && s.stageId !== null && s.stageId === sample.stageId && s.status === 'Received' && !s.labCaseId);

/** The pathway's lab stage a clinic sample goes to (MC16), if it has one. */
const labStageOf = (sample: DemoSpecimen) => {
  const pathway = db().pathways.find((p) => p.id === sample.pathwayId);
  return pathway ? labStageAfter(pathway, sample.stageId, labTeamId()) : undefined;
};

/** A lab stage's open case (MC16: one per stage at a time), if any. */
export const openCaseOfStage = (stageId: string) => (db().labCases ?? []).find((c) => c.status === 'Open' && c.stageId === stageId);

/** The case a lab stage shows (MC16): its open one, else its latest not entered in error. */
export const caseOfStage = (stageId: string) =>
  openCaseOfStage(stageId) ??
  (db().labCases ?? [])
    .filter((c) => c.stageId === stageId && c.status !== 'EnteredInError')
    .sort((a, b) => b.openedAtUtc.localeCompare(a.openedAtUtc))
    .at(0);

const isReceived = (c: DemoLabCase) => liveSteps(c).some((s) => s.kind === 'Received');

/**
 * Reaching a lab stage opens its case (MC16), unless it has an open one: waiting for its sample, with the test of the
 * samples taken for it (none yet: the lab chooses it when the sample comes). Called by the relay's handover.
 */
export function openStageCase(pathway: DemoPathway, stage: DemoStage, userId: string) {
  if (openCaseOfStage(stage.id)) return;
  const lab = labTeamId();
  const forStage = db().specimens.filter(
    (s) => s.pathwayId === pathway.id && !s.labCaseId && ['Taken', 'Sent'].includes(s.status) && labStageAfter(pathway, s.stageId, lab)?.id === stage.id,
  );
  db().labCases.push({
    id: newId(),
    number: specimenNumber(++db().specimenSequence),
    labTestId: forStage.find((s) => s.labTestId)?.labTestId ?? null,
    patientId: pathway.patientId,
    teamId: lab ?? pathway.teamId,
    source: 'Clinic',
    requestingTeamId: pathway.teamId,
    hospitalId: null,
    hospitalRequestId: null,
    pathwayId: pathway.id,
    stageId: stage.id,
    status: 'Open',
    openedAtUtc: nowUtc(),
    openedByUserId: userId,
    receivedAtUtc: null,
    steps: [],
    report: null,
    repeat: null,
    enteredInError: null,
    version: 1,
  });
}

/** Samples join a lab stage's case waiting for them: the first received is its Received step, and picks the stage up. */
function joinStageCase(c: DemoLabCase, samples: DemoSpecimen[], userId: string) {
  const [first] = samples;
  for (const s of samples) {
    s.labCaseId = c.id;
    s.labTestId ??= c.labTestId;
  }
  if (!isReceived(c)) {
    const by = first.receivedByUserId ?? userId;
    c.receivedAtUtc = first.receivedAtUtc ?? nowUtc();
    addStep(c, {kind: 'Received', testStepId: null, choice: null, outcome: null, detail: null, atUtc: c.receivedAtUtc, byUserId: by});
    if (c.pathwayId && c.stageId) pickUpLabStage(c.pathwayId, c.stageId, by);
  }
  moved(c);
}

/**
 * Receiving a clinic sample opens its case or joins one (MC15, MC16). On a pathway with a lab stage, it joins that
 * stage's open case (setting its test if it had none), or opens the stage's case; elsewhere it joins its stage's open
 * case for the same test, or a new one opens. A sample without a test, for a case without one, waits for the lab to
 * choose the test (opening it by hand).
 */
export function openCaseOnReceipt(sample: DemoSpecimen, userId: string) {
  if (sample.source !== 'Clinic' || sample.labCaseId) return;
  const labStage = labStageOf(sample);
  const waiting = labStage ? openCaseOfStage(labStage.id) : undefined;
  if (waiting) {
    waiting.labTestId ??= sample.labTestId;
    if (waiting.labTestId) joinStageCase(waiting, [sample], userId);
    return;
  }
  if (!sample.labTestId) return;
  const open = labStage
    ? undefined
    : db().labCases.find((c) => c.status === 'Open' && c.stageId !== null && c.stageId === sample.stageId && c.labTestId === sample.labTestId);
  if (open) {
    sample.labCaseId = open.id;
    moved(open);
    return;
  }
  const c = newCase([sample], sample.labTestId, userId, specimenNumber(++db().specimenSequence), labStage?.id ?? sample.stageId);
  if (labStage && c.pathwayId) pickUpLabStage(c.pathwayId, labStage.id, sample.receivedByUserId ?? userId);
}

/** Whether the lab can open a case for a sample by hand: received, in none, and the viewer works cases. */
export const canOpenCaseFor = (sample: DemoSpecimen) =>
  sample.status === 'Received' && !sample.labCaseId && worksCases() && !patientFlags(sample.patientId).isAnonymised;

/** The case a sample is in, for its page. */
export const caseOfSample = (sample: DemoSpecimen) => (sample.labCaseId ? db().labCases.find((c) => c.id === sample.labCaseId) : undefined);

const VIEWS: Record<string, {match: (c: DemoLabCase) => boolean; key: (c: DemoLabCase) => string; newestFirst?: boolean}> = {
  Open: {match: (c) => c.status === 'Open', key: (c) => c.openedAtUtc},
  ToReport: {match: (c) => nextWork(c)?.kind === 'Report', key: (c) => c.openedAtUtc},
  Reported: {match: (c) => c.status === 'Reported', key: (c) => c.report?.atUtc ?? '', newestFirst: true},
  RepeatRequested: {match: (c) => c.status === 'RepeatRequested', key: (c) => c.repeat?.atUtc ?? '', newestFirst: true},
  All: {match: () => true, key: (c) => c.openedAtUtc, newestFirst: true},
};

const same = (a: string | null | undefined, b: string | null | undefined) => !b || a?.toLowerCase() === b.toLowerCase();

/** Every audit entry the lab cases make (IDs, names and outcomes; never the results or a detail typed), for `auditEntries`. */
export function labCaseAuditEntries(): Schemas['PatientPathwayAuditEntryResponse'][] {
  const entries: Schemas['PatientPathwayAuditEntryResponse'][] = [];
  for (const c of db().labCases ?? []) {
    const pathway = db().pathways.find((p) => p.id === c.pathwayId);
    const add = (key: string, action: string, atUtc: string, userId: string, details: Record<string, unknown>) =>
      entries.push({
        id: `${c.id}:${key}`,
        patientPathwayId: c.pathwayId ?? NO_PATHWAY,
        pathwayTitle: pathway?.title ?? null,
        patientId: c.patientId,
        stageId: c.stageId,
        stageName: pathway?.stages.find((s) => s.id === c.stageId)?.name ?? null,
        action,
        userId,
        username: usernameOf(userId),
        atUtc,
        details: JSON.stringify({labCaseId: c.id, number: c.number, ...details}),
      });
    add('opened', 'LabCaseOpened', c.openedAtUtc, c.openedByUserId, {labTestId: c.labTestId, test: testOf(c)?.name ?? null, source: c.source});
    for (const s of c.steps) {
      if (s.kind !== 'Received')
        add(`${s.id}:recorded`, 'LabStepRecorded', s.atUtc, s.byUserId, {stepId: s.id, step: stepName(c, s), choice: s.choice, outcome: s.outcome});
      if (s.enteredInError)
        add(`${s.id}:error`, 'LabStepEnteredInError', s.enteredInError.atUtc, s.enteredInError.byUserId, {stepId: s.id, step: stepName(c, s)});
    }
    if (c.repeat) add('repeat', 'RepeatSampleRequested', c.repeat.atUtc, c.repeat.byUserId, {});
    if (c.report) add('report', 'LabReportAuthorised', c.report.atUtc, c.report.byUserId, {resultId: c.report.resultId});
    if (c.enteredInError) add('error', 'LabCaseEnteredInError', c.enteredInError.atUtc, c.enteredInError.byUserId, {});
  }
  return entries;
}

function requiredDetail(value: string | null | undefined, max: number, field: string, missing: string): string {
  const text = optionalText(value, max, field, 'The detail');
  if (!text) refuse(invalid({[field]: [missing]}));
  return text;
}

export function registerLabCases() {
  on('post', '/api/lab-cases/search', ({body}) => {
    requirePermission('ViewPatients');
    if ((body.search ?? '').length > 200) return invalid({search: ['Search must not exceed 200 characters.']});
    const view = VIEWS[body.view ?? ''] ?? VIEWS.Open;
    const term = body.search?.trim() ?? '';
    const compact = term.replace(/\s/g, '');
    const scope = myScope();
    const items = (db().labCases ?? [])
      .filter(
        (c) =>
          canSeeCase(c, scope) &&
          view.match(c) &&
          same(c.source, body.source) &&
          same(c.labTestId, body.labTestId) &&
          (!term ||
            contains(c.number, compact) ||
            contains(patientName(c.patientId), term) ||
            contains(db().patients.find((p) => p.id === c.patientId)?.nhsNumber, compact) ||
            contains(testOf(c)?.name, term) ||
            contains(sourceName(c), term)),
      )
      .sort((a, b) => {
        const order = view.key(a).localeCompare(view.key(b)) || a.id.localeCompare(b.id);
        return view.newestFirst ? -order : order;
      });
    const ordered =
      sortedBy(items, body.sort, body.ascending ?? true, {
        number: (c) => c.number,
        patient: (c) => patientSortName(c.patientId),
        source: (c) => sourceName(c) ?? '',
        test: (c) => testOf(c)?.name ?? '',
        opened: (c) => c.openedAtUtc,
      }) ?? items;
    const page = paged(ordered.map(caseSummary), new URLSearchParams({Page: String(body.page ?? 1), PageSize: String(body.pageSize ?? 10)}));
    for (const c of page.items) logActivity('PatientListed', {patientId: c.patientId});
    return page;
  });

  on('get', '/api/lab-cases/{id}', ({params}) => {
    requirePermission('ViewPatients');
    const scope = myScope();
    const c = findCase(params.id);
    logActivity('LabCaseViewed', {
      patientId: c.patientId,
      pathwayId: c.pathwayId,
      details: JSON.stringify({labCaseId: c.id, access: scope.seesAllTeams ? 'Admin' : isLabMember(scope) ? 'Owner' : 'Pathway'}),
    });
    return caseResponse(c);
  });

  // Opening a case by hand, for a received sample with none (the lab chooses the test; never chosen for it).
  on('post', '/api/lab-cases', ({body}) => {
    requireLabWork();
    const viewer = me();
    const sample = db().specimens.find((s) => s.id === body.specimenId);
    if (!sample) return notFound('The sample');
    if (patientFlags(sample.patientId).isAnonymised) return problem(409, "This patient's record has been anonymised, so a case can't be opened.");
    ensureVersion(body.expectedVersion, sample.version, 'Someone else changed this sample while you had it open. Reload it, then try again.');
    if (sample.labCaseId) return problem(409, `This sample is already in lab case ${caseOfSample(sample)?.number ?? 'another'}.`);
    if (sample.status !== 'Received') return problem(409, 'Only a sample the lab has received can open a lab case.');
    const test = db().labTests.find((t) => t.id === body.labTestId);
    if (!test || test.isArchived) return invalid({labTestId: ['Choose the test from the lab’s catalogue.']});
    // A lab stage's case waiting for its sample takes it, with the test chosen (MC16); else a new case opens.
    const labStage = sample.source === 'Clinic' ? labStageOf(sample) : undefined;
    const waiting = labStage ? openCaseOfStage(labStage.id) : undefined;
    if (waiting && !isReceived(waiting)) {
      waiting.labTestId = test.id;
      joinStageCase(waiting, [sample, ...fromSameStage(sample)], viewer.id);
      return caseResponse(waiting);
    }
    if (waiting) return problem(409, `This sample's lab stage already has an open case (${waiting.number}).`);
    const number = specimenNumber(++db().specimenSequence);
    const c = newCase([sample, ...fromSameStage(sample)], test.id, viewer.id, number, labStage?.id ?? sample.stageId);
    sample.labTestId ??= test.id;
    // A hospital sample's request points at its new case (its first was marked entered in error).
    const request = db().hospitalRequests.find((r) => r.id === sample.hospitalRequestId);
    if (request) {
      request.labCaseId = c.id;
      request.labCaseNumber = c.number;
    }
    return caseResponse(c);
  });

  on('post', '/api/lab-cases/{id}/steps', ({params, body}) => {
    const viewer = me();
    const c = forWork(params.id, body.expectedVersion);
    const next = nextWork(c);
    if (next?.kind !== 'Step') return problem(409, notNext(next));
    const isNext = next.testStepId ? body.testStepId === next.testStepId : body.fixedStep === 'Accessioned' && !body.testStepId;
    if (!isNext) return problem(409, notNext(next));
    const detail =
      next.stepKind === 'SentForSecondOpinion'
        ? requiredDetail(body.detail, MAX_WHERE, 'detail', 'Say where it went for the second opinion.')
        : optionalText(body.detail, MAX_WHERE, 'detail', 'The detail');
    const atUtc = stepTime(body.atUtc, latestLive(c)?.atUtc ?? null, 'done', 'atUtc');
    addStep(c, {kind: next.testStepId ? 'TestStep' : 'Accessioned', testStepId: next.testStepId, choice: null, outcome: null, detail, atUtc, byUserId: viewer.id});
    moved(c);
    return caseResponse(c);
  });

  on('post', '/api/lab-cases/{id}/outcomes', ({params, body}) => {
    const viewer = me();
    const c = forWork(params.id, body.expectedVersion);
    const next = nextWork(c);
    if (next?.kind !== 'Choice' || body.choice !== next.choice) return problem(409, notNext(next));
    const outcome = body.outcome as LabOutcome;
    if (!next.outcomes.includes(outcome)) return invalid({outcome: [`Choose the outcome: ${next.outcomes.map((o) => OUTCOME_WORDS[o]).join(', ')}.`]});
    const now = nowUtc();
    if (outcome === 'Insufficient') {
      const detail = requiredDetail(body.detail, MAX_DETAIL, 'detail', 'Say why the sample is insufficient: the clinic or hospital will need to know.');
      addStep(c, {kind: 'Outcome', testStepId: null, choice: next.choice, outcome, detail, atUtc: now, byUserId: viewer.id});
      c.status = 'RepeatRequested';
      c.repeat = {detail, atUtc: now, byUserId: viewer.id};
      const samples = samplesOf(c);
      for (const s of samples.filter((x) => x.status === 'Received')) {
        s.status = 'RejectedByLab';
        s.rejectionReason = 'InsufficientSample';
        s.closedDetail = detail;
        s.closedAtUtc = now;
        s.closedByUserId = viewer.id;
        s.version++;
      }
      // On a pathway, the stage that took the sample reopens for the repeat (MC16); the lab stage stays with the lab.
      const first = samples[0];
      const reopened = c.source === 'Clinic' && first ? reopenSampleStage(first.pathwayId, first.stageId) : null;
      // The clinic is told, with no patient details or the detail typed; the hospital learns it from the case (MC20).
      if (c.source === 'Clinic' && first)
        notify([first.requestingUserId, first.takenByUserId], viewer.id, {
          kind: 'RepeatSampleRequested',
          title: 'Repeat sample requested',
          message: `The lab asked for a repeat sample (${testOf(c)?.name ?? 'a lab test'}).${reopened ? ` ${reopened} is reopened to take it.` : ''}`,
          pathwayId: c.pathwayId,
          subjectId: first.id,
        });
    } else addStep(c, {kind: 'Outcome', testStepId: null, choice: next.choice, outcome, detail: null, atUtc: now, byUserId: viewer.id});
    moved(c);
    return caseResponse(c);
  });

  on('post', '/api/lab-cases/{id}/report', ({params, body}) => {
    const viewer = me();
    const c = forWork(params.id, body.expectedVersion);
    requirePermission('AuthoriseLabReports');
    const next = nextWork(c);
    if (next?.kind !== 'Report') return problem(409, `The case's steps aren't finished: ${notNext(next)}`);
    const text = optionalText(body.results, MAX_RESULTS, 'results', 'The results');
    if (!text) return invalid({results: ['Enter the results.']});
    const now = nowUtc();
    const samples = samplesOf(c);
    for (const s of samples.filter((x) => x.status === 'Received')) {
      s.status = 'Reported';
      s.reportedAtUtc = now;
      s.reportedByUserId = viewer.id;
      s.version++;
    }
    let resultId: string | null = null;
    const first = samples[0];
    // A clinic case's report becomes a result for the requesting clinician to sign off; the hospital's goes back to the
    // hospital (MC20 records its sending).
    if (c.source === 'Clinic' && first) {
      const typeName = db().specimenTypes.find((t) => t.id === first.specimenTypeId)?.name ?? 'Sample';
      const result: DemoResult = {
        id: newId(),
        patientId: c.patientId,
        pathwayId: c.pathwayId,
        teamId: first.teamId,
        specimenId: first.id,
        source: 'Specimen',
        title: `${typeName} ${first.number}`,
        reportAttachmentId: NO_PATHWAY,
        reportFileName: null,
        reportedAtUtc: now,
        reportedByUserId: viewer.id,
        responsibleUserId: first.requestingUserId,
        status: 'AwaitingSignOff',
        seenAtUtc: null,
        seenByUserId: null,
        signedOffAtUtc: null,
        signedOffByUserId: null,
        action: null,
        actionDetail: null,
        enteredInErrorAtUtc: null,
        enteredInErrorByUserId: null,
        errorDetail: null,
        version: 1,
        labCaseId: c.id,
        reportText: text,
      };
      db().results.push(result);
      resultId = result.id;
      notify([result.responsibleUserId], viewer.id, {
        kind: 'ResultReady',
        title: 'Result ready',
        message: `${viewer.username} reported a ${typeName} sample.`,
        pathwayId: c.pathwayId,
        subjectId: result.id,
      });
    }
    c.status = 'Reported';
    c.report = {text, atUtc: now, byUserId: viewer.id, resultId};
    moved(c);
    // A pathway's lab stage finishes with the results, in the same change, and the pathway goes back (MC16).
    if (c.pathwayId && c.stageId) finishLabStage(c.pathwayId, c.stageId, viewer.id, text, now);
    return caseResponse(c);
  });

  on('post', '/api/lab-cases/{id}/steps/{stepId}/entered-in-error', ({params, body}) => {
    const viewer = me();
    const c = forWork(params.id, body.expectedVersion);
    const step = c.steps.find((s) => s.id === params.stepId);
    if (!step) return notFound('The step');
    if (step.kind === 'Received' || step.enteredInError || latestLive(c)?.id !== step.id)
      return problem(409, 'Only the latest step recorded can be marked entered in error.');
    if (step.byUserId !== viewer.id) return problem(403, 'Only whoever recorded a step can mark it entered in error.');
    const detail = requiredDetail(body.detail, MAX_DETAIL, 'detail', 'Say what was wrong.');
    step.enteredInError = {atUtc: nowUtc(), byUserId: viewer.id, detail};
    moved(c);
    return caseResponse(c);
  });

  on('post', '/api/lab-cases/{id}/entered-in-error', ({params, body}) => {
    requirePermission('ViewPatients');
    const viewer = me();
    const c = findCase(params.id);
    if (patientFlags(c.patientId).isAnonymised) return problem(409, "This patient's record has been anonymised, so the case can't be changed.");
    ensureVersion(body.expectedVersion, c.version, CHANGED_MEANWHILE);
    if (c.status !== 'Open') return problem(409, `This case has been ${STATUS_WORDS[c.status] ?? c.status}, so it can't be changed.`);
    if (!mayMarkCaseEnteredInError(c))
      return problem(403, 'Only whoever opened this case can mark it entered in error, within 24 hours of opening it. After that, ask an admin.');
    const detail = requiredDetail(body.detail, MAX_DETAIL, 'detail', 'Say what was wrong.');
    c.status = 'EnteredInError';
    c.enteredInError = {atUtc: nowUtc(), byUserId: viewer.id, detail};
    // Its samples leave it, so the lab can open the right case for them.
    for (const s of samplesOf(c)) s.labCaseId = null;
    moved(c);
    return caseResponse(c);
  });
}
