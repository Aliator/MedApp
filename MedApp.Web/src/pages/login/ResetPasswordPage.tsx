import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {Link} from '@astryxdesign/core/Link';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQueryClient} from '@tanstack/react-query';
import {useState, type FormEvent} from 'react';
import {useResetPasswordWithLink} from '../../api/account';
import {allMessages, ApiError, ApiValidationError, fieldStatus, type ValidationErrors} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {whoamiQuery} from '../../auth/auth';
import {useEmailLink} from '../../auth/emailLink';
import {NewPasswordFields} from '../../components/forms/PasswordChangeFields';
import {StandalonePage} from '../../components/pages/StandalonePage';

const TITLE = 'Choose a new password';

/** Shown once the password has changed. */
export const PASSWORD_RESET =
  "Your password has been changed and you've been signed out everywhere. Sign in with your new password.";

/** Shown for a link that isn't whole (cut short in the email, or typed in), with a way to ask for another. */
export const BROKEN_LINK = "This link isn't complete. Open it from the email again, or ask for a new one.";

/** The Identity error code for a link that has run out or been used. */
const INVALID_TOKEN = 'InvalidToken';

/** Why the link can't be used, from a 400, or null when the 400 is about the password. */
function linkProblem(errors: ValidationErrors): string | null {
  const invalid = errors.identity?.find((e) => e.code === INVALID_TOKEN);
  if (invalid) return invalid.description;
  // The link's own parts, refused by validation (not the password).
  return errors.fields.userId || errors.fields.token ? BROKEN_LINK : null;
}

/** The new password's problems, on its field: validation's own, and Identity's (too short, too common, unchanged). */
function passwordProblems(errors: ValidationErrors) {
  return fieldStatus(errors, 'newPassword') ?? (errors.identity?.length ? {type: 'error' as const, message: allMessages(errors).join(' ')} : undefined);
}

/** A link that can't be used, with a way to ask for a new one. */
function UnusableLink({message}: {message: string}) {
  return (
    <StandalonePage title={TITLE}>
      <Banner status="error" container="card" role="alert" title={message} />
      <Button label="Ask for a new link" variant="primary" size="lg" href="/forgot-password" />
      <HStack hAlign="center">
        <Link href="/login">Back to sign in</Link>
      </HStack>
    </StandalonePage>
  );
}

/**
 * Choosing a new password from an emailed reset link (`/reset-password#user=…&token=…`, outside the signed-in
 * layout). The link's parts are in the address's fragment, which the page reads once it's in the browser
 * (`useEmailLink`); the server renders it waiting. A link that has run out or been used says so, with a way to ask for
 * another. Once changed, every session the account had has ended, so it offers to sign in.
 */
export function ResetPasswordPage() {
  const link = useEmailLink();
  const reset = useResetPasswordWithLink();
  const queryClient = useQueryClient();
  const announce = useAnnounce();
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [mismatch, setMismatch] = useState<string | null>(null);
  const [isDone, setIsDone] = useState(false);

  const validation = reset.error instanceof ApiValidationError ? reset.error.errors : null;
  const unusable = validation ? linkProblem(validation) : null;
  const nextStatus = validation ? passwordProblems(validation) : undefined;
  const failure = reset.error instanceof ApiError ? reset.error : null;
  const problem =
    mismatch ??
    (validation && !nextStatus ? allMessages(validation).join(' ') || validation.title : null) ??
    (failure
      ? failure.status === 429
        ? (failure.title ?? 'Too many attempts. Wait a few minutes, then try again.')
        : 'The password could not be changed. Please try again.'
      : null);

  if (link.status === 'reading') {
    return (
      <StandalonePage title={TITLE}>
        <Center axis="both" padding={4}>
          <Spinner label="Reading the link" />
        </Center>
      </StandalonePage>
    );
  }
  if (link.status === 'missing') return <UnusableLink message={BROKEN_LINK} />;
  if (unusable) return <UnusableLink message={unusable} />;

  if (isDone) {
    return (
      <StandalonePage title="Password changed">
        <Banner status="success" container="card" title={PASSWORD_RESET} />
        <Button label="Sign in" variant="primary" size="lg" href="/login" />
      </StandalonePage>
    );
  }

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMismatch(null);
    reset.reset();
    if (!next.trim()) return setMismatch('Please enter a new password.');
    if (next !== confirm) return setMismatch('New passwords do not match.');
    reset.mutate(
      {userId: link.userId, token: link.token, newPassword: next},
      {
        onSuccess: () => {
          setNext('');
          setConfirm('');
          // Every session ended: whoever this browser had signed in is signed out.
          queryClient.removeQueries({queryKey: whoamiQuery.queryKey});
          setIsDone(true);
          announce(PASSWORD_RESET);
        },
      },
    );
  };

  return (
    <StandalonePage title={TITLE}>
      <form onSubmit={submit} noValidate>
        <VStack gap={4} hAlign="stretch">
          <Text type="body">Choose a new password for your account. Every session it has will end, on every device.</Text>
          {problem && <Banner status="error" title={problem} container="card" role="alert" />}
          <NewPasswordFields
            next={next}
            confirm={confirm}
            onNextChange={setNext}
            onConfirmChange={setConfirm}
            nextStatus={nextStatus}
            size="lg"
          />
          <Button type="submit" label="Change password" variant="primary" size="lg" isLoading={reset.isPending} />
        </VStack>
      </form>
    </StandalonePage>
  );
}
