import {LAB, STAFF} from '../accounts';
import {PATHWAYS, TEAMS, id} from '../ids';
import {nowUtc} from '../time';
import type {HospitalSlice} from './hospitals';
import {HOSPITAL_LAB_CASES, HOSPITAL_REQUESTS} from './hospitals';
import {LAB_TESTS, RESULTS, SPECIMENS, specimenNumber, type DemoSpecimen, type LabSlice} from './lab';
import type {BaseState, DemoPathway, DemoStage} from '../state';

/**
 * The light demo's lab cases (MC15 of `docs/multi-clinic-plan.md`): the lab's unit of work, one request's samples, its
 * steps and one report, for a clinic's samples and the hospital's alike. Handlers: `handlers/labCases.ts`.
 *
 * A case's work runs: **Received** (when it opens), **Accessioned**, the choice **"Sample adequate?"** (yes, or
 * insufficient: a repeat is asked for), the test's required steps in order, then the choice **"Needs more work?"** (no,
 * extra tests such as special stains, or a second opinion, each adding the test's optional steps of that kind, and
 * asked again after them), then the results are entered and the report authorised. The lab member records each
 * outcome; MedApp never chooses or suggests one (decision 191).
 */

export type LabCaseStatus = 'Open' | 'Reported' | 'AwaitingSurgeryDetails' | 'Closed' | 'RepeatRequested' | 'EnteredInError';

/** The choice points in the lab's own workflow, and their outcomes (in a fixed order, never chosen for anyone). */
export type LabChoice = 'SampleAdequate' | 'NeedsMoreWork';
export type LabOutcome = 'Adequate' | 'Insufficient' | 'NoMoreWork' | 'ExtraTests' | 'SecondOpinion';

export const LAB_CHOICES: Record<LabChoice, {question: string; outcomes: LabOutcome[]}> = {
  SampleAdequate: {question: 'Sample adequate?', outcomes: ['Adequate', 'Insufficient']},
  NeedsMoreWork: {question: 'Needs more work?', outcomes: ['NoMoreWork', 'ExtraTests', 'SecondOpinion']},
};

/** A recorded step: the fixed ones (Received, Accessioned), a step of the test, or a choice's outcome. */
export type LabCaseStepKind = 'Received' | 'Accessioned' | 'TestStep' | 'Outcome';

export interface DemoLabCaseStep {
  id: string;
  kind: LabCaseStepKind;
  /** The test's step (MC13's `LabTestSteps`), for a `TestStep`. */
  testStepId: string | null;
  choice: LabChoice | null;
  outcome: LabOutcome | null;
  /** Free text: where a second opinion went, or why a sample is insufficient. Kept here, never in the audit log. */
  detail: string | null;
  atUtc: string;
  byUserId: string;
  enteredInError: {atUtc: string; byUserId: string; detail: string} | null;
}

export interface DemoLabCase {
  id: string;
  /** From the lab's one sequence (a hospital case shares its sample's number). */
  number: string;
  /** Null only while a lab stage's case waits for a sample with a test (MC16): the lab chooses it if none has one. */
  labTestId: string | null;
  patientId: string;
  /** The lab's: every case is the lab's work (a clinic's is also seen through its pathway). */
  teamId: string;
  source: 'Clinic' | 'Hospital';
  requestingTeamId: string | null;
  hospitalId: string | null;
  hospitalRequestId: string | null;
  pathwayId: string | null;
  /** The pathway's lab stage the case belongs to (MC16; one per stage), else the stage that took the sample. */
  stageId: string | null;
  status: LabCaseStatus;
  openedAtUtc: string;
  openedByUserId: string;
  receivedAtUtc: string | null;
  steps: DemoLabCaseStep[];
  /** The results the lab entered and the report's authorising (free text, like a result's file in the full version). */
  report: {text: string; atUtc: string; byUserId: string; resultId: string | null} | null;
  repeat: {detail: string; atUtc: string; byUserId: string} | null;
  enteredInError: {atUtc: string; byUserId: string; detail: string} | null;
  /** MC20: a hospital case's report, sent back to its request's hospital when authorised (`handlers/hospitalReports.ts`). */
  reportSent?: DemoReportSent | null;
  /** MC20: what the hospital sent after its request (the surgery's date, procedure and outcome, as written). */
  surgeryDetails?: DemoSurgeryDetails[];
  /** MC20: closed once the surgery details came after the report was sent (or none is expected). */
  closed?: {atUtc: string; byUserId: string} | null;
  version: number;
}

