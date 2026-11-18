import {Button} from '@astryxdesign/core/Button';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {Code} from '@astryxdesign/core/Code';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useMemo, useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError, ApiValidationError, fieldStatus, isPermissionMissing} from '../../api/errors';
import {
  LETTER_KIND_LABELS,
  letterMergeFieldsQuery,
  letterTemplateInput,
  letterTemplateQuery,
  useLetterTemplatePreview,
  useSaveLetterTemplate,
  type LetterKind,
  type LetterMergeField,
  type LetterTemplate,
  type LetterTemplateInput,
} from '../../api/letterTemplates';
import {specialtiesQuery, type Specialty} from '../../api/specialties';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {letterTemplateHref} from './LetterTemplatesPage';

const BACK = '/admin/letter-templates';
const BACK_LABEL = 'Back to letter templates';

/** The specialties a template can newly be given: those not archived (the route preloads them). */
export const letterTemplateSpecialtiesQuery = specialtiesQuery(false);

/** The Specialty picker's value for a template for every specialty (the API's null). */
export const EVERY_SPECIALTY = 'every';

/** The fields the API's 400 can name, each shown on its own field. */
const FIELDS = ['name', 'kind', 'specialtyId', 'letterhead', 'bodySkeleton', 'closing'];

/** The most each text may be (the API refuses longer: 400). Shown as a counter on the field. */
const MAX_PLAIN = 1000;
const MAX_BODY = 20000;

const KIND_OPTIONS = (Object.entries(LETTER_KIND_LABELS) as [LetterKind, string][]).map(([value, label]) => ({value, label}));

/**
 * What the Specialty picker offers: All specialties, then those not archived, by name, and the template's own if it's
 * archived since (it may keep it, but nothing can newly be given it), marked so.
 */
export function letterSpecialtyChoices(active: readonly Specialty[], template: LetterTemplate | null) {
  const choices = active
    .filter((s) => !s.isArchived)
    .map((s) => ({value: s.id ?? '', label: s.name ?? ''}))
    .sort((a, b) => a.label.localeCompare(b.label, undefined, {sensitivity: 'base'}));
  const current = template?.specialtyId;
  if (current && !choices.some((c) => c.value === current)) {
    choices.unshift({value: current, label: `${template?.specialtyName ?? 'Unknown specialty'} (archived)`});
  }
  return [{value: EVERY_SPECIALTY, label: 'All specialties'}, ...choices];
}

/** What a failed preview shows: a 400's messages on the fields, otherwise one message. */
export function previewProblem(error: unknown): SaveProblem {
  if (error instanceof ApiValidationError) return {validation: error.errors, message: null};
  if (isPermissionMissing(error) && error.title) return {validation: null, message: error.title};
  if (error instanceof ApiError && error.status === 401) return {validation: null, message: 'Your session has ended. Please sign in again.'};
  return {validation: null, message: 'The preview could not be made. Please try again.'};
}

/**
 * A new letter template (no `id`), or one to edit. This part loads the template and the specialties; an unknown
 * template goes back to the list. The editor mounts once they're here, so it starts from the template.
 */
export function LetterTemplateEditorPage({id}: {id?: string}) {
  const navigate = useNavigate();
  const template = useQuery({...letterTemplateQuery(id ?? ''), enabled: id !== undefined});
  const specialties = useQuery(letterTemplateSpecialtiesQuery);
  const isMissing = template.error instanceof ApiError && template.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: BACK, replace: true});
  }, [isMissing, navigate]);

  if (specialties.data && (id === undefined || template.data)) {
    return <LetterTemplateEditor key={template.data?.id ?? 'new'} template={template.data ?? null} specialties={specialties.data} />;
  }
  const failed = specialties.isError || (template.isError && !isMissing);
  return (
    <Page
      title={id ? 'Edit Letter Template' : 'New Letter Template'}
      backHref={BACK}
      backLabel={BACK_LABEL}
      isLoading={!failed}
      loadingMessage="Loading letter template..."
      message={failed && <AlertMessage message="The letter template could not be loaded. Please try again." />}
    />
  );
}

