import {Button} from '@astryxdesign/core/Button';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {
  bookingsWords,
  closedDayBookingsQuery,
  closedDaysOfYearQuery,
  hasBookings,
  isClosedEverywhere,
  useAddBankHolidays,
  useAddClosedDay,
  useDeleteClosedDay,
  type AddedBankHolidays,
  type ClosedDay,
} from '../../api/closedDays';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {sitesQuery} from '../../api/sites';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {formatShortDayDate, ukToday} from '../../utils/calendarSlots';
import {WEEK_STARTS_ON} from '../../utils/dates';

/** The years the API takes for bank holidays, and the page shows. */
export const FIRST_YEAR = 2000;
export const LAST_YEAR = 2100;

/** The Closed Days page's address: the year shown (this UK year when it's left out). */
export interface ClosedDaysSearch {
  year?: number;
}

/** Reads the address's year, dropping anything that isn't a whole year the page can show. */
export function parseClosedDaysSearch(raw: Record<string, unknown>): ClosedDaysSearch {
  const year = typeof raw.year === 'number' ? raw.year : typeof raw.year === 'string' && /^\d{4}$/.test(raw.year.trim()) ? Number(raw.year) : NaN;
  return Number.isInteger(year) && year >= FIRST_YEAR && year <= LAST_YEAR ? {year} : {};
}

/** This year in the UK. */
export const ukYear = (today: string = ukToday()) => Number(today.slice(0, 4));

/** The year a search shows. */
export const shownYear = (search: ClosedDaysSearch, today?: string) => search.year ?? ukYear(today);

/** The page's query: the year's closed days. Its route's loader preloads the same. */
export const closedDaysPageQuery = (search: ClosedDaysSearch, today?: string) => closedDaysOfYearQuery(shownYear(search, today));

/** Why a bank holiday wasn't added, in words. */
const SKIPPED_REASONS: Record<string, string> = {AlreadyClosed: 'already closed', Past: 'past'};

/** What a change did, shown above the list until it's dismissed: a headline, and a line for each thing to know. */
export interface Outcome {
  message: string;
  details: string[];
}

/** What's on a closed day, in words, once it's closed: nothing when it was empty. */
const bookedOn = (day: ClosedDay) =>
  day.bookings && hasBookings(day.bookings)
    ? [bookingsWords(day.bookings, formatShortDayDate(day.date ?? '', {withYear: false}), {isDone: true})]
    : [];

/**
 * What adding a year's bank holidays did, in words: "Added 8 bank holidays.", each one left out and why, and what was
 * already on each one added ("On Mon 28 Dec: 2 appointments, for k.ashford. They stay; k.ashford has been told.").
 */
export function bankHolidaysOutcome(result: AddedBankHolidays): Outcome {
  const added = result.added?.length ?? 0;
  const skipped = result.skipped ?? [];
  const message =
    added === 0
      ? `No bank holidays added for ${result.year}.`
      : `Added ${added === 1 ? '1 bank holiday' : `${added} bank holidays`}.`;
  const details = [
    ...skipped.map(
      (s) => `${s.name ?? ''}, ${formatShortDayDate(s.date ?? '', {withYear: false})}: ${SKIPPED_REASONS[s.reason ?? ''] ?? (s.reason ?? '').toLowerCase()}`,
    ),
    ...(result.added ?? []).flatMap(bookedOn),
  ];
  return {message, details};
}

/**
 * What adding one closed day did, in words: that it was added, and what was already on it; for a site's, where it
 * closed ("Thu 24 Dec 2026 closed at Northgate outreach clinic: Refit."), as it tells nobody and closes no calendar.
 */
export function closedDayOutcome(saved: ClosedDay, name: string): Outcome {
  if (!isClosedEverywhere(saved)) {
    return {message: `${formatShortDayDate(saved.date ?? '')} closed at ${saved.siteName ?? 'the site'}: ${saved.name ?? name}.`, details: []};
  }
  return {message: `${saved.name ?? name} added as a closed day.`, details: bookedOn(saved)};
}

