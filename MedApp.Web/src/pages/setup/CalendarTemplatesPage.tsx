import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Selector} from '@astryxdesign/core/Selector';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Tab, TabList} from '@astryxdesign/core/TabList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {
  dayTemplatesQuery,
  staffQuery,
  useCalendarActions,
  useCalendarSetupActions,
  weekTemplatesQuery,
  type DayTemplate,
  type StaffMember,
  type WeekTemplate,
} from '../../api/calendar';
import {ActivityToken} from '../../components/calendar/CalendarTokens';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/procedures/SetupCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import {TextCell} from '../../components/tables/TextCell';
import type {CalendarTemplatesSearch, CalendarTemplatesTab, SetupListSearch} from '../listSearch';
import {saveProblem, type SaveSubject} from '../saveProblem';
import {blockLabel, count, weekSummary} from './calendarSetup';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';

export interface CalendarTemplatesPageProps {
  search: CalendarTemplatesSearch;
  onSearchChange: (search: CalendarTemplatesSearch) => void;
}

const TABS: {value: CalendarTemplatesTab; label: string}[] = [
  {value: 'day', label: 'Day Templates'},
  {value: 'week', label: 'Week Templates'},
  {value: 'staff', label: 'Staff Defaults'},
];

const panelId = (tab: CalendarTemplatesTab) => `calendar-templates-${tab}`;

/** Where the editors go back to: the list of the kind they edit. */
export const calendarTemplatesHref = (tab: CalendarTemplatesTab) =>
  tab === 'day' ? '/admin/calendar-templates' : `/admin/calendar-templates?tab=${tab}`;

/**
 * Calendar templates (admin, decisions 9 to 14 and 31), in three tabs kept in the URL: **Day templates** (named sets
 * of blocks, such as a standard day with lunch), **Week templates** (a day template for each weekday) and **Staff
 * defaults** (each person's default week, which fills their calendar). Templates are applied when a calendar is
 * read, so a change shows from today on every day it applies to; past days keep what they had. Templates in use are
 * archived, not deleted.
 */
export function CalendarTemplatesPage({search, onSearchChange}: CalendarTemplatesPageProps) {
  const tab: CalendarTemplatesTab = search.tab ?? 'day';
  const [error, setError] = useState<string | null>(null);
  // Each tab keeps its own search; switching starts the other on its default.
  const listSearch: SetupListSearch = {name: search.name, status: search.status, page: search.page};
  const onListSearchChange = (next: SetupListSearch) => onSearchChange({...next, tab: search.tab});
  const list = {search: listSearch, onSearchChange: onListSearchChange, onError: setError};

  return (
    <Page
      title="Calendar Templates"
      subtitle={useTabSubtitle(tab, listSearch)}
      fill
      message={error && <AlertMessage message={error} />}
      actions={
        tab === 'day' ? (
          <Button label="New Day Template" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/admin/calendar-templates/day/new" />
        ) : tab === 'week' ? (
          <Button label="New Week Template" variant="primary" icon={<Icon icon={AppIcon.Add} />} href="/admin/calendar-templates/week/new" />
        ) : undefined
      }>
      <VStack gap={4} height="100%">
        <TabList
          value={tab}
          onChange={(next) => {
            setError(null);
            onSearchChange(next === 'day' ? {} : {tab: next as CalendarTemplatesSearch['tab']});
          }}
          role="tablist"
          hasDivider>
          {TABS.map(({value, label}) => (
            <Tab key={value} value={value} label={label} panelId={panelId(value)} />
          ))}
        </TabList>
        <StackItem size="fill">
          <VStack id={panelId(tab)} role="tabpanel" aria-label={TABS.find((t) => t.value === tab)?.label} height="100%">
            {tab === 'day' ? <DayTemplatesList {...list} /> : tab === 'week' ? <WeekTemplatesList {...list} /> : <StaffDefaultsList {...list} />}
          </VStack>
        </StackItem>
      </VStack>
    </Page>
  );
}

/** What the page's subtitle says the tab's list shows, e.g. "3 active day templates", "12 people". */
function useTabSubtitle(tab: CalendarTemplatesTab, search: SetupListSearch): string {
  // The same queries the lists read, so they share what's loaded.
  const day = useQuery({...dayTemplatesQuery, enabled: tab === 'day'});
  const week = useQuery({...weekTemplatesQuery, enabled: tab === 'week'});
  const staff = useQuery({...staffQuery, enabled: tab === 'staff'});
  if (tab === 'day') return day.data ? setupSubtitle(setupPage(day.data, search).total, search, 'day template', 'day templates') : 'Loading day templates...';
  if (tab === 'week')
    return week.data ? setupSubtitle(setupPage(week.data, search).total, search, 'week template', 'week templates') : 'Loading week templates...';
  if (!staff.data) return 'Loading staff...';
  const total = setupPage(staffRows(staff.data), {...search, status: 'any'}).total;
  return `${total} ${search.name ? 'matching ' : ''}${total === 1 ? 'person' : 'people'}`;
}

