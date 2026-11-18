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
import {useCan} from '../../auth/auth';
import {
  describeTimelineEntry,
  groupByDay,
  hasTimelineFilters,
  isLetterEntry,
  isPatientEntry,
  isReferralEntry,
  isReferralFileEntry,
  isResultEntry,
  isSpecimenEntry,
  MAX_TIMELINE_PATHWAYS,
  NO_TIMELINE_FILTERS,
  patientTimelineOverviewQuery,
  patientTimelineQuery,
  TIMELINE_KINDS,
  timelineKindLabel,
  type TimelineEntry,
  type TimelineFilters,
  type TimelineKind,
  type TimelineOverview,
  type TimelinePathway,
  type TimelineReferral,
} from '../../api/timeline';
import {letterKindWords} from '../../api/letters';
import {formatDayHeading, formatShortDate, formatTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';
import {RichText} from '../shared/RichText';
import {AppIcon} from '../shared/icons';
import {OverdueToken, PriorityToken, StatusToken} from './PathwayCells';

const KIND_ICONS: Record<TimelineKind, IconType> = {
  Created: AppIcon.Add,
  Stages: AppIcon.Success,
  Appointments: AppIcon.AppointmentBooked,
  Referrals: AppIcon.Referrals,
  WaitingTime: AppIcon.Wait,
  Specimens: AppIcon.Specimens,
  Results: AppIcon.Results,
  Consent: AppIcon.Consent,
  Letters: AppIcon.Letters,
  Notes: AppIcon.Notes,
  Assignments: AppIcon.Assign,
  Blocks: AppIcon.StatusBlocked,
  Status: AppIcon.StatusChange,
  DueDates: AppIcon.Calendar,
  Details: AppIcon.Edit,
  Files: AppIcon.Attachment,
  Mdt: AppIcon.Mdt,
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
    case 'AppointmentEnteredInError':
      return AppIcon.EnteredInError;
    case 'AppointmentNotesUpdated':
      return AppIcon.Notes;
    case 'AppointmentDnaFollowUpRecorded':
      return AppIcon.DnaFollowUp;
    case 'ReferralDetailsEdited':
      return AppIcon.Edit;
    case 'ReferralWithdrawn':
      return AppIcon.Withdraw;
    case 'ReferralAccepted':
      return AppIcon.Success;
    case 'ReferralRejected':
      return AppIcon.StatusCancelled;
    case 'ReferralRedirectedElsewhere':
    case 'ReferralSpecialtyChanged':
      return AppIcon.StatusChange;
    case 'ReferralEnteredInError':
      return AppIcon.EnteredInError;
    case 'ReferralInformationRequested':
      return AppIcon.Info;
    case 'ReferralInformationReceived':
      return AppIcon.Success;
    case 'LetterSubmitted':
      return AppIcon.Send;
    case 'LetterReturned':
      return AppIcon.Back;
    case 'LetterApproved':
      return AppIcon.Success;
    case 'LetterSent':
      return entry.dispatchStatus === 'Failed' ? AppIcon.Warning : AppIcon.Send;
    case 'LetterWithdrawn':
      return AppIcon.Withdraw;
    case 'ReferralClockPaused':
      return AppIcon.PauseClock;
    case 'ReferralClockResumed':
      return AppIcon.ResumeClock;
    case 'ReferralClockStopped':
      return AppIcon.ClockStopped;
    case 'ReferralTargetChanged':
      return AppIcon.Calendar;
    case 'StageUnlocked':
      return AppIcon.Unlock;
    case 'StageSkipped':
      return AppIcon.Skip;
    case 'BranchChosen':
      return AppIcon.Branch;
    case 'StageReinstated':
      return AppIcon.Restore;
    case 'SpecimenSent':
      return AppIcon.SpecimenSent;
    case 'SpecimenReceived':
      return AppIcon.SpecimenReceived;
    case 'SpecimenReported':
      return AppIcon.Success;
    case 'SpecimenRejected':
      return AppIcon.StatusCancelled;
    case 'SpecimenLost':
      return AppIcon.Warning;
    case 'SpecimenEnteredInError':
      return AppIcon.EnteredInError;
    case 'SpecimenDetailsEdited':
      return AppIcon.Edit;
    case 'ResultSeen':
      return AppIcon.View;
    case 'ResultSignedOff':
      return AppIcon.SignOff;
    case 'ResultReassigned':
      return AppIcon.Reassign;
    case 'ResultEnteredInError':
      return AppIcon.EnteredInError;
    case 'MdtWithdrawn':
      return AppIcon.Withdraw;
    case 'MdtDeferred':
      return AppIcon.Calendar;
    case 'Unassigned':
      return AppIcon.Revoke;
    case 'AttachmentDeleted':
      return AppIcon.Delete;
    case 'StatusChanged':
      if (entry.toStatus === 'Blocked') return AppIcon.StatusBlocked;
      // Unblocked: back to work (cancelling a blocked pathway is a status change like any other).
      return entry.fromStatus === 'Blocked' && entry.toStatus !== 'Cancelled' ? AppIcon.StatusInProgress : AppIcon.StatusChange;
    default:
      return KIND_ICONS[entry.kind as TimelineKind] ?? AppIcon.Info;
  }
}