/** Where a closed day is closed, in words: "Everywhere", or its site's name. */
export const whereWords = (day: Pick<ClosedDay, 'siteId' | 'siteName'>) => (isClosedEverywhere(day) ? 'Everywhere' : (day.siteName ?? ''));

/** The Where picker's value for closing everywhere (any other value is a site's ID). */
const EVERYWHERE = 'everywhere';

type ClosedDayRow = ClosedDay & Record<string, unknown>;

/** The fields Add Closed Day's 400 can name. */
const FIELDS = ['date', 'siteId', 'name'];

interface Problem {
  validation: ValidationErrors | null;
  message: string | null;
}

const NO_PROBLEM: Problem = {validation: null, message: null};

export interface ClosedDaysPageProps {
  search: ClosedDaysSearch;
  onSearchChange: (search: ClosedDaysSearch) => void;
}

/**
 * Closed days (admin, docs/calendar-plan.md decisions 72 to 75, and decision 121 for sites): the days the clinic is
 * closed, such as bank holidays and staff training days, everywhere or at one site, one year at a time (the year in
 * the address). A day closed everywhere is like a weekend on every calendar: nothing new can be booked, blocked or
 * arranged on it, and anything already there stays, flagged. A site's closed day closes its rooms, but nobody's
 * calendar yet (working hours, a later change, will say who works where). Admins add one by hand (a date from today
 * on, where, and a name: once the date is chosen, for everywhere the dialog says what's already on it and whose,
 * `closedDayBookingsQuery`), add a year's England and Wales bank holidays (everywhere) in one go, and remove one still
 * to come. Past days can't change. After adding, what was on each day closed (whose staff are told), and any bank
 * holidays left out and why, show above the list until dismissed.
 */
