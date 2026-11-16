import {VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {useQuery} from '@tanstack/react-query';
import {notificationPreferencesQuery, useSaveNotificationPreferences} from '../../api/notifications';
import {useAnnounce} from '../../app/announcements';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';

/**
 * Account Settings' Notifications card: which notifications the signed-in user gets. Unlike the other settings it's
 * kept on the server only (not in this browser), because the server checks it before sending. Admins choose their
 * monitoring notifications with their rules (Administration, Monitoring), so those aren't here.
 */
export function NotificationSettings() {
  const preferences = useQuery(notificationPreferencesQuery);
  const save = useSaveNotificationPreferences();
  const announce = useAnnounce();
  const procedureAssigned = save.isPending ? (save.variables?.procedureAssigned ?? true) : (preferences.data?.procedureAssigned ?? true);

  return (
    <ViewCard showFooter={false}>
      <CardSection title="Notifications">
        <VStack gap={3}>
          {(preferences.isError || save.isError) && (
            <AlertMessage
              message={save.isError ? 'Your choice could not be saved. Please try again.' : 'Your notification choices could not be loaded.'}
            />
          )}
          <Switch
            label="When I'm assigned to a procedure"
            description="Tells you in the bell when someone assigns you to a procedure. Work from My Tasks either way: notifications are a convenience."
            value={procedureAssigned}
            isDisabled={preferences.isPending || save.isPending}
            onChange={(isOn) =>
              save.mutate(
                {procedureAssigned: isOn},
                {onSuccess: () => announce(isOn ? 'Assignment notifications on' : 'Assignment notifications off')},
              )
            }
          />
        </VStack>
      </CardSection>
    </ViewCard>
  );
}
