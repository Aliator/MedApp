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
  staffWeekTemplateQuery,
  useCalendarActions,
  useCalendarSetupActions,
  weekTemplatesQuery,
  type DayTemplate,
  type WeekTemplate,
} from '../../api/calendar';
import {ActivityToken} from '../../components/calendar/CalendarTokens';
import {StaffPicker} from '../../components/calendar/StaffPicker';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions, type TableAction} from '../../components/tables/TableActions';
import {TextCell} from '../../components/tables/TextCell';
import {PanelCard} from '../../components/views/PanelCard';
import {formatDate} from '../../utils/dates';
import type {SetupListSearch} from '../listSearch';
import {saveProblem, type SaveSubject} from '../saveProblem';
import {blockLabel, count, weekSummary} from './calendarSetup';
import {useTemplatesOwner, type TemplatesOwner} from './calendarTemplatesOwner';
import {calendarTemplateEditorHref, type CalendarTemplatesSearch, type CalendarTemplatesTab} from './calendarTemplatesSearch';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';
import {NO_FIELD_STATUS_LINE} from '../../components/forms/fieldSpace';

export interface CalendarTemplatesPageProps {
  search: CalendarTemplatesSearch;
  onSearchChange: (search: CalendarTemplatesSearch) => void;
}

const TABS: {value: CalendarTemplatesTab; label: string}[] = [
  {value: 'day', label: 'Day Templates'},
  {value: 'week', label: 'Week Templates'},
  {value: 'default', label: 'Default Week'},
];

const panelId = (tab: CalendarTemplatesTab) => `calendar-templates-${tab}`;

/** The queries a tab of `ownerId`'s templates reads, for the route's loader to preload. */
export function calendarTemplatesPageQueries(tab: CalendarTemplatesTab, ownerId: string) {
  if (tab === 'week') return [weekTemplatesQuery(ownerId)];
  if (tab === 'default') return [staffWeekTemplateQuery(ownerId), weekTemplatesQuery(ownerId)];
  return [dayTemplatesQuery(ownerId)];
}

/**
 * A person's calendar templates (each person's own), yours unless the URL's `staff` says whose, in three tabs kept in
 * the URL: **Day templates** (named sets of blocks, such as a standard day with lunch), **Week templates** (one of
 * their day templates for each weekday) and their **Default week** (one of their week templates, which fills their
 * calendar). Templates are applied when a calendar is read, so a change shows from today on every day it applies to;
 * past days keep what they had. Templates in use are archived, not deleted.
 *
 * Their owner and admins change them; admins pick whose with the staff picker. Anyone else can open a link to
 * someone's templates and read them, without the actions.
 */
export function CalendarTemplatesPage({search, onSearchChange}: CalendarTemplatesPageProps) {
  const tab: CalendarTemplatesTab = search.tab ?? 'day';
  const owner = useTemplatesOwner(search.staff);
  const [error, setError] = useState<string | null>(null);
  // Each tab keeps its own search; switching starts the other on its default, for the same person.
  const listSearch: SetupListSearch = {name: search.name, status: search.status, page: search.page};
  const onListSearchChange = (next: SetupListSearch) => onSearchChange({...next, tab: search.tab, staff: owner.staffParam});
  const list = {owner, search: listSearch, onSearchChange: onListSearchChange, onError: setError};
  const ownerFailed = owner.ownerId === undefined && owner.staff.isError;
  const subtitle = useTabSubtitle(tab, listSearch, owner);

  return (
    <Page
      title={owner.title}
      subtitle={subtitle}
      fill
      message={
        ownerFailed ? (
          <AlertMessage message="The staff list could not be loaded, so the calendar templates can't be found. Please try again." />
        ) : (
          error && <AlertMessage message={error} />
        )
      }
      actions={
        // The picker never shows an error, so it drops the line kept for one, and lines up with New Day Template.
        <HStack gap={2} vAlign="center" wrap="wrap" style={NO_FIELD_STATUS_LINE}>
          {owner.isAdmin && (
            <StaffPicker
              label="Whose calendar templates"
              staff={owner.staff.data}
              myId={owner.myId}
              ownLabel="My templates"
              value={owner.ownerId ?? ''}
              onChange={(staff) => {
                setError(null);
                onSearchChange({tab: search.tab, staff});
              }}
              isLoading={owner.staff.isLoading}
              width={200}
            />
          )}
          {owner.canEdit && tab !== 'default' && (
            <Button
              label={tab === 'day' ? 'New Day Template' : 'New Week Template'}
              variant="primary"
              icon={<Icon icon={AppIcon.Add} />}
              href={calendarTemplateEditorHref(tab, 'new', owner.staffParam)}
            />
          )}
        </HStack>
      }>
      <VStack gap={4} height="100%">
        <TabList
          value={tab}
          onChange={(next) => {
            setError(null);
            onSearchChange({tab: next === 'day' ? undefined : (next as CalendarTemplatesSearch['tab']), staff: owner.staffParam});
          }}
          role="tablist"
          hasDivider>
          {TABS.map(({value, label}) => (
            <Tab key={value} value={value} label={label} panelId={panelId(value)} />
          ))}
        </TabList>
        <StackItem size="fill">
          <VStack id={panelId(tab)} role="tabpanel" aria-label={TABS.find((t) => t.value === tab)?.label} height="100%">
            {tab === 'day' ? <DayTemplatesList {...list} /> : tab === 'week' ? <WeekTemplatesList {...list} /> : <DefaultWeek owner={owner} onError={setError} />}
          </VStack>
        </StackItem>
      </VStack>
    </Page>
  );
}

