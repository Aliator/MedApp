import {useAppShellMobile} from '@astryxdesign/core/AppShell';
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
import {useLayoutEffect, useRef, useState, type MouseEvent} from 'react';
import {useColorMode} from '../../app/colorMode';
import {notificationsQuery, useMarkNotificationsRead, type AppNotification} from '../../api/notifications';
import {timeAgo} from '../../utils/dates';
import {AppIcon} from '../shared/icons';
import {placeBesideNav} from './besideNav';

/** The notifications about a letter, which name it in `subjectId`. */
const LETTER_KINDS = new Set(['LetterAwaitingApproval', 'LetterApproved', 'LetterReturned']);

/** The notifications about a result (one ready for you, reassigned to you, or signed off by someone else), which name it in `subjectId`. */
const RESULT_KINDS = new Set(['ResultReady', 'ResultReassigned', 'ResultSignedOffByOther']);

/**
 * The service alerts' lists: each alert opens the list it counts, narrowed to the rule's team (its `subjectId`)
 * where the list takes one.
 */
const SERVICE_ALERT_LISTS: Record<string, {path: string; team: boolean}> = {
  MonitoredReferralNotTriaged: {path: '/referrals?', team: true},
  MonitoredResultNotSignedOff: {path: '/results?view=Awaiting&', team: true},
  MonitoredVisitNotBooked: {path: '/calendar/requires-appointment?', team: false},
  MonitoredSpecimenOverdue: {path: '/lab/samples?view=Overdue&', team: false},
  MonitoredWaitBreached: {path: '/referrals?view=waiting&', team: true},
  MonitoredPauseReviewPassed: {path: '/referrals?view=paused&', team: true},
  MonitoredLabCaseAtRisk: {path: '/lab/cases?view=AtRisk&', team: false},
  MonitoredLabCaseOverdue: {path: '/lab/cases?view=Overdue&', team: false},
};

/** A service alert's link: its list, with the team when it has one and the list takes it. */
function serviceAlertHref(notification: AppNotification) {
  const list = SERVICE_ALERT_LISTS[notification.kind ?? ''];
  if (!list) return undefined;
  const href = list.team && notification.subjectId ? `${list.path}team=${notification.subjectId}` : list.path;
  return href.replace(/[?&]$/, '');
}

/**
 * Where a notification leads: an appointment opens on the calendar, a request to delete a note opens on its
 * pathway's page (for the admin to decide), a pathway on its page, and anything else about a day on the calendar
 * (a meeting, a day closed with something of yours on it) opens that day there, with the meeting itself open when it
 * has one (an invitation, a move, a decline; not a cancellation), so an organiser told of a decline sees who, even if
 * they aren't going. An admin's "Scheduled job failing" (a job that failed three times in a row) opens Scheduled Jobs.
 * "Referral triaged" (one you registered was accepted, rejected or redirected by someone else) opens the referral.
 * A specimen rejected by the lab or lost opens the specimen (its `subjectId`).
 * The letter kinds (a letter awaiting your approval, yours approved or returned) open the letter (`subjectId`).
 * The result kinds (a result ready for your sign-off, reassigned to you, or signed off by someone else) open the result
 * (`subjectId`). A service alert opens the list it counts.
 * "Data request due soon" (a patient's data request near its due date) opens the request (`subjectId`).
 */
const itemHrefOf = (notification: AppNotification) =>
  notification.kind === 'ScheduledJobFailing'
    ? '/admin/jobs'
    : notification.kind === 'DataRequestDueSoon' && notification.subjectId
      ? `/data-requests/${notification.subjectId}`
      : LETTER_KINDS.has(notification.kind ?? '') && notification.subjectId
        ? `/letters/${notification.subjectId}`
        : RESULT_KINDS.has(notification.kind ?? '') && notification.subjectId
          ? `/results/${notification.subjectId}`
          : notification.referralId
            ? `/referrals/${notification.referralId}`
            : SPECIMEN_KINDS.includes(notification.kind ?? '') && notification.subjectId
              ? `/specimens/${notification.subjectId}`
              : notification.appointmentId
                ? `/calendar?appointment=${notification.appointmentId}`
                : notification.noteDeletionRequestId && notification.pathwayId
                  ? `/pathways/${notification.pathwayId}?deletionRequest=${notification.noteDeletionRequestId}`
                  : notification.pathwayId
                    ? `/pathways/${notification.pathwayId}`
                    : notification.calendarDate
                      ? `/calendar?date=${notification.calendarDate}&view=day${notification.meetingId ? `&meeting=${notification.meetingId}` : ''}`
                      : undefined;

const hrefOf = (notification: AppNotification) => serviceAlertHref(notification) ?? itemHrefOf(notification);

/** The kinds about a specimen, whose `subjectId` is the specimen. */
const SPECIMEN_KINDS = ['SpecimenRejectedByLab', 'SpecimenLost', 'RepeatSampleRequested'];

