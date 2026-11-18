import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useQuery, type QueryClient} from '@tanstack/react-query';
import {useState} from 'react';
import {sessionsQuery, slotsWords, type ClinicSession} from '../../api/clinics';
import {preload} from '../../api/preload';
import {sitesQuery} from '../../api/sites';
import {useCan} from '../../auth/auth';
import {BookAppointmentDialog} from '../../components/calendar/BookAppointmentDialog';
import {ClinicSessionDialog} from '../../components/calendar/ClinicSessionDialog';
import {slotOf, type ClinicSlotChoice} from '../../components/calendar/ClinicSlotPicker';
import {SessionFormDialog} from '../../components/calendar/SessionFormDialog';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {addDays, formatCalendarDay, formatShortDay, formatTimeRange, ukToday, weekStart} from '../../utils/calendarSlots';
import type {ClinicsSearch} from './calendarSearch';

const ALL_SITES = 'all';

/** The week a search shows, Monday to Sunday: the week of its date, or this week. */
export function clinicsWeek(search: ClinicsSearch, now: Date = new Date()) {
  const from = weekStart(search.date ?? ukToday(now));
  return {from, to: addDays(from, 6)};
}

/** The query for a week's sessions, cancelled ones too (they show as cancelled). */
export const clinicsQuery = (search: ClinicsSearch, now?: Date) =>
  sessionsQuery({...clinicsWeek(search, now), siteId: search.site, includeCancelled: true});

/** What the Clinics page needs, for its route's loader. */
export const preloadClinicsPage = (queryClient: QueryClient, search: ClinicsSearch) =>
  preload(queryClient, clinicsQuery(search), sitesQuery());

type SessionRow = ClinicSession & Record<string, unknown>;

export interface ClinicsPageProps {
  search: ClinicsSearch;
  onSearchChange: (search: ClinicsSearch) => void;
}

/**
 * Clinics (`/calendar/clinics`, centre plan decisions 123 to 127): a week's clinic sessions by day, then time and room,
 * each with its clinician, team and how full it is; narrowed to one site if chosen. Opening one shows its slots,
 * and who is booked into each, where a free slot can be booked. Holders of ManageClinics add sessions (New Session),
 * change or cancel them from their dialog, and keep the clinic patterns (Clinic Patterns). The week and site are in the
 * address, so a week is a link.
 *
 * **Deprecated** (the owner, 18 November 2026): no new work, kept building and working by address, with its tests;
 * nothing links to it from the Calendar page any more (docs/agents/calendar.md).
 */
