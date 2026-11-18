import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState, type FormEvent} from 'react';
import {useEnableEmailCodes, useSendEmailCodesSetup, type EmailCodeSent} from '../../api/account';
import {allMessages, ApiValidationError, fieldStatus, otherMessages, refusalMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {OneTimeCodeField} from '../forms/OneTimeCodeField';
import {AppIcon} from '../shared/icons';
import {EmailCodeNote} from './EmailCodeNote';
import {RecoveryCodes} from './RecoveryCodes';

/** When no more codes can be sent while turning it on. */
export const SETUP_NO_MORE_CODES = 'No more codes can be sent for now. Cancel, and try again in a few minutes.';

export interface EmailCodesSetupProps {
  /** Whose account it is: named in the recovery codes' file, and given to password managers beside the password. */
  username: string;
  /** After the recovery codes have been shown, the button that carries on (e.g. "Done"). */
  doneLabel: string;
  onDone: () => void;
  /** Offers a Cancel button before it's on. */
  onCancel?: () => void;
  /** Codes by email have just turned on (the recovery codes are showing). */
  onEnabled?: () => void;
}

/**
 * Turning on two-factor authentication with codes by email, from start to finish: the account's password (so a
 * session alone can't), then MedApp emails a code to the account's confirmed address, which proves the mailbox is
 * theirs; the code turns it on, and the recovery codes show once (`RecoveryCodes`), until `onDone`. Resend sends a new
 * code (asking the API again with the same password), as often as the API allows. A 400 shows on its field; a 409
 * (already on, no confirmed address, or no email here), a locked account (423) or too many tries (429) shows in the
 * API's words. Used by Account Settings' Security page. Put it in a card; it lays out its own steps.
 */
export function EmailCodesSetup({username, doneLabel, onDone, onCancel, onEnabled}: EmailCodesSetupProps) {
  const send = useSendEmailCodesSetup();
  const enable = useEnableEmailCodes();
  const announce = useAnnounce();
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [sent, setSent] = useState<EmailCodeSent | null>(null);
  const [codes, setCodes] = useState<string[] | null>(null);

  const sendCode = (again: boolean) =>
    send.mutate(password, {
      onSuccess: (next) => {
        setSent(next);
        setCode('');
        enable.reset();
        if (again) announce(`A new code was emailed to ${next.sentTo}.`);
      },
    });

  const submitPassword = (event: FormEvent) => {
    event.preventDefault();
    sendCode(false);
  };

  const turnOn = (event: FormEvent) => {
    event.preventDefault();
    enable.mutate(code, {
      onSuccess: (recoveryCodes) => {
        // Nothing more to send: forget the password.
        setPassword('');
        setCode('');
        setSent(null);
        setCodes(recoveryCodes);
        onEnabled?.();
      },
    });
  };

  if (codes) {
    return (
      <VStack gap={4} hAlign="stretch">
        <RecoveryCodes codes={codes} username={username} announcement="Codes by email are on." />
        <HStack gap={2} hAlign="end">
          <Button label={doneLabel} variant="primary" onClick={onDone} />
        </HStack>
      </VStack>
    );
  }

  const sendErrors = send.error instanceof ApiValidationError ? send.error.errors : null;
  const sendProblem = send.isError && !sendErrors
    ? refusalMessage(send.error, 'A code could not be sent. Please try again.')
    : otherMessages(sendErrors, ['password']).join(' ') || null;

  if (!sent) {
    return (
      <form onSubmit={submitPassword} noValidate>
        <VStack gap={4} hAlign="stretch">
          <Text type="body">
            Each time you sign in, MedApp will email a 6-digit code to your confirmed email address, to enter after your
            password. Enter your password, and we&apos;ll email you a code to check it reaches you.
          </Text>
          {sendProblem && <Banner status="error" container="card" role="alert" title={sendProblem} />}
          {/* The account's name, hidden, so a password manager pairs the password with that. */}
          <input type="text" name="username" autoComplete="username" value={username} readOnly hidden />
          <TextInput
            label="Your password"
            type="password"
            value={password}
            onChange={setPassword}
            autoComplete="current-password"
            isRequired
            hasAutoFocus
            status={fieldStatus(sendErrors, 'password')}
          />
          <HStack gap={2} hAlign="end">
            {onCancel && <Button label="Cancel" variant="secondary" isDisabled={send.isPending} onClick={onCancel} />}
            <Button
              type="submit"
              label="Email me a code"
              variant="primary"
              icon={<Icon icon={AppIcon.Email} />}
              isDisabled={password === ''}
              isLoading={send.isPending}
            />
          </HStack>
        </VStack>
      </form>
    );
  }

  const enableErrors = enable.error instanceof ApiValidationError ? enable.error.errors : null;
  const enableProblem = enable.isError && !enableErrors
    ? refusalMessage(enable.error, 'Codes by email could not be turned on. Please try again.')
    : otherMessages(enableErrors, ['code']).join(' ') || null;
  // A failed resend says why above the code, in the API's words (e.g. too soon).
  const resendProblem = !send.isError
    ? null
    : sendErrors
      ? allMessages(sendErrors).join(' ')
      : refusalMessage(send.error, 'A new code could not be sent. Please try again.');
  const problem = enableProblem ?? resendProblem;

  return (
    <form onSubmit={turnOn} noValidate>
      <VStack gap={4} hAlign="stretch">
        <EmailCodeNote sent={sent} onResend={() => sendCode(true)} isResending={send.isPending} exhaustedMessage={SETUP_NO_MORE_CODES} />
        {problem && <Banner status="error" container="card" role="alert" title={problem} />}
        <OneTimeCodeField kind="email" value={code} onChange={setCode} status={fieldStatus(enableErrors, 'code')} hasAutoFocus />
        <HStack gap={2} hAlign="end">
          {onCancel && <Button label="Cancel" variant="secondary" isDisabled={enable.isPending} onClick={onCancel} />}
          <Button
            type="submit"
            label="Turn on codes by email"
            variant="primary"
            icon={<Icon icon={AppIcon.TwoFactorOn} />}
            isDisabled={code.trim() === ''}
            isLoading={enable.isPending}
          />
        </HStack>
      </VStack>
    </form>
  );
}
