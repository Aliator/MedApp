import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {fieldStatus} from '../../api/errors';
import {
  ACTIVITY_COLOURS,
  activityTypesQuery,
  useCalendarSetupActions,
  useSaveActivityType,
  type ActivityColour,
  type ActivityType,
} from '../../api/calendar';
import {ActivityToken} from '../../components/calendar/CalendarTokens';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';

type ActivityRow = ActivityType & Record<string, unknown>;

type DialogState = {kind: 'edit'; activity: ActivityType | null} | {kind: 'archive' | 'delete'; activity: ActivityType} | null;

interface ActivityForm {
  name: string;
  colour: ActivityColour;
}

const FIELDS = ['name', 'colour'];

/** The colour choices' layout: rows that wrap, a little apart. */
const COLOUR_CHOICES = {flexWrap: 'wrap', rowGap: 'var(--spacing-2)', columnGap: 'var(--spacing-4)'} as const;

/** A palette colour's name, as people say it: "Green", "Grey". */
export function colourName(colour: string | null | undefined): string {
  if (colour === 'gray') return 'Grey';
  return colour ? colour.charAt(0).toUpperCase() + colour.slice(1) : '';
}

/** A palette colour, or the first one for anything else. */
const asColour = (colour: string | null | undefined): ActivityColour =>
  ACTIVITY_COLOURS.find((c) => c === colour) ?? ACTIVITY_COLOURS[0];

/** A new activity type's colour: the first one no active type has yet, so each stands out on a calendar. */
export function freeColour(activities: readonly ActivityType[]): ActivityColour {
  const used = new Set(activities.filter((a) => !a.isArchived).map((a) => a.colour));
  return ACTIVITY_COLOURS.find((c) => !used.has(c)) ?? ACTIVITY_COLOURS[0];
}

export interface ActivityTypesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/**
 * Activity types (admin, decisions 4 and 31): what staff block time on their calendars for (Lunch, Training, ...),
 * each in a colour from a fixed palette of theme colours. Admins add and edit them, archive ones no longer wanted
 * (kept by the blocks and templates that have them, but not offered for new ones), restore them, and delete ones
 * nothing has used.
 */
