import {createFileRoute} from '@tanstack/react-router';
import {lazy, Suspense} from 'react';
import {featuresQuery} from '../api/account';
import {preload} from '../api/preload';
import {LoginPage, parseLoginSearch} from '../pages/login/LoginPage';

export const Route = createFileRoute('/login')({
  validateSearch: parseLoginSearch,
  // Whether the server can send email: "Forgot your password?" shows only if it can.
  loader: ({context}) => preload(context.queryClient, featuresQuery),
  component: LoginRoute,
});

/** The light demo's account picker, in its build only (docs/agents/light-demo.md). */
const DemoLoginPage = __LIGHT_DEMO__ ? lazy(() => import('../demo/DemoLoginPage').then((m) => ({default: m.DemoLoginPage}))) : null;

function LoginRoute() {
  const {expired, redirect} = Route.useSearch();
  if (DemoLoginPage)
    return (
      <Suspense fallback={null}>
        <DemoLoginPage />
      </Suspense>
    );
  return <LoginPage isExpired={expired} redirectTo={redirect} />;
}
