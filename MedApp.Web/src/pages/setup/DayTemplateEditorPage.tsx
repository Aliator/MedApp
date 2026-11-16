import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Selector} from '@astryxdesign/core/Selector';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useMemo, useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {activityTypesQuery, dayTemplateQuery, useSaveDayTemplate, type ActivityType, type DayTemplate} from '../../api/calendar';
import {ApiError, fieldStatus} from '../../api/errors';
import {ActivityToken} from '../../components/calendar/CalendarTokens';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {
  addMinutes,
  apiTime,
  blockProblem,
  byStart,
  clockTime,
  count,
  dayParts,
  quarterHours,
  WORK_END,
  WORK_START,
  type BlockRow,
} from './calendarSetup';
import {calendarTemplatesHref} from './CalendarTemplatesPage';

const BACK = calendarTemplatesHref('day');

let rowCount = 0;
const newKey = () => `block-${++rowCount}`;

/** The element holding a block's fields, so the focus can follow a block the editor moves. */
const rowId = (key: string) => `day-template-${key}`;

/**
 * A new day template (no `id`), or one to edit. This part loads it and the activity types; an unknown one goes back
 * to the list. The editor mounts once they're here, so it starts from the template.
 */
export function DayTemplateEditorPage({id}: {id?: string}) {
  const navigate = useNavigate();
  const template = useQuery({...dayTemplateQuery(id ?? ''), enabled: id !== undefined});
  const activities = useQuery(activityTypesQuery);
  const isMissing = template.error instanceof ApiError && template.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: BACK, replace: true});
  }, [isMissing, navigate]);

  const isLoading = activities.isPending || (id !== undefined && template.isPending) || isMissing;
  if (!isLoading && activities.data && (id === undefined || template.data)) {
    return <DayTemplateEditor template={template.data ?? null} activities={activities.data} />;
  }
  const failed = activities.isError || (template.isError && !isMissing);
  return (
    <Page
      title={id ? 'Edit Day Template' : 'New Day Template'}
      backHref={BACK}
      backLabel="Back to day templates"
      isLoading={isLoading}
      loadingMessage="Loading day template..."
      message={failed && <AlertMessage message="The day template could not be loaded. Please try again." />}
    />
  );
}

const TIMES = quarterHours();

/** The times a block can start or end at, with its own time too if that's off the 15-minute steps. */
const timeOptions = (times: string[], current: string) =>
  (times.includes(current) ? times : [...times, current].sort()).map((t) => ({value: t, label: t}));

/**
 * The day template editor (decision 9): its name, then its blocks (an activity, a start and an end in 15-minute
 * steps of the working day, and an optional note), kept in start order, each flagged at once if it ends before it
 * starts or overlaps another (the server's check is still the one that counts). Beside it (below, when narrow), the
 * day as it will look: its blocks and the free time between them. Saving changes asks first, saying they apply from
 * today and past days keep what they had.
 */
