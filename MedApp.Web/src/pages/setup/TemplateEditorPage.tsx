import {createReorderable} from '@astryxdesign/core';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useMediaQuery} from '@astryxdesign/core/hooks';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useMemo, useState, type ReactNode} from 'react';
import {ApiError, fieldStatus} from '../../api/errors';
import type {ProcedureTemplate} from '../../api/procedures';
import {stageDefinitionsQuery, templateQuery, useSaveTemplate, type StageDefinition} from '../../api/procedureSetup';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {NARROW_WINDOW_QUERY, Page} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/** A stage in the template being edited. The key is the row's own, as a definition may appear more than once. */
interface StageRow {
  key: string;
  definitionId: string;
}

const BACK = '/admin/templates';

let rowCount = 0;
/** A new row for a stage, with a key of its own (a definition can be in a template more than once). */
const row = (definitionId: string): StageRow => ({key: `stage-${++rowCount}`, definitionId});

/**
 * A new template (no `id`), or one to edit. This part loads the template and the stage definitions; an unknown
 * template goes back to the list. The editor mounts once they're here, so it starts from the template.
 */
export function TemplateEditorPage({id}: {id?: string}) {
  const navigate = useNavigate();
  const template = useQuery({...templateQuery(id ?? ''), enabled: id !== undefined});
  const definitions = useQuery(stageDefinitionsQuery);
  const isMissing = template.error instanceof ApiError && template.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: BACK, replace: true});
  }, [isMissing, navigate]);

  const isLoading = definitions.isPending || (id !== undefined && template.isPending) || isMissing;
  if (!isLoading && definitions.data && (id === undefined || template.data)) {
    return <TemplateEditor template={template.data ?? null} definitions={definitions.data} />;
  }
  const failed = definitions.isError || (template.isError && !isMissing);
  return (
    <Page
      title={id ? 'Edit Template' : 'New Template'}
      backHref={BACK}
      backLabel="Back to templates"
      isLoading={isLoading}
      loadingMessage="Loading template..."
      message={failed && <AlertMessage message="The template could not be loaded. Please try again." />}
    />
  );
}

/**
 * The template editor (decision 17): its name, and its stages in order. Stages are added from the active stage
 * definitions (a definition may be added more than once), dragged into order (or moved with the keyboard from
 * each row's handle) and removed. Saving asks first, then goes back to the list.
 *
 * Wide, the stages and the definitions to add sit side by side; narrow, they're one form card. Either way the
 * cards scroll, not the page.
 */
