import {useNavigate, useRouterState} from '@tanstack/react-router';
import {useEffect} from 'react';
import {hasSeenReadMe, READ_ME_PATH} from './readMeSeen';

/** On the light demo's first visit, opens the Read Me (`AppLayout`, the demo build only). Renders nothing. */
export function DemoFirstVisit() {
  const navigate = useNavigate();
  const pathname = useRouterState({select: (state) => state.location.pathname});

  useEffect(() => {
    if (!hasSeenReadMe() && pathname !== READ_ME_PATH) void navigate({to: READ_ME_PATH, replace: true} as Parameters<typeof navigate>[0]);
  }, [navigate, pathname]);

  return null;
}
