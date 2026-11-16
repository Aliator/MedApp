import {createFileRoute} from '@tanstack/react-router';
import {LoginPage, parseLoginSearch} from '../pages/login/LoginPage';

export const Route = createFileRoute('/login')({
  validateSearch: parseLoginSearch,
  component: LoginRoute,
});

function LoginRoute() {
  const {expired} = Route.useSearch();
  return <LoginPage isExpired={expired} />;
}
