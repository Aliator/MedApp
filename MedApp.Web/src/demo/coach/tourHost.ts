import type {TourStep} from '../tour';

/**
 * What the walkthrough (`CoachTour`) needs from the demo it runs in: the light demo's stand-in (`lightTourHost`) or the
 * full demo's backend in the browser (`src/fullDemo/tourHost.ts`). The steps and their highlights are the same; where
 * their records are, how a step is checked, and how the account is switched or the data reset differ.
 */
export interface TourHost {
  /**
   * A hook: the page a step or highlight opens (`TOUR`'s paths, with the light demo's records' IDs), in this demo:
   * the same path, or with this demo's own records' IDs. Null while it's being worked out; undefined for none.
   */
  usePage(path: string | undefined): string | null | undefined;
  /** A hook: why the step can't do what it says here (done already, its record gone), or null. Only while `isAsked`. */
  useWhyStepCantOpen(step: TourStep, isAsked: boolean): string | null;
  /** Signs in as another account and opens `path` (as `TOUR` has it) with a full load. */
  switchAccount(username: string, path: string): Promise<void>;
  /** Reset the Demo: the data as it was published or seeded, then a reload. */
  resetDemo(): Promise<void>;
  /** How an account is named in a step: "the admin", or the person's name. */
  accountLabel(username: string): string | undefined;
}
