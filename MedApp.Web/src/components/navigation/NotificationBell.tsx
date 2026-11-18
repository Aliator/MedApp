import {Badge} from '@astryxdesign/core/Badge';
import {Button} from '@astryxdesign/core/Button';
import {Heading} from '@astryxdesign/core/Heading';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {Popover} from '@astryxdesign/core/Popover';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useLayoutEffect, useState, type MouseEvent} from 'react';
import {useColorMode} from '../../app/colorMode';
import {notificationsQuery, useMarkNotificationsRead, type AppNotification} from '../../api/notifications';
import {timeAgo} from '../../utils/dates';
import {AppIcon} from '../shared/icons';

/**
 * Where a notification leads: an appointment opens on the calendar, a procedure on its page, and anything else about a
 * day on the calendar (a meeting) opens that day there.
 */
const hrefOf = (notification: AppNotification) =>
  notification.appointmentId
    ? `/calendar?appointment=${notification.appointmentId}`
    : notification.procedureId
      ? `/procedures/${notification.procedureId}`
      : notification.calendarDate
        ? `/calendar?date=${notification.calendarDate}&view=day`
        : undefined;

/** Each kind's icon, beside its title, so what happened shows at a glance. */
const KIND_ICONS: Record<string, IconType> = {
  ProcedureAssigned: AppIcon.Assign,
  AppointmentBooked: AppIcon.AppointmentBooked,
  AppointmentRescheduled: AppIcon.Book,
  AppointmentCancelled: AppIcon.StatusCancelled,
  AppointmentRecorded: AppIcon.RecordPast,
  MeetingInvited: AppIcon.Meeting,
  MeetingChanged: AppIcon.Meeting,
  MeetingCancelled: AppIcon.Meeting,
};

/** The bell's label: what it is, and how many are unread. */
const bellLabel = (unread: number) => (unread === 0 ? 'Notifications' : `Notifications, ${unread} unread`);

/**
 * The bell beside the MedApp heading: the signed-in user's notifications, with a count of the unread ones. It
 * opens a list of the newest (a procedure's opens it, an appointment's opens it on the calendar, a meeting's opens its
 * day on the calendar, and each marks it read), and Mark all read. New ones arrive live
 * (`LiveNotifications`), so the count and list change without a reload.
 */
export function NotificationBell() {
  const [isOpen, setIsOpen] = useState(false);
  const {mode} = useColorMode();

  // The list is in the app's own mode and on the cards' colour, not the side nav's (always dark), as the nav rows'
  // menus are (`RowMenu`): Astryx hosts it inside the nav, outside any Theme put round it here, so its colours are
  // switched where it is. Found once it's open (a few frames, while it's laid out).
  useLayoutEffect(() => {
    if (!isOpen) return;
    let frame = 0;
    let tries = 0;
    const style = () => {
      const panel = document.querySelector<HTMLElement>('[role="dialog"][aria-label="Notifications"]');
      if (panel) {
        (panel.closest<HTMLElement>('[data-astryx-layer-root]') ?? panel).style.colorScheme = mode;
        const surface =
          panel.closest<HTMLElement>('.astryx-popover') ?? panel.querySelector<HTMLElement>('.astryx-popover') ?? panel;
        surface.style.backgroundColor = 'var(--medapp-card-background)';
      } else if (tries++ < 10) {
        frame = requestAnimationFrame(style);
      }
    };
    style();
    return () => cancelAnimationFrame(frame);
  }, [isOpen, mode]);
  const {data} = useQuery(notificationsQuery);
  const markRead = useMarkNotificationsRead();
  const unread = data?.unreadCount ?? 0;
  const items = data?.items ?? [];

  // Following a notification's link marks it read (and any others about the same procedure or appointment), and
  // closes the list.
  // Caught on the way down, as a linked row takes no click handler of its own.
  const followLink = (event: MouseEvent) => {
    const href = (event.target as HTMLElement).closest('a')?.getAttribute('href');
    if (!href) return;
    for (const notification of items) {
      // Only by id: none would mark them all.
      if (!notification.isRead && notification.id && href === hrefOf(notification))
        markRead.mutate(notification.id);
    }
    setIsOpen(false);
  };

  const content = (
    <VStack gap={0}>
      <HStack gap={2} vAlign="center" hAlign="between" padding={3}>
        <Heading level={3}>Notifications</Heading>
        <Button
          label="Mark all read"
          variant="ghost"
          size="sm"
          isDisabled={unread === 0}
          onClick={() => markRead.mutate(undefined)}
        />
      </HStack>
      {items.length === 0 ? (
        <VStack padding={3} paddingBlockStart={0}>
          <Text type="body" color="secondary">
            Nothing yet. You’ll see it here when someone assigns you to a procedure, books, moves or cancels an appointment with you, or invites you to a meeting.
          </Text>
        </VStack>
      ) : (
        <VStack gap={0} onClickCapture={followLink}>
          <List hasDividers edge="bleed">
            {items.map((notification) => (
              <ListItem
                key={notification.id}
                href={hrefOf(notification)}
                align="first-line"
                startContent={KIND_ICONS[notification.kind ?? ''] && <Icon icon={KIND_ICONS[notification.kind ?? '']} color="secondary" />}
                label={
                  <Text type="body" weight={notification.isRead ? 'normal' : 'semibold'} maxLines={2}>
                    {notification.title}
                  </Text>
                }
                description={
                  <VStack gap={0.5}>
                    <Text type="supporting" color="secondary" maxLines={2}>
                      {notification.message}
                    </Text>
                    <Text type="supporting" color="secondary">
                      {notification.createdAtUtc ? timeAgo(notification.createdAtUtc) : null}
                    </Text>
                  </VStack>
                }
                endContent={notification.isRead ? undefined : <Badge label="New" variant="info" />}
              />
            ))}
          </List>
        </VStack>
      )}
    </VStack>
  );

  return (
    <Popover
      content={content}
      isOpen={isOpen}
      onOpenChange={setIsOpen}
      placement="end"
      alignment="start"
      width={360}
      padding={0}
      label="Notifications">
      <HStack gap={0} vAlign="center">
        <IconButton label={bellLabel(unread)} icon={<Icon icon={AppIcon.Notifications} />} variant="ghost" size="sm" />
        {unread > 0 && <Badge label={unread > 99 ? '99+' : String(unread)} variant="error" />}
      </HStack>
    </Popover>
  );
}
