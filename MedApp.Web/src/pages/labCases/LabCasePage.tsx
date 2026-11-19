import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {ApiError} from '../../api/errors';
import {labCaseStatusLabel} from '../../api/hospitals';
import {labCaseQuery, labOutcomeLabel, type LabCase, type LabCaseStep} from '../../api/labCases';
import {useCan} from '../../auth/auth';
import {LabCaseDialog, type LabCaseDialogKind} from '../../components/labCases/LabCaseDialog';
import {LabClockDetails} from '../../components/labCases/LabClock';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {hasPatientFlags, PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {SampleSourceToken, SpecimenStatusToken} from '../../components/specimens/SpecimenCells';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDateTime} from '../../utils/dates';

const by = (at: string | null | undefined, who: string | null | undefined) => (at ? `${formatDateTime(at)}${who ? ` by ${who}` : ''}` : 'Not yet');

/** Notes and results keep the lines they were typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/** A recorded step's line: its name, and for a choice the outcome recorded. */
const stepLabel = (step: LabCaseStep) => (step.kind === 'Outcome' ? `${step.name} ${labOutcomeLabel(step.outcome)}` : (step.name ?? ''));

/**
 * One lab case (`/lab/cases/$id`, MC15): the patient's banner, then what the case needs next, its details, its steps
 * (each with who and when, the outcome recorded at each choice, and any marked entered in error), its samples and its
 * report. **What's next** is the next step (Record), a choice with each outcome a button of its own, in a fixed order
 * and none chosen or suggested (decision 191), or the results (Enter Results and Authorise, for holders of
 * AuthoriseLabReports). **Clock** (MC19): when it's due, which deadline set it (the lab's target or the hospital's date,
 * the sooner), both dates, and when it's at risk. Only the lab team works a case; anyone else reads it. Opening it is
 * recorded as viewing it.
 */
