import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useState, type FormEvent} from 'react';
import {ApiError, ApiValidationError, soleFieldStatus} from '../../api/errors';
import {useLoginTwoFactor} from '../../auth/auth';
import {OneTimeCodeField} from '../../components/forms/OneTimeCodeField';

/** The 401's `code` when this sign-in can't go on (it ran out, was used, or had too many wrong codes). */
export const SIGN_IN_EXPIRED = 'sign_in_expired';

const EXPIRED = 'This sign-in has run out. Enter your password again.';

export interface TwoFactorStepProps {
  /** The challenge the password step's 202 gave. */
  challenge: string;
  /** Back to the password step: with the reason when this sign-in can't go on, without when they chose to. */
  onRestart: (reason?: string) => void;
  /** Where to go once signed in, instead of the start page (the login page's `redirect`). */
  redirectTo?: string;
}

/**
 * Signing in's second step, for an account with two-factor authentication: a code from the authenticator app, or a
 * recovery code instead. A right one signs in as a password alone does for other accounts. A wrong one stays here,
 * saying so, as do a locked account and too many codes for the account (a 429, saying when to try again); a sign-in
 * that has run out (or had too many wrong codes) goes back to the password, saying why.
 */
export function TwoFactorStep({challenge, onRestart, redirectTo}: TwoFactorStepProps) {
  const [isRecovery, setIsRecovery] = useState(false);
  const [code, setCode] = useState('');
  const login = useLoginTwoFactor(redirectTo);

  const validation = login.error instanceof ApiValidationError ? login.error.errors : null;
  const failure = login.error instanceof ApiError ? login.error : null;
  const field = isRecovery ? 'recoveryCode' : 'code';
  const status = soleFieldStatus(validation, field);
  const problem = !failure
    ? null
    : failure.status === 401
      ? (failure.title ?? "That code isn't right. Check your authenticator app and try again.")
      : failure.status === 423
        ? (failure.title ?? 'This account is locked. Try again later.')
        : failure.status === 429
          ? (failure.title ?? 'Too many sign-in attempts. Wait a minute, then try again.')
          : 'Something went wrong signing in. Please try again.';

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    login.mutate(isRecovery ? {challenge, recoveryCode: code} : {challenge, code}, {
      onError: (error) => {
        if (error instanceof ApiError && error.status === 401 && error.code === SIGN_IN_EXPIRED) onRestart(error.title ?? EXPIRED);
      },
    });
  };

  const switchKind = () => {
    setIsRecovery((recovery) => !recovery);
    setCode('');
    login.reset();
  };

  return (
    <form onSubmit={submit} noValidate>
      <VStack gap={4} hAlign="stretch">
        <Text type="body">
          {isRecovery
            ? "Enter one of the recovery codes you saved when you set up two-factor authentication. Each works once."
            : 'Enter the code from your authenticator app.'}
        </Text>
        {problem && <Banner status="error" title={problem} container="card" role="alert" />}
        <OneTimeCodeField
          // A new field for each kind, so its focus and AutoFill start afresh.
          key={field}
          kind={isRecovery ? 'recovery' : 'authenticator'}
          value={code}
          onChange={setCode}
          status={status}
          size="lg"
          hasAutoFocus
        />
        <Button type="submit" label="Sign in" variant="primary" size="lg" isLoading={login.isPending} />
        <Button
          label={isRecovery ? 'Use a code from your authenticator app' : 'Use a recovery code instead'}
          variant="ghost"
          size="lg"
          isDisabled={login.isPending}
          onClick={switchKind}
        />
        <Button label="Back" variant="secondary" size="lg" isDisabled={login.isPending} onClick={() => onRestart()} />
      </VStack>
    </form>
  );
}
