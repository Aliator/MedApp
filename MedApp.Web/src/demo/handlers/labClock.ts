import type {Schemas} from '../../api/client';
import {addDays} from '../../utils/calendarSlots';
import {endOfUkDay, ukDateOf, ukTime} from '../time';

/**
 * A lab case's strict clock (MC19 of `docs/multi-clinic-plan.md`, decisions 186 and Q15), a copy of what the API's
 * `LabClock` will do. Date arithmetic only, in calendar days and UK dates; never anything from a test's values.
 * - **The lab's own:** received + the test's `TargetDays` (the RCPath 7 days by default); its `LimitDays` (10) shown
 *   beside it.
 * - **The hospital's** (a hospital case only): the request's `ReportNeededBy`, else for a cancer pathway collection + 10
 *   days, else received + the hospital's default days.
 * - **Whichever is sooner applies** (on a tie, the hospital's is named). Due at the end of that UK day; **at risk** from
 *   the start of `AtRiskDays` before it; **breached** after it until the report is authorised. A second opinion keeps
 *   it running: nothing pauses it.
 */
export type LabClock = Schemas['LabClockResponse'];

/** The RCPath histopathology benchmark (80% in 7 calendar days, 90% in 10), for a test that names none. */
export const DEFAULT_TARGET_DAYS = 7;
export const DEFAULT_LIMIT_DAYS = 10;
export const DEFAULT_AT_RISK_DAYS = 2;
/** NHS England's standard for a cancer pathway request that gives no date: collection + 10 days. */
export const CANCER_PATHWAY_DAYS = 10;

export interface LabClockInput {
  /** When the lab received the case's first sample: no clock before it. */
  receivedAtUtc: string | null;
  status: string;
  reportedAtUtc: string | null;
  test: {targetDays?: number; limitDays?: number; atRiskDays?: number} | undefined;
  /** The hospital's side, for a hospital case. */
  hospital: {reportNeededBy: string | null; isCancerPathway: boolean; collectedAtUtc: string | null; defaultDeadlineDays: number} | null;
  /** Now, as UTC (the handlers pass the demo's clock). */
  nowUtc: string;
}

const DAY_MS = 86_400_000;
const daysBetween = (from: string, to: string) => Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / DAY_MS);

/** The hospital's due date and what set it, or null for a clinic case. */
function hospitalDue(input: LabClockInput, receivedOn: string): {on: string; basis: string} | null {
  const h = input.hospital;
  if (!h) return null;
  if (h.reportNeededBy) return {on: h.reportNeededBy.slice(0, 10), basis: 'ReportNeededBy'};
  if (h.isCancerPathway) return {on: addDays(ukDateOf(h.collectedAtUtc ?? input.receivedAtUtc!), CANCER_PATHWAY_DAYS), basis: 'CancerPathway'};
  return {on: addDays(receivedOn, h.defaultDeadlineDays), basis: 'HospitalDefault'};
}

/** The case's clock, or undefined before the lab has received it. */
export function labClock(input: LabClockInput): LabClock | undefined {
  if (!input.receivedAtUtc) return undefined;
  const receivedOn = ukDateOf(input.receivedAtUtc);
  const labDueOn = addDays(receivedOn, input.test?.targetDays ?? DEFAULT_TARGET_DAYS);
  const labLimitOn = addDays(receivedOn, input.test?.limitDays ?? DEFAULT_LIMIT_DAYS);
  const hospital = hospitalDue(input, receivedOn);
  const hospitalSets = hospital !== null && hospital.on <= labDueOn;
  const dueOn = hospitalSets ? hospital.on : labDueOn;
  const atRiskOn = addDays(dueOn, -(input.test?.atRiskDays ?? DEFAULT_AT_RISK_DAYS));
  const dueAtUtc = endOfUkDay(dueOn);
  const now = Date.parse(input.nowUtc);
  const reportedOn = input.reportedAtUtc ? ukDateOf(input.reportedAtUtc) : null;
  const state =
    reportedOn !== null
      ? reportedOn <= dueOn
        ? 'ReportedOnTime'
        : 'ReportedLate'
      : input.status !== 'Open'
        ? 'Stopped'
        : now > Date.parse(dueAtUtc)
          ? 'Breached'
          : now >= Date.parse(ukTime(atRiskOn, 0))
            ? 'AtRisk'
            : 'OnTime';
  return {
    labDueOn,
    labLimitOn,
    hospitalDueOn: hospital?.on ?? null,
    hospitalDueBasis: hospital?.basis ?? null,
    dueOn,
    setBy: hospitalSets ? 'Hospital' : 'Lab',
    dueAtUtc,
    atRiskOn,
    state,
    daysLeft: daysBetween(ukDateOf(input.nowUtc), dueOn),
  };
}

/** Whether a clock is still running (open, not yet reported). */
export const isRunning = (clock: LabClock | undefined) => clock?.state === 'OnTime' || clock?.state === 'AtRisk' || clock?.state === 'Breached';
