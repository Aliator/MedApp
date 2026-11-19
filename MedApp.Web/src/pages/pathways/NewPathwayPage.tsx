import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {fieldStatus} from '../../api/errors';
import {patientQuery} from '../../api/patients';
import {pathwayTemplatesQuery, useCreatePathway, type NewPathwayDetailsInput, type PathwayTemplate} from '../../api/pathways';
import {VisitRequirementToken} from '../../components/calendar/CalendarTokens';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PathwaySummary} from '../../components/pathways/PathwaySummary';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {todayIso} from '../../utils/dates';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {EMPTY_PATHWAY_DETAILS, PATHWAY_DETAILS_FIELDS, PathwayDetailsFields} from './PathwayDetailsFields';
import {SnomedConcept} from '../../components/pathways/SnomedConcept';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';

const NEW_PATHWAY_FIELDS = ['templateId', ...PATHWAY_DETAILS_FIELDS];

export interface NewPathwayPageProps {
  /** The patient it's for. Without one (e.g. a hand-typed URL), the page says to start from a patient. */
  patientId?: string;
}

/**
 * New pathway (decision 8): for one patient, from a template. Picking a template shows its stages, which the
 * pathway copies, and fills the title with the template's name until the title is changed. Then the target date,
 * priority and notes (its first note); a confirmation; and on to the new pathway's page.
 *
 * It fits the window: the template and its stages beside the details, or in a narrow window one form card; either
 * way the cards scroll, not the page.
 */
export function NewPathwayPage({patientId}: NewPathwayPageProps) {
  const navigate = useNavigate();
  const isNarrow = useIsNarrow();
  const create = useCreatePathway();
  const announce = useAnnounce();
  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== undefined});
  const templates = useQuery(pathwayTemplatesQuery());
  const [templateId, setTemplateId] = useState('');
  const [details, setDetails] = useState<NewPathwayDetailsInput>(EMPTY_PATHWAY_DETAILS);
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const usable = (templates.data ?? []).filter((t) => (t.stages?.length ?? 0) > 0);
  const template = usable.find((t) => t.id === templateId);
  const patientName = patient.data ? `${patient.data.firstName} ${patient.data.lastName}` : null;
  const backToList = () =>
    patientId ? void navigate({to: '/patients/$id/pathways', params: {id: patientId}}) : void navigate({to: '/pathways'});

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
        onSuccess: (pathway) => {
          announce('Pathway created');
          void navigate({to: '/pathways/$id', params: {id: pathway.id ?? ''}});
        },
        onError: (error) => {
          setProblem(saveProblem(error, 'add', 'pathway'));
          setIsConfirming(false);
        },
      },
    );

  const pageProps = {
    title: 'New Pathway',
    subtitle: patientName ? `For ${patientName}` : undefined,
    onBack: backToList,
    backLabel: patientId ? "Back to the patient's pathways" : 'Back to pathways',
  };

  const templateFields = (
    <VStack gap={4}>
      <AnchoredSelector
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
    <PathwayDetailsFields
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
      saveText="Create Pathway"
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
        <AlertMessage label="No patient chosen" message="A pathway is for a patient: open the patient's pathways and choose New Pathway there." />
      </Page>
    );
  }

  return (
    <Page
      {...pageProps}
      isLoading={templates.isPending}
      loadingMessage="Loading templates..."
      fill
      banner={patient.data && <PatientBanner patient={patient.data} />}
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={NEW_PATHWAY_FIELDS} />
        ) : problem.message ? (
          <AlertMessage label="Pathway not added" message={problem.message} />
        ) : templates.error ? (
          <AlertMessage label="Templates not loaded" />
        ) : (
          // The patient's restricted, disputed or anonymised banners, as on every page about the patient (hazard H-22).
          patient.data && <PatientFlagBanners patient={patient.data} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title="Confirm New Pathway"
          message="Are you sure you want to create this pathway?"
          icon={AppIcon.Add}
          confirmText="Create Pathway"
          isProcessing={create.isPending}
          onConfirm={add}
          onCancel={() => setIsConfirming(false)}>
          <PathwaySummary
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

/** The stages a template gives a pathway, in order, read-only, each saying whether it involves a visit. */
function TemplateStages({template}: {template: PathwayTemplate}) {
  const stages = [...(template.stages ?? [])].sort((a, b) => (a.stageOrder ?? 0) - (b.stageOrder ?? 0));
  return (
    <VStack gap={2}>
      <Text type="body" weight="semibold">
        {`Stages (${stages.length})`}
      </Text>
      {stages.map((stage) => (
        <VStack key={stage.stageOrder} gap={0}>
          <HStack gap={2} vAlign="center" wrap="wrap">
            <Text type="body">{`${stage.stageOrder}. ${stage.name}`}</Text>
            <VisitRequirementToken requirement={stage.visitRequirement} />
          </HStack>
          {stage.description && (
            <Text type="supporting" color="secondary">
              {stage.description}
            </Text>
          )}
          <SnomedConcept code={stage.snomedCode} term={stage.snomedTerm} />
        </VStack>
      ))}
    </VStack>
  );
}
