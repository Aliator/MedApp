import type {InputStatus} from '@astryxdesign/core/Field';
import {TextInput, type TextInputSize} from '@astryxdesign/core/TextInput';

export interface OneTimeCodeFieldProps {
  value: string;
  onChange: (value: string) => void;
  /**
   * `authenticator` (the default): the 6-digit code an authenticator app shows now, which phones can fill from the
   * app (`one-time-code`) and type on a number pad. `recovery`: one of the account's recovery codes, letters and digits.
   * `either`: one or the other, so a full keyboard.
   */
  kind?: 'authenticator' | 'recovery' | 'either';
  /** The field's label, if not the default for its kind. */
  label?: string;
  description?: string;
  status?: InputStatus;
  size?: TextInputSize;
  hasAutoFocus?: boolean;
  onEnter?: () => void;
}

const LABELS = {
  authenticator: 'Code from your authenticator app',
  recovery: 'Recovery code',
  either: 'Authenticator code or recovery code',
};

const PLACEHOLDERS = {
  authenticator: 'Enter the 6-digit code',
  recovery: 'Enter a recovery code',
  either: 'Enter a code from the app, or a recovery code',
};

/**
 * A two-factor code: from an authenticator app, or a recovery code. Used wherever one is asked for: the second step of
 * signing in, turning two-factor on (`TwoFactorSetup`) or off, and getting new recovery codes. The API checks it (and
 * ignores spaces), so nothing is checked here.
 */
export function OneTimeCodeField({
  value,
  onChange,
  kind = 'authenticator',
  label = LABELS[kind],
  description,
  status,
  size,
  hasAutoFocus,
  onEnter,
}: OneTimeCodeFieldProps) {
  return (
    <TextInput
      label={label}
      description={description}
      value={value}
      onChange={onChange}
      placeholder={PLACEHOLDERS[kind]}
      // A recovery code is never one an app could fill in.
      autoComplete={kind === 'recovery' ? 'off' : 'one-time-code'}
      inputMode={kind === 'authenticator' ? 'numeric' : 'text'}
      autoCapitalize="off"
      hasAutoCorrect={false}
      hasSpellCheck={false}
      isRequired
      hasAutoFocus={hasAutoFocus}
      onEnter={onEnter}
      status={status}
      size={size}
    />
  );
}
