import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {ApiError} from '../../api/errors';
import {
  hasStage,
  inputOf,
  MAX_PEOPLE,
  MONITORING_EVENTS,
  monitoringPeopleQuery,
  monitoringRulesQuery,
  useDeleteMonitoringRule,
  useSaveMonitoringRule,
  useSetMonitoringRuleEnabled,
  type MonitoringEvent,
  type MonitoringRule,
  type MonitoringRuleInput,
} from '../../api/monitoring';
import {allTemplatesQuery, stageDefinitionsQuery} from '../../api/pathwaySetup';
import {useAnnounce} from '../../app/announcements';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TableActions} from '../../components/tables/TableActions';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {RuleSentence} from './RuleSentence';
import {MINIMUM_PRIORITIES, describeRule, sentenceText, type RuleDescription} from './ruleText';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';

type RuleRow = MonitoringRule & Record<string, unknown>;

type DialogState = {kind: 'edit'; rule: MonitoringRule | null} | {kind: 'delete'; rule: MonitoringRule} | null;

/** The Selectors' value for "any" (they take strings, and the API takes null). */
const ANY = 'any';

const EMPTY: MonitoringRuleInput = {
  subjectUserId: null,
  event: 'StageFinished',
  minimumPriority: null,
  templateId: null,
  stageDefinitionId: null,
  isEnabled: true,
};

/** The fields shown, whose errors show on them (so the banner leaves them out). */
const FIELDS = ['subjectUserId', 'event', 'minimumPriority', 'templateId', 'stageDefinitionId'];

const fromAny = (value: string) => (value === ANY ? null : value);
const sameId = (a: string | null | undefined, b: string | null | undefined) => !!a && !!b && a.toLowerCase() === b.toLowerCase();

/**
 * What a failed save, switch or delete says. A 404 means the rule has gone since the page loaded, or (for a save) a
 * person, template or stage newly chosen for it has: what a rule already names may be deleted and stay.
 */
function problemOf(error: unknown, action: 'save' | 'add' | 'delete' | 'switch'): SaveProblem {
  if (error instanceof ApiError && error.status === 404)
    return {
      validation: null,
      message:
        action === 'save' || action === 'add'
          ? 'The rule, or something newly chosen for it, no longer exists. Reload the page and try again.'
          : 'That rule no longer exists. Reload the page.',
    };
  return saveProblem(error, action === 'switch' ? 'save' : action, 'rule');
}

const isAdmin = (roles: readonly string[] | null | undefined) => roles?.some((r) => r.toLowerCase() === 'admin') ?? false;

/**
 * Monitoring (admin): the signed-in admin's rules for being told what staff do to pathways, e.g. "Notify me when
 * **Sam** finishes **Bloods** on a **High or Urgent** priority pathway". When a change matches one of their switched-on
 * rules, they get a notification in the bell, live. Each admin sees and changes only their own rules.
 *
 * Each rule shows as that sentence, with a switch to turn it off and on, and Edit and Delete. Adding or editing one
 * opens a form (who, what, and the conditions), with the sentence previewed as it's built. On a narrow layout (a
 * narrow window or a half view) the table becomes cards by itself.
 */
