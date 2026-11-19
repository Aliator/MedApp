import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useCurrentUser, useEnterApp, useLogout} from '../../auth/auth';
import {PASSWORD_CHANGE_FIELDS, PasswordChangeFields, usePasswordChangeForm} from '../../components/forms/PasswordChangeFields';
import {StandalonePage} from '../../components/pages/StandalonePage';
import {ValidationAlert} from '../../components/shared/AlertMessage';

const TITLE = 'Choose your password';

/** The API refuses a password change from an account with no role (a new account an admin hasn't given one yet). */
export const NO_ROLE = 'This account has no role yet. Ask an admin to give it one, then sign in again.';

/**
 * The page someone lands on when they sign in with a password an admin gave them (a new account, or a reset): they
 * must choose their own before anything else, and the API refuses everything else until they do. It sits outside the
 * signed-in layout, like the login page. Once it's changed, they go into the app, to their start page; or they can
 * sign out.
 */
export function RequiredPasswordChangePage() {
  const me = useCurrentUser();
  const enterApp = useEnterApp();
  const logout = useLogout();
  const form = usePasswordChangeForm({username: me.username, onChanged: enterApp, forbiddenMessage: NO_ROLE});

  return (
    <StandalonePage title={TITLE}>
      <form onSubmit={form.submit} noValidate>
        <VStack gap={4} hAlign="stretch">
          <Text type="body">
            You&apos;re signed in as {me.username} with a password you were given. Before you continue, choose your own
            password: you&apos;ll use it from now on.
          </Text>

          {form.error ? (
            <Banner status="error" title={form.error} container="card" role="alert" />
          ) : (
            form.validation && <ValidationAlert errors={form.validation} fields={PASSWORD_CHANGE_FIELDS} />
          )}

          <PasswordChangeFields
            form={form}
            currentLabel="Password you were given"
            currentPlaceholder="Enter the password you were given"
            size="lg"
          />

          <Button type="submit" label="Change Password" variant="primary" size="lg" isLoading={form.isPending} />
          <Button label="Sign Out" variant="ghost" size="lg" onClick={() => logout.mutate()} isLoading={logout.isPending} />
        </VStack>
      </form>
    </StandalonePage>
  );
}
