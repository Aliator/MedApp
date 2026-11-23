import {CoachTour} from '../demo/coach/CoachTour';
import {fullTourHost} from './tourHost';

/** The walkthrough in the full demo (`AppLayout`, its build only): the light demo's steps, on the real backend. */
export function FullDemoWalkthrough() {
  return <CoachTour host={fullTourHost} />;
}
