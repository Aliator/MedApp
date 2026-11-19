import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Link} from '@astryxdesign/core/Link';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState, type FormEvent} from 'react';
import {featuresQuery} from '../../api/account';
import {ApiError, ApiValidationError, fieldStatus} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useLogin, type TwoFactorChallenge} from '../../auth/auth';
import {safeRedirectPath} from '../../auth/redirect';
import {StandalonePage} from '../../components/pages/StandalonePage';
import {TwoFactorStep} from './TwoFactorStep';

export interface LoginSearch {
  /** Came here because the session timed out (`SessionTimeout`). */
  expired?: true;
  /** Where to go once signed in, instead of the start page: a path in this app (`safeRedirectPath`). */
  redirect?: string;
}

/**
 * The login page's URL search: `?expired=true` after a session timed out, and `?redirect=/path` to come back to a
 * page after signing in (`/confirm-email` does), kept only if it's a safe path in this app. Anything else is ignored.
 */
export function parseLoginSearch(search: Record<string, unknown>): LoginSearch {
  const redirect = safeRedirectPath(search.redirect);
  return {
    ...(search.expired === true || search.expired === 'true' ? {expired: true as const} : {}),
    ...(redirect ? {redirect} : {}),
  };
}

/** Said to screen readers as the code step replaces the password, which they wouldn't otherwise hear about. */
export const CODE_STEP_ANNOUNCEMENT = 'Password accepted. Enter the code from your authenticator app.';

/** The same, for an account using codes by email. */
export const EMAIL_CODE_STEP_ANNOUNCEMENT = 'Password accepted. We emailed you a code: enter it to sign in.';

/** A wrong username or password (a 401): which one isn't said, so usernames can't be guessed. */
const INCORRECT_CREDENTIALS = 'Username or password is incorrect.';

/**
 * Sign in (replaces Blazor's Login page). Field errors from a 400 go on their fields. An account locked after too
 * many wrong passwords (a 423) says so, and for how long, in the API's words. So do too many attempts in a short time
 * (a 429, from the API's sign-in throttling), with how long to wait. A 401 (a wrong username or password) shows
 * "Username or password is incorrect."; any other failure (a server error) asks them to try again. After a session timed out (`isExpired`),
 * it says so until a sign-in is tried.
 *
 * An account with two-factor authentication answers the password with a challenge (a 202): the password form gives
 * way to `TwoFactorStep`, for a code from the authenticator app (or the one emailed, for an account using codes by
 * email) or a recovery code. If that sign-in runs out, it comes back here, saying why. "Forgot your password?" shows
 * only when the server can send email. Signed in, it goes to the start page, or to `redirectTo` (already checked by
 * `parseLoginSearch`).
 */
export function LoginPage({isExpired = false, redirectTo}: {isExpired?: boolean; redirectTo?: string}) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [challenge, setChallenge] = useState<TwoFactorChallenge | null>(null);
  // Why the code step sent them back here (it ran out, or had too many wrong codes), in the API's words.
  const [restartReason, setRestartReason] = useState<string | null>(null);
  const login = useLogin(redirectTo);
  const announce = useAnnounce();
  const {data: features} = useQuery(featuresQuery);

  const validation = login.error instanceof ApiValidationError ? login.error.errors : null;
  const hasFieldErrors = validation !== null && Object.keys(validation.fields).length > 0;
  const showBanner = login.isError && !hasFieldErrors;
  const failure = login.error instanceof ApiError ? login.error : null;
  const bannerTitle =
    failure?.status === 423
      ? (failure.title ?? 'This account is locked. Try again later.')
      : failure?.status === 429
        ? (failure.title ?? 'Too many sign-in attempts. Wait a minute, then try again.')
        : failure?.status === 401
          ? INCORRECT_CREDENTIALS
          : 'Sign-in failed. Please try again.';

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setRestartReason(null);
    login.mutate(
      {username, password},
      {
        onSuccess: (result) => {
          if (result.kind !== 'twoFactor') return;
          setChallenge(result);
          announce(result.method === 'Email' ? EMAIL_CODE_STEP_ANNOUNCEMENT : CODE_STEP_ANNOUNCEMENT);
        },
      },
    );
  };

  /** Back to the password, to start again: with `reason` when the code step ran out. */
  const restart = (reason?: string) => {
    setChallenge(null);
    setPassword('');
    login.reset();
    setRestartReason(reason ?? null);
  };

  return (
    <StandalonePage title="Sign in" maxWidth={400}>
      {challenge !== null ? (
        <TwoFactorStep challenge={challenge} onRestart={restart} redirectTo={redirectTo} />
      ) : (
        <form onSubmit={handleSubmit} noValidate>
          <VStack gap={4} hAlign="stretch">
            {showBanner && <Banner status="error" title={bannerTitle} container="card" role="alert" />}
            {restartReason && !login.isError && <Banner status="warning" title={restartReason} container="card" role="alert" />}
            {isExpired && !login.isError && !restartReason && (
              <Banner
                status="info"
                title="Your session timed out, so you were signed out. Sign in again to carry on."
                container="card"
                // An alert, so it's read out as it appears: a live region that arrives already filled often isn't.
                role="alert"
              />
            )}

            <TextInput
              label="Username"
              value={username}
              onChange={setUsername}
              placeholder="Enter your username"
              autoComplete="username"
              isRequired
              status={fieldStatus(validation, 'username')}
              size="lg"
            />

            <TextInput
              label="Password"
              type="password"
              value={password}
              onChange={setPassword}
              placeholder="Enter your password"
              autoComplete="current-password"
              isRequired
              status={fieldStatus(validation, 'password')}
              size="lg"
            />

            <Button type="submit" label="Sign In" variant="primary" size="lg" isLoading={login.isPending} />
            {features?.canSendEmail && (
              <HStack hAlign="center">
                <Link href="/forgot-password">Forgot your password?</Link>
              </HStack>
            )}
          </VStack>
        </form>
      )}
    </StandalonePage>
  );
}