interface ListProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
  /** Says why an action failed, in the page's header (null clears it). */
  onError: (message: string | null) => void;
}

type Target<T> = {kind: 'archive' | 'delete'; item: T} | null;

/** The archive and delete confirmations a template list asks before acting. */
function ArchiveDeleteDialogs<T extends {id?: string; name?: string | null}>({
  target,
  onClose,
  subject,
  archiveMessage,
  deleteMessage,
  summary,
  actions,
  onFailed,
}: {
  target: Target<T>;
  onClose: () => void;
  subject: SaveSubject;
  archiveMessage: string;
  deleteMessage: string;
  summary: ReactNode;
  actions: ReturnType<typeof useCalendarSetupActions>;
  onFailed: (action: 'archive' | 'delete') => (e: unknown) => void;
}) {
  const Subject = subject.charAt(0).toUpperCase() + subject.slice(1);
  const Title = Subject.replace(/ t/, ' T');
  const id = target?.item.id ?? '';
  return (
    <>
      <ConfirmDialog
        isOpen={target?.kind === 'archive'}
        title={`Archive ${Title}`}
        message={archiveMessage}
        icon={AppIcon.Archive}
        intent="warning"
        confirmText="Archive"
        isProcessing={actions.archive.isPending}
        onConfirm={() => actions.archive.mutate(id, {onSuccess: onClose, onError: onFailed('archive')})}
        onCancel={onClose}>
        {summary}
      </ConfirmDialog>
      <ConfirmDialog
        isOpen={target?.kind === 'delete'}
        title="Confirm Delete"
        message={deleteMessage}
        isDanger
        confirmText={`Delete ${Title}`}
        isProcessing={actions.remove.isPending}
        onConfirm={() => actions.remove.mutate(id, {onSuccess: onClose, onError: onFailed('delete')})}
        onCancel={onClose}>
        {summary}
      </ConfirmDialog>
    </>
  );
}

/** Archive, restore and delete for one kind of template, saying in the page's header why one failed. */
function useTemplateTargets<T>(kind: 'day-templates' | 'week-templates', subject: SaveSubject, onError: (message: string | null) => void) {
  const actions = useCalendarSetupActions(kind);
  const [target, setTarget] = useState<Target<T>>(null);
  const close = () => setTarget(null);
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    onError(saveProblem(e, action, subject).message);
    close();
  };
  const open = (next: 'archive' | 'delete', item: T) => {
    onError(null);
    setTarget({kind: next, item});
  };
  return {actions, target, close, failed, open};
}

/** A setup list's search bar and table, with the archived ones a search away. */
function SetupTable<T extends Record<string, unknown>>({
  rows,
  columns,
  isPending,
  search,
  onSearchChange,
  total,
  page,
  list,
  noun,
}: {
  rows: T[];
  columns: DataTableColumn<T>[];
  isPending: boolean;
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
  total: number;
  page: number;
  list: string;
  noun: string;
}) {
  const {pageSize} = useListSettings();
  return (
    <DataTable
      data={rows}
      columns={columns}
      idKey="id"
      isLoading={isPending}
      search={
        <ListFilterBar
          fields={SETUP_FILTER_FIELDS}
          values={setupBarValues(search)}
          onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
          list={list}
          isUnfiltered={!search.name && search.status === undefined}
          label={`Search ${noun}`}
          placeholder={`Search ${noun} by name, or filter by status...`}
          textKey="name"
          favourites={SETUP_FAVOURITES}
          namesQuickFilters={false}
        />
      }
      pagination={{
        page,
        pageSize,
        totalItems: total,
        onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
      }}
      emptyState={
        <EmptyState
          title={`No ${noun} match your search`}
          description="Search for archived ones too, to see and restore them."
          isCompact
          actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />}
        />
      }
    />
  );
}


type DayRow = DayTemplate & Record<string, unknown>;

