import {createFileRoute} from '@tanstack/react-router';
import {featuresQuery} from '../api/account';
import {preload} from '../api/preload';
import {ForgotPasswordPage} from '../pages/login/ForgotPasswordPage';

/** Outside the signed-in layout, like `/login`. Whether the server can send email decides what it offers. */
export const Route = createFileRoute('/forgot-password')({
  loader: ({context}) => preload(context.queryClient, featuresQuery),
  component: ForgotPasswordPage,
});