/** A hospital case's report as it went back (FHIR UK Core `DiagnosticReport-Lab`'s shape; the letter is `LabReport`). */
export interface DemoReportSent {
  atUtc: string;
  byUserId: string;
  hospitalId: string;
  /** The request's surgeon, as written: the report is addressed from the request, never chosen (H-72). */
  addressedTo: string;
  /** The hospital's `LetterDispatchMethod`, by name. */
  method: string;
}

/** Surgery details from the hospital, as written (free text: kept here, never in the audit log; nothing is worked out from them). */
export interface DemoSurgeryDetails {
  id: string;
  surgeryOn: string | null;
  procedure: string | null;
  outcome: string | null;
  noSurgeryExpected: boolean;
  detail: string | null;
  addedAtUtc: string;
  addedByUserId: string;
}

export interface LabCaseSlice {
  labCases: DemoLabCase[];
}

export const LAB_CASES = {
  /** Ilona Petrova's blood sample: every step done, "No more work" recorded: to report. */
  ilonaBlood: id(0xca, 1),
  /** Priya Holloway's biopsy, her pathway's Histology (lab) case: every step done, to report (MC16). */
  priyaBiopsy: id(0xca, 2),
  /** Callum Ashby's biopsy: reported three days ago, which finished his Histology (its result awaits sign-off). */
  callumBiopsy: id(0xca, 3),
  /** Aisha Khoury's second biopsy: "Sample adequate?" recorded Insufficient, so closed with a repeat requested (MC22). */
  aishaBiopsy: id(0xca, 4),
  ...HOSPITAL_LAB_CASES,
} as const;

/** The IDs the Read Me's tour opens. */
export const LAB_CASE_IDS = {
  /** Wilfred Ashdown's hospital case, at "Needs more work?". */
  atChoice: LAB_CASES.wilfred,
  /** Ilona Petrova's case, ready for its results. */
  toReport: LAB_CASES.ilonaBlood,
  /** Maya Thistlewood's hospital case, just received (its sample's adequacy not yet recorded). */
  justReceived: LAB_CASES.maya,
  /** Priya Holloway's Histology case: authorising it finishes her pathway's lab stage (MC16). */
  stageToReport: LAB_CASES.priyaBiopsy,
  /** Ellis Varden's earlier hospital case: reported and sent, awaiting the hospital's surgery details (MC20). */
  awaitingSurgeryDetails: LAB_CASES.ellisBiopsy,
  /** Aisha Khoury's second biopsy: closed with a repeat requested, its Take biopsy reopened (MC22). */
  repeatRequested: LAB_CASES.aishaBiopsy,
} as const;

/**
 * The pathway's lab stage a sample taken at `stageId` goes to (MC16): that stage if it's the lab's, else the nearest
 * lab-owned stage after it on its routes. None on a pathway without one (the case then keeps the sample's stage).
 */
export function labStageAfter(pathway: Pick<DemoPathway, 'stages' | 'links'>, stageId: string | null, labTeamId: string | null): DemoStage | undefined {
  if (!stageId || !labTeamId) return undefined;
  const isLabs = (id: string) => pathway.stages.find((s) => s.id === id && s.ownerTeamId === labTeamId);
  const seen = new Set([stageId]);
  for (const queue = [stageId]; queue.length > 0; ) {
    const from = queue.shift()!;
    const found = isLabs(from);
    if (found) return found;
    for (const link of pathway.links)
      if (link.fromStageId === from && !seen.has(link.toStageId)) {
        seen.add(link.toStageId);
        queue.push(link.toStageId);
      }
  }
  return undefined;
}

/** `n` times spread evenly after `fromUtc` and before `toUtc`. */
function spread(fromUtc: string, toUtc: string, n: number): string[] {
  const from = Date.parse(fromUtc);
  const gap = (Date.parse(toUtc) - from) / (n + 1);
  return Array.from({length: n}, (_, i) => new Date(from + gap * (i + 1)).toISOString());
}

type StepPlan = Pick<DemoLabCaseStep, 'kind'> & Partial<Pick<DemoLabCaseStep, 'testStepId' | 'choice' | 'outcome' | 'detail'>>;