/** Day templates: each with its blocks as activity tokens with their times, and how many week templates use it. */
function DayTemplatesList({search, onSearchChange, onError}: ListProps) {
  const {data = [], isPending, isError} = useQuery(dayTemplatesQuery);
  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const {actions, target, close, failed, open} = useTemplateTargets<DayTemplate>('day-templates', 'day template', onError);

  const usedIn = (t: DayTemplate) =>
    t.weekTemplateCount ? count(t.weekTemplateCount, 'week template', 'week templates') : 'No week templates';

  const columns: DataTableColumn<DayRow>[] = [
    {key: 'name', header: 'Day template', width: proportional(2, {minWidth: 140}), renderCell: (t) => <SetupNameCell name={t.name ?? ''} />},
    {
      key: 'blocks',
      header: 'Blocks',
      width: proportional(3, {minWidth: 160}),
      renderCell: (t) =>
        t.blocks?.length ? (
          <HStack gap={1} wrap="wrap">
            {t.blocks.map((b) => (
              <ActivityToken key={b.id} name={blockLabel(b)} colour={b.colour} />
            ))}
          </HStack>
        ) : (
          <Text type="body" color="secondary">
            No blocks
          </Text>
        ),
    },
    {key: 'used', header: 'Used in', width: pixel(150), renderCell: (t) => <TextCell text={usedIn(t)} />},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (t) => <SetupStatusToken isArchived={t.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (t) => (
        <TableActions
          actions={[
            {label: 'Edit day template', icon: AppIcon.Edit, href: `/admin/calendar-templates/day/${t.id}`},
            t.isArchived
              ? {label: 'Restore day template', icon: AppIcon.Restore, onClick: () => actions.restore.mutate(t.id ?? '', {onError: failed('restore')})}
              : {label: 'Archive day template', icon: AppIcon.Archive, onClick: () => open('archive', t)},
            {
              label: t.canBeDeleted ? 'Delete day template' : 'Used by a week template or a day, so it can be archived but not deleted',
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !t.canBeDeleted,
              onClick: () => open('delete', t),
            },
          ]}
        />
      ),
    },
  ];

  if (isError) return <AlertMessage message="The day templates could not be loaded. Please try again." />;
  if (!isPending && data.length === 0)
    return (
      <EmptyState
        title="No day templates yet"
        description="A day template is a day's usual blocks, such as lunch at 12:00. Week templates are made from them."
        icon={<Icon icon={AppIcon.CalendarTemplates} size="lg" />}
        actions={<Button label="Create Your First Day Template" variant="primary" href="/admin/calendar-templates/day/new" />}
      />
    );

  return (
    <>
      <SetupTable
        rows={rows as DayRow[]}
        columns={columns}
        isPending={isPending}
        search={search}
        onSearchChange={onSearchChange}
        total={total}
        page={page}
        list="dayTemplates"
        noun="day templates"
      />
      <ArchiveDeleteDialogs
        target={target}
        onClose={close}
        subject="day template"
        archiveMessage="It can't be chosen for week templates or days any more. Where it's already used, it keeps applying, and it can be restored."
        deleteMessage="No week template or day has used it. This action cannot be undone."
        summary={
          target && (
            <MetadataList>
              <MetadataListItem label="Day template">{target.item.name}</MetadataListItem>
              <MetadataListItem label="Blocks">{count(target.item.blocks?.length ?? 0, 'block', 'blocks')}</MetadataListItem>
            </MetadataList>
          )
        }
        actions={actions}
        onFailed={failed}
      />
    </>
  );
}

type WeekRow = WeekTemplate & Record<string, unknown>;

/** Week templates: each with its day templates, Monday to Friday, and how many staff have it as their default. */
function WeekTemplatesList({search, onSearchChange, onError}: ListProps) {
  const {data = [], isPending, isError} = useQuery(weekTemplatesQuery);
  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const {actions, target, close, failed, open} = useTemplateTargets<WeekTemplate>('week-templates', 'week template', onError);

  const usedBy = (t: WeekTemplate) => (t.staffCount ? `Default for ${count(t.staffCount, 'person', 'people')}` : 'No one');

  const columns: DataTableColumn<WeekRow>[] = [
    {
      key: 'name',
      header: 'Week template',
      width: proportional(3, {minWidth: 160}),
      renderCell: (t) => <SetupNameCell name={t.name ?? ''} secondary={weekSummary(t)} />,
    },
    {key: 'used', header: 'Used by', width: pixel(160), renderCell: (t) => <TextCell text={usedBy(t)} />},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (t) => <SetupStatusToken isArchived={t.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (t) => (
        <TableActions
          actions={[
            {label: 'Edit week template', icon: AppIcon.Edit, href: `/admin/calendar-templates/week/${t.id}`},
            t.isArchived
              ? {label: 'Restore week template', icon: AppIcon.Restore, onClick: () => actions.restore.mutate(t.id ?? '', {onError: failed('restore')})}
              : {label: 'Archive week template', icon: AppIcon.Archive, onClick: () => open('archive', t)},
            {
              label: t.canBeDeleted ? 'Delete week template' : "Someone has had it as their default, so it can be archived but not deleted",
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !t.canBeDeleted,
              onClick: () => open('delete', t),
            },
          ]}
        />
      ),
    },
  ];

  if (isError) return <AlertMessage message="The week templates could not be loaded. Please try again." />;
  if (!isPending && data.length === 0)
    return (
      <EmptyState
        title="No week templates yet"
        description="A week template gives each weekday a day template. Staff have one as their default week."
        icon={<Icon icon={AppIcon.CalendarTemplates} size="lg" />}
        actions={<Button label="Create Your First Week Template" variant="primary" href="/admin/calendar-templates/week/new" />}
      />
    );

  return (
    <>
      <SetupTable
        rows={rows as WeekRow[]}
        columns={columns}
        isPending={isPending}
        search={search}
        onSearchChange={onSearchChange}
        total={total}
        page={page}
        list="weekTemplates"
        noun="week templates"
      />
      <ArchiveDeleteDialogs
        target={target}
        onClose={close}
        subject="week template"
        archiveMessage="It can't be chosen as anyone's default any more. Staff who have it keep it until it's changed, and it can be restored."
        deleteMessage="No one has had it as their default. This action cannot be undone."
        summary={
          target && (
            <MetadataList>
              <MetadataListItem label="Week template">{target.item.name}</MetadataListItem>
              <MetadataListItem label="Days">{weekSummary(target.item)}</MetadataListItem>
            </MetadataList>
          )
        }
        actions={actions}
        onFailed={failed}
      />
    </>
  );
}

