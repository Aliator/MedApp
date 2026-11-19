import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {Icon} from '@astryxdesign/core/Icon';
import {Spinner} from '@astryxdesign/core/Spinner';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useConfirmEmail} from '../../api/account';
import {allMessages, ApiError, ApiValidationError} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useLogout, whoamiQuery} from '../../auth/auth';
import {pendingEmailConfirmation, useEmailLink} from '../../auth/emailLink';
import {StandalonePage} from '../../components/pages/StandalonePage';
import {AppIcon} from '../../components/shared/icons';

const TITLE = 'Confirm your email address';

/** Where this page is, to come back to after signing in. */
const CONFIRM_EMAIL_PATH = '/confirm-email';

/** Shown once the address is confirmed. */
export const EMAIL_CONFIRMED = 'Your email address is confirmed. MedApp will use it from now on.';

/** Shown for a link that isn't whole (cut short in the email, or typed in). */
export const BROKEN_EMAIL_LINK = "This link isn't complete. Open it from the email again.";

/** Shown to someone signed out: the link's account must be signed in to confirm it. */
export const SIGN_IN_TO_CONFIRM = 'Sign in to confirm your email address';

/** The Identity error's code when the link is for another account than the one signed in. */
export const WRONG_ACCOUNT = 'WrongAccount';

/** What to do about a link that can't be used: ask for another where the address was given. */
const NEW_LINK_HINT = 'To get a new link, change your email address again in Account Settings, or ask an admin.';

/** What to do when signed in as someone else. */
const WRONG_ACCOUNT_HINT = 'Sign out, then open the link from the email again and sign in with the account it was sent for.';

/**
 * Confirming an email address from an emailed link (`/confirm-email#user=…&token=…`, outside the signed-in layout).
 * The API confirms it only for the link's own account, signed in, so a link that reached the wrong inbox can't be
 * used by whoever holds it:
 * - Signed out, it says to sign in, keeps the link in this tab (`pendingEmailConfirmation`, sessionStorage), and sends
 *   them to `/login?redirect=/confirm-email`, which brings them back here to confirm it.
 * - Signed in, it waits for a click on "Confirm email address" rather than confirming as it opens, so a mail scanner
 *   that follows links can't confirm it. The kept link is cleared once it's used or refused.
 * - Signed in as another account (the API's `WrongAccount`), it says so and offers to sign out.
 * The link's parts are in the address's fragment, read once the page is in the browser (`useEmailLink`).
 */
export function ConfirmEmailPage() {
  const link = useEmailLink(pendingEmailConfirmation.read);
  const {data: me, isPending: isCheckingUser} = useQuery({...whoamiQuery, enabled: link.status === 'found'});
  const queryClient = useQueryClient();
  const confirm = useConfirmEmail();
  const logout = useLogout();
  const navigate = useNavigate();
  const announce = useAnnounce();

  if (link.status === 'reading' || (link.status === 'found' && isCheckingUser)) {
    return (
      <StandalonePage title={TITLE}>
        <Center axis="both" padding={4}>
          <Spinner label="Reading the link" />
        </Center>
      </StandalonePage>
    );
  }

  const continueButton = <Button label="Continue to MedApp" variant="secondary" size="lg" href="/" />;

  if (link.status === 'missing') {
    return (
      <StandalonePage title={TITLE}>
        <Banner status="error" container="card" role="alert" title={BROKEN_EMAIL_LINK} description={NEW_LINK_HINT} />
        {continueButton}
      </StandalonePage>
    );
  }

  if (confirm.isSuccess) {
    return (
      <StandalonePage title="Email address confirmed">
        <Banner status="success" container="card" title={EMAIL_CONFIRMED} />
        <Button label="Continue to MedApp" variant="primary" size="lg" href="/" />
      </StandalonePage>
    );
  }

  if (!me?.isAuthenticated) {
    const signIn = () => {
      pendingEmailConfirmation.save({userId: link.userId, token: link.token});
      void navigate({to: '/login', search: {redirect: CONFIRM_EMAIL_PATH}});
    };
    return (
      <StandalonePage title={TITLE}>
        <Banner
          status="info"
          container="card"
          title={SIGN_IN_TO_CONFIRM}
          description="Sign in with the account this address was given for. You'll come back here to confirm it."
        />
        <Button label="Sign In" variant="primary" size="lg" icon={<Icon icon={AppIcon.Login} />} onClick={signIn} />
      </StandalonePage>
    );
  }

  const refusal = confirm.error instanceof ApiValidationError ? confirm.error.errors : null;
  const wrongAccount = refusal?.identity?.find((problem) => problem.code === WRONG_ACCOUNT);

  if (wrongAccount) {
    return (
      <StandalonePage title={TITLE}>
        <Banner status="error" container="card" role="alert" title={wrongAccount.description} description={WRONG_ACCOUNT_HINT} />
        <Button
          label="Sign Out"
          variant="primary"
          size="lg"
          icon={<Icon icon={AppIcon.Logout} />}
          isLoading={logout.isPending}
          onClick={() => logout.mutate()}
        />
        {continueButton}
      </StandalonePage>
    );
  }

  const problem = refusal
    ? allMessages(refusal).join(' ') || refusal.title
    : confirm.isError
      ? 'The email address could not be confirmed. Please try again.'
      : null;
  const isRefused = refusal !== null;

  const confirmLink = () =>
    confirm.mutate(
      {userId: link.userId, token: link.token},
      {
        onSuccess: () => announce(EMAIL_CONFIRMED),
        onError: (error) => {
          // The session ended meanwhile: ask who's signed in again, which brings back the sign-in step.
          if (error instanceof ApiError && error.status === 401) void queryClient.invalidateQueries({queryKey: whoamiQuery.queryKey});
        },
        // Used or refused, the kept link has done its job.
        onSettled: () => pendingEmailConfirmation.clear(),
      },
    );

  return (
    <StandalonePage title={TITLE}>
      <Text type="body">
        Confirm this is your email address, so MedApp can send you emails such as password reset links. You&apos;re
        signed in as {me.username}.
      </Text>
      {problem && (
        <Banner status="error" container="card" role="alert" title={problem} description={isRefused ? NEW_LINK_HINT : undefined} />
      )}
      {!isRefused && (
        <Button label="Confirm Email Address" variant="primary" size="lg" isLoading={confirm.isPending} onClick={confirmLink} />
      )}
      {isRefused && continueButton}
    </StandalonePage>
  );
}
