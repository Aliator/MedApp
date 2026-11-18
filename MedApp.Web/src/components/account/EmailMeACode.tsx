import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {useSendTwoFactorEmailCode} from '../../api/account';
import {ApiValidationError, otherMessages, refusalMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {EmailCodeNote} from './EmailCodeNote';

/** When no more codes can be emailed for a change for now. */
export const CHANGE_NO_MORE_CODES = 'No more codes can be sent for now. Try again in a few minutes, or use a recovery code.';

/** Shown until the password is typed: the code is emailed only once it's been checked. */
export const PASSWORD_FIRST = "Enter your password, then we'll email you a code.";

/** The mutation that emails a code for confirming a change (`useSendTwoFactorEmailCode`). */
export type SendEmailCode = ReturnType<typeof useSendTwoFactorEmailCode>;

export interface EmailMeACodeProps {
  /** The caller's mutation, so it can show a 400 on its own password field (`fieldStatus(errors, 'password')`). */
  send: SendEmailCode;
  /** The password typed so far, sent with the request: the API checks it before emailing anything. */
  password: string;
}

/**
 * "Email me a code", for a change an account using codes by email confirms with an emailed code (turning two-factor
 * off, new recovery codes, switching to an app, changing the address), then where it went, with Resend. As when codes
 * by email are set up, the password comes first: the button waits for it, and it's sent with the request, so a session
 * alone can't fill the owner's mailbox with codes. A wrong password shows on the caller's field; a refusal (409, 423,
 * 429) shows here, in the API's words.
 */
export function EmailMeACode({send, password}: EmailMeACodeProps) {
  const announce = useAnnounce();
  const sendCode = (again: boolean) =>
    send.mutate(password, {onSuccess: (sent) => again && announce(`A new code was emailed to ${sent.sentTo}.`)});

  const validation = send.error instanceof ApiValidationError ? send.error.errors : null;
  const other = otherMessages(validation, ['password']);
  const problem = send.isError && !validation ? refusalMessage(send.error, 'A code could not be sent. Please try again.') : null;

  return (
    <VStack gap={2} hAlign="stretch">
      {(problem || other.length > 0) && <AlertMessage message={problem ?? other.join(' ')} />}
      {send.data ? (
        <EmailCodeNote
          sent={send.data}
          onResend={() => sendCode(true)}
          isResending={send.isPending}
          exhaustedMessage={CHANGE_NO_MORE_CODES}
        />
      ) : (
        <>
          <Button
            label="Email me a code"
            variant="secondary"
            icon={<Icon icon={AppIcon.Email} />}
            isDisabled={password === ''}
            isLoading={send.isPending}
            onClick={() => sendCode(false)}
          />
          {password === '' && (
            <Text type="supporting" color="secondary">
              {PASSWORD_FIRST}
            </Text>
          )}
        </>
      )}
    </VStack>
  );
}
