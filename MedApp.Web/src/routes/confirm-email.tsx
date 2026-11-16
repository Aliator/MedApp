import {createFileRoute} from '@tanstack/react-router';
import {ConfirmEmailPage} from '../pages/account/ConfirmEmailPage';

/**
 * An emailed confirmation link opens here, outside the signed-in layout (the link proves the address, signed in or
 * not). Its token is in the fragment, read by the page.
 */
export const Route = createFileRoute('/confirm-email')({
  component: ConfirmEmailPage,
});
