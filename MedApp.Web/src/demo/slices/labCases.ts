import {LAB} from '../accounts';
import {TEAMS, id} from '../ids';
import {nowUtc} from '../time';
import type {HospitalSlice} from './hospitals';
import {HOSPITAL_LAB_CASES, HOSPITAL_REQUESTS} from './hospitals';
import {LAB_TESTS, RESULTS, SPECIMENS, type DemoSpecimen, type LabSlice} from './lab';
import type {BaseState} from '../state';

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
  labTestId: string;
  patientId: string;
  /** The lab's: every case is the lab's work (a clinic's is also seen through its pathway). */
  teamId: string;
  source: 'Clinic' | 'Hospital';
  requestingTeamId: string | null;
  hospitalId: string | null;
  hospitalRequestId: string | null;
  pathwayId: string | null;
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
  /** Priya Holloway's biopsy: reported two days ago (its result awaits Nadia Thornbury's sign-off). */
  priyaBiopsy: id(0xca, 2),
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
  /** Ellis Varden's earlier hospital case: reported and sent, awaiting the hospital's surgery details (MC20). */
  awaitingSurgeryDetails: LAB_CASES.ellisBiopsy,
} as const;

/** `n` times spread evenly after `fromUtc` and before `toUtc`. */
function spread(fromUtc: string, toUtc: string, n: number): string[] {
  const from = Date.parse(fromUtc);
  const gap = (Date.parse(toUtc) - from) / (n + 1);
  return Array.from({length: n}, (_, i) => new Date(from + gap * (i + 1)).toISOString());
}

type StepPlan = Pick<DemoLabCaseStep, 'kind'> & Partial<Pick<DemoLabCaseStep, 'testStepId' | 'choice' | 'outcome' | 'detail'>>;

/**
 * Seeds the cases, after every other slice: the hospital's three (one each, from their requests) and two of the
 * clinics' (Ilona Petrova's blood sample in the lab, Priya Holloway's reported biopsy). Older reported samples predate
 * lab cases and have none, as after the full version's migration.
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

  const priya = sample(SPECIMENS.priyaBiopsy);
  const priyaResult = state.results.find((r) => r.id === RESULTS.priyaBiopsy)!;
  const priyaReport =
    'Synthetic demo report. Skin, upper back, punch biopsy: a benign compound naevus. No atypia. Margins clear. (Typed by the lab; MedApp never reads or interprets it.)';
  priyaResult.labCaseId = LAB_CASES.priyaBiopsy;
  priyaResult.reportText = priyaReport;
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

  return {
    labCases: [
      make(LAB_CASES.ilonaBlood, ilona.number, [ilona], LAB_TESTS.fullBloodCount, [...adequate, {kind: 'TestStep', testStepId: step(LAB_TESTS.fullBloodCount, 'Analyser run')}, noMoreWork], hoursAgo(2)),
      make(LAB_CASES.priyaBiopsy, priya.number, [priya], histology, [...adequate, ...histologySteps, noMoreWork], priya.reportedAtUtc!, {
        status: 'Reported',
        report: {text: priyaReport, atUtc: priya.reportedAtUtc!, byUserId: LAB.id, resultId: RESULTS.priyaBiopsy},
      }),
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
    ],
  };
}
