import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {NavIcon} from '@astryxdesign/core/NavIcon';
import {SideNav, SideNavHeading, SideNavItem, SideNavSection} from '@astryxdesign/core/SideNav';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useRouterState} from '@tanstack/react-router';
import {patientQuery} from '../../api/patients';
import {useColorMode} from '../../app/colorMode';
import {useCurrentUser, useLogout} from '../../auth/auth';
import {AppIcon} from '../shared/icons';

/** Is `path` the current page or one under it? Home only matches itself (Blazor's NavLinkMatch.All). */
export function isCurrentSection(pathname: string, path: string): boolean {
  if (path === '/') return pathname === '/';
  return pathname === path || pathname.startsWith(`${path}/`);
}

const PATIENT_PAGE = /^\/patients\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?:\/|$)/i;

/** The patient whose pages (view, edit, procedures) are open, from the path; null elsewhere. */
export function openPatientId(pathname: string): string | null {
  return PATIENT_PAGE.exec(pathname)?.[1] ?? null;
}

/**
 * Side navigation (replaces Blazor's NavMenu): main pages, admin pages for admins, and the account footer.
 * While a patient's pages are open, that patient appears under Patients as an indented sub-item, with the
 * open arrow on Patients.
 */
export function AppSideNav() {
  const user = useCurrentUser();
  const logout = useLogout();
  const {mode, toggle} = useColorMode();
  const pathname = useRouterState({select: (state) => state.location.pathname});
  const isAt = (path: string) => isCurrentSection(pathname, path);
  const nextMode = mode === 'dark' ? 'light' : 'dark';

  const patientId = openPatientId(pathname);
  // The page has usually loaded the patient already, so this is the cached copy.
  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== null});
  const patientName = patient.data ? `${patient.data.firstName} ${patient.data.lastName}` : 'Patient';

  return (
    <SideNav
      header={
        <SideNavHeading
          heading="MedApp"
          headingHref="/"
          icon={<NavIcon icon={<Icon icon={AppIcon.Shield} size="sm" />} />}
        />
      }
      footer={
        <SideNavSection title="Account" isHeaderHidden>
          <SideNavItem
            label={user.username}
            icon={AppIcon.Account}
            href="/account"
            isSelected={isAt('/account')}
            endContent={
              <Text type="supporting" color="secondary">
                {user.isAdmin ? 'Administrator' : 'User'}
              </Text>
            }
          />
          <SideNavItem
            label="Logout"
            icon={AppIcon.Logout}
            isDisabled={logout.isPending}
            onClick={() => logout.mutate()}
          />
        </SideNavSection>
      }
      footerIcons={
        <IconButton
          label={`Switch to ${nextMode} mode`}
          tooltip={`Switch to ${nextMode} mode`}
          icon={<Icon icon={mode === 'dark' ? AppIcon.LightMode : AppIcon.DarkMode} />}
          variant="ghost"
          onClick={toggle}
        />
      }>
      <SideNavSection title="Main" isHeaderHidden>
        <SideNavItem label="Home" icon={AppIcon.Home} href="/" isSelected={isAt('/')} />
        {patientId ? (
          <SideNavItem label="Patients" icon={AppIcon.Patients} href="/patients" collapsible>
            <SideNavItem label={patientName} icon={AppIcon.SubItem} href={`/patients/${patientId}`} isSelected />
          </SideNavItem>
        ) : (
          <SideNavItem label="Patients" icon={AppIcon.Patients} href="/patients" isSelected={isAt('/patients')} />
        )}
        <SideNavItem label="Procedures" icon={AppIcon.Procedures} href="/procedures" isSelected={isAt('/procedures')} />
      </SideNavSection>
      {user.isAdmin && (
        <SideNavSection title="Administration">
          <SideNavItem label="Users" icon={AppIcon.Users} href="/admin/users" isSelected={isAt('/admin/users')} />
        </SideNavSection>
      )}
    </SideNav>
  );
}
