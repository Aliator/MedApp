import {HttpTransportType, HubConnectionBuilder, LogLevel} from '@microsoft/signalr';
import {useQueryClient} from '@tanstack/react-query';
import {useEffect, useRef} from 'react';
import {notificationsKey, type AppNotification} from '../api/notifications';
import {useAnnounce} from './announcements';

/** Where the API's notifications hub is (`NotificationsHub`), and the message a new notification comes as. */
export const NOTIFICATIONS_HUB = '/api/notifications/hub';
export const NOTIFICATION_MESSAGE = 'notification';
/** How the connection is made: a WebSocket only, without negotiating first (see below). */
export const NOTIFICATIONS_CONNECTION = {skipNegotiation: true, transport: HttpTransportType.WebSockets};

/**
 * Keeps a live connection to the API while signed in, so new notifications (being assigned a pathway) show at
 * once, on whatever page is open: the bell's list and count refresh, pathway lists and pages refresh (My Worklist
 * gains the pathway), and screen readers hear it (with announcements on). It reconnects by itself after a drop,
 * and refreshes the list then, in case one came while it was away. Renders nothing.
 */
export function LiveNotifications() {
  const queryClient = useQueryClient();
  const announce = useAnnounce();
  // The latest announce (it changes with the setting), without reconnecting.
  const announceRef = useRef(announce);
  useEffect(() => {
    announceRef.current = announce;
  }, [announce]);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      // Straight to a WebSocket, with no negotiate request first: with several copies of the API, the negotiate and
      // the WebSocket could reach different copies, and the second wouldn't know the connection.
      .withUrl(NOTIFICATIONS_HUB, NOTIFICATIONS_CONNECTION)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    const refresh = () => queryClient.invalidateQueries({queryKey: notificationsKey});

    connection.on(NOTIFICATION_MESSAGE, (notification: AppNotification) => {
      void refresh();
      if (notification.pathwayId) void queryClient.invalidateQueries({queryKey: ['pathways']});
      announceRef.current(`New notification: ${notification.title}. ${notification.message}`);
    });
    connection.onreconnected(() => void refresh());

    const start = connection.start().catch(() => {
      // The API isn't reachable (or the session ended): the list still loads, and the next page load tries again.
    });
    // Stopped once started (stopping mid-start would throw).
    return () => void start.finally(() => connection.stop());
  }, [queryClient]);

  return null;
}
