import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {useClipboard} from '@astryxdesign/core/hooks';
import {Icon} from '@astryxdesign/core/Icon';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQueryClient} from '@tanstack/react-query';
import {useEffect, useRef, useState, type FormEvent} from 'react';
import {
  twoFactorStatusQuery,
  useBeginAuthenticatorSetup,
  useEnableTwoFactor,
  useSendTwoFactorEmailCode,
  type AuthenticatorSetup,
} from '../../api/account';
import {ApiValidationError, fieldStatus, otherMessages, refusalMessage} from '../../api/errors';
import {OneTimeCodeField} from '../forms/OneTimeCodeField';
import {AppIcon} from '../shared/icons';
import {EmailMeACode} from './EmailMeACode';
import {RecoveryCodes} from './RecoveryCodes';

/** How big the QR code is drawn, in pixels: big enough for a phone's camera across a desk. */
const QR_DISPLAY_SIZE = 200;

/**
 * A QR code from its SVG path (the dark modules) in a square `size` modules wide, quiet zone included. QR codes must be
 * dark on light for cameras to read them, whatever the app's colours or mode, so this is the one place with fixed
 * colours (black on white) rather than the theme's tokens.
 */
function QrCode({path, size}: {path: string; size: number}) {
  return (
    <svg
      role="img"
      aria-label="QR code for your authenticator app"
      viewBox={`0 0 ${size} ${size}`}
      width={QR_DISPLAY_SIZE}
      height={QR_DISPLAY_SIZE}
      shapeRendering="crispEdges">
      <rect width={size} height={size} fill="#ffffff" />
      <path d={path} fill="#000000" />
    </svg>
  );
}

/** The key as text, for an app that can't scan, with a button to copy it. */
function SharedKey({sharedKey}: {sharedKey: string}) {
  const {copy, isCopied} = useClipboard({announce: 'Copied'});
  return (
    <VStack gap={2} hAlign="center">
      <Text type="supporting" color="secondary" justify="center">
        Can&apos;t scan it? Enter this key in the app instead:
      </Text>
      <Text type="code" size="lg" justify="center">
        {sharedKey}
      </Text>
      <Button
        label={isCopied ? 'Copied' : 'Copy key'}
        size="sm"
        icon={<Icon icon={isCopied ? AppIcon.Check : AppIcon.Copy} />}
        // Without the spaces that group it for reading: apps take it either way, and some only without.
        onClick={() => void copy(sharedKey.replace(/\s/g, ''))}
      />
    </VStack>
  );
}

export interface TwoFactorSetupProps {
  /** Whose account it is: named in the recovery codes' file, and given to password managers beside the password. */
  username: string;
  /** After the recovery codes have been shown, the button that carries on (e.g. "Continue" or "Done"). */
  doneLabel: string;
  onDone: () => void;
  /** Offers a Cancel button before two-factor is on, when there's somewhere to go back to. */
  onCancel?: () => void;
  /** Two-factor has just turned on (the recovery codes are showing). */
  onEnabled?: () => void;
  /**
   * The account signs in with codes by email now, so switching to the app must also prove that factor: a code emailed
   * to it ("Email me a code", after the password), or a recovery code.
   */
  switchingFromEmail?: boolean;
}

/**
 * The fields turning it on shows, whose 400 messages show on them. The current code's field shows only while switching
 * from codes by email; otherwise its message goes in the banner.
 */
const PASSWORD_AND_CODES = ['password', 'code', 'currentCode'];
const PASSWORD_AND_CODE = ['password', 'code'];

/** Why switching asks for a code from the email too. */
export const PROVE_CURRENT =
  "You sign in with codes by email now, so we also need a code we email you (or one of your recovery codes) to check it's you before the app replaces them.";

/**
 * Setting up two-factor authentication with an authenticator app, from start to finish: it asks the API for a new key
 * as it opens, shows it as a QR code to scan and as text to type, then turns two-factor on with the account's password
 * (so a session alone can't) and a code from the app, and shows the recovery codes (`RecoveryCodes`) once, until
 * `onDone`. A 400 shows on its field; a 409 (already on), a locked account (423) or too many tries (429) shows in the
 * API's words. Used by Account Settings' Security page (also to switch from codes by email to an app: the API allows
 * this set-up while codes by email are on, and two-factor stays on throughout) and by `/set-up-two-factor`, where an
 * admin is held until they've set it up. Put it in a card; it lays out its own steps.
 */
