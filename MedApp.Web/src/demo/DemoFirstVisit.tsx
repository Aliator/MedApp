import {useNavigate, useRouterState} from '@tanstack/react-router';
import {useEffect} from 'react';
import {CoachTour} from './coach/CoachTour';
import {hasSeenReadMe, READ_ME_PATH} from './readMeSeen';

export function DemoFirstVisit() {
  const navigate = useNavigate();
  const pathname = useRouterState({select: (state) => state.location.pathname});

  useEffect(() => {
    if (!hasSeenReadMe() && pathname !== READ_ME_PATH) void navigate({to: READ_ME_PATH, replace: true} as Parameters<typeof navigate>[0]);
  }, [navigate, pathname]);

  return <CoachTour />;
}