/** One of the owner's queries, waiting until we know who they are (and, with `isWanted` false, not wanted yet). */
function ofOwner<T extends object>(query: (ownerId: string) => T, owner: TemplatesOwner, isWanted = true) {
  return {...query(owner.ownerId ?? ''), enabled: owner.ownerId !== undefined && isWanted};
}

/** What the page's subtitle says the tab shows, e.g. "3 active day templates", "Default week: Full time". */
function useTabSubtitle(tab: CalendarTemplatesTab, search: SetupListSearch, owner: TemplatesOwner): string {
  // The same queries the tabs read, so they share what's loaded.
  const day = useQuery(ofOwner(dayTemplatesQuery, owner, tab === 'day'));
  const week = useQuery(ofOwner(weekTemplatesQuery, owner, tab === 'week'));
  const current = useQuery(ofOwner(staffWeekTemplateQuery, owner, tab === 'default'));
  if (tab === 'day') return day.data ? setupSubtitle(setupPage(day.data, search).total, search, 'day template', 'day templates') : 'Loading day templates...';
  if (tab === 'week')
    return week.data ? setupSubtitle(setupPage(week.data, search).total, search, 'week template', 'week templates') : 'Loading week templates...';
  return current.data ? `Default week: ${current.data.weekTemplateName ?? 'None'}` : 'Loading the default week...';
}

interface ListProps {
  owner: TemplatesOwner;
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

type Template = {id?: string; isArchived?: boolean; canEdit?: boolean; canBeDeleted?: boolean};

/**
 * A template's row actions: Edit, Archive or Restore, and Delete (or why it can't be deleted, `inUse`), for those who
 * can change it (its `canEdit`); only View for anyone else.
 */
function templateActions<T extends Template>(
  t: T,
  kind: 'day' | 'week',
  owner: TemplatesOwner,
  {actions, failed, open}: ReturnType<typeof useTemplateTargets<T>>,
  inUse: string,
): TableAction[] {
  const noun = `${kind} template`;
  const href = calendarTemplateEditorHref(kind, t.id ?? '', owner.staffParam);
  if (!t.canEdit) return [{label: `View ${noun}`, icon: AppIcon.View, href}];
  return [
    {label: `Edit ${noun}`, icon: AppIcon.Edit, href},
    t.isArchived
      ? {label: `Restore ${noun}`, icon: AppIcon.Restore, onClick: () => actions.restore.mutate(t.id ?? '', {onError: failed('restore')})}
      : {label: `Archive ${noun}`, icon: AppIcon.Archive, onClick: () => open('archive', t)},
    {
      label: t.canBeDeleted ? `Delete ${noun}` : inUse,
      icon: AppIcon.Delete,
      isDestructive: true,
      isDisabled: !t.canBeDeleted,
      onClick: () => open('delete', t),
    },
  ];
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

/** A list with nothing in it yet: what the templates are for, and a button to make one for those who can. */
function NoTemplates({owner, kind, description}: {owner: TemplatesOwner; kind: 'day' | 'week'; description: string}) {
  const Kind = kind === 'day' ? 'Day' : 'Week';
  return (
    <EmptyState
      title={`No ${kind} templates yet`}
      description={description}
      icon={<Icon icon={AppIcon.CalendarTemplates} size="lg" />}
      actions={
        owner.canEdit ? (
          <Button
            label={owner.isOwn ? `Create Your First ${Kind} Template` : `Create a ${Kind} Template`}
            variant="primary"
            href={calendarTemplateEditorHref(kind, 'new', owner.staffParam)}
          />
        ) : undefined
      }
    />
  );
}

type DayRow = DayTemplate & Record<string, unknown>;

/** Day templates: each with its blocks as activity tokens with their times, and how many week templates use it. */
function DayTemplatesList({owner, search, onSearchChange, onError}: ListProps) {
  const {data = [], isPending, isError} = useQuery(ofOwner(dayTemplatesQuery, owner));
  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const targets = useTemplateTargets<DayTemplate>('day-templates', 'day template', onError);

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
        <TableActions actions={templateActions(t, 'day', owner, targets, 'Used by a week template or a day, so it can be archived but not deleted')} />
      ),
    },
  ];

  if (isError) return <AlertMessage message="The day templates could not be loaded. Please try again." />;
  if (!isPending && data.length === 0)
    return (
      <NoTemplates owner={owner} kind="day" description="A day template is a day's usual blocks, such as lunch at 12:00. Week templates are made from them." />
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
        target={targets.target}
        onClose={targets.close}
        subject="day template"
        archiveMessage="It can't be chosen for week templates or days any more. Where it's already used, it keeps applying, and it can be restored."
        deleteMessage="No week template or day has used it. This action cannot be undone."
        summary={
          targets.target && (
            <MetadataList>
              <MetadataListItem label="Day template">{targets.target.item.name}</MetadataListItem>
              <MetadataListItem label="Blocks">{count(targets.target.item.blocks?.length ?? 0, 'block', 'blocks')}</MetadataListItem>
            </MetadataList>
          )
        }
        actions={targets.actions}
        onFailed={targets.failed}
      />
    </>
  );
}

