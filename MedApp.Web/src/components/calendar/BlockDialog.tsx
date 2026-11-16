import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Selector, SelectorOption} from '@astryxdesign/core/Selector';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useRef, useState} from 'react';
import {activityTypesQuery, useCalendarActions, type Calendar, type CalendarBlock, type CalendarDay} from '../../api/calendar';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {
  addMinutes,
  busyTimes,
  formatCalendarDay,
  formatTimeRange,
  minutesBetween,
  overlaps,
  SLOT_MINUTES,
  type Interval,
} from '../../utils/calendarSlots';
import {formatTime, parseUtc} from '../../utils/dates';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {DialogFrame} from '../modals/DialogFrame';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {ActivityToken} from './CalendarTokens';

export interface BlockDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** Whose calendar it's on. */
  staffUserId: string;
  /** The days it can be on (the calendar's): working days that aren't past are offered. */
  days: CalendarDay[];
  /** An existing block (stored, or from a day template) to change; otherwise a new one. */
  block?: CalendarBlock;
  /** For a new block: its day and time, e.g. from a selection on the calendar. */
  date?: string;
  startUtc?: string;
  endUtc?: string;
  /** Whether it can be changed (the calendar's owner or an admin, not on a past day); otherwise it's only shown. */
  canEdit?: boolean;
  /**
   * What's already on the calendar (its blocks and appointments), when it's loaded: a new block without a time then
   * starts in the first free time instead of on top of something.
   */
  calendar?: Pick<Calendar, 'blocks' | 'appointments'>;
}

/** The fields a 400 can name, shown on the fields themselves. */
const FIELDS = ['activityTypeId', 'startUtc', 'endUtc', 'note'];

/**
 * Adding or changing blocked time (docs/calendar-plan.md, decisions 5 and 12): the activity (in its colour), the day,
 * the start and end in 15-minute steps through the working day, and a note, which is about staff, never patients.
 *
 * A stored block is saved or deleted. A block from a day template is changed or removed for that day only (the
 * template stays as it is), and the dialog says which template it's from. Clashes (409) and field errors (400) show
 * in the dialog. Use it from the calendar: a selection's Block time, the New menu, or a click on a block.
 */
export function BlockDialog(props: BlockDialogProps) {
  // A fresh form each time it opens.
  const [session, setSession] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setSession((s) => s + 1);
  }
  return <BlockForm key={session} {...props} />;
}

const MINUTE = 60_000;
/** A block's default length: an hour. */
const DEFAULT_LENGTH = 4 * SLOT_MINUTES;

/**
 * Where a new block starts and ends when nothing chose a time, in minutes from the start of the working day.
 *
 * It starts at the start of the working day, or on today the next 15-minute step from now, so it isn't offered in time
 * that has gone. Knowing what's already on the calendar (`busy`), it starts at the first 15 minutes from there that are
 * free, and ends an hour later or where the next thing starts, whichever is sooner. Otherwise, or when nothing is free,
 * it's an hour from that start (an hour before the end of the day at the latest).
 */
function defaultTimes(day: CalendarDay | undefined, dayMinutes: number, busy: Interval[] | undefined, now: Date = new Date()) {
  const fallback = (start: number) => ({start, end: Math.min(start + DEFAULT_LENGTH, dayMinutes)});
  if (!day?.workStartUtc) return fallback(0);
  const dayStart = day.workStartUtc;
  const from = day.isToday ? Math.max(Math.ceil(minutesBetween(dayStart, now.toISOString()) / SLOT_MINUTES) * SLOT_MINUTES, 0) : 0;
  const latest = Math.max(dayMinutes - DEFAULT_LENGTH, 0);
  if (!busy) return fallback(Math.min(from, latest));
  const base = parseUtc(dayStart).getTime();
  const at = (minutes: number) => base + minutes * MINUTE;
  for (let start = from; start + SLOT_MINUTES <= dayMinutes; start += SLOT_MINUTES) {
    const slot = {start: at(start), end: at(start + SLOT_MINUTES)};
    if (busy.some((b) => overlaps(slot, b))) continue;
    // Nothing overlaps its first 15 minutes, so anything in the next hour starts after them.
    const hour = {start: slot.start, end: at(Math.min(start + DEFAULT_LENGTH, dayMinutes))};
    const next = Math.min(hour.end, ...busy.filter((b) => overlaps(hour, b)).map((b) => b.start));
    return {start, end: start + Math.round((next - slot.start) / MINUTE)};
  }
  return fallback(Math.min(from, latest));
}

