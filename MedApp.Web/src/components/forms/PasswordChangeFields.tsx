import {TextInput, type TextInputSize} from '@astryxdesign/core/TextInput';
import {useState, type FormEvent} from 'react';
import {ApiError, ApiValidationError, fieldStatus, type ValidationErrors} from '../../api/errors';
import {useChangePassword} from '../../api/users';

/** The request fields a password change's 400 can name, which show their messages on the inputs. */
export const PASSWORD_CHANGE_FIELDS = ['oldPassword', 'newPassword'] as const;

const EMPTY = {current: '', next: '', confirm: ''};

/** Why a password change failed, in the Blazor page's words. */
function changeError(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.status) {
      case 401:
        return 'Current password is incorrect.';
      case 403:
        return 'You do not have permission to update this password.';
      case 404:
        return 'User not found.';
      default:
        if (error.status >= 500) return 'A server error occurred. Please try again later.';
    }
  }
  if (error instanceof TypeError) return 'Unable to connect to the server. Please check your connection and try again.';
  return 'An unexpected error occurred while updating the password.';
}

export interface PasswordChangeForm {
  fields: typeof EMPTY;
  set: (key: keyof typeof EMPTY) => (value: string) => void;
  /** Checks the fields (the new ones match, none is blank), then asks the API to change the password. */
  submit: (event?: FormEvent) => void;
  /** Why it failed, for a banner: a local check, or the API's status in words. */
  error: string | null;
  /** A 400's messages: on the fields (`PasswordChangeFields`), and the rest for a `ValidationAlert`. */
  validation: ValidationErrors | null;
  isPending: boolean;
}

/**
 * A password change form's state: the current password, the new one and its confirmation, and changing `username`'s
 * own password with them. On success the fields empty and `onChanged` runs. The server's rules (length, not the same
 * as the current one) come back as a 400, shown on the fields; only the checks the API can't make (the two new
 * passwords match) and blanks are made here. Use it with `PasswordChangeFields`, for any page where people change
 * their own password: Account Settings, and the change a generated password forces at sign-in.
 */
export function usePasswordChangeForm({username, onChanged}: {username: string; onChanged?: () => void | Promise<void>}): PasswordChangeForm {
  const change = useChangePassword();
  const [fields, setFields] = useState(EMPTY);
  const [error, setError] = useState<string | null>(null);
  const [validation, setValidation] = useState<ValidationErrors | null>(null);

  const set = (key: keyof typeof EMPTY) => (value: string) => setFields((f) => ({...f, [key]: value}));

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    setError(null);
    setValidation(null);

    if (fields.next !== fields.confirm) return setError('New passwords do not match.');
    if (!fields.current.trim()) return setError('Please enter your current password.');
    if (!fields.next.trim()) return setError('Please enter a new password.');

    change.mutate(
      {username, oldPassword: fields.current, newPassword: fields.next},
      {
        onSuccess: async () => {
          setFields(EMPTY);
          await onChanged?.();
        },
        onError: (e) => (e instanceof ApiValidationError ? setValidation(e.errors) : setError(changeError(e))),
      },
    );
  };

  return {fields, set, submit, error, validation, isPending: change.isPending};
}

export interface PasswordChangeFieldsProps {
  form: PasswordChangeForm;
  /** What the current password is called, e.g. "The password you were given". */
  currentLabel?: string;
  size?: TextInputSize;
}

/**
 * The three inputs of a password change (current, new, confirm), with the server's messages on the first two. Put
 * them in the page's own form or `FormCard`, with `usePasswordChangeForm` behind them.
 */
export function PasswordChangeFields({form, currentLabel = 'Current password', size}: PasswordChangeFieldsProps) {
  const {fields, set, validation} = form;
  return (
    <>
      <TextInput
        label={currentLabel}
        type="password"
        value={fields.current}
        onChange={set('current')}
        placeholder="Enter your current password"
        autoComplete="current-password"
        isRequired
        status={fieldStatus(validation, 'oldPassword')}
        size={size}
      />
      <TextInput
        label="New password"
        type="password"
        value={fields.next}
        onChange={set('next')}
        placeholder="Enter a new password"
        autoComplete="new-password"
        isRequired
        status={fieldStatus(validation, 'newPassword')}
        size={size}
      />
      <TextInput
        label="Confirm new password"
        type="password"
        value={fields.confirm}
        onChange={set('confirm')}
        placeholder="Confirm your new password"
        autoComplete="new-password"
        isRequired
        size={size}
      />
    </>
  );
}