export function LabCasePage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  const {data: labCase, error, isPending} = useQuery(labCaseQuery(id));
  const canProcess = useCan('ProcessSpecimens');
  const [dialog, setDialog] = useState<LabCaseDialogKind | null>(null);
  const [open, setOpen] = useState({next: true, clock: true, details: true, steps: true, samples: true, report: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const isMissing = error instanceof ApiError && error.status === 404;
  const close = () => setDialog(null);
  const canWork = Boolean(labCase?.canRecord || labCase?.canAuthorise) && canProcess;

  return (
    <Page
      title={labCase ? `Lab Case ${labCase.number}` : 'Lab Case'}
      subtitle={labCase ? [labCase.labTestName, labCaseStatusLabel(labCase.status)].filter(Boolean).join(' · ') : undefined}
      backHref="/lab/cases"
      backLabel="Back to lab cases"
      isLoading={isPending}
      loadingMessage="Loading lab case..."
      fill={labCase !== undefined}
      banner={labCase?.patient && <PatientBanner patient={labCase.patient} />}
      message={
        isMissing ? (
          <AlertMessage message="Lab case not found. It may have been deleted, or it's another team's." />
        ) : error ? (
          <AlertMessage message="The lab case could not be loaded. Please try again." />
        ) : labCase?.patientFlags && hasPatientFlags(labCase.patientFlags) ? (
          <PatientFlagBanners patient={labCase.patientFlags} />
        ) : (
          labCase?.readOnlyReason && <SummaryAlert status="info" label="Read-only case" message={labCase.readOnlyReason} />
        )
      }
      actions={
        labCase?.canMarkEnteredInError && (
          <DropdownMenu
            items={[
              {
                id: 'enteredInError',
                label: 'Entered in Error',
                description: 'Opened by mistake, or with the wrong test',
                icon: AppIcon.EnteredInError,
                variant: 'destructive' as const,
                onClick: () => setDialog({kind: 'caseError'}),
              },
            ]}
            alignment="end"
            button={{label: 'More', variant: 'secondary', icon: <Icon icon={AppIcon.Menu} />}}
          />
        )
      }
      dialogs={labCase && dialog && <LabCaseDialog dialog={dialog} labCase={labCase} onClose={close} />}>
      {labCase &&
        (isNarrow ? (
          <FoldingCard sections={[next(labCase), clock(labCase), details(labCase), steps(labCase), samples(labCase), report(labCase)]} />
        ) : (
          <TwoColumns
            start={<FoldingCard sections={[next(labCase), clock(labCase), details(labCase), report(labCase)]} />}
            end={<FoldingCard sections={[steps(labCase), samples(labCase)]} />}
          />
        ))}
    </Page>
  );

  function section(key: keyof typeof open, title: string, children: ReactNode): FoldingSection {
    return {
      key,
      title,
      isOpen: open[key],
      onToggle: () => toggle(key),
      children: (
        <Section variant="transparent" padding={6}>
          {children}
        </Section>
      ),
    };
  }

  function next(c: LabCase) {
    return section('next', "What's Next", <NextWork labCase={c} canWork={canWork} onOpen={setDialog} />);
  }
  function clock(c: LabCase) {
    return section('clock', 'Clock', <LabClockDetails clock={c.clock} />);
  }
  function details(c: LabCase) {
    return section('details', 'Details', <CaseDetails labCase={c} />);
  }
  function steps(c: LabCase) {
    return section('steps', 'Steps', <CaseSteps labCase={c} onOpen={setDialog} />);
  }
  function samples(c: LabCase) {
    return section('samples', 'Samples', <CaseSamples labCase={c} />);
  }
  function report(c: LabCase) {
    return section('report', 'Report', <CaseReport labCase={c} />);
  }
}

/** What the case needs next, and the buttons that record it (for the lab team), or why nothing's left to do. */
function NextWork({labCase, canWork, onOpen}: {labCase: LabCase; canWork: boolean; onOpen: (dialog: LabCaseDialogKind) => void}) {
  const next = labCase.next;
  if (!next || labCase.status !== 'Open')
    return (
      <Text type="body" color="secondary">
        {labCase.status === 'Reported'
          ? 'Nothing: the report has been authorised.'
          : labCase.status === 'RepeatRequested'
            ? 'Nothing here: a repeat sample was asked for. A new sample opens a new case.'
            : `Nothing: the case is ${labCaseStatusLabel(labCase.status).toLowerCase()}.`}
      </Text>
    );
  // A pathway's lab stage opened its case on arrival (MC16): it waits for the sample to be received at the lab.
  if (next.kind === 'Receive')
    return (
      <Text type="body" color="secondary">
        Waiting for the sample. Receiving it at the lab starts the case{labCase.labTestName ? '' : ', and the lab chooses the test'}.
      </Text>
    );
  if (next.kind === 'Choice')
    return (
      <VStack gap={3} hAlign="stretch">
        <Text type="body" weight="semibold">
          {next.choice?.question}
        </Text>
        <Text type="supporting" color="secondary">
          Record the outcome you decided. Nothing is chosen for you.
        </Text>
        {canWork && labCase.canRecord ? (
          <HStack gap={2} wrap="wrap">
            {(next.choice?.outcomes ?? []).map((outcome) => (
              <Button key={outcome} label={labOutcomeLabel(outcome)} variant="secondary" onClick={() => onOpen({kind: 'outcome', outcome})} />
            ))}
          </HStack>
        ) : (
          <Text type="body" color="secondary">
            The lab records it.
          </Text>
        )}
      </VStack>
    );
  if (next.kind === 'Report')
    return (
      <VStack gap={3} hAlign="start">
        <Text type="body">Every step is done. The results are entered and the report authorised by a holder of the permission to authorise lab reports.</Text>
        {labCase.canAuthorise && (
          <Button label="Enter Results and Authorise" variant="primary" icon={<Icon icon={AppIcon.SignOff} />} onClick={() => onOpen({kind: 'report'})} />
        )}
      </VStack>
    );
  return (
    <VStack gap={3} hAlign="start">
      <Text type="body" weight="semibold">
        {next.name}
      </Text>
      {canWork && labCase.canRecord ? (
        <Button label={`Record ${next.name}`} variant="primary" icon={<Icon icon={AppIcon.Success} />} onClick={() => onOpen({kind: 'step'})} />
      ) : (
        <Text type="body" color="secondary">
          The lab records it.
        </Text>
      )}
    </VStack>
  );
}

const shown = (value: string | null | undefined) => value?.trim() || '—';

function CaseDetails({labCase}: {labCase: LabCase}) {
  return (
    <MetadataList columns={2} label={{position: 'top'}}>
      <MetadataListItem label="Status">{labCaseStatusLabel(labCase.status)}</MetadataListItem>
      <MetadataListItem label="Test">{shown(labCase.labTestName)}</MetadataListItem>
      <MetadataListItem label="From">
        <HStack gap={2} vAlign="center" wrap="wrap">
          <SampleSourceToken source={labCase.source} />
          <Text type="body">{shown(labCase.source === 'Hospital' ? labCase.hospitalName : labCase.requestingTeamName)}</Text>
        </HStack>
      </MetadataListItem>
      <MetadataListItem label="Opened">{by(labCase.openedAtUtc, labCase.openedByUsername)}</MetadataListItem>
      {labCase.pathwayId ? (
        <>
          <MetadataListItem label="Pathway">
            <Link href={`/pathways/${labCase.pathwayId}`}>{labCase.pathwayTitle}</Link>
          </MetadataListItem>
          <MetadataListItem label="Stage">{shown(labCase.stageName)}</MetadataListItem>
        </>
      ) : (
        labCase.hospitalRequestId && (
          <MetadataListItem label="Hospital request">
            <Link href={`/hospital-requests/${labCase.hospitalRequestId}`}>Open the request</Link>
          </MetadataListItem>
        )
      )}
      {labCase.status === 'EnteredInError' && (
        <MetadataListItem label="Entered in error">{`${by(labCase.enteredInErrorAtUtc, labCase.enteredInErrorByUsername)}: ${shown(labCase.errorDetail)}`}</MetadataListItem>
      )}
    </MetadataList>
  );
}

/** Each step recorded, oldest first, with who and when; a step entered in error says so, and what was wrong. */
function CaseSteps({labCase, onOpen}: {labCase: LabCase; onOpen: (dialog: LabCaseDialogKind) => void}) {
  return (
    <List edge="flush" hasDividers aria-label="Steps">
      {(labCase.steps ?? []).map((step) => (
        <ListItem
          key={step.id}
          align="first-line"
          startContent={<Icon icon={step.isEnteredInError ? AppIcon.EnteredInError : AppIcon.Success} color="secondary" />}
          label={
            <Text type="body" weight="semibold" color={step.isEnteredInError ? 'secondary' : undefined}>
              {step.isEnteredInError ? `${stepLabel(step)} (entered in error)` : stepLabel(step)}
            </Text>
          }
          description={
            <VStack gap={0}>
              <Text type="body">{by(step.atUtc, step.byUsername)}</Text>
              {step.detail && (
                <Text type="supporting" color="secondary" style={PRESERVE_LINES}>
                  {step.detail}
                </Text>
              )}
              {step.isEnteredInError && (
                <Text type="supporting" color="secondary" style={PRESERVE_LINES}>
                  {`Entered in error ${by(step.enteredInErrorAtUtc, step.enteredInErrorByUsername)}: ${step.errorDetail ?? ''}`}
                </Text>
              )}
            </VStack>
          }
          endContent={
            step.canMarkEnteredInError && (
              <Button label="Entered in Error" variant="ghost" size="sm" onClick={() => onOpen({kind: 'stepError', step})} />
            )
          }
        />
      ))}
    </List>
  );
}

function CaseSamples({labCase}: {labCase: LabCase}) {
  const samples = labCase.samples ?? [];
  if (samples.length === 0)
    return (
      <Text type="body" color="secondary">
        {labCase.status === 'EnteredInError' ? 'Its samples left it when it was marked entered in error.' : 'No samples.'}
      </Text>
    );
  return (
    <List edge="flush" hasDividers aria-label="Samples">
      {samples.map((s) => (
        <ListItem
          key={s.id}
          label={
            <Text type="body" weight="semibold">
              <Link href={`/specimens/${s.id}`}>{s.number}</Link>
            </Text>
          }
          description={
            <Text type="supporting" color="secondary">
              {`${s.specimenTypeName} · taken ${formatDateTime(s.takenAtUtc ?? '')}`}
            </Text>
          }
          endContent={<SpecimenStatusToken status={s.status} />}
        />
      ))}
    </List>
  );
}

/** The results as the lab entered them, who authorised them and when, and the clinic's result; or the repeat asked for. */
function CaseReport({labCase}: {labCase: LabCase}) {
  if (labCase.status === 'RepeatRequested')
    return (
      <VStack gap={2} hAlign="stretch">
        <Text type="body">{`A repeat sample was asked for ${by(labCase.repeatRequestedAtUtc, labCase.repeatRequestedByUsername)}.`}</Text>
        <Text type="body" style={PRESERVE_LINES}>
          {shown(labCase.repeatDetail)}
        </Text>
      </VStack>
    );
  if (!labCase.reportText)
    return (
      <Text type="body" color="secondary">
        No report yet.
      </Text>
    );
  return (
    <VStack gap={3} hAlign="stretch">
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Authorised">{by(labCase.reportedAtUtc, labCase.reportedByUsername)}</MetadataListItem>
        <MetadataListItem label="Goes back to">
          {labCase.resultId ? (
            <Link href={`/results/${labCase.resultId}`}>{`${shown(labCase.requestingTeamName)}: its result`}</Link>
          ) : (
            shown(labCase.source === 'Hospital' ? labCase.hospitalName : labCase.requestingTeamName)
          )}
        </MetadataListItem>
      </MetadataList>
      <VStack gap={1} hAlign="stretch">
        <Text type="body" weight="semibold">
          Results
        </Text>
        <Text type="body" style={PRESERVE_LINES}>
          {labCase.reportText}
        </Text>
      </VStack>
    </VStack>
  );
}