export function MonitoringPage() {
  const rules = useQuery(monitoringRulesQuery);
  const people = useQuery(monitoringPeopleQuery);
  const templates = useQuery(allTemplatesQuery);
  const stages = useQuery(stageDefinitionsQuery);
  const save = useSaveMonitoringRule();
  const setEnabled = useSetMonitoringRuleEnabled();
  const remove = useDeleteMonitoringRule();
  // The rules being switched, and to what: each shows its new state, held still, until its own save is done.
  const [switching, setSwitching] = useState<Record<string, boolean>>({});
  const announce = useAnnounce();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [form, setForm] = useState<MonitoringRuleInput>(EMPTY);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);

  const data = rules.data ?? [];
  const editing = dialog?.kind === 'edit' ? dialog.rule : null;
  const target = dialog?.kind === 'delete' ? dialog.rule : null;
  const close = () => setDialog(null);
  const set = <K extends keyof MonitoringRuleInput>(key: K, value: MonitoringRuleInput[K]) => setForm((current) => ({...current, [key]: value}));

  const openEditor = (rule: MonitoringRule | null) => {
    setForm(rule ? inputOf(rule) : EMPTY);
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', rule});
  };

  const saveRule = () =>
    save.mutate(
      // A stage only goes with the events that have one.
      {id: editing?.id, ...form, stageDefinitionId: hasStage(form.event) ? form.stageDefinitionId : null},
      {
        onSuccess: () => {
          announce(editing ? 'Rule saved' : 'Rule added');
          close();
        },
        onError: (e) => setProblem(problemOf(e, editing ? 'save' : 'add')),
      },
    );

  // Each switch handles its own outcome (mutateAsync), so switching two rules at once loses neither's.
  const switchRule = async (rule: MonitoringRule, isEnabled: boolean) => {
    const id = rule.id ?? '';
    // An earlier switch's error stays until the page is left or another action replaces it, so it isn't lost.
    setSwitching((current) => ({...current, [id]: isEnabled}));
    try {
      await setEnabled.mutateAsync({id, isEnabled});
      announce(isEnabled ? 'Rule switched on' : 'Rule switched off');
    } catch (e) {
      setError(problemOf(e, 'switch').message ?? 'The rule could not be changed. Please try again.');
    } finally {
      setSwitching((current) => {
        const rest = {...current};
        delete rest[id];
        return rest;
      });
    }
  };

  const deleteRule = () =>
    target &&
    remove.mutate(target.id ?? '', {
      onSuccess: () => {
        announce('Rule deleted');
        close();
      },
      onError: (e) => {
        setError(problemOf(e, 'delete').message);
        close();
      },
    });

  // Names now: from the lists, or for a person past the first 100, the rule's own. Null for one since deleted.
  const personName = (id: string | null) =>
    id ? (people.data?.items?.find((u) => sameId(u.userId, id))?.username ?? (sameId(editing?.subjectUserId, id) ? editing?.subjectUsername : null) ?? null) : null;
  const templateName = (id: string | null) => (id ? (templates.data?.find((t) => sameId(t.id, id))?.name ?? null) : null);
  const stageName = (id: string | null) => (id ? (stages.data?.find((d) => sameId(d.id, id))?.name ?? null) : null);

  // The options, each with "any" first. Archived templates and stages are offered too (old pathways still have
  // them), and a rule's own choice stays offered even when it's no longer in a list.
  const withChosen = (options: {value: string; label: string}[], id: string | null, label: string) =>
    id && !options.some((o) => sameId(o.value, id)) ? [...options, {value: id.toLowerCase(), label}] : options;
  // Only admins unlock stages, so "Stage unlocked" offers only admins (the API refuses anyone else).
  const onlyAdmins = form.event === 'StageUnlocked';
  const personOptions = withChosen(
    [
      {value: ANY, label: 'Anyone'},
      ...(people.data?.items ?? [])
        .filter((u) => !onlyAdmins || isAdmin(u.roles))
        .map((u) => ({value: u.userId?.toLowerCase() ?? '', label: u.username ?? ''})),
    ],
    form.subjectUserId,
    personName(form.subjectUserId) ?? 'Deleted user',
  );
  const personHint = [
    onlyAdmins ? 'Only admins unlock stages, so only admins are listed.' : 'For an assignment, the person assigned.',
    (people.data?.totalCount ?? 0) > MAX_PEOPLE ? `Only the first ${MAX_PEOPLE} accounts by username are listed for now.` : null,
  ]
    .filter(Boolean)
    .join(' ');
  const templateOptions = withChosen(
    [
      {value: ANY, label: 'Any template'},
      ...(templates.data ?? []).map((t) => ({value: t.id?.toLowerCase() ?? '', label: t.isArchived ? `${t.name} (archived)` : (t.name ?? '')})),
    ],
    form.templateId,
    'Deleted template',
  );
  const stageOptions = withChosen(
    [
      {value: ANY, label: 'Any stage'},
      ...(stages.data ?? []).map((d) => ({value: d.id?.toLowerCase() ?? '', label: d.isArchived ? `${d.name} (archived)` : (d.name ?? '')})),
    ],
    form.stageDefinitionId,
    'Deleted stage',
  );
  const priorityOptions = [{value: ANY, label: 'Any priority'}, ...MINIMUM_PRIORITIES.map((p) => ({value: p.value, label: p.label}))];

  // The sentence being built.
  const preview: RuleDescription = {
    event: form.event,
    subjectUserId: form.subjectUserId,
    subjectUsername: personName(form.subjectUserId),
    minimumPriority: form.minimumPriority,
    templateId: form.templateId,
    templateName: templateName(form.templateId),
    stageDefinitionId: hasStage(form.event) ? form.stageDefinitionId : null,
    stageName: stageName(form.stageDefinitionId),
  };

  const columns: DataTableColumn<RuleRow>[] = [
    {
      key: 'rule',
      header: 'Rule',
      width: proportional(1, {minWidth: 220}),
      renderCell: (rule) => <RuleSentence rule={{...rule, event: rule.event ?? ''}} />,
    },
    {
      key: 'isEnabled',
      header: 'On',
      width: pixel(80),
      // Named by its sentence, which stays put while the switch says on or off; held while its save runs.
      renderCell: (rule) => (
        <Switch
          label={sentenceText(describeRule({...rule, event: rule.event ?? ''}))}
          isLabelHidden
          value={switching[rule.id ?? ''] ?? rule.isEnabled ?? false}
          isDisabled={(rule.id ?? '') in switching}
          isLoading={(rule.id ?? '') in switching}
          onChange={(isEnabled) => void switchRule(rule, isEnabled)}
        />
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(96),
      renderCell: (rule) => (
        <TableActions
          actions={[
            {label: 'Edit rule', icon: AppIcon.Edit, onClick: () => openEditor(rule)},
            {label: 'Delete rule', icon: AppIcon.Delete, isDestructive: true, onClick: () => setDialog({kind: 'delete', rule})},
          ]}
        />
      ),
    },
  ];

  const on = data.filter((r) => r.isEnabled).length;
  const subtitle = rules.isPending
    ? 'Loading rules...'
    : data.length === 0
      ? "What staff do that you're told about"
      : `${data.length} ${data.length === 1 ? 'rule' : 'rules'}, ${on} on`;

  return (
    <Page
      title="Monitoring"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage message={error} />
        ) : (
          rules.isError && <AlertMessage message="Your rules could not be loaded. Please try again." />
        )
      }
      actions={<Button label="Add Rule" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Rule' : 'Add Rule'}
            message="You're told in the bell when a change matches, never about your own changes. Leave a choice on Any to match everything."
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Rule'}
            isProcessing={save.isPending}
            hasSaveShortcut
            onConfirm={saveRule}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={FIELDS} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <AnchoredSelector
                label="Staff member"
                description={personHint}
                options={personOptions}
                value={form.subjectUserId?.toLowerCase() ?? ANY}
                onChange={(value: string) => set('subjectUserId', fromAny(value))}
                status={fieldStatus(problem.validation, 'subjectUserId')}
              />
              <AnchoredSelector
                label="Does this"
                options={MONITORING_EVENTS.map((e) => ({value: e.value, label: e.label}))}
                value={form.event}
                onChange={(value: string) => set('event', value as MonitoringEvent)}
                isRequired
                status={fieldStatus(problem.validation, 'event')}
              />
              {hasStage(form.event) && (
                <AnchoredSelector
                  label="Stage"
                  options={stageOptions}
                  value={form.stageDefinitionId?.toLowerCase() ?? ANY}
                  onChange={(value: string) => set('stageDefinitionId', fromAny(value))}
                  status={fieldStatus(problem.validation, 'stageDefinitionId')}
                />
              )}
              <AnchoredSelector
                label="Priority"
                options={priorityOptions}
                value={form.minimumPriority ?? ANY}
                onChange={(value: string) => set('minimumPriority', fromAny(value))}
                status={fieldStatus(problem.validation, 'minimumPriority')}
              />
              <AnchoredSelector
                label="Template"
                options={templateOptions}
                value={form.templateId?.toLowerCase() ?? ANY}
                onChange={(value: string) => set('templateId', fromAny(value))}
                status={fieldStatus(problem.validation, 'templateId')}
              />
              <Switch label="On" description="A rule that's off is kept, but tells you nothing." value={form.isEnabled} onChange={(v) => set('isEnabled', v)} />
              <VStack gap={1} aria-live="polite">
                <Text type="supporting">Preview</Text>
                <RuleSentence rule={preview} />
              </VStack>
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Delete Rule"
            message="You won't be told about this any more. Notifications it has already sent stay in the bell."
            isDanger
            confirmText="Delete Rule"
            isProcessing={remove.isPending}
            onConfirm={deleteRule}
            onCancel={close}>
            {target && <RuleSentence rule={{...target, event: target.event ?? ''}} />}
          </ConfirmDialog>
        </>
      }>
      {!rules.isPending && !rules.isError && data.length === 0 ? (
        <EmptyState
          title="No rules yet"
          description="Add a rule to be told when a member of staff does something, such as finishing a stage on an urgent pathway."
          icon={<Icon icon={AppIcon.Monitoring} size="lg" />}
          actions={<Button label="Add Your First Rule" variant="primary" onClick={() => openEditor(null)} />}
        />
      ) : (
        <DataTable data={data as RuleRow[]} columns={columns} idKey="id" isLoading={rules.isPending} skeletonRows={3} />
      )}
    </Page>
  );
}