export function ClinicsPage({search, onSearchChange}: ClinicsPageProps) {
  const canManage = useCan('ManageClinics');
  const canBook = useCan('BookAppointments');
  const {from, to} = clinicsWeek(search);
  const sessions = useQuery(clinicsQuery(search));
  const sites = useQuery(sitesQuery());
  const [isAdding, setIsAdding] = useState(false);
  const [editing, setEditing] = useState<ClinicSession | null>(null);
  const [booking, setBooking] = useState<ClinicSlotChoice | null>(null);

  const go = (next: Partial<ClinicsSearch>) => onSearchChange({...search, ...next});
  const open = (id: string | undefined) => go({session: id});

  const columns: DataTableColumn<SessionRow>[] = [
    {
      key: 'when',
      header: 'When',
      width: pixel(180),
      renderCell: (s) => (
        <VStack gap={0}>
          <Text type="body" weight="semibold">
            {formatShortDay(s.date ?? '')}
          </Text>
          <Text type="supporting" color="secondary">
            {formatTimeRange(s.startUtc ?? '', s.endUtc ?? '')}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'room',
      header: 'Room',
      width: proportional(1, {minWidth: 150}),
      renderCell: (s) => (
        <VStack gap={0}>
          <Text type="body">{s.roomName}</Text>
          <Text type="supporting" color="secondary">
            {s.siteName}
          </Text>
        </VStack>
      ),
    },
    {key: 'clinician', header: 'Clinician', width: proportional(1, {minWidth: 130}), renderCell: (s) => <Text type="body">{s.staffUsername}</Text>},
    {key: 'team', header: 'Team', width: proportional(1, {minWidth: 140}), renderCell: (s) => <Text type="body">{s.teamName}</Text>},
    {
      key: 'slots',
      header: 'Slots',
      width: pixel(150),
      renderCell: (s) =>
        s.status === 'Cancelled' ? (
          <Token size="sm" label="Cancelled" color="gray" />
        ) : (
          <Text type="body">{`${s.slotMinutes} min · ${slotsWords(s)}`}</Text>
        ),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(100),
      renderCell: (s) => (
        <Button
          label={`Open the ${s.teamName} session, ${formatShortDay(s.date ?? '')} ${formatTimeRange(s.startUtc ?? '', s.endUtc ?? '')}, ${s.roomName}`}
          variant="secondary"
          size="sm"
          onClick={() => open(s.id)}>
          Open
        </Button>
      ),
    },
  ];

  const count = sessions.data?.filter((s) => s.status !== 'Cancelled').length ?? 0;
  const subtitle = `Week of ${formatCalendarDay(from)} · ${sessions.isPending ? 'Loading...' : count === 1 ? '1 session' : `${count} sessions`}`;

  return (
    <Page
      title="Clinics"
      subtitle={subtitle}
      fill
      message={sessions.isError ? <AlertMessage message="The clinic sessions could not be loaded. Please try again." /> : undefined}
      actions={
        <>
          {canManage && <Button label="Clinic Patterns" variant="secondary" href="/calendar/clinics/patterns" icon={<Icon icon={AppIcon.ClinicPatterns} />} />}
          {canManage && <Button label="New Session" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => setIsAdding(true)} />}
        </>
      }
      dialogs={
        <>
          <ClinicSessionDialog
            sessionId={search.session ?? null}
            onClose={() => open(undefined)}
            onEdit={(s) => {
              open(undefined);
              setEditing(s);
            }}
            onBookSlot={
              canBook
                ? (s, start) => {
                    open(undefined);
                    setBooking(slotOf(s, start));
                  }
                : undefined
            }
          />
          <SessionFormDialog isOpen={isAdding} date={from >= ukToday() ? from : ukToday()} onClose={() => setIsAdding(false)} />
          <SessionFormDialog isOpen={editing !== null} session={editing ?? undefined} onClose={() => setEditing(null)} />
          <BookAppointmentDialog isOpen={booking !== null} clinicSlot={booking ?? undefined} onClose={() => setBooking(null)} />
        </>
      }>
      <DataTable
        data={(sessions.data ?? []) as SessionRow[]}
        columns={columns}
        idKey="id"
        isLoading={sessions.isPending}
        skeletonRows={3}
        search={
          <HStack gap={2} vAlign="end" wrap="wrap">
            <IconButton label="Previous week" tooltip="Previous week" variant="secondary" icon={<Icon icon={AppIcon.ChevronLeft} />} onClick={() => go({date: addDays(from, -7)})} />
            <Button label="This week" variant="secondary" onClick={() => go({date: undefined})} />
            <IconButton label="Next week" tooltip="Next week" variant="secondary" icon={<Icon icon={AppIcon.ChevronRight} />} onClick={() => go({date: addDays(from, 7)})} />
            <AnchoredSelector
              label="Site"
              value={search.site ?? ALL_SITES}
              onChange={(value) => go({site: value === ALL_SITES ? undefined : value})}
              isLoading={sites.isLoading}
              options={[{value: ALL_SITES, label: 'Every site'}, ...(sites.data ?? []).map((s) => ({value: s.id ?? '', label: s.name ?? ''}))]}
            />
          </HStack>
        }
        emptyState={
          <EmptyState
            title="No clinic sessions this week"
            description={canManage ? 'Add one with New Session, or make them from a clinic pattern.' : `Nothing from ${formatCalendarDay(from)} to ${formatCalendarDay(to)}.`}
            icon={<Icon icon={AppIcon.Clinics} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}