type StaffRow = StaffMember & {name: string} & Record<string, unknown>;

/** Staff as a setup list's rows: named by their username. */
const staffRows = (staff: readonly StaffMember[]) => staff.map((s) => ({...s, name: s.username ?? ''}));

const NO_WEEK = 'none';

/**
 * Each member of staff and their default week template, changed in place: it fills their calendar from today
 * (decision 10). Archived templates can't be chosen, but someone who has one keeps it until it's changed.
 */
function StaffDefaultsList({search, onSearchChange, onError}: ListProps) {
  const staff = useQuery(staffQuery);
  const weeks = useQuery(weekTemplatesQuery);
  const {setWeekTemplate} = useCalendarActions();
  const announce = useAnnounce();
  const {pageSize} = useListSettings();
  const [savingFor, setSavingFor] = useState<string | null>(null);
  const {rows, page, total} = setupPage(staffRows(staff.data ?? []), {...search, status: 'any'}, pageSize);
  const isPending = staff.isPending || weeks.isPending;

  const choose = (person: StaffMember, value: string) => {
    const weekTemplateId = value === NO_WEEK ? null : value;
    if (weekTemplateId === (person.weekTemplateId ?? null)) return;
    onError(null);
    setSavingFor(person.userId ?? null);
    setWeekTemplate.mutate(
      {userId: person.userId ?? '', weekTemplateId},
      {
        onSuccess: () => announce(`Default week saved for ${person.username}`),
        onError: (e) => onError(saveProblem(e, 'save', 'week template').message),
        onSettled: () => setSavingFor(null),
      },
    );
  };

  /** The active week templates, and the person's own if it's archived (so it still shows as theirs). */
  const optionsFor = (person: StaffMember) => [
    {value: NO_WEEK, label: 'No default week'},
    ...(weeks.data ?? [])
      .filter((w) => !w.isArchived || w.id === person.weekTemplateId)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
      .map((w) => ({value: w.id ?? '', label: w.isArchived ? `${w.name} (archived)` : (w.name ?? '')})),
  ];

  const columns: DataTableColumn<StaffRow>[] = [
    {key: 'name', header: 'Member of staff', width: proportional(2, {minWidth: 140}), renderCell: (s) => <SetupNameCell name={s.name} />},
    {
      key: 'week',
      header: 'Default week',
      width: proportional(3, {minWidth: 200}),
      renderCell: (s) => (
        <Selector
          label={`Default week for ${s.name}`}
          isLabelHidden
          size="sm"
          options={optionsFor(s)}
          value={s.weekTemplateId ?? NO_WEEK}
          isDisabled={savingFor === s.userId}
          onChange={(value) => choose(s, value)}
          containerStyle={{width: '100%'}}
        />
      ),
    },
  ];

  if (staff.isError || weeks.isError) return <AlertMessage message="The staff and their default weeks could not be loaded. Please try again." />;

  return (
    <DataTable
      data={rows as StaffRow[]}
      columns={columns}
      idKey="userId"
      isLoading={isPending}
      search={
        <ListFilterBar
          fields={[{kind: 'text', key: 'name', label: 'Username'}]}
          values={{name: search.name}}
          onChange={(values) => onSearchChange({name: typeof values.name === 'string' ? values.name : undefined})}
          list="staffWeekTemplates"
          label="Search staff"
          placeholder="Search staff by username..."
          textKey="name"
        />
      }
      pagination={{
        page,
        pageSize,
        totalItems: total,
        onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
      }}
      emptyState={<EmptyState title="No staff match your search" description="Search for another username." isCompact />}
    />
  );
}