/**
 * Seeds the cases, after every other slice: the hospital's three (one each, from their requests), four of the
 * clinics' (Ilona Petrova's blood sample in the lab; Priya Holloway's biopsy, her Histology stage's, to report; Callum
 * Ashby's, reported, which finished his Histology; Aisha Khoury's second biopsy, closed with a repeat requested), and one waiting for its sample for every other lab stage reached
 * (MC16). Older reported samples predate lab cases and have none, as after the full version's migration.
 */
export function seedLabCases(state: BaseState & LabSlice & HospitalSlice): LabCaseSlice {
  let stepNumber = 0;
  const testSteps = (testId: string) => state.labTests.find((t) => t.id === testId)!.steps;
  const step = (testId: string, name: string) => testSteps(testId).find((s) => s.name === name)!.id;
  const sample = (specimenId: string) => state.specimens.find((s) => s.id === specimenId)!;
  const hospitalSampleOf = (requestId: string) => state.specimens.find((s) => s.hospitalRequestId === requestId)!;

  const make = (
    caseId: string,
    number: string,
    samples: DemoSpecimen[],
    testId: string,
    plans: StepPlan[],
    until: string,
    more: Partial<DemoLabCase> = {},
  ): DemoLabCase => {
    const first = samples[0];
    const receivedAtUtc = first.receivedAtUtc!;
    const times = spread(receivedAtUtc, until, plans.length);
    for (const s of samples) s.labCaseId = caseId;
    return {
      id: caseId,
      number,
      labTestId: testId,
      patientId: first.patientId,
      teamId: TEAMS.lab,
      source: first.source,
      requestingTeamId: first.requestingTeamId,
      hospitalId: first.hospitalId,
      hospitalRequestId: first.hospitalRequestId,
      pathwayId: first.pathwayId,
      stageId: first.stageId,
      status: 'Open',
      openedAtUtc: receivedAtUtc,
      openedByUserId: LAB.id,
      receivedAtUtc,
      steps: [
        {kind: 'Received' as const, testStepId: null, choice: null, outcome: null, detail: null, atUtc: receivedAtUtc},
        ...plans.map((p, i) => ({testStepId: null, choice: null, outcome: null, detail: null, ...p, atUtc: times[i]})),
      ].map((s) => ({...s, id: id(0xcb, ++stepNumber), byUserId: LAB.id, enteredInError: null})),
      report: null,
      repeat: null,
      enteredInError: null,
      version: plans.length + 1,
      ...more,
    };
  };

  const adequate: StepPlan[] = [{kind: 'Accessioned'}, {kind: 'Outcome', choice: 'SampleAdequate', outcome: 'Adequate'}];
  const histology = LAB_TESTS.skinHistology;
  const histologySteps = ['Cut-up', 'Processing and embedding', 'Sectioning and staining', 'Microscopy and report'].map(
    (name): StepPlan => ({kind: 'TestStep', testStepId: step(histology, name)}),
  );
  const noMoreWork: StepPlan = {kind: 'Outcome', choice: 'NeedsMoreWork', outcome: 'NoMoreWork'};
  const hoursAgo = (hours: number) => new Date(Date.parse(nowUtc()) - hours * 3600_000).toISOString();
  const request = (requestId: string) => state.hospitalRequests.find((r) => r.id === requestId)!;

  const pathwayOf = (pathwayId: string) => state.pathways.find((p) => p.id === pathwayId)!;
  const histologyOf = (pathwayId: string) => pathwayOf(pathwayId).stages.find((s) => s.ownerTeamId === TEAMS.lab)!;
  const priya = sample(SPECIMENS.priyaBiopsy);
  const callum = sample(SPECIMENS.callumBiopsy);
  const callumHistology = histologyOf(PATHWAYS.backFromLab);
  const callumResult = state.results.find((r) => r.id === RESULTS.callumBiopsy)!;
  // The results on his Histology stage are the report authorised (MC16): one text, on the stage and the result.
  const callumReport = callumHistology.result!.text;
  callumResult.labCaseId = LAB_CASES.callumBiopsy;
  callumResult.reportText = callumReport;
  const ilona = sample(SPECIMENS.ilonaBlood);
  const daysAgo = (days: number, hour = 11) => {
    const at = new Date(Date.parse(nowUtc()) - days * 86400_000);
    at.setUTCHours(hour, 0, 0, 0);
    return at.toISOString();
  };
  const ukDay = (iso: string) => iso.slice(0, 10);
  /** MC20: a hospital case reported `days` ago and sent back to its hospital in the same action (its sample reported too). */
  const sentBack = (requestId: string, days: number, text: string): Partial<DemoLabCase> => {
    const at = daysAgo(days);
    const r = request(requestId);
    const s = hospitalSampleOf(requestId);
    s.status = 'Reported';
    s.reportedAtUtc = at;
    s.reportedByUserId = LAB.id;
    const hospital = state.hospitals.find((h) => h.id === r.hospitalId)!;
    return {
      status: 'AwaitingSurgeryDetails',
      report: {text, atUtc: at, byUserId: LAB.id, resultId: null},
      reportSent: {atUtc: at, byUserId: LAB.id, hospitalId: hospital.id, addressedTo: r.surgeonName, method: hospital.reportDeliveryMethod},
    };
  };
  const biopsyReport = 'Synthetic demo report. Skin, punch biopsy: findings as typed by the lab. (MedApp never reads or interprets it.)';
  const wilfredBiopsy = sentBack(HOSPITAL_REQUESTS.wilfredBiopsy, 10, biopsyReport);
  const wilfredSurgeryAt = daysAgo(2, 9);

  // MC22: Aisha Khoury's second biopsy, recorded insufficient as the outcomes handler does it: the case closed with a
  // repeat requested, the sample rejected (insufficient), the added route's Take biopsy reopened for the repeat (MC16),
  // the lab's Histology picked up at receipt and still the lab's, and the clinic told without the reason.
  const aisha = sample(SPECIMENS.aishaBiopsy);
  const aishaPathway = pathwayOf(PATHWAYS.forkedTests);
  const aishaHistology = labStageAfter(aishaPathway, aisha.stageId, TEAMS.lab)!;
  const aishaRepeat = 'Too little tissue to section (synthetic).';
  const aishaCase = make(
    LAB_CASES.aishaBiopsy,
    aisha.number,
    [aisha],
    histology,
    [{kind: 'Accessioned'}, {kind: 'Outcome', choice: 'SampleAdequate', outcome: 'Insufficient', detail: aishaRepeat}],
    new Date(Date.parse(aisha.receivedAtUtc!) + 6 * 3600_000).toISOString(),
    {stageId: aishaHistology.id, status: 'RepeatRequested'},
  );
  const aishaRejectedAt = aishaCase.steps.at(-1)!.atUtc;
  aishaCase.repeat = {detail: aishaRepeat, atUtc: aishaRejectedAt, byUserId: LAB.id};
  Object.assign(aisha, {
    status: 'RejectedByLab',
    rejectionReason: 'InsufficientSample',
    closedDetail: aishaRepeat,
    closedAtUtc: aishaRejectedAt,
    closedByUserId: LAB.id,
    version: aisha.version + 1,
  } satisfies Partial<DemoSpecimen>);
  aishaPathway.stages.find((s) => s.id === aisha.stageId)!.unlocked = true;
  aishaHistology.pickedUpAtUtc = aisha.receivedAtUtc;
  aishaHistology.pickedUpByUserId = LAB.id;
  state.notifications.push({
    id: id(0xe5, 104),
    userId: STAFF.id,
    kind: 'RepeatSampleRequested',
    title: 'Repeat sample requested',
    message: 'The lab asked for a repeat sample (Skin histology). Take biopsy is reopened to take it.',
    pathwayId: PATHWAYS.forkedTests,
    subjectId: aisha.id,
    createdAtUtc: aishaRejectedAt,
    isRead: false,
  });

  const seeded = [
    make(LAB_CASES.ilonaBlood, ilona.number, [ilona], LAB_TESTS.fullBloodCount, [...adequate, {kind: 'TestStep', testStepId: step(LAB_TESTS.fullBloodCount, 'Analyser run')}, noMoreWork], hoursAgo(2)),
    make(LAB_CASES.priyaBiopsy, priya.number, [priya], histology, [...adequate, ...histologySteps, noMoreWork], hoursAgo(4), {
      stageId: histologyOf(PATHWAYS.histologyToFinish).id,
    }),
    make(LAB_CASES.callumBiopsy, callum.number, [callum], histology, [...adequate, ...histologySteps, noMoreWork], callum.reportedAtUtc!, {
      stageId: callumHistology.id,
      status: 'Reported',
      report: {text: callumReport, atUtc: callum.reportedAtUtc!, byUserId: LAB.id, resultId: RESULTS.callumBiopsy},
    }),
    aishaCase,
    make(
      HOSPITAL_LAB_CASES.wilfred,
      request(HOSPITAL_REQUESTS.wilfredExcision).labCaseNumber,
      [hospitalSampleOf(HOSPITAL_REQUESTS.wilfredExcision)],
      histology,
      [...adequate, ...histologySteps],
      hoursAgo(3),
      {
        // MC20: the hospital's extra details, arrived after its request (before the report).
        surgeryDetails: [
          {
            id: id(0xcd, 1),
            surgeryOn: ukDay(daysAgo(3)),
            procedure: 'Wide local excision, left forearm (synthetic)',
            outcome: null,
            noSurgeryExpected: false,
            detail: 'Suture marks 12 o’clock (synthetic).',
            addedAtUtc: hoursAgo(20),
            addedByUserId: LAB.id,
          },
        ],
      },
    ),
    make(HOSPITAL_LAB_CASES.maya, request(HOSPITAL_REQUESTS.mayaBiopsy).labCaseNumber, [hospitalSampleOf(HOSPITAL_REQUESTS.mayaBiopsy)], histology, [], hoursAgo(1)),
    make(
      HOSPITAL_LAB_CASES.ellis,
      request(HOSPITAL_REQUESTS.ellisExcision).labCaseNumber,
      [hospitalSampleOf(HOSPITAL_REQUESTS.ellisExcision)],
      histology,
      [
        ...adequate,
        ...histologySteps,
        {kind: 'Outcome', choice: 'NeedsMoreWork', outcome: 'SecondOpinion'},
        {kind: 'TestStep', testStepId: step(histology, 'Sent for a second opinion'), detail: 'Regional dermatopathology service (synthetic)'},
      ],
      hoursAgo(5),
    ),
    // MC20: Wilfred's biopsy, reported and sent, then closed when the surgery details came.
    make(
      HOSPITAL_LAB_CASES.wilfredBiopsy,
      request(HOSPITAL_REQUESTS.wilfredBiopsy).labCaseNumber,
      [hospitalSampleOf(HOSPITAL_REQUESTS.wilfredBiopsy)],
      histology,
      [...adequate, ...histologySteps, noMoreWork],
      wilfredBiopsy.report!.atUtc,
      {
        ...wilfredBiopsy,
        status: 'Closed',
        surgeryDetails: [
          {
            id: id(0xcd, 2),
            surgeryOn: ukDay(daysAgo(3)),
            procedure: 'Wide local excision (synthetic)',
            outcome: 'Completed; the excision sent to the lab (synthetic).',
            noSurgeryExpected: false,
            detail: null,
            addedAtUtc: wilfredSurgeryAt,
            addedByUserId: LAB.id,
          },
        ],
        closed: {atUtc: wilfredSurgeryAt, byUserId: LAB.id},
      },
    ),
    // MC20: Ellis's biopsy, reported and sent 12 days ago: awaiting the hospital's surgery details.
    make(
      HOSPITAL_LAB_CASES.ellisBiopsy,
      request(HOSPITAL_REQUESTS.ellisBiopsy).labCaseNumber,
      [hospitalSampleOf(HOSPITAL_REQUESTS.ellisBiopsy)],
      histology,
      [...adequate, ...histologySteps, noMoreWork],
      daysAgo(12),
      sentBack(HOSPITAL_REQUESTS.ellisBiopsy, 12, biopsyReport),
    ),
  ];
  // Every other lab stage reached and still to do opened its case then, waiting for a sample (and its test).
  let waiting = 0;
  for (const pathway of state.pathways)
    for (const stage of pathway.stages.filter((s) => s.ownerTeamId === TEAMS.lab && s.reachedAtUtc && !s.completedAtUtc && !s.skippedAtUtc))
      if (!seeded.some((c) => c.stageId === stage.id))
        seeded.push({
          id: id(0xca, 100 + ++waiting),
          number: specimenNumber(++state.specimenSequence, stage.reachedAtUtc!.slice(0, 10)),
          labTestId: null,
          patientId: pathway.patientId,
          teamId: TEAMS.lab,
          source: 'Clinic',
          requestingTeamId: pathway.teamId,
          hospitalId: null,
          hospitalRequestId: null,
          pathwayId: pathway.id,
          stageId: stage.id,
          status: 'Open',
          openedAtUtc: stage.reachedAtUtc!,
          openedByUserId: pathway.handovers.find((h) => h.stageId === stage.id)?.byUserId ?? LAB.id,
          receivedAtUtc: null,
          steps: [],
          report: null,
          repeat: null,
          enteredInError: null,
          version: 1,
        });
  return {labCases: seeded};
}
