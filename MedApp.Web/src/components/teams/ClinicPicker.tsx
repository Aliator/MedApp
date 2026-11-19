import {useQuery, type QueryClient} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {settingsQuery} from '../../api/settings';
import {teamsQuery} from '../../api/teams';
import {appSettings, defineSetting, useAppSetting} from '../../app/settingsStore';
import {can, useCurrentUser, whoamiQuery, type CurrentUser} from '../../auth/auth';
import {ALL_CLINICS, parseClinic} from '../../pages/listSearch';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {NO_STATUS_LINE} from '../forms/fieldSpace';

/**
 * The clinic picker (MC6, `docs/multi-clinic-plan.md`): a select at the top of the grid pages narrowing the list to
 * one team, or "All teams". Offered to admins (`ViewAllTeams`: every team) and to staff in more than one team (their
 * own only; the API refuses another with a 400). Kept in the URL (`clinic`: a team's ID, or `all`) and remembered per
 * page with the account (`medapp.clinicPicker.<page>`), so the page opens on the last one chosen when its URL doesn't say.
 */
export const CLINIC_PICKER_PAGES = ['patients', 'pathways', 'referrals', 'appointments', 'results', 'letters', 'insights'] as const;
export type ClinicPickerPage = (typeof CLINIC_PICKER_PAGES)[number];

const KEYS = Object.fromEntries(CLINIC_PICKER_PAGES.map((page) => [page, defineSetting(`medapp.clinicPicker.${page}`)])) as Record<
  ClinicPickerPage,
  string
>;

type PickerUser = Pick<CurrentUser, 'permissions' | 'teams'>;

/** Whether the person is offered the picker: an admin, or staff in more than one team. */
export const isOfferedClinicPicker = (user: PickerUser) => can(user, 'ViewAllTeams') || (user.teams?.length ?? 0) > 1;

/**
 * The team a list is narrowed to (undefined for every team): the URL's, else the one remembered, else all. Never one
 * the person can't pick (a link from someone else, a remembered team they've since left): that shows them all.
 */
export function pickedClinic(user: PickerUser, urlClinic: string | undefined, remembered: string | null): string | undefined {
  if (!isOfferedClinicPicker(user)) return undefined;
  const value = urlClinic ?? parseClinic(remembered) ?? ALL_CLINICS;
  if (value === ALL_CLINICS) return undefined;
  if (!can(user, 'ViewAllTeams') && !(user.teams ?? []).some((t) => t.id.toLowerCase() === value)) return undefined;
  return value;
}

/**
 * The picked team for a route's loader, so it preloads what the page asks for: whoami's person (the guard has loaded
 * it) and the remembered choice (on the server, the account's settings; in the browser, this browser's).
 */
export function clinicFor(queryClient: QueryClient, page: ClinicPickerPage, urlClinic: string | undefined): string | undefined {
  const user = queryClient.getQueryData(whoamiQuery.queryKey);
  if (!user) return undefined;
  const key = KEYS[page];
  const stored = typeof window !== 'undefined' ? appSettings.get(key) : queryClient.getQueryData(settingsQuery.queryKey)?.[key];
  return pickedClinic(user, urlClinic, typeof stored === 'string' ? stored : null);
}

export interface ClinicPickerState {
  /** The team the list is narrowed to, for the API's `TeamId`; undefined for every team. */
  teamId: string | undefined;
  /** Its name, for a subtitle; undefined for every team. */
  teamName: string | undefined;
  /** The select, for the page's header actions; null when the person isn't offered it. */
  picker: ReactNode;
}

/**
 * The clinic picker for one page: what it's narrowed to, and the select. Choosing remembers it for the page and
 * calls `onChange` with the URL's new `clinic` (undefined for all teams, which the page then opens on).
 */
export function useClinicPicker(page: ClinicPickerPage, urlClinic: string | undefined, onChange: (clinic: string | undefined) => void): ClinicPickerState {
  const user = useCurrentUser();
  const [remembered, remember] = useAppSetting(KEYS[page]);
  const seesAll = can(user, 'ViewAllTeams');
  const isShown = isOfferedClinicPicker(user);
  const teams = useQuery({...teamsQuery(true), enabled: isShown && seesAll});
  const teamId = pickedClinic(user, urlClinic, remembered);

  // Archived teams too: their records are still there.
  const choices = seesAll
    ? (teams.data ?? []).map((t) => ({value: t.id?.toLowerCase() ?? '', label: t.isArchived ? `${t.name} (archived)` : (t.name ?? '')}))
    : [...(user.teams ?? [])].sort((a, b) => a.name.localeCompare(b.name)).map((t) => ({value: t.id.toLowerCase(), label: t.name}));
  const teamName = teamId ? (choices.find((c) => c.value === teamId)?.label ?? 'one team') : undefined;

  const choose = (value: string) => {
    const clinic = value === ALL_CLINICS ? undefined : value;
    remember(clinic ?? null);
    onChange(clinic);
  };

  const picker = isShown ? (
    <AnchoredSelector
      label="Team"
      isLabelHidden
      options={[{value: ALL_CLINICS, label: 'All teams'}, ...choices]}
      value={teamId ?? ALL_CLINICS}
      onChange={choose}
      width={220}
      containerStyle={NO_STATUS_LINE}
    />
  ) : null;
  return {teamId, teamName, picker};
}
