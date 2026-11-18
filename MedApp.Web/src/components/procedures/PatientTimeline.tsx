import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {Section} from '@astryxdesign/core/Section';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {ToggleButton} from '@astryxdesign/core/ToggleButton';
import {Token} from '@astryxdesign/core/Token';
import {useInfiniteQuery, useQuery} from '@tanstack/react-query';
import {useEffect, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {
  describeTimelineEntry,
  groupByDay,
  hasTimelineFilters,
  isPatientEntry,
  MAX_TIMELINE_PROCEDURES,
  NO_TIMELINE_FILTERS,
  patientTimelineOverviewQuery,
  patientTimelineQuery,
  TIMELINE_KINDS,
  timelineKindLabel,
  type TimelineEntry,
  type TimelineFilters,
  type TimelineKind,
  type TimelineOverview,
  type TimelineProcedure,
} from '../../api/timeline';
import {formatDayHeading, formatShortDate, formatTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';
import {RichText} from '../shared/RichText';
import {AppIcon} from '../shared/icons';
import {OverdueToken, StatusToken} from './ProcedureCells';

const KIND_ICONS: Record<TimelineKind, IconType> = {
  Created: AppIcon.Add,
  Stages: AppIcon.Success,
  Appointments: AppIcon.AppointmentBooked,
  Notes: AppIcon.Notes,
  Assignments: AppIcon.Assign,
  Blocks: AppIcon.StatusBlocked,
  Status: AppIcon.StatusChange,
  DueDates: AppIcon.Calendar,
  Details: AppIcon.Edit,
  Files: AppIcon.Attachment,
  Deleted: AppIcon.Delete,
};

/** Each entry's icon: by what it did, where its kind covers several things (a stage started, finished or unlocked). */
function entryIcon(entry: TimelineEntry): IconType {
  switch (entry.action) {
    case 'StageStarted':
      return AppIcon.StatusInProgress;
    case 'StageFinished':
      return entry.finishedWithoutVisit ? AppIcon.Warning : AppIcon.Success;
    case 'AppointmentRequested':
      return AppIcon.RequiresAppointment;
    case 'AppointmentAttended':
      return AppIcon.Success;
    case 'AppointmentDidNotAttend':
      return AppIcon.AppointmentDidNotAttend;
    case 'AppointmentRecordedAfterEvent':
      return AppIcon.RecordPast;
    case 'AppointmentCancelled':
      return AppIcon.StatusCancelled;
    case 'AppointmentNotesUpdated':
      return AppIcon.Notes;
    case 'StageUnlocked':
      return AppIcon.Unlock;
    case 'Unassigned':
      return AppIcon.Revoke;
    case 'AttachmentDeleted':
      return AppIcon.Delete;
    case 'StatusChanged':
      if (entry.toStatus === 'Blocked') return AppIcon.StatusBlocked;
      // Unblocked: back to work (cancelling a blocked procedure is a status change like any other).
      return entry.fromStatus === 'Blocked' && entry.toStatus !== 'Cancelled' ? AppIcon.StatusInProgress : AppIcon.StatusChange;
    default:
      return KIND_ICONS[entry.kind as TimelineKind] ?? AppIcon.Info;
  }
}

export interface PatientTimelineProps {
  patientId: string;
  /** What the feed is narrowed to. Held by the page, so it carries over when the page changes layout. */
  filters: TimelineFilters;
  onFiltersChange: (filters: TimelineFilters) => void;
}

/**
 * A patient's timeline: everything that happened to their procedures and appointments, from the procedure audit log
 * and the appointment history, in one feed.
 * At the top, what's due (open procedures by due date, overdue flagged); then filter chips by kind and by
 * procedure; then the feed, newest first, grouped by UK day, with "Load more" for older entries (keyset paging on
 * the server). Each entry says when, who, what and which procedure (a link to it). Files attached to or deleted from
 * the patient, a procedure or a stage are entries too (the Files kind); only admins get their names.
 *
 * Use it wherever a patient's history belongs, at its own height: in a card's scrolling body (a `PanelCard`, or a
 * section of a `FoldingCard`). It lays out its own padding. The API decides what each person sees: staff get only
 * procedures that still exist and none of the free text the log keeps; admins get everything, deleted procedures
 * labelled as such.
 */
export function PatientTimeline({patientId, filters, onFiltersChange}: PatientTimelineProps) {
  const overview = useQuery(patientTimelineOverviewQuery(patientId));

  return (
    <VStack gap={0}>
      <Section variant="transparent" padding={6} dividers={['bottom']}>
        <UpcomingProcedures patientId={patientId} overview={overview.data} isPending={overview.isPending} isError={overview.isError} />
      </Section>
      <Section variant="transparent" padding={6} dividers={['bottom']}>
        <TimelineFilterChips procedures={overview.data?.procedures ?? []} filters={filters} onFiltersChange={onFiltersChange} />
      </Section>
      <Section variant="transparent" padding={6}>
        <TimelineFeed patientId={patientId} filters={filters} onFiltersChange={onFiltersChange} />
      </Section>
    </VStack>
  );
}

/** The procedures due next, soonest first, with a link to the rest. */
function UpcomingProcedures({
  patientId,
  overview,
  isPending,
  isError,
}: {
  patientId: string;
  overview: TimelineOverview | undefined;
  isPending: boolean;
  isError: boolean;
}) {
  const upcoming = overview?.upcoming ?? [];
  const more = (overview?.openCount ?? 0) - upcoming.length;
  return (
    <VStack gap={3}>
      <Heading level={3}>Upcoming</Heading>
      {isError ? (
        <AlertMessage message="What's due could not be loaded." />
      ) : isPending ? (
        <Spinner size="sm" label="Loading what's due..." />
      ) : upcoming.length === 0 ? (
        <Text type="body" color="secondary">
          Nothing is due: the patient has no open procedures.
        </Text>
      ) : (
        <List edge="flush" density="compact">
          {upcoming.map((procedure) => (
            <ListItem
              key={procedure.procedureId}
              label={
                <Text type="body" weight="semibold" maxLines={2}>
                  <Link href={`/procedures/${procedure.procedureId}`}>{procedure.title}</Link>
                </Text>
              }
              description={
                <HStack gap={2} vAlign="center" wrap="wrap">
                  <Text type="supporting" color="secondary">
                    {`Due ${formatShortDate(procedure.dueDate ?? '')}`}
                  </Text>
                  <StatusToken status={procedure.status ?? ''} />
                  {procedure.isOverdue && <OverdueToken />}
                </HStack>
              }
            />
          ))}
        </List>
      )}
      {more > 0 && (
        <Text type="supporting" color="secondary">
          {`And ${more} more. `}
          <Link href={`/patients/${patientId}/procedures`}>See all procedures</Link>
        </Text>
      )}
    </VStack>
  );
}

/** How many procedure chips show before "Show all": the newest, and any chosen. */
export const PROCEDURE_CHIPS_SHOWN = 10;

/**
 * A procedure's chip: its title and the day it was created, so procedures with the same title can be told apart
 * (a deleted one says when it was deleted).
 */
export function procedureChipLabel(procedure: TimelineProcedure): string {
  const title = procedure.title ?? '';
  if (procedure.isDeleted) return procedure.deletedOn ? `${title}, deleted ${formatShortDate(procedure.deletedOn)}` : `${title} (deleted)`;
  return procedure.createdOn ? `${title}, ${formatShortDate(procedure.createdOn)}` : title;
}

/** Chips that narrow the feed, by kind and by procedure; none chosen shows everything. */
function TimelineFilterChips({
  procedures,
  filters,
  onFiltersChange,
}: {
  procedures: NonNullable<TimelineOverview['procedures']>;
  filters: TimelineFilters;
  onFiltersChange: (filters: TimelineFilters) => void;
}) {
  const {isAdmin} = useCurrentUser();
  // Staff never see deleted procedures' entries, so they have nothing to filter to.
  const kinds = TIMELINE_KINDS.filter((kind) => isAdmin || kind !== 'Deleted');
  const toggle = <T,>(list: T[], value: T, isOn: boolean) => (isOn ? [...list, value] : list.filter((other) => other !== value));
  const [isShowingAll, setIsShowingAll] = useState(false);
  // The newest few, and any chosen, so a choice never hides.
  const shown = isShowingAll
    ? procedures
    : procedures.filter((procedure, index) => index < PROCEDURE_CHIPS_SHOWN || filters.procedureIds.includes(procedure.id ?? ''));
  // The API takes at most this many procedures at once.
  const isAtLimit = filters.procedureIds.length >= MAX_TIMELINE_PROCEDURES;

  return (
    <VStack gap={3}>
      <HStack gap={2} vAlign="center">
        <Heading level={3}>Show</Heading>
        {hasTimelineFilters(filters) && (
          <Button label="Show everything" variant="ghost" size="sm" onClick={() => onFiltersChange(NO_TIMELINE_FILTERS)} />
        )}
      </HStack>
      <HStack gap={2} wrap="wrap" role="group" aria-label="Kinds of change">
        {kinds.map((kind) => (
          <ToggleButton
            key={kind}
            label={timelineKindLabel(kind)}
            size="sm"
            icon={<Icon icon={KIND_ICONS[kind]} />}
            isPressed={filters.kinds.includes(kind)}
            onPressedChange={(isOn) => onFiltersChange({...filters, kinds: toggle(filters.kinds, kind, isOn)})}>
            {timelineKindLabel(kind)}
          </ToggleButton>
        ))}
      </HStack>
      {procedures.length > 1 && (
        <HStack gap={2} wrap="wrap" role="group" aria-label="Procedures">
          {shown.map((procedure) => {
            const id = procedure.id ?? '';
            const label = procedureChipLabel(procedure);
            const isPressed = filters.procedureIds.includes(id);
            return (
              <ToggleButton
                key={id}
                label={label}
                size="sm"
                icon={<Icon icon={procedure.isDeleted ? AppIcon.Delete : AppIcon.Procedures} />}
                isPressed={isPressed}
                isDisabled={isAtLimit && !isPressed}
                tooltip={isAtLimit && !isPressed ? `Choose at most ${MAX_TIMELINE_PROCEDURES} procedures` : undefined}
                onPressedChange={(isOn) => onFiltersChange({...filters, procedureIds: toggle(filters.procedureIds, id, isOn)})}>
                {label}
              </ToggleButton>
            );
          })}
          {procedures.length > PROCEDURE_CHIPS_SHOWN && (
            <Button
              label={isShowingAll ? 'Show fewer' : `Show all ${procedures.length}`}
              variant="ghost"
              size="sm"
              onClick={() => setIsShowingAll((current) => !current)}
            />
          )}
        </HStack>
      )}
    </VStack>
  );
}

/** The feed itself: days, newest first, each with its entries, and "Load more" while there are older ones. */
function TimelineFeed({patientId, filters, onFiltersChange}: PatientTimelineProps) {
  const announce = useAnnounce();
  const feed = useInfiniteQuery(patientTimelineQuery(patientId, filters));
  const entries = feed.data?.pages.flatMap((page) => page.items ?? []) ?? [];
  const isFiltered = hasTimelineFilters(filters);
  // The first entry "Load more" brought in, which takes the focus, so it is never left on the button (which goes
  // once the last page is in).
  const [focusEntryId, setFocusEntryId] = useState<string | null>(null);

  useEffect(() => {
    if (focusEntryId) document.getElementById(entryAnchor(focusEntryId))?.focus();
  }, [focusEntryId]);

  if (feed.isPending)
    return (
      <Center axis="horizontal" padding={6}>
        <Spinner label="Loading the timeline..." />
      </Center>
    );

  if (feed.isError && entries.length === 0) return <AlertMessage message="The timeline could not be loaded. Please try again." />;

  if (entries.length === 0)
    return (
      <EmptyState
        title={isFiltered ? 'Nothing matches these filters' : 'Nothing has happened yet'}
        description={isFiltered ? 'Choose other kinds or procedures, or show everything.' : "Changes to the patient's procedures and appointments will show here."}
        isCompact
        actions={isFiltered ? <Button label="Show Everything" variant="secondary" onClick={() => onFiltersChange(NO_TIMELINE_FILTERS)} /> : undefined}
      />
    );

  const loadMore = async () => {
    const before = entries.length;
    const result = await feed.fetchNextPage();
    // A failure says so in its alert.
    if (result.isError) return;
    const loaded = (result.data?.pages.flatMap((page) => page.items ?? []) ?? []).slice(before);
    if (loaded.length === 0) return;
    announce(`${loaded.length} older ${loaded.length === 1 ? 'entry' : 'entries'} loaded.`);
    setFocusEntryId(loaded[0].id ?? null);
  };

  return (
    <VStack gap={6}>
      {groupByDay(entries).map(({day, entries: dayEntries}) => (
        <List key={day} edge="flush" hasDividers header={<Heading level={3}>{formatDayHeading(dayEntries[0].atUtc ?? '')}</Heading>}>
          {dayEntries.map((entry) => (
            <TimelineEntryRow key={entry.id} entry={entry} />
          ))}
        </List>
      ))}
      {feed.isFetchNextPageError && <AlertMessage message="Older entries could not be loaded. Please try again." />}
      {feed.hasNextPage && (
        <HStack hAlign="center">
          <Button
            label="Load More"
            variant="secondary"
            icon={<Icon icon={AppIcon.ChevronDown} />}
            isLoading={feed.isFetchingNextPage}
            onClick={() => void loadMore()}
          />
        </HStack>
      )}
    </VStack>
  );
}

/** The element on each entry that can take the focus (after "Load more"). */
const entryAnchor = (id: string) => `timeline-entry-${id}`;

/** One entry: its icon, what happened, then which procedure (an appointment may have none), when and who. */
function TimelineEntryRow({entry}: {entry: TimelineEntry}) {
  const {title, detail, noteText} = describeTimelineEntry(entry);
  return (
    <ListItem
      align="first-line"
      startContent={<Icon icon={entryIcon(entry)} color="secondary" />}
      label={
        // Focusable from code only, so "Load more" can put the focus on the first entry it brought in.
        <VStack id={entryAnchor(entry.id ?? '')} tabIndex={-1}>
          <Text type="body" weight="semibold" maxLines={2}>
            {title}
          </Text>
        </VStack>
      }
      description={
        <VStack gap={1}>
          {detail && (
            <Text type="body" maxLines={3}>
              {detail}
            </Text>
          )}
          {/* A note's text (admins only), formatted as it is on the procedure's page. */}
          {noteText && <RichText>{noteText}</RichText>}
          <HStack gap={2} vAlign="center" wrap="wrap">
            {/* No procedure to link to: an appointment not for one, or a patient's own file (its detail says "To the patient"). */}
            {!entry.procedureId || isPatientEntry(entry) ? null : entry.isProcedureDeleted ? (
              <>
                <Text type="supporting" color="secondary" maxLines={1}>
                  {entry.procedureTitle}
                </Text>
                <Token size="sm" label="Deleted" color="gray" />
              </>
            ) : (
              <Text type="supporting" maxLines={1}>
                <Link href={`/procedures/${entry.procedureId}`}>{entry.procedureTitle}</Link>
              </Text>
            )}
            <Text type="supporting" color="secondary">
              {`${formatTime(entry.atUtc ?? '')} · ${entry.username}`}
            </Text>
          </HStack>
        </VStack>
      }
    />
  );
}
