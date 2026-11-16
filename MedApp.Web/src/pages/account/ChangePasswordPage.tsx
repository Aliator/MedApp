import {useState, type FormEvent} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {PASSWORD_CHANGE_FIELDS, PasswordChangeFields, usePasswordChangeForm} from '../../components/forms/PasswordChangeFields';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';

/** Change your own password (replaces Blazor's Account page form), reached from Account Settings. */
export function ChangePasswordPage() {
  const me = useCurrentUser();
  const announce = useAnnounce();
  const [isUpdated, setIsUpdated] = useState(false);
  const form = usePasswordChangeForm({
    username: me.username,
    onChanged: () => {
      // Its success banner isn't a live region (only errors are), so it's said here.
      announce('Password updated');
      setIsUpdated(true);
    },
  });

  const submit = (event?: FormEvent) => {
    setIsUpdated(false);
    form.submit(event);
  };

  return (
    <Page
      title="Change Password"
      backHref="/account"
      backLabel="Back to account settings"
      message={
        form.error ? (
          <AlertMessage message={form.error} />
        ) : form.validation ? (
          <ValidationAlert errors={form.validation} fields={PASSWORD_CHANGE_FIELDS} />
        ) : (
          isUpdated && <AlertMessage status="success" message="Password updated successfully!" />
        )
      }>
      <FormCard onSave={submit} saveText="Update password" isProcessing={form.isPending}>
        <FormSection title="Password" useGrid={false}>
          <PasswordChangeFields form={form} />
        </FormSection>
      </FormCard>
    </Page>
  );
}
