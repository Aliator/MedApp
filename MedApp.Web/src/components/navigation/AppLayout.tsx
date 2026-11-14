import {AppShell} from '@astryxdesign/core/AppShell';
import {Outlet} from '@tanstack/react-router';
import {AppSideNav} from './AppSideNav';

/** The signed-in frame: side navigation plus the current page (replaces Blazor's MainLayout). */
export function AppLayout() {
  return (
    <AppShell sideNav={<AppSideNav />} contentPadding={6}>
      <Outlet />
    </AppShell>
  );
}
