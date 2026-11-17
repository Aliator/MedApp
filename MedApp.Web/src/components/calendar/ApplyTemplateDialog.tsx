import {CheckboxList, CheckboxListItem} from '@astryxdesign/core/CheckboxList';
import {Selector, type SelectorOptionType} from '@astryxdesign/core/Selector';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {calendarQuery, dayTemplatesQuery, useCalendarActions, type CalendarDay} from '../../api/calendar';
import {problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {addDays, formatShortDay, weekStart} from '../../utils/calendarSlots';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** The template choice that's an empty day: an override with no day template. */
export const NO_TEMPLATE = 'no-template';
/** The template choice that removes the day's override, so the default week's template applies again. */
export const WEEK_TEMPLATE = 'week-template';

export interface ApplyTemplateDialogProps {
  isOpen: boolean;
  /** Whose calendar it is (their user ID), for the changes and their day templates. */
  staffUserId: string;
  /**
   * The calendar's `staffUserId` as the page asks for it (none for your own), so the week's days come from the same
   * query as the page's week.
   */
  calendarStaff?: string;
  /** A date in the week to change. */
  date: string;
  /** The day ticked when it opens, if it's in that week and can still change: the day shown, or today. */
  startDate?: string;
  onClose: () => void;
}

/**
 * Apply Template (docs/calendar-plan.md, decision 69): one of a person's day templates, an empty day ("No template"),
 * or "The default week's" (the day's override removed), on any of the working days of a week, ticked. Each day says
 * what applies now, and past days are shown but can't be ticked, as the API refuses them (409). There's no batch
 * endpoint, so each day is its own call, all at once; days that fail are named with the API's reason and stay ticked,
 * and the dialog stays open, while those that worked stay applied.
 *
 * Use it from the Calendar page, for a calendar the viewer can change (its owner, or an admin).
 */
export function ApplyTemplateDialog(props: ApplyTemplateDialogProps) {
  // A fresh form each time it opens.
  const [session, setSession] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setSession((s) => s + 1);
  }
  return <ApplyTemplateForm key={session} {...props} />;
}

