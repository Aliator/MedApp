import {CoachTour} from './coach/CoachTour';
import {lightTourHost} from './lightTourHost';

export function DemoWalkthrough() {
  return <CoachTour host={lightTourHost} />;
}
