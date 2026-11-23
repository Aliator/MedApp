import {accountLabel, personByUsername} from './accounts';
import type {TourHost} from './coach/tourHost';
import {resetDemo, switchAccount} from './session';
import {db} from './store';
import type {TourStep} from './tour';

const RECORDS: Record<string, (id: string) => boolean> = {
  patients: (id) => db().patients.some((p) => p.id === id),
  pathways: (id) => db().pathways.some((p) => p.id === id),
  referrals: (id) => db().referrals.some((r) => r.id === id),
};

/** Why a step can't do what it says in this copy of the light demo: its record gone, or its own `whyNot`. */
export function whyStepCantOpen(step: TourStep): string | null {
  const [, kind, id] = (step.path ?? '').split(/[/?]/);
  if (id && RECORDS[kind] && !RECORDS[kind](id)) return 'That record isn’t in your copy of the demo any more. Reset the Demo, below, brings it back.';
  return step.whyNot?.(db()) ?? null;
}

/** The walkthrough in the light demo: its records' IDs as `TOUR` has them, checked against the stand-in's state. */
export const lightTourHost: TourHost = {
  usePage: (path) => path,
  useWhyStepCantOpen: (step, isAsked) => (isAsked ? whyStepCantOpen(step) : null),
  switchAccount,
  resetDemo,
  accountLabel: (username) => {
    const person = personByUsername(username);
    return person && accountLabel(person);
  },
};