/** A letter's entry's links: the letter, then the pathway (unless deleted) or the referral it's about. */
function LetterEntryLinks({entry}: {entry: TimelineEntry}) {
  const hasPathway = Boolean(entry.pathwayId) && !isPatientEntry(entry) && !entry.isPathwayDeleted;
  return (
    <>
      <Text type="supporting" maxLines={1}>
        <Link href={`/letters/${entry.letterId}`}>{`The ${letterKindWords(entry.letterKind).toLowerCase()}`}</Link>
      </Text>
      {hasPathway ? (
        <Text type="supporting" maxLines={1}>
          <Link href={`/pathways/${entry.pathwayId}`}>{entry.pathwayTitle || 'Its pathway'}</Link>
        </Text>
      ) : (
        entry.referralId && (
          <Text type="supporting" maxLines={1}>
            <Link href={`/referrals/${entry.referralId}`}>{entry.specialtyName ? `Referral to ${entry.specialtyName}` : 'The referral'}</Link>
          </Text>
        )
      )}
    </>
  );
}

export interface PatientTimelineProps {
  patientId: string;
  /** What the feed is narrowed to. Held by the page, so it carries over when the page changes layout. */
  filters: TimelineFilters;
  onFiltersChange: (filters: TimelineFilters) => void;
}

/**
 * A patient's timeline: everything that happened to their pathways, appointments and referrals, from the pathway
 * audit log and the appointment and referral histories, in one feed.
 * At the top, what's due (open pathways by target date, overdue flagged, and referrals awaiting triage); then filter chips by kind and by
 * pathway; then the feed, newest first, grouped by UK day, with "Load more" for older entries (keyset paging on
 * the server). Each entry says when, who, what and which pathway (a link to it). Files attached to or deleted from
 * the patient, a pathway or a stage are entries too (the Files kind); only admins get their names.
 *
 * Use it wherever a patient's history belongs, at its own height: in a card's scrolling body (a `PanelCard`, or a
 * section of a `FoldingCard`). It lays out its own padding. The API decides what each person sees: staff get only
 * pathways that still exist and none of the free text the log keeps; admins get everything, deleted pathways
 * labelled as such.
 */
export function PatientTimeline({patientId, filters, onFiltersChange}: PatientTimelineProps) {
  const overview = useQuery(patientTimelineOverviewQuery(patientId));

  return (
    <VStack gap={0}>
      <Section variant="transparent" padding={6} dividers={['bottom']}>
        <UpcomingPathways patientId={patientId} overview={overview.data} isPending={overview.isPending} isError={overview.isError} />
      </Section>
      <Section variant="transparent" padding={6} dividers={['bottom']}>
        <TimelineFilterChips pathways={overview.data?.pathways ?? []} filters={filters} onFiltersChange={onFiltersChange} />
      </Section>
      <Section variant="transparent" padding={6}>
        <TimelineFeed patientId={patientId} filters={filters} onFiltersChange={onFiltersChange} />
      </Section>
    </VStack>
  );
}

