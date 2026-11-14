import {TextInput} from '@astryxdesign/core/TextInput';
import {useState, type FormEvent} from 'react';
import {ApiError, ApiValidationError, fieldStatus, type ValidationErrors} from '../../api/errors';
import {useChangePassword} from '../../api/users';
import {useCurrentUser} from '../../auth/auth';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';

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

/** The signed-in user's account (replaces Blazor's Account page): change your own password. */
export function AccountPage() {
  const me = useCurrentUser();
  const change = useChangePassword();
  const [fields, setFields] = useState(EMPTY);
  const [error, setError] = useState<string | null>(null);
  const [validation, setValidation] = useState<ValidationErrors | null>(null);
  const [isUpdated, setIsUpdated] = useState(false);

  const set = (key: keyof typeof EMPTY) => (value: string) => setFields((f) => ({...f, [key]: value}));

  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    setError(null);
    setValidation(null);
    setIsUpdated(false);

    if (fields.next !== fields.confirm) return setError('New passwords do not match.');
    if (!fields.current.trim()) return setError('Please enter your current password.');
    if (!fields.next.trim()) return setError('Please enter a new password.');

    change.mutate(
      {username: me.username, oldPassword: fields.current, newPassword: fields.next},
      {
        onSuccess: () => {
          setIsUpdated(true);
          setFields(EMPTY);
        },
        onError: (e) => (e instanceof ApiValidationError ? setValidation(e.errors) : setError(changeError(e))),
      },
    );
  };

  return (
    <Page title="Account Settings" subtitle="Manage your account password" maxWidth={640}>
      <FormCard onSave={submit} saveText="Update password" isProcessing={change.isPending}>
        <FormSection title="Change Password" useGrid={false}>
          <TextInput
            label="Current password"
            type="password"
            value={fields.current}
            onChange={set('current')}
            placeholder="Enter your current password"
            autoComplete="current-password"
            isRequired
            status={fieldStatus(validation, 'oldPassword')}
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
          />
          <TextInput
            label="Confirm new password"
            type="password"
            value={fields.confirm}
            onChange={set('confirm')}
            placeholder="Confirm your new password"
            autoComplete="new-password"
            isRequired
          />
          {error && <AlertMessage message={error} />}
          {validation && <ValidationAlert errors={validation} />}
          {isUpdated && <AlertMessage status="success" message="Password updated successfully!" />}
        </FormSection>
      </FormCard>
    </Page>
  );
}