/** "Mon 23", "Mon 23 and Tue 24", "Mon 23, Tue 24 and Wed 25". */
function dayList(dates: string[]): string {
  const names = dates.map(formatShortDay);
  return names.length <= 1 ? (names[0] ?? '') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`;
}

/** What applies on a day now: "Mon 23: Clinic day", or "Mon 23: No template". */
function dayLabel(day: CalendarDay): string {
  return `${formatShortDay(day.date ?? '')}: ${day.dayTemplateId ? (day.dayTemplateName ?? 'A day template') : 'No template'}`;
}

interface Failure {
  date: string;
  message: string;
}

function ApplyTemplateForm({isOpen, staffUserId, calendarStaff, date, startDate, onClose}: ApplyTemplateDialogProps) {
  const announce = useAnnounce();
  const actions = useCalendarActions();
  const from = weekStart(date);
  // The same query as the page's week (so it's already here there); in the day view, the week is read on opening.
  const week = useQuery({...calendarQuery({staffUserId: calendarStaff, from, to: addDays(from, 6)}), enabled: isOpen});
  const templates = useQuery({...dayTemplatesQuery(staffUserId), enabled: isOpen && staffUserId !== ''});

  const days = (week.data?.days ?? []).filter((d) => d.isWorkingDay);
  const canChange = (d: CalendarDay) => !d.isPast;

  const [choice, setChoice] = useState<string | undefined>(undefined);
  // What's ticked: until someone ticks or unticks, the start date when it's a day that can change.
  const [ticked, setTicked] = useState<string[] | null>(null);
  const startTicked = days.filter((d) => d.date === startDate && canChange(d)).map((d) => d.date ?? '');
  const selected = ticked ?? startTicked;
  const [failures, setFailures] = useState<Failure[]>([]);
  const [isApplying, setIsApplying] = useState(false);

  const active = (templates.data ?? []).filter((t) => !t.isArchived);
  const options: SelectorOptionType[] = [
    ...(active.length > 0
      ? [{type: 'section' as const, title: 'Day templates', options: active.map((t) => ({value: t.id ?? '', label: t.name ?? ''}))}, {type: 'divider' as const}]
      : []),
    {value: NO_TEMPLATE, label: 'No template', description: 'An empty day'},
    {value: WEEK_TEMPLATE, label: "The default week's", description: 'Whatever the default week gives the day'},
  ];
  const chosenName = active.find((t) => t.id === choice)?.name ?? '';

  /** What was done, for the announcement: "Clinic day applied to Mon 23 and Tue 24". */
  const doneMessage = (dates: string[]) =>
    choice === WEEK_TEMPLATE
      ? `Back to the default week's template on ${dayList(dates)}`
      : choice === NO_TEMPLATE
        ? `No template on ${dayList(dates)}`
        : `${chosenName} applied to ${dayList(dates)}`;

  const apply = async () => {
    if (!choice || selected.length === 0) return;
    // In week order, whatever order they were ticked in.
    const dates = days.map((d) => d.date ?? '').filter((d) => selected.includes(d));
    // "The default week's" only changes a day that has an override; the others already follow the week.
    const toChange =
      choice === WEEK_TEMPLATE ? dates.filter((d) => days.find((day) => day.date === d)?.isDayTemplateOverridden) : dates;
    setFailures([]);
    setIsApplying(true);
    const results = await Promise.allSettled(
      toChange.map((d) =>
        choice === WEEK_TEMPLATE
          ? actions.clearDayTemplate.mutateAsync({userId: staffUserId, date: d})
          : actions.applyDayTemplate.mutateAsync({userId: staffUserId, date: d, dayTemplateId: choice === NO_TEMPLATE ? null : choice}),
      ),
    );
    setIsApplying(false);
    const failed: Failure[] = results.flatMap((result, i) =>
      result.status === 'rejected' ? [{date: toChange[i], message: problemMessage(result.reason, 'The day could not be changed.')}] : [],
    );
    const worked = dates.filter((d) => !failed.some((f) => f.date === d));
    if (worked.length > 0) announce(doneMessage(worked));
    if (failed.length === 0) {
      onClose();
      return;
    }
    // The days that worked stay applied, and unticked; the ones that failed stay ticked, to try again.
    setFailures(failed);
    setTicked(failed.map((f) => f.date));
  };

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Apply Template"
      icon={AppIcon.ApplyTemplate}
      message="A template applied to a day replaces the default week's on that day only."
      confirmText="Apply"
      confirmDisabled={!choice || selected.length === 0}
      isProcessing={isApplying}
      onConfirm={() => void apply()}
      onCancel={onClose}
      hasSaveShortcut>
      {failures.length > 0 && (
        <AlertMessage
          message={failures.length === 1 ? "One day couldn't be changed" : `${failures.length} days couldn't be changed`}
          details={failures.map((f) => `${formatShortDay(f.date)}: ${f.message}`)}
        />
      )}
      <Selector
        label="Template"
        placeholder="Choose a template"
        value={choice}
        onChange={setChoice}
        options={options}
        isLoading={templates.isLoading}
        isDisabled={isApplying}
      />
      {week.isError ? (
        <AlertMessage message="The week could not be loaded. Please try again." />
      ) : !week.data ? (
        <VStack gap={2}>
          <Skeleton height="var(--spacing-8)" />
          <Skeleton height="var(--spacing-8)" />
          <Skeleton height="var(--spacing-8)" />
        </VStack>
      ) : (
        <CheckboxList label="Days" description="What applies on each day now" value={selected} onChange={setTicked} isDisabled={isApplying} hasDividers>
          {days.map((d) => (
            <CheckboxListItem
              key={d.date}
              value={d.date ?? ''}
              label={dayLabel(d)}
              description={!canChange(d) ? "Past days can't change" : d.isDayTemplateOverridden ? 'Changed for this day' : undefined}
              isDisabled={!canChange(d)}
            />
          ))}
        </CheckboxList>
      )}
    </ConfirmDialog>
  );
}