export function ActivityTypesPage({search, onSearchChange}: ActivityTypesPageProps) {
  const {data = [], isPending, isError} = useQuery(activityTypesQuery);
  const save = useSaveActivityType();
  const actions = useCalendarSetupActions('activity-types');
  const announce = useAnnounce();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [form, setForm] = useState<ActivityForm>({name: '', colour: ACTIVITY_COLOURS[0]});
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const close = () => setDialog(null);

  const openEditor = (activity: ActivityType | null) => {
    setForm(activity ? {name: activity.name ?? '', colour: asColour(activity.colour)} : {name: '', colour: freeColour(data)});
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', activity});
  };
  const open = (kind: 'archive' | 'delete', activity: ActivityType) => {
    setError(null);
    setDialog({kind, activity});
  };
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(saveProblem(e, action, 'activity type').message);
    close();
  };

  const editing = dialog?.kind === 'edit' ? dialog.activity : null;
  const target = dialog && dialog.kind !== 'edit' ? dialog.activity : null;

  const saveActivity = () =>
    save.mutate(
      {id: editing?.id, name: form.name.trim(), colour: form.colour},
      {
        onSuccess: () => {
          announce(editing ? 'Activity type saved' : 'Activity type added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editing ? 'save' : 'add', 'activity type')),
      },
    );

  const columns: DataTableColumn<ActivityRow>[] = [
    {
      key: 'name',
      header: 'Activity',
      width: proportional(3, {minWidth: 140}),
      renderCell: (a) => <ActivityToken name={a.name ?? ''} colour={a.colour} />,
    },
    {key: 'colour', header: 'Colour', width: pixel(110), renderCell: (a) => <Text type="body">{colourName(a.colour)}</Text>},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (a) => <SetupStatusToken isArchived={a.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (a) => (
        <TableActions
          actions={[
            {label: 'Edit activity type', icon: AppIcon.Edit, onClick: () => openEditor(a)},
            a.isArchived
              ? {
                  label: 'Restore activity type',
                  icon: AppIcon.Restore,
                  onClick: () => actions.restore.mutate(a.id ?? '', {onError: failed('restore')}),
                }
              : {label: 'Archive activity type', icon: AppIcon.Archive, onClick: () => open('archive', a)},
            {
              label: a.canBeDeleted ? 'Delete activity type' : 'Used on calendars or templates, so it can be archived but not deleted',
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !a.canBeDeleted,
              onClick: () => open('delete', a),
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading activity types...' : setupSubtitle(total, search, 'activity type', 'activity types');

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Activity">
        <ActivityToken name={target.name ?? ''} colour={target.colour} />
      </MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Activity Types"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage message={error} />
        ) : (
          isError && <AlertMessage message="The activity types could not be loaded. Please try again." />
        )
      }
      actions={<Button label="Add Activity Type" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Activity Type' : 'Add Activity Type'}
            message={
              editing
                ? 'Calendars and templates show the change everywhere it is used, past days included.'
                : 'Something staff block time on their calendars for, such as Lunch or Training.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Activity Type'}
            isProcessing={save.isPending}
            confirmDisabled={!form.name.trim()}
            hasSaveShortcut
            onConfirm={saveActivity}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <TextInput
                label="Name"
                value={form.name}
                onChange={(name) => setForm((current) => ({...current, name}))}
                placeholder="e.g. Training"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              <RadioList
                label="Colour"
                description="How its blocks look on calendars."
                orientation="horizontal"
                // Ten colours don't fit on one line of the dialog: they wrap, in rows.
                controlStyle={COLOUR_CHOICES}
                value={form.colour}
                onChange={(colour) => setForm((current) => ({...current, colour: asColour(colour)}))}
                status={fieldStatus(problem.validation, 'colour')}>
                {ACTIVITY_COLOURS.map((colour) => (
                  <RadioListItem
                    key={colour}
                    value={colour}
                    aria-label={colourName(colour)}
                    label={<ActivityToken name={colourName(colour)} colour={colour} />}
                  />
                ))}
              </RadioList>
              <HStack gap={2} vAlign="center">
                <Text type="supporting" color="secondary">
                  On a calendar:
                </Text>
                <ActivityToken name={form.name.trim() || 'New activity'} colour={form.colour} />
              </HStack>
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Activity Type"
            message="It won't be offered for new blocks or templates. Blocks and templates that have it keep it, and it can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() => target && actions.archive.mutate(target.id ?? '', {onSuccess: close, onError: failed('archive')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="No calendar or template has used this activity type. This action cannot be undone."
            isDanger
            confirmText="Delete Activity Type"
            isProcessing={actions.remove.isPending}
            onConfirm={() => target && actions.remove.mutate(target.id ?? '', {onSuccess: close, onError: failed('delete')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No activity types yet"
          description="Staff block time on their calendars for activities, such as Lunch or Training."
          icon={<Icon icon={AppIcon.ActivityTypes} size="lg" />}
          actions={<Button label="Add Your First Activity Type" variant="primary" onClick={() => openEditor(null)} />}
        />
      ) : (
        <DataTable
          data={rows as ActivityRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          search={
            <ListFilterBar
              fields={SETUP_FILTER_FIELDS}
              values={setupBarValues(search)}
              onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
              list="activityTypes"
              isUnfiltered={!search.name && search.status === undefined}
              label="Search activity types"
              placeholder="Search activity types by name, or filter by status..."
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
              title="No activity types match your search"
              description="Search for archived ones too, to see and restore them."
              isCompact
              actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />}
            />
          }
        />
      )}
    </Page>
  );
}
