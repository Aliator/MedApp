import {SideNavItem} from '@astryxdesign/core/SideNav';
import type {ReactNode} from 'react';
import type {NavRailEntry} from '../components/navigation/NavRail';
import {AppIcon} from '../components/shared/icons';
import {startCoachTour} from './coach/coachState';
import {READ_ME_PATH} from './readMeSeen';

/**
 * The Read Me's item at the top of the side nav (`AppSideNav`, in the light demo's build only), built like the nav's
 * other items: its row, marked when it's the current page or the half view's, with its menu.
 */
export function readMeNavEntry({
  isSelected,
  isHalfView,
  halfViewRef,
  menu,
}: {
  isSelected: boolean;
  isHalfView: boolean;
  halfViewRef: (link: HTMLElement | null) => void;
  menu: ReactNode;
}): NavRailEntry {
  return {
    key: 'read-me',
    label: 'Read Me',
    icon: AppIcon.Info,
    href: READ_ME_PATH,
    isSelected,
    isHalfView,
    row: <SideNavItem label="Read Me" icon={AppIcon.Info} href={READ_ME_PATH} isSelected={isSelected} ref={halfViewRef} actions={menu} />,
  };
}

export function guidedTourNavEntry(): NavRailEntry {
  return {
    key: 'guided-tour',
    label: 'Guided Tour',
    icon: AppIcon.Question,
    isSelected: false,
    row: <SideNavItem label="Guided Tour" icon={AppIcon.Question} onClick={() => startCoachTour()} />,
  };
}
