import {createFileRoute} from '@tanstack/react-router';
import {ConfirmEmailPage} from '../pages/account/ConfirmEmailPage';

/**
 * An emailed confirmation link opens here, outside the signed-in layout: the page itself asks someone signed out to
 * sign in (and brings them back), since only the link's own account can confirm it. Its token is in the fragment,
 * read by the page.
 */
export const Route = createFileRoute('/confirm-email')({
  component: ConfirmEmailPage,
});
