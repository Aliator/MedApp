import {useCallback, useMemo, useState} from 'react';
import {appSettings, defineSetting, useAppSetting} from '../../app/settingsStore';
import type {Permission} from '../../auth/permissions';
import {AppIcon} from '../shared/icons';

type NavIcon = (typeof AppIcon)[keyof typeof AppIcon];

/** One admin page in the side nav, shown only to people whose roles grant its permission (its route checks the same). */
export interface AdminPage {
  label: string;
  icon: NavIcon;
  href: string;
  permission: Permission;
}

/** A group of admin pages in the side nav: a row that opens with a click to show them. */
export interface AdminCategory {
  key: string;
  label: string;
  icon: NavIcon;
  pages: AdminPage[];
}

/**
 * The Administration section's pages, in categories (the owner's grouping, 18 November 2026, and the owner's changes
 * the same evening: Account Management and Calendar renamed, Practices into Calendar and Data Requests into Oversight,
 * Sites and Rooms hidden as deprecated). A category shows only when the person can see at least one page in it, and
 * the section only when they can see any. The keys are kept as they were, as the account's settings remember them.
 */
export const ADMIN_CATEGORIES: AdminCategory[] = [
  {
    key: 'people',
    label: 'Account Management',
    icon: AppIcon.AdminPeople,
    pages: [
      {label: 'Users', icon: AppIcon.Users, href: '/admin/users', permission: 'ManageUsers'},
      {label: 'Roles and Permissions', icon: AppIcon.Roles, href: '/admin/roles', permission: 'ManageRoles'},
    ],
  },
  {
    key: 'pathway-setup',
    label: 'Pathway Setup',
    icon: AppIcon.AdminPathwaySetup,
    pages: [
      {label: 'Specialties', icon: AppIcon.Specialties, href: '/admin/specialties', permission: 'ManageSetup'},
      {label: 'Stage Library', icon: AppIcon.StageDefinitions, href: '/admin/stages', permission: 'ManageSetup'},
      {label: 'Pathway Templates', icon: AppIcon.Templates, href: '/admin/pathway-templates', permission: 'ManageSetup'},
      {label: 'Letter Templates', icon: AppIcon.LetterTemplates, href: '/admin/letter-templates', permission: 'ManageSetup'},
      {label: 'Consent Forms', icon: AppIcon.Consent, href: '/admin/consent-forms', permission: 'ManageSetup'},
      {label: 'Specimen Types and Labs', icon: AppIcon.Lab, href: '/admin/specimen-setup', permission: 'ManageSetup'},
    ],
  },
  {
    key: 'clinics',
    label: 'Calendar',
    icon: AppIcon.AdminClinics,
    // Sites and Rooms (/admin/sites) is deprecated and left out of the nav: its page still works by address.
    pages: [
      {label: 'Activity Types', icon: AppIcon.ActivityTypes, href: '/admin/activity-types', permission: 'ManageSetup'},
      {label: 'Closed Days', icon: AppIcon.ClosedDays, href: '/admin/closed-days', permission: 'ManageSetup'},
      // The GP practice directory, for admins (its route checks the same); staff pick practices on the patient form.
      {label: 'Practices', icon: AppIcon.Practices, href: '/practices', permission: 'ManageSetup'},
    ],
  },
  {
    key: 'oversight',
    label: 'Oversight',
    icon: AppIcon.AdminOversight,
    pages: [
      {label: 'Insights', icon: AppIcon.Insights, href: '/admin/insights', permission: 'ViewInsights'},
      {label: 'Monitoring', icon: AppIcon.Monitoring, href: '/admin/monitoring', permission: 'ManageMonitoring'},
      {label: 'Audit Log', icon: AppIcon.AuditLog, href: '/admin/audit', permission: 'ViewAudit'},
      {label: 'Activity Log', icon: AppIcon.ActivityLog, href: '/admin/activity', permission: 'ViewAudit'},
      // Patients' data requests (subject access), for those who answer them.
      {label: 'Data Requests', icon: AppIcon.DataRequests, href: '/data-requests', permission: 'ManagePrivacy'},
    ],
  },
  {
    key: 'privacy-system',
    label: 'Privacy and System',
    icon: AppIcon.AdminPrivacy,
    pages: [
      {label: 'Retention Review', icon: AppIcon.Retention, href: '/admin/retention', permission: 'ManagePrivacy'},
      {label: 'Scheduled Jobs', icon: AppIcon.ScheduledJobs, href: '/admin/jobs', permission: 'ManageSetup'},
      {label: 'Admin Settings', icon: AppIcon.Settings, href: '/admin/settings', permission: 'ManageSecuritySettings'},
    ],
  },
];

/**
 * Which categories are open: one of the account's settings, as the pins are, so it follows the person between
 * browsers and signing out clears it from this one (`settingsStore` keeps it, and copes with storage being blocked).
 * Only the categories' keys are kept.
 */
export const NAV_CATEGORIES_KEY = defineSetting('medapp.navCategories');

function parseKeys(stored: string | null): string[] {
  try {
    const parsed: unknown = JSON.parse(stored ?? '[]');
    return Array.isArray(parsed) ? [...new Set(parsed.filter((key): key is string => typeof key === 'string'))] : [];
  } catch {
    return [];
  }
}

export interface NavCategories {
  /** Whether a category is open: the person opened it, or it holds the current page (or the half view's). */
  isOpen: (key: string, holdsCurrent: boolean) => boolean;
  /** A click on its row: open it, or close it. */
  toggle: (key: string, holdsCurrent: boolean) => void;
}

/**
 * The admin categories' open state. Closed until opened, and remembered once opened. The one holding the current page
 * (or the half view's) is open too, until the person closes it there: it stays closed until they go elsewhere. That
 * isn't remembered, so going to a page in a closed category opens it without changing what's kept.
 *
 * `here` names where the person is (the current page and the half view's): closing a category for this page lasts
 * until it changes.
 */
export function useNavCategories(here: string): NavCategories {
  const [stored, store] = useAppSetting(NAV_CATEGORIES_KEY);
  const opened = useMemo(() => parseKeys(stored), [stored]);
  const [closedHere, setClosedHere] = useState<{here: string; keys: string[]}>({here, keys: []});
  const closed = useMemo(() => (closedHere.here === here ? closedHere.keys : []), [closedHere, here]);

  const isOpen = useCallback(
    (key: string, holdsCurrent: boolean) => opened.includes(key) || (holdsCurrent && !closed.includes(key)),
    [opened, closed],
  );
  const toggle = useCallback(
    (key: string, holdsCurrent: boolean) => {
      // From the latest kept, not this render's, so two quick clicks on different categories both count.
      const latest = parseKeys(appSettings.get(NAV_CATEGORIES_KEY)).filter((k) => k !== key);
      if (isOpen(key, holdsCurrent)) {
        store(latest.length > 0 ? JSON.stringify(latest) : null);
        if (holdsCurrent) setClosedHere({here, keys: [...closed, key]});
      } else {
        store(JSON.stringify([...latest, key]));
        setClosedHere({here, keys: closed.filter((k) => k !== key)});
      }
    },
    [isOpen, store, here, closed],
  );
  return {isOpen, toggle};
}