function TemplateEditor({template, definitions}: {template: ProcedureTemplate | null; definitions: StageDefinition[]}) {
  const navigate = useNavigate();
  const isNarrow = useMediaQuery(NARROW_WINDOW_QUERY);
  const save = useSaveTemplate();
  const [name, setName] = useState(template?.name ?? '');
  const [stages, setStages] = useState<StageRow[]>(() => (template?.stageDefinitionIdsInOrder ?? []).map(row));
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const byId = useMemo(() => new Map(definitions.map((d) => [d.id ?? '', d])), [definitions]);
  const available = definitions
    .filter((d) => !d.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
  const backToList = () => void navigate({to: BACK});
  const usedBy = template?.procedureCount ?? 0;

  const reorderable = useMemo(
    () =>
      createReorderable({
        onReorder: ({key, to}) =>
          setStages((current) => {
            const from = current.findIndex((s) => s.key === key);
            if (from < 0) return current;
            const next = [...current];
            const [moved] = next.splice(from, 1);
            next.splice(to, 0, moved);
            return next;
          }),
      }),
    [],
  );

  const add = (definitionId: string) => setStages((current) => [...current, row(definitionId)]);
  const remove = (key: string) => setStages((current) => current.filter((s) => s.key !== key));

  const submit = () =>
    save.mutate(
      {id: template?.id, name: name.trim(), stageDefinitionIdsInOrder: stages.map((s) => s.definitionId)},
      {
        onSuccess: backToList,
        onError: (e) => {
          setProblem(saveProblem(e, template ? 'save' : 'add', 'template'));
          setIsConfirming(false);
        },
      },
    );

  const nameField = (
    <TextInput
      label="Name"
      description="What staff choose when they create a procedure."
      value={name}
      onChange={setName}
      placeholder="e.g. Diagnostic procedure"
      autoComplete="off"
      isRequired
      isDisabled={save.isPending}
      status={fieldStatus(problem.validation, 'name')}
    />
  );

  const stageList =
    stages.length === 0 ? (
      <EmptyState
        title="No stages yet"
        description={isNarrow ? 'Add stages from the list below.' : 'Add stages from the stage definitions beside this.'}
        isCompact
      />
    ) : (
      <List reorderable={reorderable} hasDividers aria-label="Stages in order">
        {stages.map((stage, i) => {
          const definition = byId.get(stage.definitionId);
          return (
            <ListItem
              key={stage.key}
              itemKey={stage.key}
              label={`${i + 1}. ${definition?.name ?? 'Unknown stage'}`}
              description={definition?.description ?? undefined}
              endContent={
                <HStack gap={2} vAlign="center">
                  {definition?.isArchived && <Token size="sm" label="Archived" color="gray" />}
                  <IconButton
                    label={`Remove ${definition?.name ?? 'stage'} (stage ${i + 1})`}
                    tooltip="Remove"
                    icon={<Icon icon={AppIcon.Cancel} />}
                    variant="ghost"
                    size="sm"
                    isDisabled={save.isPending}
                    onClick={() => remove(stage.key)}
                  />
                </HStack>
              }
            />
          );
        })}
      </List>
    );

  const definitionList = (
    <List hasDividers aria-label="Stage definitions to add">
      {available.map((definition) => (
        <ListItem
          key={definition.id}
          label={definition.name ?? ''}
          description={definition.description ?? undefined}
          endContent={
            <IconButton
              label={`Add ${definition.name}`}
              tooltip="Add to the end"
              icon={<Icon icon={AppIcon.Add} />}
              variant="ghost"
              size="sm"
              isDisabled={save.isPending}
              onClick={() => add(definition.id ?? '')}
            />
          }
        />
      ))}
    </List>
  );

  const form = (children: ReactNode) => (
    <FormCard
      fill
      saveText={template ? 'Save' : 'Create Template'}
      onSave={() => {
        setProblem(NO_PROBLEM);
        setIsConfirming(true);
      }}
      onCancel={backToList}
      isProcessing={save.isPending}>
      {children}
    </FormCard>
  );

  const stagesTitle = `Stages (${stages.length})`;

  return (
    <Page
      title={template ? 'Edit Template' : 'New Template'}
      subtitle={template?.name ?? undefined}
      onBack={backToList}
      backLabel="Back to templates"
      fill
      message={
        problem.validation ? (
          // The name's errors show on it; the stages' (e.g. none chosen) show here.
          <ValidationAlert errors={problem.validation} fields={['name']} />
        ) : (
          problem.message && <AlertMessage message={problem.message} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title={template ? 'Confirm Changes' : 'Confirm New Template'}
          message={
            template
              ? usedBy > 0
                ? `New procedures will use these stages. The ${usedBy} ${usedBy === 1 ? 'procedure' : 'procedures'} already made from it keep theirs.`
                : 'Are you sure you want to save these changes?'
              : 'Are you sure you want to create this template?'
          }
          icon={template ? AppIcon.Save : AppIcon.Add}
          confirmText={template ? 'Confirm Save' : 'Create Template'}
          isProcessing={save.isPending}
          onConfirm={submit}
          onCancel={() => setIsConfirming(false)}>
          <MetadataList>
            <MetadataListItem label="Template">{name.trim() || 'No name'}</MetadataListItem>
            <MetadataListItem label="Stages">
              {stages.length ? stages.map((s, i) => `${i + 1}. ${byId.get(s.definitionId)?.name}`).join(', ') : 'None'}
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
            <FormSection title={stagesTitle} useGrid={false}>
              {stageList}
            </FormSection>
            <FormSection title="Add Stages" useGrid={false}>
              {definitionList}
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
              <FormSection title={stagesTitle} useGrid={false}>
                {stageList}
              </FormSection>
            </>,
          )}
          end={<PanelCard title="Stage Definitions">{definitionList}</PanelCard>}
        />
      )}
    </Page>
  );
}
