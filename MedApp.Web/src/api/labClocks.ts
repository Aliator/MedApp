import type {Schemas} from './client';

/**
 * A lab case's strict clock (MC19 of `docs/multi-clinic-plan.md`): the lab's own target from the test's catalogue entry
 * and, for a hospital case, the hospital's date from its request. **Whichever is sooner applies**, due at the end of that
 * UK day; at risk from the test's at-risk days before it; overdue (breached) after it until the report is authorised.
 * Date arithmetic only, never anything from a test's values. A second opinion keeps it running.
 */
export type LabClock = Schemas['LabClockResponse'];

const STATE_LABELS: Record<string, string> = {
  OnTime: 'On time',
  AtRisk: 'At risk',
  Breached: 'Overdue',
  ReportedOnTime: 'Reported on time',
  ReportedLate: 'Reported late',
  Stopped: 'Clock stopped',
};

export const labClockStateLabel = (state: string | null | undefined) => STATE_LABELS[state ?? ''] ?? '';

/** Whether the clock is still running: the case is open and not yet reported. */
export const isLabClockRunning = (clock: LabClock | null | undefined) =>
  clock?.state === 'OnTime' || clock?.state === 'AtRisk' || clock?.state === 'Breached';

const days = (n: number) => `${n} ${n === 1 ? 'day' : 'days'}`;

/** The countdown: "Due in 3 days", "Due tomorrow", "Due today", "2 days overdue"; a stopped clock's state. */
export function labClockCountdown(clock: LabClock | null | undefined): string {
  if (!clock) return 'No clock yet';
  if (!isLabClockRunning(clock)) return labClockStateLabel(clock.state);
  const left = clock.daysLeft ?? 0;
  if (clock.state === 'Breached') return `${days(Math.max(1, -left))} overdue`;
  return left === 0 ? 'Due today' : left === 1 ? 'Due tomorrow' : `Due in ${days(left)}`;
}

/** Which deadline set the due date: "The hospital's date" or "The lab's target". */
export const labClockSetBy = (clock: LabClock) => (clock.setBy === 'Hospital' ? "The hospital's date" : "The lab's target");

const BASIS: Record<string, string> = {
  ReportNeededBy: 'as the request gave it',
  CancerPathway: 'a cancer pathway with no date: collection + 10 days',
  HospitalDefault: "no date given: the hospital's default days from receipt",
};

/** How the hospital's date was found. */
export const hospitalDueBasis = (basis: string | null | undefined) => BASIS[basis ?? ''] ?? '';