/** The pathways due next, soonest first, with a link to the rest. */
function UpcomingPathways({
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
  const referrals = overview?.awaitingReferrals ?? [];
  return (
    <VStack gap={3}>
      <Heading level={3}>Upcoming</Heading>
      {isError ? (
        <AlertMessage message="What's due could not be loaded." />
      ) : isPending ? (
        <Spinner size="sm" label="Loading what's due..." />
      ) : upcoming.length === 0 ? (
        <Text type="body" color="secondary">
          Nothing is due: the patient has no open pathways.
        </Text>
      ) : (
        <List edge="flush" density="compact">
          {upcoming.map((pathway) => (
            <ListItem
              key={pathway.pathwayId}
              label={
                <Text type="body" weight="semibold" maxLines={2}>
                  <Link href={`/pathways/${pathway.pathwayId}`}>{pathway.title}</Link>
                </Text>
              }
              description={
                <HStack gap={2} vAlign="center" wrap="wrap">
                  <Text type="supporting" color="secondary">
                    {`Due ${formatShortDate(pathway.dueDate ?? '')}`}
                  </Text>
                  <StatusToken status={pathway.status ?? ''} />
                  {pathway.isOverdue && <OverdueToken />}
                </HStack>
              }
            />
          ))}
        </List>
      )}
      {more > 0 && (
        <Text type="supporting" color="secondary">
          {`And ${more} more. `}
          <Link href={`/patients/${patientId}/pathways`}>See all pathways</Link>
        </Text>
      )}
      {referrals.length > 0 && <AwaitingReferrals referrals={referrals} />}
    </VStack>
  );
}

/** The patient's referrals awaiting triage, oldest first, beside their open pathways: each links to its page. */
function AwaitingReferrals({referrals}: {referrals: readonly TimelineReferral[]}) {
  return (
    <VStack gap={2}>
      <Heading level={4}>Referrals awaiting triage</Heading>
      <List edge="flush" density="compact" aria-label="Referrals awaiting triage">
        {referrals.map((referral) => (
          <ListItem
            key={referral.referralId}
            label={
              <Text type="body" weight="semibold" maxLines={2}>
                <Link href={`/referrals/${referral.referralId}`}>{`Referral to ${referral.specialtyName}`}</Link>
              </Text>
            }
            description={
              <HStack gap={2} vAlign="center" wrap="wrap">
                <Text type="supporting" color="secondary">
                  {`Received ${formatShortDate(referral.receivedOn ?? '')}`}
                </Text>
                <PriorityToken priority={referral.requestedPriority ?? ''} />
              </HStack>
            }
          />
        ))}
      </List>
    </VStack>
  );
}

/** How many pathway chips show before "Show all": the newest, and any chosen. */
export const PATHWAY_CHIPS_SHOWN = 10;

/**
 * A pathway's chip: its title and the day it was created, so pathways with the same title can be told apart
 * (a deleted one says when it was deleted).
 */
export function pathwayChipLabel(pathway: TimelinePathway): string {
  const title = pathway.title ?? '';
  if (pathway.isDeleted) return pathway.deletedOn ? `${title}, deleted ${formatShortDate(pathway.deletedOn)}` : `${title} (deleted)`;
  return pathway.createdOn ? `${title}, ${formatShortDate(pathway.createdOn)}` : title;
}

/** Chips that narrow the feed, by kind and by pathway; none chosen shows everything. */
function TimelineFilterChips({
  pathways,
  filters,
  onFiltersChange,
}: {
  pathways: NonNullable<TimelineOverview['pathways']>;
  filters: TimelineFilters;
  onFiltersChange: (filters: TimelineFilters) => void;
}) {
  // Only the timeline's full view (ViewAudit) has deleted pathways' entries; anyone else has nothing to filter to.
  const hasFullView = useCan('ViewAudit');
  const kinds = TIMELINE_KINDS.filter((kind) => hasFullView || kind !== 'Deleted');
  const toggle = <T,>(list: T[], value: T, isOn: boolean) => (isOn ? [...list, value] : list.filter((other) => other !== value));
  const [isShowingAll, setIsShowingAll] = useState(false);
  // The newest few, and any chosen, so a choice never hides.
  const shown = isShowingAll
    ? pathways
    : pathways.filter((pathway, index) => index < PATHWAY_CHIPS_SHOWN || filters.pathwayIds.includes(pathway.id ?? ''));
  // The API takes at most this many pathways at once.
  const isAtLimit = filters.pathwayIds.length >= MAX_TIMELINE_PATHWAYS;

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
      {pathways.length > 1 && (
        <HStack gap={2} wrap="wrap" role="group" aria-label="Pathways">
          {shown.map((pathway) => {
            const id = pathway.id ?? '';
            const label = pathwayChipLabel(pathway);
            const isPressed = filters.pathwayIds.includes(id);
            return (
              <ToggleButton
                key={id}
                label={label}
                size="sm"
                icon={<Icon icon={pathway.isDeleted ? AppIcon.Delete : AppIcon.Pathways} />}
                isPressed={isPressed}
                isDisabled={isAtLimit && !isPressed}
                tooltip={isAtLimit && !isPressed ? `Choose at most ${MAX_TIMELINE_PATHWAYS} pathways` : undefined}
                onPressedChange={(isOn) => onFiltersChange({...filters, pathwayIds: toggle(filters.pathwayIds, id, isOn)})}>
                {label}
              </ToggleButton>
            );
          })}
          {pathways.length > PATHWAY_CHIPS_SHOWN && (
            <Button
              label={isShowingAll ? 'Show fewer' : `Show all ${pathways.length}`}
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
        description={isFiltered ? 'Choose other kinds or pathways, or show everything.' : "Changes to the patient's pathways, appointments and referrals will show here."}
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

/** One entry: its icon, what happened, then which pathway (an appointment may have none), when and who. */
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
          {/* A note's text (admins only), formatted as it is on the pathway's page. */}
          {noteText && <RichText>{noteText}</RichText>}
          <HStack gap={2} vAlign="center" wrap="wrap">
            {/* A result's entry links to the result, and to its specimen by its number, beside its pathway. */}
            {isResultEntry(entry) && entry.resultId && (
              <>
                <Text type="supporting" maxLines={1}>
                  <Link href={`/results/${entry.resultId}`}>The result</Link>
                </Text>
                {entry.specimenId && (
                  <Text type="supporting" maxLines={1}>
                    <Link href={`/specimens/${entry.specimenId}`}>{`Specimen ${entry.specimenNumber ?? ''}`.trim()}</Link>
                  </Text>
                )}
              </>
            )}
            {/* A specimen's entry links to the specimen, by its number, beside its pathway. */}
            {isSpecimenEntry(entry) && entry.specimenId && (
              <Text type="supporting" maxLines={1}>
                <Link href={`/specimens/${entry.specimenId}`}>{`Specimen ${entry.specimenNumber ?? ''}`.trim()}</Link>
              </Text>
            )}
            {/* A referral's entry, or its letter's, links to the referral. No pathway to link to: an appointment not for
                one, or a patient's own file (its detail says "To the patient"). */}
            {/* A letter's entry links to the letter, and to the pathway or referral it's about. */}
            {isLetterEntry(entry) && entry.letterId ? (
              <LetterEntryLinks entry={entry} />
            ) : (isReferralEntry(entry) || isReferralFileEntry(entry)) && entry.referralId ? (
              <>
                <Text type="supporting" maxLines={1}>
                  <Link href={`/referrals/${entry.referralId}`}>{entry.specialtyName ? `Referral to ${entry.specialtyName}` : 'The referral'}</Link>
                </Text>
                {/* Accepted: the pathway it was accepted into. */}
                {entry.action === 'ReferralAccepted' && entry.pathwayId && !entry.isPathwayDeleted && (
                  <Text type="supporting" maxLines={1}>
                    <Link href={`/pathways/${entry.pathwayId}`}>{entry.pathwayTitle || 'Its pathway'}</Link>
                  </Text>
                )}
              </>
            ) : !entry.pathwayId || isPatientEntry(entry) || isReferralFileEntry(entry) ? null : entry.isPathwayDeleted ? (
              <>
                <Text type="supporting" color="secondary" maxLines={1}>
                  {entry.pathwayTitle}
                </Text>
                <Token size="sm" label="Deleted" color="gray" />
              </>
            ) : (
              <>
                <Text type="supporting" maxLines={1}>
                  <Link href={`/pathways/${entry.pathwayId}`}>{entry.pathwayTitle}</Link>
                </Text>
                {/* A pathway accepted from a referral: its Created entry links to the referral. */}
                {entry.action === 'Created' && entry.referralId && (
                  <Text type="supporting" maxLines={1}>
                    <Link href={`/referrals/${entry.referralId}`}>From a referral</Link>
                  </Text>
                )}
              </>
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