/**
 * The letter template editor (decision 99): its name, kind and specialty (or every specialty), and its text: the
 * letterhead, the body skeleton in Markdown with the headings its author fills in, and the closing, each of which may
 * hold merge fields, listed beside it. **Download Preview (PDF)** sends the text as it is now, saved or not, and the
 * API fills it with made-up details, marking every page as a preview. Saving a change sends the version this page
 * started from, so another admin's change made meanwhile is refused (409) with the API's words, and the template is
 * read again. A new template, once created, opens on its own page.
 */
function LetterTemplateEditor({template, specialties}: {template: LetterTemplate | null; specialties: Specialty[]}) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const save = useSaveLetterTemplate();
  const preview = useLetterTemplatePreview();
  const mergeFields = useQuery(letterMergeFieldsQuery);
  const [input, setInput] = useState<LetterTemplateInput>(() => letterTemplateInput(template));
  const [largePrint, setLargePrint] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const choices = useMemo(() => letterSpecialtyChoices(specialties, template), [specialties, template]);
  const isBusy = save.isPending || preview.isPending;
  const backToList = () => void navigate({to: BACK});
  const set = <K extends keyof LetterTemplateInput>(key: K) => (value: LetterTemplateInput[K]) => setInput((current) => ({...current, [key]: value}));

  const submit = () => {
    setProblem(NO_PROBLEM);
    save.mutate(
      {id: template?.id, expectedVersion: template?.version, input},
      {
        onSuccess: (saved) => {
          announce(template ? 'Letter template saved' : 'Letter template created');
          if (template) queryClient.setQueryData(letterTemplateQuery(template.id ?? '').queryKey, saved);
          else void navigate({to: letterTemplateHref(saved.id ?? ''), replace: true});
        },
        // A 409 (the name is taken, or someone changed it meanwhile) shows the API's words; the template has been
        // read again by then (`useSaveLetterTemplate`), so saving again, having been told, starts from its new version.
        onError: (e) => setProblem(saveProblem(e, template ? 'save' : 'add', 'letter template')),
      },
    );
  };

  const downloadPreview = () => {
    setProblem(NO_PROBLEM);
    preview.mutate(
      {name: input.name, letterhead: input.letterhead, bodySkeleton: input.bodySkeleton, closing: input.closing, largePrint},
      {onSuccess: () => announce('Preview downloaded'), onError: (e) => setProblem(previewProblem(e))},
    );
  };

  const details = (
    <>
      <TextInput
        label="Name"
        description="What authors choose when they start a letter."
        value={input.name}
        onChange={set('name')}
        placeholder="e.g. Dermatology clinic letter"
        autoComplete="off"
        isRequired
        isDisabled={isBusy}
        status={fieldStatus(problem.validation, 'name')}
      />
      <AnchoredSelector
        label="Kind"
        description="What letters from it are for."
        options={KIND_OPTIONS}
        value={input.kind}
        onChange={(value: string) => set('kind')(value as LetterKind)}
        isRequired
        isDisabled={isBusy}
        status={fieldStatus(problem.validation, 'kind')}
      />
      <AnchoredSelector
        label="Specialty"
        description="Whose letters it's offered for: one specialty's, or every specialty's."
        options={choices}
        value={input.specialtyId ?? EVERY_SPECIALTY}
        onChange={(value: string) => set('specialtyId')(value === EVERY_SPECIALTY ? null : value)}
        isRequired
        isDisabled={isBusy}
        status={fieldStatus(problem.validation, 'specialtyId')}
      />
    </>
  );

  const text = (
    <>
      <TextArea
        label="Letterhead"
        description="Plain lines at the top of the first page, such as the clinic's name, address and phone number."
        value={input.letterhead}
        onChange={set('letterhead')}
        rows={4}
        isDisabled={isBusy}
        maxLength={MAX_PLAIN}
        status={fieldStatus(problem.validation, 'letterhead')}
      />
      <TextArea
        label="Body skeleton"
        description="In Markdown (## for a heading, ** round bold words, - for a list). Give the headings the author fills in, such as ## Diagnosis and ## Plan. Never put a real patient's details here: merge fields fill them in."
        value={input.bodySkeleton}
        onChange={set('bodySkeleton')}
        rows={14}
        isDisabled={isBusy}
        maxLength={MAX_BODY}
        status={fieldStatus(problem.validation, 'bodySkeleton')}
      />
      <TextArea
        label="Closing"
        description="Plain lines after the body, such as the sign-off with {{author.name}} and {{author.jobTitle}}."
        value={input.closing}
        onChange={set('closing')}
        rows={4}
        isDisabled={isBusy}
        maxLength={MAX_PLAIN}
        status={fieldStatus(problem.validation, 'closing')}
      />
    </>
  );

  const previewSection = (
    <VStack gap={3}>
      <Text type="supporting" color="secondary">
        A PDF of the text above, saved or not, with every merge field filled with made-up details, never a real patient's.
        Every page is marked as a preview.
      </Text>
      <CheckboxInput
        label="Large print"
        description="16 point, as for someone who needs large print (the Accessible Information Standard)."
        value={largePrint}
        onChange={setLargePrint}
        isDisabled={isBusy}
      />
      <HStack gap={2}>
        <Button
          label="Download Preview (PDF)"
          variant="secondary"
          icon={<Icon icon={AppIcon.Download} />}
          isLoading={preview.isPending}
          isDisabled={save.isPending}
          onClick={downloadPreview}
        />
      </HStack>
    </VStack>
  );

  const mergeFieldList = <MergeFieldList fields={mergeFields.data} isPending={mergeFields.isPending} isError={mergeFields.isError} />;

  const form = (children: ReactNode) => (
    <FormCard fill saveText={template ? 'Save' : 'Create Letter Template'} onSave={submit} onCancel={backToList} isProcessing={isBusy}>
      <FormSection title="Details" useGrid={false}>
        {details}
      </FormSection>
      <FormSection title="Text" useGrid={false}>
        {text}
      </FormSection>
      <FormSection title="Preview" useGrid={false}>
        {previewSection}
      </FormSection>
      {children}
    </FormCard>
  );

  return (
    <Page
      title={template ? 'Edit Letter Template' : 'New Letter Template'}
      subtitle={template?.name ?? undefined}
      onBack={backToList}
      backLabel={BACK_LABEL}
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={FIELDS} />
        ) : problem.message ? (
          <AlertMessage message={problem.message} />
        ) : (
          template?.isArchived && (
            <AlertMessage status="warning" message="This letter template is archived: it isn't offered for new letters. Restore it from the list." />
          )
        )
      }>
      {isNarrow ? (
        form(
          <FormSection title="Merge Fields" useGrid={false}>
            {mergeFieldList}
          </FormSection>,
        )
      ) : (
        <TwoColumns
          start={form(null)}
          end={
            // Nothing in the list takes focus, so the keyboard needs the body itself to scroll it (WCAG 2.1.1).
            <PanelCard title="Merge Fields" isBodyKeyboardScrollable>
              {mergeFieldList}
            </PanelCard>
          }
        />
      )}
    </Page>
  );
}

