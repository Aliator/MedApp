import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {AppIcon} from '../../components/shared/icons';

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** How long the stand-in "send" takes, as in Blazor. */
export const RESET_DELAY_MS = 1000;

/**
 * "Forgot password?" (ported from the Blazor login page). NOTE: like Blazor's, this is a stand-in. The API has
 * no password-reset endpoint, so nothing is sent: after a short delay it only says a link was sent. Replace
 * `sendResetLink` once the API supports password resets.
 */
async function sendResetLink(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, RESET_DELAY_MS));
}

export function ForgotPasswordDialog({isOpen, onClose}: {isOpen: boolean; onClose: () => void}) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string>();
  const [isSending, setIsSending] = useState(false);
  const [isSent, setIsSent] = useState(false);

  const close = () => {
    if (isSending) return;
    setEmail('');
    setError(undefined);
    setIsSent(false);
    onClose();
  };

  const send = async () => {
    const trimmed = email.trim();
    if (!trimmed) return setError('Please enter your email address.');
    if (!EMAIL.test(trimmed)) return setError('Please enter a valid email address.');
    setError(undefined);
    setIsSending(true);
    try {
      await sendResetLink();
      setIsSent(true);
    } catch {
      setError('An unexpected error occurred. Please try again.');
    } finally {
      setIsSending(false);
    }
  };

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Reset your password"
      icon={isSent ? AppIcon.Success : AppIcon.Email}
      intent="accent"
      message={isSent ? undefined : "Enter your email address and we'll send you a link to reset your password."}
      confirmText={isSent ? 'Close' : 'Send reset link'}
      isProcessing={isSending}
      confirmDisabled={!isSent && !email.trim()}
      onConfirm={isSent ? close : () => void send()}
      onCancel={close}>
      {isSent ? (
        <>
          <Text type="body" weight="semibold" justify="center">
            Check your email
          </Text>
          <Text type="body" justify="center">
            We&apos;ve sent a password reset link to {email.trim()}.
          </Text>
          <Text type="body" color="secondary" justify="center">
            If you don&apos;t see the email, check your spam folder or try again.
          </Text>
        </>
      ) : (
        <TextInput
          label="Email address"
          type="email"
          value={email}
          onChange={setEmail}
          placeholder="Enter your email"
          autoComplete="email"
          isRequired
          onEnter={() => void send()}
          status={error ? {type: 'error', message: error} : undefined}
        />
      )}
    </ConfirmDialog>
  );
}
