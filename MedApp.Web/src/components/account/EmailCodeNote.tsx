import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useEffect, useState} from 'react';
import type {EmailCodeSent} from '../../api/account';
import {AppIcon} from '../shared/icons';

/**
 * Seconds before another code can be sent, counting down from the server's `resendInSeconds` (by the server's clock,
 * so a device with a wrong clock still counts right). The count starts after mounting, from when this code's details
 * arrived, so the server's render and the first in the browser match; each new code starts it again.
 */
export function useResendWait(sent: EmailCodeSent | null): number {
  const [left, setLeft] = useState(sent?.resendInSeconds ?? 0);
  // A new code starts the count again (set while rendering, as React advises for state that follows a prop).
  const [counting, setCounting] = useState(sent);
  if (counting !== sent) {
    setCounting(sent);
    setLeft(sent?.resendInSeconds ?? 0);
  }

  useEffect(() => {
    const wait = sent?.resendInSeconds ?? 0;
    if (wait <= 0) return;
    const started = Date.now();
    const ticking = setInterval(() => {
      const remaining = Math.max(0, wait - Math.floor((Date.now() - started) / 1000));
      setLeft(remaining);
      if (remaining === 0) clearInterval(ticking);
    }, 1000);
    return () => clearInterval(ticking);
  }, [sent]);

  return left;
}

export interface EmailCodeNoteProps {
  /** The code just emailed (where it went, and when another can be sent). */
  sent: EmailCodeSent;
  /** Sends another, replacing it. */
  onResend: () => void;
  isResending?: boolean;
  /** What to say once no more codes can be sent this time, e.g. to start again or use a recovery code. */
  exhaustedMessage: string;
}

/**
 * Where an emailed two-factor code went ("We emailed a code to j•••@example.com", the address as the server masked
 * it), with Resend: disabled, saying how long, until the server allows another (once a minute), and replaced by
 * `exhaustedMessage` when no more can be sent (three resends). Used by the sign-in's code step and `EmailCodesSetup`.
 */
export function EmailCodeNote({sent, onResend, isResending = false, exhaustedMessage}: EmailCodeNoteProps) {
  const wait = useResendWait(sent);
  const exhausted = sent.resendsLeft <= 0;

  return (
    <VStack gap={2} hAlign="stretch">
      <Text type="body">
        We emailed a code to {sent.sentTo}. It may take a minute to arrive; check your junk folder
        too.
      </Text>
      {exhausted ? (
        <Text type="supporting" color="secondary">
          {exhaustedMessage}
        </Text>
      ) : (
        <Button
          label={wait > 0 ? `Resend in ${wait} s` : 'Resend code'}
          variant="secondary"
          icon={<Icon icon={AppIcon.Refresh} />}
          isDisabled={wait > 0}
          isLoading={isResending}
          onClick={onResend}
        />
      )}
    </VStack>
  );
}
