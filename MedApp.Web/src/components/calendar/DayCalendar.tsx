import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Section} from '@astryxdesign/core/Section';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState, type ReactNode} from 'react';
import {calendarQuery} from '../../api/calendar';
import {formatCalendarDay, nextWeekday, ukToday} from '../../utils/calendarSlots';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {PanelHeader} from '../views/PanelCard';
import {CalendarGrid, dayColumns, notWorkingWords} from './CalendarGrid';
import {appointmentKey, calendarItems, type CalendarGridItem} from './calendarItems';

/** One day of someone's calendar (no `staffUserId`: the signed-in user's), the query Home preloads. */
export function dayCalendarQuery(date: string, staffUserId?: string) {
  return calendarQuery({staffUserId, from: date, to: date});
}

export interface DayCalendarProps {
  /** Whose calendar; the signed-in user's by default. */
  staffUserId?: string;
  /** The day it starts on (`yyyy-mm-dd`); today (UK) by default. */
  initialDate?: string;
  /** More controls at the end of its header bar, e.g. a link to the full calendar. */
  actions?: ReactNode;
}

/**
 * One person's day (decision 32): its appointments, blocked time and meetings on a read-only `CalendarGrid`, with
 * Previous day and Next day (working days), and Open Calendar, which opens the full calendar on the day shown. Clicking an appointment opens its pathway, or its patient when it
 * isn't for one; blocks and meetings are only shown. It fills the height it's given, in a card. Use it where someone wants to see a day at a glance, like Home;
 * the Calendar page is where the time is changed.
 */
export function DayCalendar({staffUserId, initialDate, actions}: DayCalendarProps) {
  const navigate = useNavigate();
  const [today] = useState(() => ukToday());
  const [date, setDate] = useState(initialDate ?? today);
  const calendar = useQuery(dayCalendarQuery(date, staffUserId));
  const day = calendar.data?.days?.find((d) => d.date === date);
  // Only appointments open (their pathway or patient); blocked time and meetings are just shown (the Calendar page
  // has their details).
  const items = calendarItems(calendar.data).map((item) => (item.kind === 'appointment' ? item : {...item, isReadOnly: true}));
  const hasAppointments = (calendar.data?.appointments ?? []).length > 0;

  const open = (item: CalendarGridItem) => {
    const appointment = calendar.data?.appointments?.find((a) => appointmentKey(a.id) === item.key);
    if (!appointment) return;
    if (appointment.pathwayId) void navigate({to: '/pathways/$id', params: {id: appointment.pathwayId}});
    else void navigate({to: '/patients/$id', params: {id: appointment.patientId ?? ''}});
  };

  const heading = date === today ? `Today, ${formatCalendarDay(date)}` : formatCalendarDay(date);

  return (
    <Card padding={0} width="100%" height="100%">
      <VStack gap={0} height="100%">
        <PanelHeader
          title={heading}
          actions={
            <HStack gap={1} vAlign="center">
              <IconButton
                label="Previous day"
                tooltip="Previous day"
                variant="ghost"
                icon={<Icon icon={AppIcon.ChevronLeft} />}
                onClick={() => setDate(nextWeekday(date, -1))}
              />
              <IconButton
                label="Next day"
                tooltip="Next day"
                variant="ghost"
                icon={<Icon icon={AppIcon.ChevronRight} />}
                onClick={() => setDate(nextWeekday(date, 1))}
              />
              {/* In place of a Today button (the owner's choice): it opens the full calendar on the day shown. */}
              <Button
                label="Open Calendar"
                variant="secondary"
                size="sm"
                icon={<Icon icon={AppIcon.Calendar} />}
                href={`/calendar?date=${date}${staffUserId ? `&staff=${staffUserId}` : ''}`}
              />
              {actions}
            </HStack>
          }
        />
        {calendar.isError ? (
          <Section variant="transparent" padding={4}>
            <AlertMessage message="The calendar could not be loaded. Please try again." />
          </Section>
        ) : !day ? (
          <Section variant="transparent" padding={4}>
            <VStack gap={2}>
              <Skeleton height="var(--spacing-8)" />
              <Skeleton height="var(--spacing-8)" />
              <Skeleton height="var(--spacing-8)" />
            </VStack>
          </Section>
        ) : (
          <>
            {!hasAppointments && (
              <Section variant="transparent" paddingInline={6} paddingBlock={2} dividers={['bottom']}>
                <Text type="body" color="secondary">
                  {day.isWorkingDay ? 'Nothing booked' : notWorkingWords(day)}
                </Text>
              </Section>
            )}
            <StackItem size="fill">
              <CalendarGrid
                label={`${calendar.data?.staffUsername ?? ''}'s calendar, ${formatCalendarDay(date)}`}
                columns={dayColumns([day], items)}
                onItemOpen={open}
                isHeadingHidden
              />
            </StackItem>
          </>
        )}
      </VStack>
    </Card>
  );
}
