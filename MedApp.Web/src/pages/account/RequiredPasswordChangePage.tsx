import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Center} from '@astryxdesign/core/Center';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import type {CSSProperties} from 'react';
import {usePageTitle} from '../../app/announcements';
import {useCurrentUser, useEnterApp, useLogout} from '../../auth/auth';
import {PASSWORD_CHANGE_FIELDS, PasswordChangeFields, usePasswordChangeForm} from '../../components/forms/PasswordChangeFields';
import {ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';

// Like the login page, this one has no AppShell, so it paints the body background itself.
const pageStyle: CSSProperties = {minHeight: '100dvh', backgroundColor: 'var(--color-background-body)'};
// Stack has no maxWidth prop, so the column's cap is set here.
const columnStyle: CSSProperties = {width: '100%', maxWidth: 440};

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
  usePageTitle(TITLE, true);

  return (
    <Center axis="both" padding={6} style={pageStyle}>
      <VStack gap={4} hAlign="center" style={columnStyle}>
        <VStack gap={2} hAlign="center">
          <Icon icon={AppIcon.Shield} size="lg" color="accent" />
          <Text type="body" weight="bold" size="lg">
            MedApp
          </Text>
        </VStack>

        <Card padding={8} width="100%">
          <form onSubmit={form.submit} noValidate>
            <VStack gap={4} hAlign="stretch">
              <Heading level={1}>{TITLE}</Heading>
              <Text type="body">
                You&apos;re signed in as {me.username} with a password you were given. Before you continue, choose your
                own password: you&apos;ll use it from now on.
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

              <Button type="submit" label="Change password" variant="primary" size="lg" isLoading={form.isPending} />
              <Button label="Sign out" variant="ghost" size="lg" onClick={() => logout.mutate()} isLoading={logout.isPending} />
            </VStack>
          </form>
        </Card>
      </VStack>
    </Center>
  );
}
