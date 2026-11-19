import {List, ListItem} from '@astryxdesign/core/List';
import {Switch} from '@astryxdesign/core/Switch';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {securitySettingsQuery, useSaveSecuritySettings, type SecuritySettings} from '../../api/account';
import {problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {formatDateTime} from '../../utils/dates';

/**
 * How many admin accounts don't use an authenticator app (codes by email don't meet the requirement), and what happens
 * to them, in words.
 */
export function adminsWithoutTwoFactorText({
  adminsWithoutTwoFactor: count,
  requireTwoFactorForAdmins,
}: Pick<SecuritySettings, 'adminsWithoutTwoFactor' | 'requireTwoFactorForAdmins'>): string {
  if (count === 0) return 'Every admin account uses an authenticator app.';
  const accounts = count === 1 ? '1 admin account hasn\'t' : `${count} admin accounts haven't`;
  return requireTwoFactorForAdmins
    ? `${accounts} set up an authenticator app; they'll be asked to at their next request.`
    : `${accounts} set up an authenticator app.`;
}

/** Who last changed the settings, and when, or that nobody has. */
function lastChangedText({updatedAtUtc, updatedBy}: SecuritySettings): string {
  if (!updatedAtUtc) return 'Not changed yet.';
  return `Last changed ${updatedBy ? `by ${updatedBy} ` : ''}on ${formatDateTime(updatedAtUtc)}.`;
}

/**
 * Admin Settings (`/admin/settings`, admin only): the sign-in rules for everyone. So far one: whether admin accounts
 * must use two-factor authentication with an authenticator app, with how many admins haven't set one up (codes by
 * email don't count: they're weaker), and who. Turned on, an admin without one is held to setting it up at their next
 * request (`/set-up-two-factor`). The API refuses it (a 409, shown in its words) until the admin saving it uses an
 * authenticator app themselves, so they can't lock themselves out.
 */
export function AdminSettingsPage() {
  const {data: settings, isPending, isError} = useQuery(securitySettingsQuery);
  const save = useSaveSecuritySettings();
  const announce = useAnnounce();
  // The switch as changed here, until it's saved; the saved value otherwise.
  const [draft, setDraft] = useState<boolean | null>(null);
  const [isSaved, setIsSaved] = useState(false);

  const required = draft ?? settings?.requireTwoFactorForAdmins ?? false;
  const isChanged = settings !== undefined && required !== settings.requireTwoFactorForAdmins;

  const submit = () => {
    setIsSaved(false);
    if (!isChanged) return;
    save.mutate(required, {
      onSuccess: () => {
        setDraft(null);
        setIsSaved(true);
        announce('Settings saved');
      },
    });
  };

  return (
    <Page
      title="Admin Settings"
      subtitle="Sign-in rules for every account"
      isLoading={isPending}
      message={
        isError ? (
          <AlertMessage label="Settings not loaded" message="The settings could not be loaded. Please try again." />
        ) : save.isError ? (
          <AlertMessage label="Settings not saved" message={problemMessage(save.error, 'The settings could not be saved. Please try again.')} />
        ) : (
          isSaved && <AlertMessage status="success" label="Settings saved" />
        )
      }>
      {settings && (
        <FormCard onSave={submit} isProcessing={save.isPending}>
          <FormSection title="Two-factor authentication" useGrid={false}>
            <VStack gap={4}>
              <Switch
                label="Require two-factor authentication for admin accounts"
                description="Admins then sign in with a code from an authenticator app as well as their password. Codes by email don't count, as they're weaker. Set up an app for your own account first, in Account Settings, Security."
                value={required}
                onChange={(value) => {
                  setIsSaved(false);
                  save.reset();
                  setDraft(value);
                }}
              />
              <Text type="body">{adminsWithoutTwoFactorText(settings)}</Text>
              {settings.adminsWithoutTwoFactorUsernames.length > 0 && (
                <List
                  listStyle="disc"
                  edge="flush"
                  density="compact"
                  header={
                    <Text type="supporting" color="secondary">
                      Admin accounts without an authenticator app
                    </Text>
                  }>
                  {settings.adminsWithoutTwoFactorUsernames.map((username) => (
                    <ListItem key={username} label={username} />
                  ))}
                </List>
              )}
              <Text type="supporting" color="secondary">
                {lastChangedText(settings)}
              </Text>
            </VStack>
          </FormSection>
        </FormCard>
      )}
    </Page>
  );
}
