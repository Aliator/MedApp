import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {Link} from '@astryxdesign/core/Link';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState, type FormEvent} from 'react';
import {featuresQuery, useForgotPassword} from '../../api/account';
import {allMessages, ApiError, ApiValidationError, fieldStatus} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {StandalonePage} from '../../components/pages/StandalonePage';

/** What's said once a link is asked for, whatever matched: it never tells whether an account exists. */
export const LINK_REQUESTED =
  "If the account exists and has a confirmed email address, we've sent a link to that address to reset the password. The link works once, for 30 minutes.";

/** Said when the server can't send email, so has no link to offer. */
export const NO_EMAIL = "Password reset by email isn't available here. Ask an admin to reset your password.";

/** Back to the login page. */
function BackToSignIn() {
  return (
    <HStack hAlign="center">
      <Link href="/login">Back to sign in</Link>
    </HStack>
  );
}

/**
 * "Forgot your password?" (`/forgot-password`, outside the signed-in layout): asks for a username or email address
 * and has the API email a reset link (`/reset-password`) to the account's confirmed address. It says the same
 * whatever matched, so it can't be used to find out who has an account. When the server can't send email, it says
 * so instead of offering a link it couldn't send.
 */
export function ForgotPasswordPage() {
  const features = useQuery(featuresQuery);
  const forgot = useForgotPassword();
  const announce = useAnnounce();
  const [usernameOrEmail, setUsernameOrEmail] = useState('');
  const [isSent, setIsSent] = useState(false);

  const validation = forgot.error instanceof ApiValidationError ? forgot.error.errors : null;
  const fieldError = fieldStatus(validation, 'usernameOrEmail');
  const failure = forgot.error instanceof ApiError ? forgot.error : null;
  const problem = validation
    ? fieldError
      ? null
      : allMessages(validation).join(' ') || validation.title
    : !failure
      ? null
      : failure.status === 429
        ? (failure.title ?? 'Too many requests. Wait a few minutes, then try again.')
        : 'The link could not be requested. Please try again.';

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    forgot.mutate(usernameOrEmail.trim(), {
      onSuccess: () => {
        setIsSent(true);
        // The message replaces the form; it isn't a live region, so it's said here.
        announce(LINK_REQUESTED);
      },
    });
  };

  if (features.isPending) {
    return (
      <StandalonePage title="Reset your password">
        <Center axis="both" padding={4}>
          <Spinner />
        </Center>
      </StandalonePage>
    );
  }

  if (!features.data?.canSendEmail) {
    return (
      <StandalonePage title="Reset your password">
        <Text type="body">{NO_EMAIL}</Text>
        <BackToSignIn />
      </StandalonePage>
    );
  }

  if (isSent) {
    return (
      <StandalonePage title="Check your email">
        <Banner status="success" container="card" title={LINK_REQUESTED} />
        <Text type="body" color="secondary">
          If no email arrives in a few minutes, check your spam folder, or ask an admin to reset your password.
        </Text>
        <BackToSignIn />
      </StandalonePage>
    );
  }

  return (
    <StandalonePage title="Reset your password">
      <form onSubmit={submit} noValidate>
        <VStack gap={4} hAlign="stretch">
          <Text type="body">
            Enter your username or email address. If your account has a confirmed email address, we&apos;ll send it a
            link to choose a new password.
          </Text>
          {problem && <Banner status="error" title={problem} container="card" role="alert" />}
          <TextInput
            label="Username or email address"
            value={usernameOrEmail}
            onChange={setUsernameOrEmail}
            placeholder="Enter your username or email address"
            autoComplete="username"
            autoCapitalize="off"
            hasAutoCorrect={false}
            hasSpellCheck={false}
            isRequired
            hasAutoFocus
            status={fieldError}
            size="lg"
          />
          <Button type="submit" label="Send Reset Link" variant="primary" size="lg" isLoading={forgot.isPending} />
          <BackToSignIn />
        </VStack>
      </form>
    </StandalonePage>
  );
}