export function TwoFactorSetup({username, doneLabel, onDone, onCancel, onEnabled, switchingFromEmail = false}: TwoFactorSetupProps) {
  const begin = useBeginAuthenticatorSetup();
  const enable = useEnableTwoFactor();
  const send = useSendTwoFactorEmailCode();
  const queryClient = useQueryClient();
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [currentCode, setCurrentCode] = useState('');
  const [codes, setCodes] = useState<string[] | null>(null);
  const [setup, setSetup] = useState<AuthenticatorSetup | null>(null);

  const start = () => begin.mutate(undefined, {onSuccess: setSetup});
  // A new key as it opens: once, even when effects run twice (React's StrictMode), as each replaces the last.
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    start();
    // Only as it opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const turnOn = (event: FormEvent) => {
    event.preventDefault();
    enable.mutate(
      {password, code, ...(switchingFromEmail ? {currentCode} : {})},
      {
        onSuccess: (recoveryCodes) => {
          setPassword('');
          setCode('');
          setCurrentCode('');
          send.reset();
          // The key is no longer needed: from here, only the codes show.
          setSetup(null);
          setCodes(recoveryCodes);
          onEnabled?.();
        },
        // A 400 on the current code: perhaps asked for without its field showing, as the status we had is out of date
        // (codes by email turned on in another tab), so load it again, and the field appears if it's needed.
        onError: (error) => {
          if (error instanceof ApiValidationError && error.errors.fields.currentCode)
            void queryClient.invalidateQueries({queryKey: twoFactorStatusQuery.queryKey});
        },
      },
    );
  };

  if (codes) {
    return (
      <VStack gap={4} hAlign="stretch">
        <RecoveryCodes codes={codes} username={username} announcement="Two-factor authentication is on." />
        <HStack gap={2} hAlign="end">
          <Button label={doneLabel} variant="primary" onClick={onDone} />
        </HStack>
      </VStack>
    );
  }

  if (begin.isError) {
    return (
      <VStack gap={4} hAlign="stretch">
        <Banner
          status="error"
          container="card"
          role="alert"
          title={refusalMessage(begin.error, 'A key for your authenticator app could not be made. Please try again.')}
        />
        <HStack gap={2} hAlign="end">
          {onCancel && <Button label="Cancel" variant="secondary" onClick={onCancel} />}
          <Button label="Try again" variant="primary" icon={<Icon icon={AppIcon.Refresh} />} onClick={start} />
        </HStack>
      </VStack>
    );
  }

  if (!setup) {
    return (
      <Center axis="both" padding={6}>
        <Spinner label="Making a key for your authenticator app" />
      </Center>
    );
  }

  const validation = enable.error instanceof ApiValidationError ? enable.error.errors : null;
  const sendErrors = send.error instanceof ApiValidationError ? send.error.errors : null;
  const other = otherMessages(validation, switchingFromEmail ? PASSWORD_AND_CODES : PASSWORD_AND_CODE);
  const enableProblem = enable.isError && !validation
    ? refusalMessage(enable.error, 'Two-factor authentication could not be turned on. Please try again.')
    : other.length > 0
      ? other.join(' ')
      : null;

  return (
    <form onSubmit={turnOn} noValidate>
      <VStack gap={4} hAlign="stretch">
        <Text type="body">
          1. Open an authenticator app on your phone, add an account, and scan this QR code with it.
        </Text>
        <VStack gap={4} hAlign="center">
          <QrCode path={setup.qrCodePath} size={setup.qrCodeSize} />
          <SharedKey sharedKey={setup.sharedKey} />
        </VStack>
        <Text type="body">
          2. Enter your password and the 6-digit code the app shows for MedApp, to check it&apos;s set up and turn
          two-factor on.
        </Text>
        {enableProblem && <Banner status="error" container="card" role="alert" title={enableProblem} />}
        {/* The account's name, hidden, so a password manager pairs the password with that. */}
        <input type="text" name="username" autoComplete="username" value={username} readOnly hidden />
        <TextInput
          label="Your password"
          type="password"
          value={password}
          onChange={setPassword}
          autoComplete="current-password"
          isRequired
          status={fieldStatus(validation, 'password') ?? fieldStatus(sendErrors, 'password')}
        />
        {switchingFromEmail && (
          <>
            <Text type="body">{PROVE_CURRENT}</Text>
            <EmailMeACode send={send} password={password} />
            <OneTimeCodeField
              kind="either"
              label="Code from the email, or a recovery code"
              value={currentCode}
              onChange={setCurrentCode}
              status={fieldStatus(validation, 'currentCode')}
            />
          </>
        )}
        <OneTimeCodeField value={code} onChange={setCode} status={fieldStatus(validation, 'code')} />
        <HStack gap={2} hAlign="end">
          {onCancel && <Button label="Cancel" variant="secondary" isDisabled={enable.isPending} onClick={onCancel} />}
          <Button
            type="submit"
            label="Turn on two-factor"
            variant="primary"
            icon={<Icon icon={AppIcon.TwoFactorOn} />}
            isLoading={enable.isPending}
          />
        </HStack>
      </VStack>
    </form>
  );
}
