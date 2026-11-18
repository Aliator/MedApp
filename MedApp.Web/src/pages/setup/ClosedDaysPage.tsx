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
import {closedDaysOfYearQuery, useAddBankHolidays, useAddClosedDay, useDeleteClosedDay, type AddedBankHolidays, type ClosedDay} from '../../api/closedDays';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
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

/** What adding a year's bank holidays did, in words: "Added 8 bank holidays.", and each one left out and why. */
export function bankHolidaysOutcome(result: AddedBankHolidays): {message: string; details: string[]} {
  const added = result.added?.length ?? 0;
  const skipped = result.skipped ?? [];
  const message =
    added === 0
      ? `No bank holidays added for ${result.year}.`
      : `Added ${added === 1 ? '1 bank holiday' : `${added} bank holidays`}.`;
  const details = skipped.map(
    (s) => `${s.name ?? ''}, ${formatShortDayDate(s.date ?? '', {withYear: false})}: ${SKIPPED_REASONS[s.reason ?? ''] ?? (s.reason ?? '').toLowerCase()}`,
  );
  return {message, details};
}

type ClosedDayRow = ClosedDay & Record<string, unknown>;

/** The fields Add Closed Day's 400 can name. */
const FIELDS = ['date', 'name'];

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
 * Closed days (admin, decisions 61 to 69): the days the whole clinic is closed, such as bank holidays and staff
 * training days, one year at a time (the year in the address). A closed day is like a weekend on every calendar:
 * nothing new can be booked, blocked or arranged on it, and anything already there stays, flagged. Admins add one by
 * hand (a date from today on, and a name), add a year's England and Wales bank holidays in one go (saying which were
 * left out, and why), and remove one still to come. Past days can't change.
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
  const [name, setName] = useState('');
  const [problem, setProblem] = useState<Problem>(NO_PROBLEM);
  const [removing, setRemoving] = useState<ClosedDay | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<{message: string; details: string[]} | null>(null);

  const goToYear = (next: number) => {
    setError(null);
    setOutcome(null);
    onSearchChange(next === ukYear(today) ? {} : {year: next});
  };

  const openAdd = () => {
    setDate(undefined);
    setName('');
    setProblem(NO_PROBLEM);
    setError(null);
    setIsAdding(true);
  };

  const saveClosedDay = () => {
    if (!date || !name.trim()) return false;
    setProblem(NO_PROBLEM);
    add.mutate(
      {date, name: name.trim()},
      {
        onSuccess: (saved) => {
          announce(`${saved.name ?? name.trim()} added as a closed day`);
          setIsAdding(false);
          // Show the year it's in, so it's there to see.
          const savedYear = Number((saved.date ?? date).slice(0, 4));
          if (savedYear !== year) goToYear(savedYear);
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

  const confirmRemove = () => {
    if (!removing) return;
    remove.mutate(removing.id ?? '', {
      onSuccess: () => {
        announce(`${removing.name ?? 'Closed day'} removed`);
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
        setOutcome(next);
        announce(next.details.length > 0 ? `${next.message} ${next.details.length} left out.` : next.message);
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
              label: d.isPast ? "Past days can't change" : `Remove ${d.name ?? 'closed day'}`,
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
        ) : isError ? (
          <AlertMessage message="The closed days could not be loaded. Please try again." />
        ) : (
          outcome && <AlertMessage status="success" message={outcome.message} details={outcome.details} />
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
            message="Nobody can book, block or arrange anything new on it. Anything already there stays, flagged as being on a closed day."
            icon={AppIcon.ClosedDays}
            confirmText="Add Closed Day"
            isProcessing={add.isPending}
            confirmDisabled={!date || !name.trim()}
            hasSaveShortcut
            onConfirm={saveClosedDay}
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
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={removing !== null}
            title="Remove Closed Day"
            message="The day becomes a working day again on every calendar."
            isDanger
            confirmText="Remove"
            isProcessing={remove.isPending}
            onConfirm={confirmRemove}
            onCancel={() => setRemoving(null)}>
            {removing && (
              <MetadataList>
                <MetadataListItem label="Date">{formatShortDayDate(removing.date ?? '')}</MetadataListItem>
                <MetadataListItem label="Name">{removing.name}</MetadataListItem>
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
          <Text type="body" color="secondary">
            A closed day is like a weekend on every calendar: nothing new can be booked, blocked or arranged on it, and
            anything already there stays, flagged. Bank holidays are England and Wales&apos;; add a one-off extra one (a
            coronation, say) by hand.
          </Text>
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