/**
 * The merge fields a template's text may hold, each as it's written (`{{patient.name}}`), with its label and where its
 * value comes from, and what they're for.
 */
function MergeFieldList({fields, isPending, isError}: {fields?: LetterMergeField[]; isPending: boolean; isError: boolean}) {
  if (isError) return <AlertMessage message="The merge fields could not be loaded. Please try again." />;
  return (
    <VStack gap={3}>
      <Text type="supporting" color="secondary">
        Write a merge field in the text as shown, with its double braces, and each letter fills it in from its record.
        They copy the patient's identity and administrative details only, never a clinical value: what the letter says
        clinically is its author's to write. A letter with a merge field that has no value can't be approved.
      </Text>
      {isPending ? (
        <EmptyState title="Loading merge fields..." isCompact />
      ) : (
        <List hasDividers aria-label="Merge fields">
          {(fields ?? []).map((field) => (
            <ListItem
              key={field.name}
              label={
                <HStack gap={2} vAlign="center" wrap="wrap">
                  <Code>{`{{${field.name}}}`}</Code>
                  <Text type="body" weight="semibold">
                    {field.label}
                  </Text>
                </HStack>
              }
              description={
                <Text type="supporting" color="secondary">
                  {field.description}
                </Text>
              }
            />
          ))}
        </List>
      )}
    </VStack>
  );
}
