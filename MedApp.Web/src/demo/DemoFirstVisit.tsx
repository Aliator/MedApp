import {useEffect} from 'react';
import {CoachTour} from './coach/CoachTour';
import {startCoachTour} from './coach/coachState';
import {hasSeenReadMe, markReadMeSeen} from './readMeSeen';

export function DemoFirstVisit() {
  useEffect(() => {
    if (hasSeenReadMe()) return;
    markReadMeSeen();
    startCoachTour();
  }, []);

  return <CoachTour />;
}