type WeekRow = WeekTemplate & Record<string, unknown>;

/** Week templates: each with its day templates, Monday to Friday, and whether it's the default week now. */
function WeekTemplatesList({owner, search, onSearchChange, onError}: ListProps) {
  const {data = [], isPending, isError} = useQuery(ofOwner(weekTemplatesQuery, owner));
  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const targets = useTemplateTargets<WeekTemplate>('week-templates', 'week template', onError);

  const columns: DataTableColumn<WeekRow>[] = [
    {
      key: 'name',
      header: 'Week template',
      width: proportional(3, {minWidth: 160}),
      renderCell: (t) => <SetupNameCell name={t.name ?? ''} secondary={weekSummary(t)} />,
    },
    {key: 'default', header: 'Default week', width: pixel(130), renderCell: (t) => <TextCell text={t.staffCount ? 'Yes' : 'No'} />},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (t) => <SetupStatusToken isArchived={t.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (t) => (
        <TableActions actions={templateActions(t, 'week', owner, targets, 'It has been the default week, so it can be archived but not deleted')} />
      ),
    },
  ];

  if (isError) return <AlertMessage message="The week templates could not be loaded. Please try again." />;
  if (!isPending && data.length === 0)
    return (
      <NoTemplates
        owner={owner}
        kind="week"
        description="A week template gives each weekday a day template. One can be the default week, which fills the calendar."
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
        target={targets.target}
        onClose={targets.close}
        subject="week template"
        archiveMessage="It can't be chosen as the default week any more. If it's the default week now, it stays so until that's changed, and it can be restored."
        deleteMessage="It has never been the default week. This action cannot be undone."
        summary={
          targets.target && (
            <MetadataList>
              <MetadataListItem label="Week template">{targets.target.item.name}</MetadataListItem>
              <MetadataListItem label="Days">{weekSummary(targets.target.item)}</MetadataListItem>
            </MetadataList>
          )
        }
        actions={targets.actions}
        onFailed={targets.failed}
      />
    </>
  );
}

const NO_WEEK = 'none';

/**
 * The person's default week: one of their own week templates (or none), which fills their calendar from today
 * (decision 10). An archived one can't be chosen, but if it's the default now it stays until it's changed.
 */
function DefaultWeek({owner, onError}: {owner: TemplatesOwner; onError: (message: string | null) => void}) {
  const current = useQuery(ofOwner(staffWeekTemplateQuery, owner));
  const weeks = useQuery(ofOwner(weekTemplatesQuery, owner));
  const {setWeekTemplate} = useCalendarActions();
  const announce = useAnnounce();

  if (current.isError || weeks.isError) return <AlertMessage message="The default week could not be loaded. Please try again." />;

  const currentId = current.data?.weekTemplateId ?? null;
  const isLoading = current.isPending || weeks.isPending;
  const options = [
    {value: NO_WEEK, label: 'No default week'},
    ...(weeks.data ?? [])
      .filter((w) => !w.isArchived || w.id === currentId)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
      .map((w) => ({value: w.id ?? '', label: w.isArchived ? `${w.name} (archived)` : (w.name ?? '')})),
  ];
  const whose = owner.isOwn ? 'your' : owner.ownerName ? `${owner.ownerName}'s` : 'their';

  const choose = (value: string) => {
    const weekTemplateId = value === NO_WEEK ? null : value;
    if (weekTemplateId === currentId) return;
    onError(null);
    setWeekTemplate.mutate(
      {userId: owner.ownerId ?? '', weekTemplateId},
      {
        onSuccess: () => announce(weekTemplateId ? 'Default week saved' : 'Default week removed'),
        onError: (e) => onError(saveProblem(e, 'save', 'week template').message),
      },
    );
  };

  return (
    <PanelCard title="Default Week">
      <VStack gap={2}>
        <Selector
          label="Default week"
          description={`One of ${whose} week templates. It fills ${whose} calendar from today; past days keep what they had, and one day can use another day template on the Calendar.`}
          options={options}
          value={currentId ?? NO_WEEK}
          onChange={choose}
          isLoading={isLoading}
          isDisabled={!owner.canEdit || isLoading || setWeekTemplate.isPending}
          containerStyle={{maxWidth: 480}}
        />
        {currentId && current.data?.since && (
          <Text type="supporting" color="secondary">{`The default week since ${formatDate(current.data.since)}.`}</Text>
        )}
        {!owner.canEdit && <Text type="supporting" color="secondary">{`Only ${owner.ownerName || 'its owner'} or an admin can change it.`}</Text>}
      </VStack>
    </PanelCard>
  );
}
