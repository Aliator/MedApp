import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {Spinner} from '@astryxdesign/core/Spinner';
import {Text} from '@astryxdesign/core/Text';
import {useConfirmEmail} from '../../api/account';
import {allMessages, ApiValidationError} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useEmailLink} from '../../auth/emailLink';
import {StandalonePage} from '../../components/pages/StandalonePage';

const TITLE = 'Confirm your email address';

/** Shown once the address is confirmed. */
export const EMAIL_CONFIRMED = 'Your email address is confirmed. MedApp will use it from now on.';

/** Shown for a link that isn't whole (cut short in the email, or typed in). */
export const BROKEN_EMAIL_LINK = "This link isn't complete. Open it from the email again.";

/** What to do about a link that can't be used: ask for another where the address was given. */
const NEW_LINK_HINT = 'To get a new link, change your email address again in Account Settings, or ask an admin.';

/**
 * Confirming an email address from an emailed link (`/confirm-email#user=…&token=…`, outside the signed-in layout,
 * since the link proves the address on its own). It waits for a click on "Confirm email address" rather than
 * confirming as it opens, so a mail scanner that follows links can't confirm it. The link's parts are in the
 * address's fragment, read once the page is in the browser (`useEmailLink`).
 */
export function ConfirmEmailPage() {
  const link = useEmailLink();
  const confirm = useConfirmEmail();
  const announce = useAnnounce();

  if (link.status === 'reading') {
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

  const problem =
    confirm.error instanceof ApiValidationError
      ? allMessages(confirm.error.errors).join(' ') || confirm.error.errors.title
      : confirm.isError
        ? 'The email address could not be confirmed. Please try again.'
        : null;
  const isRefused = confirm.error instanceof ApiValidationError;

  return (
    <StandalonePage title={TITLE}>
      <Text type="body">Confirm this is your email address, so MedApp can send you emails such as password reset links.</Text>
      {problem && (
        <Banner status="error" container="card" role="alert" title={problem} description={isRefused ? NEW_LINK_HINT : undefined} />
      )}
      {!isRefused && (
        <Button
          label="Confirm email address"
          variant="primary"
          size="lg"
          isLoading={confirm.isPending}
          onClick={() =>
            confirm.mutate({userId: link.userId, token: link.token}, {onSuccess: () => announce(EMAIL_CONFIRMED)})
          }
        />
      )}
      {isRefused && continueButton}
    </StandalonePage>
  );
}