export function ClosedDaysPage({search, onSearchChange}: ClosedDaysPageProps) {
  const [today] = useState(() => ukToday());
  const year = shownYear(search, today);
  const {data = [], isPending, isError} = useQuery(closedDaysPageQuery(search, today));
  const add = useAddClosedDay();
  const remove = useDeleteClosedDay();
  const addBankHolidays = useAddBankHolidays();
  const announce = useAnnounce();

  const [isAdding, setIsAdding] = useState(false);
  const [date, setDate] = useState<string | undefined>();
  const [where, setWhere] = useState(EVERYWHERE);
  const [name, setName] = useState('');
  const [problem, setProblem] = useState<Problem>(NO_PROBLEM);
  const [removing, setRemoving] = useState<ClosedDay | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  // Each outcome is a new banner, so one dismissed doesn't hide the next.
  const [outcomeKey, setOutcomeKey] = useState(0);
  const show = (next: Outcome) => {
    setOutcome(next);
    setOutcomeKey((k) => k + 1);
  };
  // The open sites, for Add Closed Day's Where.
  const sites = useQuery({...sitesQuery(), enabled: isAdding});
  const isEverywhere = where === EVERYWHERE;
  const siteName = isEverywhere ? undefined : (sites.data?.find((s) => s.id === where)?.name ?? 'the site');
  // What's already on the date chosen in Add Closed Day, when it closes everywhere (a site's closes nobody's calendar).
  const bookings = useQuery({...closedDayBookingsQuery(date ?? ''), enabled: isAdding && date !== undefined && isEverywhere});

  const goToYear = (next: number) => {
    setError(null);
    setOutcome(null);
    onSearchChange(next === ukYear(today) ? {} : {year: next});
  };

  const openAdd = () => {
    setDate(undefined);
    setWhere(EVERYWHERE);
    setName('');
    setProblem(NO_PROBLEM);
    setError(null);
    setOutcome(null);
    setIsAdding(true);
  };

  // Only called while Add Closed Day can be confirmed: a date and a name (`confirmDisabled` holds Ctrl+S back too).
  const saveClosedDay = (day: string) => {
    setProblem(NO_PROBLEM);
    add.mutate(
      isEverywhere ? {date: day, name: name.trim()} : {date: day, name: name.trim(), siteId: where},
      {
        onSuccess: (saved) => {
          const next = closedDayOutcome(saved, name.trim());
          announce([next.message, ...next.details].join(' '));
          setIsAdding(false);
          // Show the year it's in, so it's there to see; then what adding it did (moving year clears the last outcome).
          const savedYear = Number((saved.date ?? day).slice(0, 4));
          if (savedYear !== year) goToYear(savedYear);
          show(next);
        },
        onError: (e) =>
          setProblem(
            e instanceof ApiValidationError
              ? {validation: e.errors, message: null}
              : {validation: null, message: problemMessage(e, 'The closed day could not be added. Please try again.')},
          ),
      },
    );
  };

  const confirmRemove = (day: ClosedDay) => {
    remove.mutate(day.id ?? '', {
      onSuccess: () => {
        announce(`${day.name ?? 'Closed day'} removed`);
        setRemoving(null);
      },
      onError: (e) => {
        setError(problemMessage(e, 'The closed day could not be removed. Please try again.'));
        setRemoving(null);
      },
    });
  };

  const addYearsBankHolidays = () => {
    setError(null);
    setOutcome(null);
    addBankHolidays.mutate(year, {
      onSuccess: (result) => {
        const next = bankHolidaysOutcome(result);
        show(next);
        const skipped = result.skipped?.length ?? 0;
        announce([next.message, skipped > 0 ? `${skipped} left out.` : null, ...next.details.slice(skipped)].filter(Boolean).join(' '));
      },
      onError: (e) => setError(problemMessage(e, 'The bank holidays could not be added. Please try again.')),
    });
  };

  const columns: DataTableColumn<ClosedDayRow>[] = [
    {
      key: 'date',
      header: 'Date',
      width: pixel(150),
      renderCell: (d) => (
        <Text type="body" weight="semibold" maxLines={1}>
          {formatShortDayDate(d.date ?? '')}
        </Text>
      ),
    },
    {key: 'name', header: 'Name', width: proportional(3, {minWidth: 160}), renderCell: (d) => <Text type="body">{d.name}</Text>},
    {
      key: 'site',
      header: 'Site',
      width: proportional(2, {minWidth: 130}),
      renderCell: (d) => (
        <Text type="body" color={isClosedEverywhere(d) ? 'secondary' : undefined} maxLines={1}>
          {whereWords(d)}
        </Text>
      ),
    },
    {
      key: 'past',
      header: 'Status',
      width: pixel(110),
      renderCell: (d) => (d.isPast ? <Token size="sm" label="Past" color="gray" /> : <Token size="sm" label="To come" color="blue" />),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(80),
      renderCell: (d) => (
        <TableActions
          actions={[
            {
              label: d.isPast
                ? "Past days can't change"
                : `Remove ${d.name ?? 'closed day'}${isClosedEverywhere(d) ? '' : ` at ${d.siteName ?? 'the site'}`}`,
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: d.isPast === true,
              onClick: () => {
                setError(null);
                setRemoving(d);
              },
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading closed days...' : `${year}: ${data.length === 1 ? '1 closed day' : `${data.length} closed days`}`;

  return (
    <Page
      title="Closed Days"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage message={error} />
        ) : (
          isError && <AlertMessage message="The closed days could not be loaded. Please try again." />
        )
      }
      actions={
        <>
          <IconButton
            label="Previous year"
            tooltip="Previous year"
            variant="secondary"
            icon={<Icon icon={AppIcon.ChevronLeft} />}
            isDisabled={year <= FIRST_YEAR}
            onClick={() => goToYear(year - 1)}
          />
          <IconButton
            label="Next year"
            tooltip="Next year"
            variant="secondary"
            icon={<Icon icon={AppIcon.ChevronRight} />}
            isDisabled={year >= LAST_YEAR}
            onClick={() => goToYear(year + 1)}
          />
          <Button
            label={`Add Bank Holidays for ${year}`}
            variant="secondary"
            icon={<Icon icon={AppIcon.Calendar} />}
            isLoading={addBankHolidays.isPending}
            onClick={addYearsBankHolidays}
          />
          <Button label="Add Closed Day" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={openAdd} />
        </>
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={isAdding}
            title="Add Closed Day"
            message={
              isEverywhere
                ? 'Nobody can book, block or arrange anything new on it. Anything already there stays, flagged as being on a closed day.'
                : `Closes ${siteName}'s rooms. Staff calendars aren't closed: to close everyone's, choose Everywhere.`
            }
            icon={AppIcon.ClosedDays}
            confirmText="Add Closed Day"
            isProcessing={add.isPending}
            confirmDisabled={!date || !name.trim()}
            hasSaveShortcut
            onConfirm={() => date !== undefined && saveClosedDay(date)}
            onCancel={() => setIsAdding(false)}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <DateInput
                label="Date"
                value={date as ISODateString | undefined}
                onChange={(value) => setDate(value ?? undefined)}
                min={today as ISODateString}
                weekStartsOn={WEEK_STARTS_ON}
                format="date_weekday"
                isRequired
                status={fieldStatus(problem.validation, 'date')}
              />
              <AnchoredSelector
                label="Where"
                options={[
                  {value: EVERYWHERE, label: 'Everywhere'},
                  ...(sites.data ?? []).map((s) => ({value: s.id ?? '', label: s.name ?? ''})),
                  // A site chosen that has since left the list (archived meanwhile) stays chosen, so it's never swapped for everywhere.
                  ...(isEverywhere || sites.data?.some((s) => s.id === where) ? [] : [{value: where, label: siteName ?? ''}]),
                ]}
                value={where}
                onChange={(value: string) => setWhere(value)}
                isRequired
                status={fieldStatus(problem.validation, 'siteId')}
              />
              <TextInput
                label="Name"
                description="What it is, for everyone's calendars. About the clinic, never a patient."
                value={name}
                onChange={setName}
                placeholder="e.g. Staff training day"
                autoComplete="off"
                isRequired
                status={fieldStatus(problem.validation, 'name')}
              />
              {date !== undefined &&
                isEverywhere &&
                (bookings.isError ? (
                  <AlertMessage status="warning" message="What's already booked then could not be checked." />
                ) : bookings.data ? (
                  <AlertMessage
                    status={hasBookings(bookings.data) ? 'warning' : 'info'}
                    message={bookingsWords(bookings.data, formatShortDayDate(date, {withYear: false}))}
                  />
                ) : (
                  <Text type="supporting" color="secondary">
                    Checking what&apos;s booked then...
                  </Text>
                ))}
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={removing !== null}
            title="Remove Closed Day"
            message={
              removing && !isClosedEverywhere(removing)
                ? `${removing.siteName ?? 'The site'}'s rooms open again that day.`
                : 'The day becomes a working day again on every calendar.'
            }
            isDanger
            confirmText="Remove"
            isProcessing={remove.isPending}
            onConfirm={() => removing && confirmRemove(removing)}
            onCancel={() => setRemoving(null)}>
            {removing && (
              <MetadataList>
                <MetadataListItem label="Date">{formatShortDayDate(removing.date ?? '')}</MetadataListItem>
                <MetadataListItem label="Name">{removing.name}</MetadataListItem>
                <MetadataListItem label="Where">{whereWords(removing)}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
        </>
      }>
      <DataTable
        data={data as ClosedDayRow[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        search={
          <VStack gap={3} hAlign="stretch">
            <Text type="body" color="secondary">
              A day closed everywhere is like a weekend on every calendar: nothing new can be booked, blocked or arranged
              on it, and anything already there stays, flagged, and whoever it belongs to is told. A day closed at one
              site closes its rooms, not anyone&apos;s calendar. Bank holidays are England and Wales&apos;, closed
              everywhere; add a one-off extra one (a coronation, say) by hand.
            </Text>
            {outcome && (
              <AlertMessage
                key={outcomeKey}
                status="success"
                message={outcome.message}
                details={outcome.details}
                onDismiss={() => setOutcome(null)}
              />
            )}
          </VStack>
        }
        emptyState={
          <EmptyState
            title={`No closed days in ${year}`}
            description="Add the year's bank holidays, or a day the clinic is closed."
            icon={<Icon icon={AppIcon.ClosedDays} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}
