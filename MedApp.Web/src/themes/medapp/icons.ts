/**
 * Astryx's semantic icons, drawn with Remix Icon (outline style) so the app uses one icon set (plan Q8).
 * Names not listed here (upload, viewColumns, checkDouble, wrench, stop, microphone) keep stone's artwork.
 */
import {createElement} from 'react';
import type {IconRegistry} from '@astryxdesign/core/Icon';
import {
  RiArrowDownLine,
  RiArrowDownSLine,
  RiArrowLeftDoubleLine,
  RiArrowLeftSLine,
  RiArrowRightDoubleLine,
  RiArrowRightSLine,
  RiArrowUpDownLine,
  RiArrowUpLine,
  RiCalendarLine,
  RiCheckboxCircleLine,
  RiCheckLine,
  RiCloseCircleLine,
  RiCloseLine,
  RiErrorWarningLine,
  RiExternalLinkLine,
  RiEyeOffLine,
  RiFileCopyLine,
  RiFilterLine,
  RiInformationLine,
  RiMenuLine,
  RiMoreLine,
  RiSearchLine,
  RiTimeLine,
} from '@remixicon/react';

// Plain createElement rather than JSX: the theme CLI compiles this file with the classic JSX transform.
const icon = (Component: typeof RiCloseLine) =>
  createElement(Component, {size: '1em', 'aria-hidden': true});

export const medappIconRegistry: Partial<IconRegistry> = {
  close: icon(RiCloseLine),
  check: icon(RiCheckLine),
  success: icon(RiCheckboxCircleLine),
  error: icon(RiCloseCircleLine),
  warning: icon(RiErrorWarningLine),
  info: icon(RiInformationLine),
  chevronDown: icon(RiArrowDownSLine),
  chevronLeft: icon(RiArrowLeftSLine),
  chevronRight: icon(RiArrowRightSLine),
  chevronsLeft: icon(RiArrowLeftDoubleLine),
  chevronsRight: icon(RiArrowRightDoubleLine),
  calendar: icon(RiCalendarLine),
  clock: icon(RiTimeLine),
  search: icon(RiSearchLine),
  menu: icon(RiMenuLine),
  moreHorizontal: icon(RiMoreLine),
  externalLink: icon(RiExternalLinkLine),
  arrowUp: icon(RiArrowUpLine),
  arrowDown: icon(RiArrowDownLine),
  arrowsUpDown: icon(RiArrowUpDownLine),
  funnel: icon(RiFilterLine),
  eyeSlash: icon(RiEyeOffLine),
  copy: icon(RiFileCopyLine),
};
