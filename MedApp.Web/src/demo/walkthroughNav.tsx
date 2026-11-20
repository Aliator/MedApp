import {SideNavItem} from '@astryxdesign/core/SideNav';
import type {NavRailEntry} from '../components/navigation/NavRail';
import {AppIcon} from '../components/shared/icons';
import {startCoachTour} from './coach/coachState';

export function walkthroughNavEntry(): NavRailEntry {
  return {
    key: 'walkthrough',
    label: 'Walkthrough',
    icon: AppIcon.Question,
    isSelected: false,
    row: <SideNavItem label="Walkthrough" icon={AppIcon.Question} onClick={() => startCoachTour()} />,
  };
}