function DayTemplateEditor({template, activities}: {template: DayTemplate | null; activities: ActivityType[]}) {
  const navigate = useNavigate();
  const isNarrow = useIsNarrow();
  const save = useSaveDayTemplate();
  const announce = useAnnounce();
  const [name, setName] = useState(template?.name ?? '');
  const [rows, setRows] = useState<BlockRow[]>(() =>
    byStart(
      (template?.blocks ?? []).map((b) => ({
        key: newKey(),
        activityTypeId: b.activityTypeId ?? '',
        startTime: clockTime(b.startTime),
        endTime: clockTime(b.endTime),
        note: b.note ?? '',
      })),
    ),
  );
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [focusKey, setFocusKey] = useState<{key: string; field: number} | null>(null);

  const byId = useMemo(() => new Map(activities.map((a) => [a.id ?? '', a])), [activities]);
  const active = activities
    .filter((a) => !a.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
  const activityName = (id: string) => byId.get(id)?.name ?? 'an activity';
  const problems = new Map(rows.map((r) => [r.key, blockProblem(r, rows, activityName)]));
  const hasProblems = [...problems.values()].some(Boolean);
  const backToList = () => void navigate({to: BACK});
  const usedIn = template?.weekTemplateCount ?? 0;

  // Puts the focus back on a block's field after the block moved (its start changed) or was added.
  useEffect(() => {
    if (!focusKey) return;
    const fields = document.getElementById(rowId(focusKey.key))?.querySelectorAll<HTMLElement>('[role="combobox"]');
    fields?.[focusKey.field]?.focus();
  }, [focusKey]);

  const change = (key: string, values: Partial<BlockRow>) => {
    const next = rows.map((r) => (r.key === key ? {...r, ...values} : r));
    if (values.startTime === undefined) {
      setRows(next);
      return;
    }
    // A new start can move the block: keep them in order, and the focus on its start.
    setRows(byStart(next));
    setFocusKey({key, field: 1});
  };

  const add = () => {
    const last = rows[rows.length - 1];
    const startTime = last ? (last.endTime < WORK_END ? last.endTime : addMinutes(WORK_END, -60)) : '09:00';
    const key = newKey();
    setRows(byStart([...rows, {key, activityTypeId: active[0]?.id ?? '', startTime, endTime: addMinutes(startTime, 60), note: ''}]));
    setFocusKey({key, field: 0});
  };

  const remove = (row: BlockRow) => {
    setRows(rows.filter((r) => r.key !== row.key));
    announce(`${activityName(row.activityTypeId)} block removed`);
  };

  const submit = () =>
    save.mutate(
      {
        id: template?.id,
        name: name.trim(),
        blocks: byStart(rows).map((r) => ({
          activityTypeId: r.activityTypeId,
          startTime: apiTime(r.startTime),
          endTime: apiTime(r.endTime),
          note: r.note.trim() || null,
        })),
      },
      {
        onSuccess: () => {
          announce(template ? 'Day template saved' : 'Day template created');
          backToList();
        },
        onError: (e) => {
          setProblem(saveProblem(e, template ? 'save' : 'add', 'day template'));
          setIsConfirming(false);
        },
      },
    );

  const onSave = () => {
    setProblem(NO_PROBLEM);
    if (hasProblems) {
      setProblem({validation: null, message: 'Some blocks need changing first: see the messages beside them.'});
      return;
    }
    // A new one applies nowhere yet; changes to one in use apply from today, so they're confirmed.
    if (template) setIsConfirming(true);
    else submit();
  };

  const nameField = (
    <TextInput
      label="Name"
      description="What admins choose for a weekday in a week template, or for one day on a calendar."
      value={name}
      onChange={setName}
      placeholder="e.g. Standard day"
      autoComplete="off"
      isRequired
      isDisabled={save.isPending}
      status={fieldStatus(problem.validation, 'name')}
    />
  );

  const blockRows = (
    <VStack gap={4}>
      <Text type="supporting" color="secondary">
        {`Blocks are staff time, between ${WORK_START} and ${WORK_END}: keep patient details out of their notes.`}
      </Text>
      {rows.length === 0 ? (
        <EmptyState title="No blocks yet" description="A day with no blocks is free for appointments all day." isCompact />
      ) : (
        rows.map((row, i) => {
          const rowProblem = problems.get(row.key);
          const activity = byId.get(row.activityTypeId);
          const options = active
            .concat(activity?.isArchived ? [activity] : [])
            .map((a) => ({value: a.id ?? '', label: a.isArchived ? `${a.name} (archived)` : (a.name ?? '')}));
          return (
            <VStack key={row.key} id={rowId(row.key)} role="group" aria-label={`Block ${i + 1}: ${activityName(row.activityTypeId)}, ${row.startTime} to ${row.endTime}`}>
              <HStack gap={2} vAlign="start" wrap="wrap">
                <StackItem size="fill">
                  <Selector
                    label="Activity"
                    options={options}
                    value={row.activityTypeId}
                    onChange={(activityTypeId) => change(row.key, {activityTypeId})}
                    isDisabled={save.isPending}
                    containerStyle={{minWidth: 160}}
                  />
                </StackItem>
                <Selector
                  label="Start"
                  options={timeOptions(TIMES.slice(0, -1), row.startTime)}
                  value={row.startTime}
                  onChange={(startTime) => change(row.key, {startTime})}
                  isDisabled={save.isPending}
                  status={rowProblem?.field === 'startTime' ? {type: 'error', message: rowProblem.message} : undefined}
                  containerStyle={{width: 130}}
                />
                <Selector
                  label="End"
                  options={timeOptions(TIMES.slice(1), row.endTime)}
                  value={row.endTime}
                  onChange={(endTime) => change(row.key, {endTime})}
                  isDisabled={save.isPending}
                  status={rowProblem?.field === 'endTime' ? {type: 'error', message: rowProblem.message} : undefined}
                  containerStyle={{width: 130}}
                />
                <StackItem size="fill">
                  <TextInput
                    label="Note"
                    isOptional
                    value={row.note}
                    onChange={(note) => change(row.key, {note})}
                    placeholder="e.g. Team huddle"
                    autoComplete="off"
                    isDisabled={save.isPending}
                    containerStyle={{minWidth: 160}}
                  />
                </StackItem>
                <VStack gap={0} hAlign="end">
                  {/* Level with the fields, under their labels. */}
                  <Text type="supporting" color="secondary" aria-hidden>
                    {' '}
                  </Text>
                  <IconButton
                    label={`Remove ${activityName(row.activityTypeId)}, ${row.startTime} to ${row.endTime}`}
                    tooltip="Remove"
                    icon={<Icon icon={AppIcon.Delete} />}
                    variant="ghost"
                    isDisabled={save.isPending}
                    onClick={() => remove(row)}
                  />
                </VStack>
              </HStack>
            </VStack>
          );
        })
      )}
      <HStack>
        <Button
          label="Add Block"
          variant="secondary"
          icon={<Icon icon={AppIcon.Add} />}
          isDisabled={save.isPending || active.length === 0}
          onClick={add}
        />
      </HStack>
      {active.length === 0 && (
        <Text type="supporting" color="secondary">
          There are no active activity types to block time for. Add one on the Activity Types page first.
        </Text>
      )}
    </VStack>
  );

  const preview = <DayPreview rows={rows} activityName={activityName} colourOf={(id) => byId.get(id)?.colour} />;

  const form = (children: ReactNode) => (
    <FormCard fill saveText={template ? 'Save' : 'Create Day Template'} onSave={onSave} onCancel={backToList} isProcessing={save.isPending}>
      {children}
    </FormCard>
  );

  const blocksTitle = `Blocks (${rows.length})`;

  return (
    <Page
      title={template ? 'Edit Day Template' : 'New Day Template'}
      subtitle={template?.name ?? undefined}
      onBack={backToList}
      backLabel="Back to day templates"
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={['name']} />
        ) : (
          problem.message && <AlertMessage message={problem.message} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title="Save Day Template"
          message={`Changes apply from today; past days keep what they had.${
            usedIn > 0 ? ` It's used in ${count(usedIn, 'week template', 'week templates')}, so their staff's calendars change too.` : ''
          }`}
          icon={AppIcon.Save}
          confirmText="Save"
          isProcessing={save.isPending}
          onConfirm={submit}
          onCancel={() => setIsConfirming(false)}>
          <MetadataList>
            <MetadataListItem label="Day template">{name.trim() || 'No name'}</MetadataListItem>
            <MetadataListItem label="Blocks">
              {rows.length ? rows.map((r) => `${activityName(r.activityTypeId)} ${r.startTime} to ${r.endTime}`).join(', ') : 'None'}
            </MetadataListItem>
          </MetadataList>
        </ConfirmDialog>
      }>
      {isNarrow ? (
        form(
          <>
            <FormSection title="Details" useGrid={false}>
              {nameField}
            </FormSection>
            <FormSection title={blocksTitle} useGrid={false}>
              {blockRows}
            </FormSection>
            <FormSection title="The Day" useGrid={false}>
              {preview}
            </FormSection>
          </>,
        )
      ) : (
        <TwoColumns
          start={form(
            <>
              <FormSection title="Details" useGrid={false}>
                {nameField}
              </FormSection>
              <FormSection title={blocksTitle} useGrid={false}>
                {blockRows}
              </FormSection>
            </>,
          )}
          end={<PanelCard title="The Day">{preview}</PanelCard>}
        />
      )}
    </Page>
  );
}

/** The working day as the template makes it: its blocks in their colours, and the time left free for appointments. */
function DayPreview({
  rows,
  activityName,
  colourOf,
}: {
  rows: BlockRow[];
  activityName: (id: string) => string;
  colourOf: (id: string) => string | null | undefined;
}) {
  return (
    <List hasDividers density="compact" aria-label="The day, from the template">
      {dayParts(rows).map((part) =>
        part.kind === 'free' ? (
          <ListItem
            key={`free-${part.startTime}`}
            label={
              <Text type="body" color="secondary">
                Free for appointments
              </Text>
            }
            startContent={<Text type="body" color="secondary">{`${part.startTime} to ${part.endTime}`}</Text>}
          />
        ) : (
          <ListItem
            key={part.row.key}
            label={<ActivityToken name={activityName(part.row.activityTypeId)} colour={colourOf(part.row.activityTypeId)} />}
            description={part.row.note.trim() || undefined}
            startContent={<Text type="body">{`${part.row.startTime} to ${part.row.endTime}`}</Text>}
          />
        ),
      )}
    </List>
  );
}
