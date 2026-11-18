import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useState, type FormEvent} from 'react';
import type {EmailCodeSent} from '../../api/account';
import {ApiError, ApiValidationError, refusalMessage, soleFieldStatus} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useLoginTwoFactor, useResendSignInCode, type TwoFactorChallenge} from '../../auth/auth';
import {EmailCodeNote} from '../../components/account/EmailCodeNote';
import {OneTimeCodeField} from '../../components/forms/OneTimeCodeField';

/** The 401's `code` when this sign-in can't go on (it ran out, was used, or had too many wrong codes). */
export const SIGN_IN_EXPIRED = 'sign_in_expired';

const EXPIRED = 'This sign-in has run out. Enter your password again.';

/** When no more codes can be emailed for this sign-in. */
export const NO_MORE_CODES = 'No more codes can be sent for this sign-in. Use a recovery code, or go back and start again.';

/** When the password was right but no code could be emailed. */
export const CODE_NOT_SENT = "We couldn't email you a code just now. Use a recovery code, or try signing in again later.";

export interface TwoFactorStepProps {
  /** The password step's 202: the challenge, and how the code comes. */
  challenge: Pick<TwoFactorChallenge, 'challenge' | 'method' | 'emailCode'>;
  /** Back to the password step: with the reason when this sign-in can't go on, without when they chose to. */
  onRestart: (reason?: string) => void;
  /** Where to go once signed in, instead of the start page (the login page's `redirect`). */
  redirectTo?: string;
}

/**
 * Signing in's second step, for an account with two-factor authentication: a code from the authenticator app, or, for
 * an account using codes by email, the code MedApp just emailed (saying where, as the server masked the address, with
 * Resend and its wait), or a recovery code instead. A right one signs in as a password alone does for other accounts.
 * A wrong one stays here, saying so, as do a locked account and too many codes for the account (a 429, saying when to
 * try again); a sign-in that has run out (or had too many wrong codes) goes back to the password, saying why.
 */
export function TwoFactorStep({challenge: {challenge, method, emailCode}, onRestart, redirectTo}: TwoFactorStepProps) {
  const isEmail = method === 'Email';
  // With codes by email but none sent, only a recovery code can sign in.
  const [isRecovery, setIsRecovery] = useState(isEmail && emailCode === null);
  const [code, setCode] = useState('');
  const [sent, setSent] = useState<EmailCodeSent | null>(emailCode);
  const login = useLoginTwoFactor(redirectTo);
  const resend = useResendSignInCode();
  const announce = useAnnounce();

  const validation = login.error instanceof ApiValidationError ? login.error.errors : null;
  const failure = login.error instanceof ApiError ? login.error : null;
  const field = isRecovery ? 'recoveryCode' : 'code';
  const status = soleFieldStatus(validation, field);
  const wrongCode = isEmail
    ? "That code isn't right. Check the newest email and try again."
    : "That code isn't right. Check your authenticator app and try again.";
  const problem = !failure
    ? null
    : failure.status === 401
      ? (failure.title ?? wrongCode)
      : failure.status === 423
        ? (failure.title ?? 'This account is locked. Try again later.')
        : failure.status === 429
          ? (failure.title ?? 'Too many sign-in attempts. Wait a minute, then try again.')
          : 'Something went wrong signing in. Please try again.';
  const resendProblem = resend.isError ? refusalMessage(resend.error, 'A new code could not be sent. Please try again.') : null;

  const restartIfExpired = (error: unknown) => {
    if (error instanceof ApiError && error.status === 401 && error.code === SIGN_IN_EXPIRED) onRestart(error.title ?? EXPIRED);
  };

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    login.mutate(isRecovery ? {challenge, recoveryCode: code} : {challenge, code}, {onError: restartIfExpired});
  };

  const sendAgain = () =>
    resend.mutate(challenge, {
      onSuccess: (next) => {
        setSent(next);
        setCode('');
        login.reset();
        announce(`A new code was emailed to ${next.sentTo}.`);
      },
      onError: restartIfExpired,
    });

  const switchKind = () => {
    setIsRecovery((recovery) => !recovery);
    setCode('');
    login.reset();
  };

  const instructions = isRecovery
    ? 'Enter one of the recovery codes you saved when you set up two-factor authentication. Each works once.'
    : isEmail
      ? 'Enter the 6-digit code from the email.'
      : 'Enter the code from your authenticator app.';

  return (
    <form onSubmit={submit} noValidate>
      <VStack gap={4} hAlign="stretch">
        {isEmail && sent === null && <Banner status="warning" title={CODE_NOT_SENT} container="card" role="alert" />}
        {isEmail && sent !== null && !isRecovery && (
          <EmailCodeNote sent={sent} onResend={sendAgain} isResending={resend.isPending} exhaustedMessage={NO_MORE_CODES} />
        )}
        <Text type="body">{instructions}</Text>
        {problem && <Banner status="error" title={problem} container="card" role="alert" />}
        {resendProblem && !problem && <Banner status="error" title={resendProblem} container="card" role="alert" />}
        <OneTimeCodeField
          // A new field for each kind, so its focus and AutoFill start afresh.
          key={field}
          kind={isRecovery ? 'recovery' : isEmail ? 'email' : 'authenticator'}
          value={code}
          onChange={setCode}
          status={status}
          size="lg"
          hasAutoFocus
        />
        <Button type="submit" label="Sign in" variant="primary" size="lg" isLoading={login.isPending} />
        {!(isEmail && sent === null) && (
          <Button
            label={
              !isRecovery ? 'Use a recovery code instead' : isEmail ? 'Use a code from the email' : 'Use a code from your authenticator app'
            }
            variant="ghost"
            size="lg"
            isDisabled={login.isPending}
            onClick={switchKind}
          />
        )}
        <Button label="Back" variant="secondary" size="lg" isDisabled={login.isPending} onClick={() => onRestart()} />
      </VStack>
    </form>
  );
}