/** Each kind's icon, beside its title, so what happened shows at a glance. */
const KIND_ICONS: Record<string, IconType> = {
  PathwayAssigned: AppIcon.Assign,
  PathwayHandedOver: AppIcon.Send,
  AppointmentBooked: AppIcon.AppointmentBooked,
  AppointmentRescheduled: AppIcon.Book,
  AppointmentCancelled: AppIcon.StatusCancelled,
  AppointmentRecorded: AppIcon.RecordPast,
  MeetingInvited: AppIcon.Meeting,
  MeetingChanged: AppIcon.Meeting,
  MeetingCancelled: AppIcon.Meeting,
  MeetingDeclined: AppIcon.MeetingDeclined,
  DayClosed: AppIcon.ClosedDays,
  StageVisitWithdrawn: AppIcon.EnteredInError,
  NoteDeletionRequested: AppIcon.Delete,
  NoteDeletionApproved: AppIcon.Delete,
  NoteDeletionDeclined: AppIcon.Notes,
  AuthorsNoteDeleted: AppIcon.Delete,
  ScheduledJobFailing: AppIcon.ScheduledJobs,
  ReferralTriaged: AppIcon.Referrals,
  SpecimenRejectedByLab: AppIcon.StatusCancelled,
  SpecimenLost: AppIcon.Warning,
  RepeatSampleRequested: AppIcon.Refresh,
  LetterAwaitingApproval: AppIcon.Letters,
  LetterApproved: AppIcon.Success,
  LetterReturned: AppIcon.Back,
  ResultReady: AppIcon.Results,
  ResultReassigned: AppIcon.Reassign,
  ResultSignedOffByOther: AppIcon.SignOff,
  MdtOutcomeRecorded: AppIcon.Mdt,
  ConsultRequested: AppIcon.Question,
  ConsultReplied: AppIcon.Question,
  DataRequestDueSoon: AppIcon.DataRequests,
  MonitoredReferralNotTriaged: AppIcon.Referrals,
  MonitoredResultNotSignedOff: AppIcon.Results,
  MonitoredVisitNotBooked: AppIcon.Warning,
  MonitoredSpecimenOverdue: AppIcon.Warning,
  MonitoredWaitBreached: AppIcon.Warning,
  MonitoredPauseReviewPassed: AppIcon.Warning,
  MonitoredLabCaseAtRisk: AppIcon.Warning,
  MonitoredLabCaseOverdue: AppIcon.Overdue,
};

/** The bell, which the unread count is placed over. */
const BELL = {position: 'relative'} as const;
/** The unread count, centred on the bell's top right corner, so it covers only that corner of the bell. */
const COUNT = {
  position: 'absolute',
  insetBlockStart: 0,
  insetInlineEnd: 0,
  translate: '45% -45%',
  pointerEvents: 'none',
  // Thin: 16px tall rather than Astryx's 20, at least as wide as it's tall (a circle for one digit), with a little room
  // either side of the number.
  blockSize: 'var(--spacing-4)',
  minInlineSize: 'var(--spacing-4)',
  lineHeight: 'var(--spacing-4)',
  paddingBlock: 0,
  paddingInline: 'var(--spacing-1)',
  justifyContent: 'center',
} as const;

/** The bell's label: what it is, and how many are unread. */
const bellLabel = (unread: number) => (unread === 0 ? 'Notifications' : `Notifications, ${unread} unread`);

/**
 * The bell beside the MedApp heading: the signed-in user's notifications, with a count of the unread ones. It
 * opens a list of the newest (a pathway's opens it, an appointment's opens it on the calendar, a meeting's opens its
 * day on the calendar, and each marks it read), and Mark all read. New ones arrive live
 * (`LiveNotifications`), so the count and list change without a reload.
 */
export function NotificationBell() {
  const [isOpen, setIsOpen] = useState(false);
  const {mode} = useColorMode();

  const {isMobile} = useAppShellMobile();
  const bell = useRef<HTMLButtonElement | null>(null);

  // The list opens as a nav row's menu does (`placeBesideNav`): beside the nav, as far out from its edge as the rows are
  // in from it, level with the bell's row (the heading; folded, the bell's own line under the logo), in the app's own
  // mode and on the cards' colour, not the side nav's (always dark). On a phone the nav is a drawer over the page, so
  // there it's left where Astryx puts it, beside the bell, only taking the app's colours. Found once it's open (a few
  // frames, while it's laid out).
  useLayoutEffect(() => {
    if (!isOpen) return;
    let frame = 0;
    let tries = 0;
    const place = () => {
      const panel = document.querySelector<HTMLElement>('[role="dialog"][aria-label="Notifications"]');
      const button = bell.current;
      const heading = button?.closest<HTMLElement>('.astryx-side-nav-heading');
      const row = heading ?? button?.closest<HTMLElement>('[data-nav-header]');
      if (panel && button && row && !isMobile) {
        if (placeBesideNav({panel, row, levelWith: heading ?? button, mode})) return;
      } else if (panel) {
        (panel.closest<HTMLElement>('[data-astryx-layer-root]') ?? panel).style.colorScheme = mode;
        const surface =
          panel.closest<HTMLElement>('.astryx-popover') ?? panel.querySelector<HTMLElement>('.astryx-popover') ?? panel;
        surface.style.backgroundColor = 'var(--medapp-card-background)';
        return;
      }
      if (tries++ < 10) frame = requestAnimationFrame(place);
    };
    place();
    return () => cancelAnimationFrame(frame);
  }, [isOpen, mode, isMobile]);
  const {data} = useQuery(notificationsQuery);
  const markRead = useMarkNotificationsRead();
  const unread = data?.unreadCount ?? 0;
  const items = data?.items ?? [];

  // Following a notification's link marks it read (and any others about the same pathway or appointment), and
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
            Nothing yet. Notifications about your pathways, appointments, meetings, results and letters appear here.
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
      {/* The count sits over the bell's top right corner, as on most apps' bells, so the bell is the same size with
          or without one, in the side nav and its folded icons alike. The bell's name already says how many are unread,
          so the count is hidden from screen readers, and a click on it reaches the bell. */}
      <HStack gap={0} vAlign="center" style={BELL}>
        <IconButton ref={bell} label={bellLabel(unread)} icon={<Icon icon={AppIcon.Notifications} />} variant="ghost" size="sm" />
        {unread > 0 && <Badge label={unread > 99 ? '99+' : String(unread)} variant="error" aria-hidden style={COUNT} />}
      </HStack>
    </Popover>
  );
}