function BlockForm({isOpen, onClose, staffUserId, days, block, date, startUtc, endUtc, canEdit = true, calendar}: BlockDialogProps) {
  const announce = useAnnounce();
  const actions = useCalendarActions();
  const activityTypes = useQuery({...activityTypesQuery, enabled: isOpen});

  const workingDays = days.filter((d) => d.isWorkingDay && (!d.isPast || d.date === (block?.date ?? date)));
  const initialDate = block?.date ?? date ?? workingDays.find((d) => !d.isPast)?.date ?? '';
  const [chosenDate, setChosenDate] = useState(initialDate);
  const day = days.find((d) => d.date === chosenDate);
  const dayStart = day?.workStartUtc ?? '';
  const dayMinutes = day?.workStartUtc && day.workEndUtc ? minutesBetween(day.workStartUtc, day.workEndUtc) : 600;

  const offsetOf = (iso: string | null | undefined) => (iso && dayStart ? minutesBetween(dayStart, iso) : null);
  const [initial] = useState(() => {
    const chosen = {start: offsetOf(block?.startUtc ?? startUtc), end: offsetOf(block?.endUtc ?? endUtc)};
    if (chosen.start !== null && chosen.end !== null) return {start: chosen.start, end: chosen.end};
    const times = defaultTimes(day, dayMinutes, calendar ? busyTimes(calendar) : undefined);
    return {start: chosen.start ?? times.start, end: chosen.end ?? times.end};
  });
  const [start, setStart] = useState(initial.start);
  const [end, setEnd] = useState(initial.end);
  const [activityTypeId, setActivityTypeId] = useState(block?.activityTypeId ?? '');
  const [note, setNote] = useState(block?.note ?? '');
  const [problem, setProblem] = useState<{validation: ValidationErrors | null; message: string | null}>({validation: null, message: null});

  const isTemplateBlock = block?.isFromTemplate === true;
  const content = useRef<HTMLDivElement>(null);

  const activities = (activityTypes.data ?? []).filter((a) => !a.isArchived || a.id === block?.activityTypeId);
  const colourOf = (id: string) => activities.find((a) => a.id === id)?.colour;
  // The first activity is the default for a new block.
  const activityId = activityTypeId || (block ? '' : (activities[0]?.id ?? ''));

  /** Times in 15-minute steps through the working day, keeping an existing time that isn't on a step. */
  const timeOptions = (from: number, to: number, keep: number) => {
    const offsets = new Set<number>();
    for (let m = from; m <= to; m += SLOT_MINUTES) offsets.add(m);
    if (keep >= 0 && keep <= dayMinutes) offsets.add(keep);
    return [...offsets].sort((a, b) => a - b).map((m) => ({value: String(m), label: dayStart ? formatTime(addMinutes(dayStart, m)) : String(m)}));
  };

  const isSaving =
    actions.addBlock.isPending ||
    actions.saveBlock.isPending ||
    actions.deleteBlock.isPending ||
    actions.changeTemplateBlock.isPending ||
    actions.removeTemplateBlock.isPending;
  const isValid = activityId !== '' && end > start && dayStart !== '';

  const fail = (error: unknown) => {
    if (error instanceof ApiValidationError) setProblem({validation: error.errors, message: null});
    else setProblem({validation: null, message: problemMessage(error, 'The blocked time could not be saved. Please try again.')});
  };

  const done = (message: string) => () => {
    announce(message);
    onClose();
  };

  const values = () => ({
    activityTypeId: activityId,
    startUtc: addMinutes(dayStart, start),
    endUtc: addMinutes(dayStart, end),
    note: note.trim() || null,
  });

  const save = () => {
    if (!isValid || isSaving || !canEdit) return false;
    setProblem({validation: null, message: null});
    if (!block) {
      actions.addBlock.mutate({staffUserId, ...values()}, {onSuccess: done('Time blocked'), onError: fail});
    } else if (isTemplateBlock) {
      actions.changeTemplateBlock.mutate(
        {userId: staffUserId, date: block.date ?? '', templateBlockId: block.templateBlockId ?? '', ...values()},
        {onSuccess: done('Changed for this day'), onError: fail},
      );
    } else {
      actions.saveBlock.mutate({id: block.blockId ?? '', ...values()}, {onSuccess: done('Blocked time saved'), onError: fail});
    }
  };

  const remove = (block: CalendarBlock) => {
    setProblem({validation: null, message: null});
    if (isTemplateBlock) {
      actions.removeTemplateBlock.mutate(
        {userId: staffUserId, date: block.date ?? '', templateBlockId: block.templateBlockId ?? ''},
        {onSuccess: done('Removed from this day'), onError: fail},
      );
    } else {
      actions.deleteBlock.mutate(block.blockId ?? '', {onSuccess: done('Blocked time deleted'), onError: fail});
    }
  };

  useSaveShortcut(content, isOpen && canEdit ? save : undefined);

  const title = !block ? 'Block Time' : canEdit ? 'Change Blocked Time' : 'Blocked Time';
  const dayTemplateName = days.find((d) => d.date === block?.date)?.dayTemplateName;

  return (
    <DialogFrame
      isOpen={isOpen}
      title={title}
      icon={<Icon icon={AppIcon.BlockTime} size="lg" color="accent" />}
      onClose={onClose}
      width={480}
      purpose={isSaving ? 'required' : 'form'}
      footer={
        canEdit ? (
          <>
            {block && (
              <StackItem size="fill">
                <HStack>
                  <Button
                    label={isTemplateBlock ? 'Remove from This Day' : 'Delete'}
                    variant="destructive"
                    icon={<Icon icon={AppIcon.Delete} />}
                    isDisabled={isSaving}
                    onClick={() => remove(block)}
                  />
                </HStack>
              </StackItem>
            )}
            <Button label="Cancel" variant="secondary" isDisabled={isSaving} onClick={onClose} />
            <Button
              label={!block ? 'Block Time' : isTemplateBlock ? 'Change for This Day' : 'Save'}
              variant="primary"
              isDisabled={!isValid}
              isLoading={isSaving}
              onClick={save}
            />
          </>
        ) : (
          <Button label="Close" variant="secondary" onClick={onClose} />
        )
      }>
      {isOpen && (
        <VStack ref={content} gap={4} hAlign="stretch">
          {problem.message && <AlertMessage message={problem.message} />}
          {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
          {isTemplateBlock && (
            <AlertMessage
              status="info"
              message={dayTemplateName ? `From the '${dayTemplateName}' day template` : 'From a day template'}
              details={canEdit ? ['Changes here apply to this day only; the template stays as it is.'] : []}
            />
          )}
          {!canEdit && block ? (
            <MetadataList label={{position: 'start', width: 100}}>
              <MetadataListItem label="Activity">
                <ActivityToken name={block.activityName ?? ''} colour={block.colour} />
              </MetadataListItem>
              <MetadataListItem label="When">
                {`${formatCalendarDay(block.date ?? '')}, ${formatTimeRange(block.startUtc ?? '', block.endUtc ?? '')}`}
              </MetadataListItem>
              {block.note && <MetadataListItem label="Note">{block.note}</MetadataListItem>}
              {block.overlapsAppointment && <MetadataListItem label="Overlaps">An appointment</MetadataListItem>}
            </MetadataList>
          ) : (
            <>
              {block?.overlapsAppointment && (
                <AlertMessage status="warning" message="This overlaps an appointment. Move one of them, or leave both if that's right." />
              )}
              <Selector
                label="Activity"
                value={activityId}
                onChange={setActivityTypeId}
                isLoading={activityTypes.isLoading}
                status={fieldStatus(problem.validation, 'activityTypeId')}
                options={activities.map((a) => ({value: a.id ?? '', label: a.name ?? ''}))}
                // Each activity in its colour, as it shows on the calendar.
                renderOption={(option) => <SelectorOption label={<ActivityToken name={option.label ?? ''} colour={colourOf(option.value)} />} />}
                renderValue={(option) => <ActivityToken name={option.label ?? ''} colour={colourOf(option.value)} />}
              />
              <Selector
                label="Day"
                value={chosenDate}
                onChange={setChosenDate}
                isDisabled={isTemplateBlock}
                disabledMessage={isTemplateBlock ? "A template block is changed for its own day; add a block to change another day's time." : undefined}
                options={workingDays.map((d) => ({value: d.date ?? '', label: formatCalendarDay(d.date ?? '')}))}
              />
              <HStack gap={3}>
                <StackItem size="fill">
                  <Selector
                    label="Start"
                    value={String(start)}
                    onChange={(value) => {
                      const next = Number(value);
                      // Keep its length where it can, so moving the start moves the whole block.
                      setEnd(Math.min(dayMinutes, next + Math.max(SLOT_MINUTES, end - start)));
                      setStart(next);
                    }}
                    status={fieldStatus(problem.validation, 'startUtc')}
                    options={timeOptions(0, dayMinutes - SLOT_MINUTES, start)}
                  />
                </StackItem>
                <StackItem size="fill">
                  <Selector
                    label="End"
                    value={String(end)}
                    onChange={(value) => setEnd(Number(value))}
                    status={end <= start ? {type: 'error', message: 'Ends before it starts.'} : fieldStatus(problem.validation, 'endUtc')}
                    options={timeOptions(SLOT_MINUTES, dayMinutes, end)}
                  />
                </StackItem>
              </HStack>
              <TextArea
                label="Note"
                isOptional
                description="About the time, for staff. Don't put patient details here."
                value={note}
                onChange={setNote}
                rows={2}
                maxLength={200}
                status={fieldStatus(problem.validation, 'note')}
              />
              <Text type="supporting" color="secondary">
                {dayStart && end > start ? `${formatCalendarDay(chosenDate)}, ${formatTimeRange(addMinutes(dayStart, start), addMinutes(dayStart, end))}` : ''}
              </Text>
            </>
          )}
        </VStack>
      )}
    </DialogFrame>
  );
}
