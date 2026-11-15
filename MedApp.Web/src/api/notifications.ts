import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type AppNotification = Schemas['NotificationResponse'];
export type Notifications = Schemas['NotificationsResponse'];

export const notificationsKey = ['notifications'] as const;

/** The signed-in user's newest notifications and their unread count. New ones arrive live (`LiveNotifications`). */
export const notificationsQuery = queryOptions({
  queryKey: notificationsKey,
  queryFn: () => unwrap(api.GET('/api/notifications')),
});

/** Marks one notification read, or all of them (no id). */
export function useMarkNotificationsRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id?: string) => {
      if (id)
        await unwrap(
          api.POST('/api/notifications/{id}/read', {
            params: {path: {id}},
          }),
        );
      else await unwrap(api.POST('/api/notifications/read'));
    },
    onSuccess: () => queryClient.invalidateQueries({queryKey: notificationsKey}),
  });
}
