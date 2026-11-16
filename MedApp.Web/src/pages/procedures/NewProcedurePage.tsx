import {Selector} from '@astryxdesign/core/Selector';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {fieldStatus} from '../../api/errors';
import {patientQuery} from '../../api/patients';
import {procedureTemplatesQuery, useCreateProcedure, type ProcedureDetailsInput, type ProcedureTemplate} from '../../api/procedures';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {ProcedureSummary} from '../../components/procedures/ProcedureSummary';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {todayIso} from '../../utils/dates';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {EMPTY_PROCEDURE_DETAILS, PROCEDURE_DETAILS_FIELDS, ProcedureDetailsFields} from './ProcedureDetailsFields';

const NEW_PROCEDURE_FIELDS = ['templateId', ...PROCEDURE_DETAILS_FIELDS];

export interface NewProcedurePageProps {
  /** The patient it's for. Without one (e.g. a hand-typed URL), the page says to start from a patient. */
  patientId?: string;
}

/**
 * New procedure (decision 8): for one patient, from a template. Picking a template shows its stages, which the
 * procedure copies, and fills the title with the template's name until the title is changed. Then the due date,
 * priority and notes; a confirmation; and on to the new procedure's page.
 *
 * It fits the window: the template and its stages beside the details, or in a narrow window one form card; either
 * way the cards scroll, not the page.
 */
export function NewProcedurePage({patientId}: NewProcedurePageProps) {
  const navigate = useNavigate();
  const isNarrow = useIsNarrow();
  const create = useCreateProcedure();
  const announce = useAnnounce();
  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== undefined});
  const templates = useQuery(procedureTemplatesQuery());
  const [templateId, setTemplateId] = useState('');
  const [details, setDetails] = useState<ProcedureDetailsInput>(EMPTY_PROCEDURE_DETAILS);
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const usable = (templates.data ?? []).filter((t) => (t.stages?.length ?? 0) > 0);
  const template = usable.find((t) => t.id === templateId);
  const patientName = patient.data ? `${patient.data.firstName} ${patient.data.lastName}` : null;
  const backToList = () =>
    patientId ? void navigate({to: '/patients/$id/procedures', params: {id: patientId}}) : void navigate({to: '/procedures'});

  /** Picks a template; the title follows it while it's blank or still the previous template's name. */
  const chooseTemplate = (nextId: string) => {
    const previousName = template?.name ?? '';
    const nextName = usable.find((t) => t.id === nextId)?.name ?? '';
    setTemplateId(nextId);
    setDetails((current) => (current.title === '' || current.title === previousName ? {...current, title: nextName} : current));
  };

  const add = () =>
    create.mutate(
      {patientId, templateId: templateId || undefined, ...details, dueDate: details.dueDate || undefined},
      {
        onSuccess: (procedure) => {
          announce('Procedure created');
          void navigate({to: '/procedures/$id', params: {id: procedure.id ?? ''}});
        },
        onError: (error) => {
          setProblem(saveProblem(error, 'add', 'procedure'));
          setIsConfirming(false);
        },
      },
    );

  const pageProps = {
    title: 'New Procedure',
    subtitle: patientName ? `For ${patientName}` : undefined,
    onBack: backToList,
    backLabel: patientId ? "Back to the patient's procedures" : 'Back to procedures',
  };

  const templateFields = (
    <VStack gap={4}>
      <Selector
        label="Template"
        placeholder="Choose a template"
        options={usable.map((t) => ({value: t.id ?? '', label: t.name ?? ''}))}
        value={templateId || undefined}
        onChange={(value: string) => chooseTemplate(value)}
        isRequired
        isDisabled={create.isPending}
        status={fieldStatus(problem.validation, 'templateId')}
      />
      {template && <TemplateStages template={template} />}
    </VStack>
  );

  const detailsFields = (
    <ProcedureDetailsFields
      value={details}
      onChange={setDetails}
      errors={problem.validation}
      isDisabled={create.isPending}
      minDueDate={todayIso()}
      titleDescription="Starts as the template's name."
    />
  );

  const form = (children: ReactNode) => (
    <FormCard
      fill
      saveText="Create Procedure"
      onSave={() => {
        setProblem(NO_PROBLEM);
        setIsConfirming(true);
      }}
      onCancel={backToList}
      isProcessing={create.isPending}>
      {children}
    </FormCard>
  );

  if (!patientId) {
    return (
      <Page {...pageProps}>
        <AlertMessage message="A procedure is for a patient: open the patient's procedures and choose New Procedure there." />
      </Page>
    );
  }

  return (
    <Page
      {...pageProps}
      isLoading={templates.isPending}
      loadingMessage="Loading templates..."
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={NEW_PROCEDURE_FIELDS} />
        ) : problem.message ? (
          <AlertMessage message={problem.message} />
        ) : (
          templates.error && <AlertMessage message="The templates could not be loaded. Please try again." />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title="Confirm New Procedure"
          message="Are you sure you want to create this procedure?"
          icon={AppIcon.Add}
          confirmText="Create Procedure"
          isProcessing={create.isPending}
          onConfirm={add}
          onCancel={() => setIsConfirming(false)}>
          <ProcedureSummary
            title={details.title || template?.name}
            patientName={patientName}
            templateName={template?.name}
            dueDate={details.dueDate}
            priority={details.priority}
          />
        </ConfirmDialog>
      }>
      {isNarrow ? (
        form(
          <>
            <FormSection title="Template" useGrid={false}>
              {templateFields}
            </FormSection>
            {detailsFields}
          </>,
        )
      ) : (
        <TwoColumns
          start={<PanelCard title="Template">{templateFields}</PanelCard>}
          end={form(detailsFields)}
        />
      )}
    </Page>
  );
}

/** The stages a template gives a procedure, in order, read-only. */
function TemplateStages({template}: {template: ProcedureTemplate}) {
  const stages = [...(template.stages ?? [])].sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0));
  return (
    <VStack gap={2}>
      <Text type="body" weight="semibold">
        {`Stages (${stages.length})`}
      </Text>
      {stages.map((stage) => (
        <VStack key={stage.stageOrder} gap={0}>
          <Text type="body">{`${stage.stageOrder}. ${stage.name}`}</Text>
          {stage.description && (
            <Text type="supporting" color="secondary">
              {stage.description}
            </Text>
          )}
        </VStack>
      ))}
    </VStack>
  );
}
