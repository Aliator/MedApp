import {createReorderable} from '@astryxdesign/core';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {HStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useMemo, useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError, fieldStatus} from '../../api/errors';
import type {PathwayTemplate} from '../../api/pathways';
import {stageDefinitionsQuery, templateQuery, useSaveTemplate, type StageDefinition} from '../../api/pathwaySetup';
import {specialtiesQuery, type Specialty} from '../../api/specialties';
import {VisitRequirementToken} from '../../components/calendar/CalendarTokens';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {stageDescription} from '../../components/pathways/SnomedConcept';

/** A stage in the template being edited. The key is the row's own, as a definition may appear more than once. */
interface StageRow {
  key: string;
  definitionId: string;
}

const BACK = '/admin/pathway-templates';

/** The specialties a template can be given: the active ones (the route preloads them). */
export const templateSpecialtiesQuery = specialtiesQuery(false);

/** Shown on Specialty when a new template is created without one (the API says the same, as the last word). */
export const SPECIALTY_REQUIRED = 'Choose the specialty this template belongs to.';

/** No specialty: what the API sends for a template without one. */
const NO_ID = '00000000-0000-0000-0000-000000000000';

/**
 * The specialties a template can have: the active ones, by name, and the one it has now if that's archived (it may
 * keep it, but no other template can be given it), marked so.
 */
export function specialtyChoices(active: readonly Specialty[], template: PathwayTemplate | null) {
  const choices = active
    .filter((s) => !s.isArchived)
    .map((s) => ({value: s.id ?? '', label: s.name ?? ''}))
    .sort((a, b) => a.label.localeCompare(b.label, undefined, {sensitivity: 'base'}));
  const current = template?.specialtyId;
  if (current && current !== NO_ID && !choices.some((c) => c.value === current)) {
    choices.unshift({value: current, label: `${template?.specialtyName ?? 'Unknown specialty'} (archived)`});
  }
  return choices;
}

let rowCount = 0;
/** A new row for a stage, with a key of its own (a definition can be in a template more than once). */
const row = (definitionId: string): StageRow => ({key: `stage-${++rowCount}`, definitionId});

/**
 * A new template (no `id`), or one to edit. This part loads the template and the stage definitions; an unknown
 * template goes back to the list. The editor mounts once they're here, so it starts from the template.
 */
export function PathwayTemplateEditorPage({id}: {id?: string}) {
  const navigate = useNavigate();
  const template = useQuery({...templateQuery(id ?? ''), enabled: id !== undefined});
  const definitions = useQuery(stageDefinitionsQuery);
  const specialties = useQuery(templateSpecialtiesQuery);
  const isMissing = template.error instanceof ApiError && template.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: BACK, replace: true});
  }, [isMissing, navigate]);

  const isLoading = definitions.isPending || specialties.isPending || (id !== undefined && template.isPending) || isMissing;
  if (!isLoading && definitions.data && specialties.data && (id === undefined || template.data)) {
    return <TemplateEditor template={template.data ?? null} definitions={definitions.data} specialties={specialties.data} />;
  }
  const failed = definitions.isError || specialties.isError || (template.isError && !isMissing);
  return (
    <Page
      title={id ? 'Edit Pathway Template' : 'New Pathway Template'}
      backHref={BACK}
      backLabel="Back to pathway templates"
      isLoading={isLoading}
      loadingMessage="Loading template..."
      message={failed && <AlertMessage message="The template could not be loaded. Please try again." />}
    />
  );
}

/**
 * The template editor (decision 17): its name, the specialty it belongs to, and its stages in order. Stages are added from the active stage
 * definitions (a definition may be added more than once), dragged into order (or moved with the keyboard from
 * each row's handle) and removed. Saving asks first, then goes back to the list.
 *
 * Wide, the stages and the definitions to add sit side by side; narrow, they're one form card. Either way the
 * cards scroll, not the page.
 */
