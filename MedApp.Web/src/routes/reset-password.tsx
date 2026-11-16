import {createFileRoute} from '@tanstack/react-router';
import {ResetPasswordPage} from '../pages/login/ResetPasswordPage';

/** An emailed reset link opens here, outside the signed-in layout. Its token is in the fragment, read by the page. */
export const Route = createFileRoute('/reset-password')({
  component: ResetPasswordPage,
});
