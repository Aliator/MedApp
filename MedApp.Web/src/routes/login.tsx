import {createFileRoute} from '@tanstack/react-router';
import {featuresQuery} from '../api/account';
import {preload} from '../api/preload';
import {LoginPage, parseLoginSearch} from '../pages/login/LoginPage';

export const Route = createFileRoute('/login')({
  validateSearch: parseLoginSearch,
  // Whether the server can send email: "Forgot your password?" shows only if it can.
  loader: ({context}) => preload(context.queryClient, featuresQuery),
  component: LoginRoute,
});

function LoginRoute() {
  const {expired} = Route.useSearch();
  return <LoginPage isExpired={expired} />;
}