function TemplateEditor({
  template,
  definitions,
  specialties,
}: {
  template: PathwayTemplate | null;
  definitions: StageDefinition[];
  specialties: Specialty[];
}) {
  const navigate = useNavigate();
  const isNarrow = useIsNarrow();
  const save = useSaveTemplate();
  const announce = useAnnounce();
  const [name, setName] = useState(template?.name ?? '');
  const choices = useMemo(() => specialtyChoices(specialties, template), [specialties, template]);
  const startingSpecialty = template?.specialtyId && template.specialtyId !== NO_ID ? template.specialtyId : '';
  const [specialtyId, setSpecialtyId] = useState(startingSpecialty);
  const [isSpecialtyMissing, setIsSpecialtyMissing] = useState(false);
  const [stages, setStages] = useState<StageRow[]>(() => (template?.stageDefinitionIdsInOrder ?? []).map(row));
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const byId = useMemo(() => new Map(definitions.map((d) => [d.id ?? '', d])), [definitions]);
  const available = definitions
    .filter((d) => !d.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}));
  const backToList = () => void navigate({to: BACK});
  const usedBy = template?.pathwayCount ?? 0;

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
      {
        id: template?.id,
        name: name.trim(),
        stageDefinitionIdsInOrder: stages.map((s) => s.definitionId),
        // A new template always has one; a saved one sends it only when it changed, so a template keeps an archived one.
        ...(specialtyId && specialtyId !== startingSpecialty ? {specialtyId} : {}),
      },
      {
        onSuccess: () => {
          announce(template ? 'Template saved' : 'Template created');
          backToList();
        },
        onError: (e) => {
          setProblem(saveProblem(e, template ? 'save' : 'add', 'template'));
          setIsConfirming(false);
        },
      },
    );

  const specialtyField = (
    <AnchoredSelector
      label="Specialty"
      description="Which specialty its pathways belong to."
      placeholder="Choose a specialty"
      options={choices}
      value={specialtyId || undefined}
      onChange={(value: string) => {
        setSpecialtyId(value);
        setIsSpecialtyMissing(false);
      }}
      isRequired
      isDisabled={save.isPending}
      status={isSpecialtyMissing ? {type: 'error', message: SPECIALTY_REQUIRED} : fieldStatus(problem.validation, 'specialtyId')}
    />
  );

  const nameField = (
    <TextInput
      label="Name"
      description="What staff choose when they create a pathway."
      value={name}
      onChange={setName}
      placeholder="e.g. Diagnostic pathway"
      autoComplete="off"
      isRequired
      isDisabled={save.isPending}
      status={fieldStatus(problem.validation, 'name')}
    />
  );

  const details = (
    <>
      {nameField}
      {specialtyField}
    </>
  );

  const stageList =
    stages.length === 0 ? (
      <EmptyState
        title="No stages yet"
        description={isNarrow ? 'Add stages from the list below.' : 'Add stages from All Stages, beside this.'}
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
              description={stageDescription(definition?.description, {code: definition?.snomedCode, term: definition?.snomedTerm})}
              endContent={
                <HStack gap={2} vAlign="center">
                  <VisitRequirementToken requirement={definition?.visitRequirement} />
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
    <List hasDividers aria-label="Stages to add">
      {available.map((definition) => (
        <ListItem
          key={definition.id}
          label={definition.name ?? ''}
          description={stageDescription(definition.description, {code: definition.snomedCode, term: definition.snomedTerm})}
          endContent={
            <HStack gap={2} vAlign="center">
              <VisitRequirementToken requirement={definition.visitRequirement} />
              <IconButton
                label={`Add ${definition.name}`}
                tooltip="Add to the end"
                icon={<Icon icon={AppIcon.Add} />}
                variant="ghost"
                size="sm"
                isDisabled={save.isPending}
                onClick={() => add(definition.id ?? '')}
              />
            </HStack>
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
        // A new template needs a specialty: say so on the field rather than ask to confirm one that can't be saved.
        if (!specialtyId) {
          setIsSpecialtyMissing(true);
          return;
        }
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
      title={template ? 'Edit Pathway Template' : 'New Pathway Template'}
      subtitle={template?.name ?? undefined}
      onBack={backToList}
      backLabel="Back to pathway templates"
      fill
      message={
        problem.validation ? (
          // The name's errors show on it; the stages' (e.g. none chosen) show here.
          <ValidationAlert errors={problem.validation} fields={['name', 'specialtyId']} />
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
                ? `New pathways will use these stages. The ${usedBy} ${usedBy === 1 ? 'pathway' : 'pathways'} already made from it keep theirs.`
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
            <MetadataListItem label="Specialty">{choices.find((c) => c.value === specialtyId)?.label ?? 'None'}</MetadataListItem>
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
              {details}
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
                {details}
              </FormSection>
              <FormSection title={stagesTitle} useGrid={false}>
                {stageList}
              </FormSection>
            </>,
          )}
          end={<PanelCard title="All Stages">{definitionList}</PanelCard>}
        />
      )}
    </Page>
  );
}
