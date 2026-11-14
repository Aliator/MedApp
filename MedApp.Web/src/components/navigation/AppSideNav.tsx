import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {NavIcon} from '@astryxdesign/core/NavIcon';
import {SideNav, SideNavHeading, SideNavItem, SideNavSection} from '@astryxdesign/core/SideNav';
import {Text} from '@astryxdesign/core/Text';
import {useRouterState} from '@tanstack/react-router';
import {useColorMode} from '../../app/colorMode';
import {useCurrentUser, useLogout} from '../../auth/auth';
import {AppIcon} from '../shared/icons';

/** Is `path` the current page or one under it? Home only matches itself (Blazor's NavLinkMatch.All). */
export function isCurrentSection(pathname: string, path: string): boolean {
  if (path === '/') return pathname === '/';
  return pathname === path || pathname.startsWith(`${path}/`);
}

/** Side navigation (replaces Blazor's NavMenu): main pages, admin pages for admins, and the account footer. */
export function AppSideNav() {
  const user = useCurrentUser();
  const logout = useLogout();
  const {mode, toggle} = useColorMode();
  const pathname = useRouterState({select: (state) => state.location.pathname});
  const isAt = (path: string) => isCurrentSection(pathname, path);
  const nextMode = mode === 'dark' ? 'light' : 'dark';

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
        <SideNavItem label="Patients" icon={AppIcon.Patients} href="/patients" isSelected={isAt('/patients')} />
      </SideNavSection>
      {user.isAdmin && (
        <SideNavSection title="Administration">
          <SideNavItem label="Users" icon={AppIcon.Users} href="/admin/users" isSelected={isAt('/admin/users')} />
        </SideNavSection>
      )}
    </SideNav>
  );
}
