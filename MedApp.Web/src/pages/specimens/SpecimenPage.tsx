import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {attachmentContentUrl, attachmentsQuery} from '../../api/attachments';
import {ApiError} from '../../api/errors';
import {
  rejectionReasonLabel,
  sampleSourceName,
  specimenPrintHref,
  specimenQuery,
  specimenStatusLabel,
  type Specimen,
} from '../../api/specimens';
import {useCan} from '../../auth/auth';
import {AttachmentRow, DownloadLink} from '../../components/attachments/Attachments';
import {labClockLine} from '../../components/labCases/LabClock';
import {OpenLabCaseDialog} from '../../components/labCases/OpenLabCaseDialog';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {patientDetailsSection} from '../../components/patients/DetailsPatient';
import {hasPatientFlags, PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {ReportSpecimenDialog} from '../../components/specimens/ReportSpecimenDialog';
import {OverdueAtLabToken, SampleSourceToken, SpecimenStatusToken} from '../../components/specimens/SpecimenCells';
import {SpecimenDetailsDialog} from '../../components/specimens/SpecimenDetailsDialog';
import {SpecimenStepDialog, type SpecimenStep} from '../../components/specimens/SpecimenStepDialog';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDateTime} from '../../utils/dates';
import {specimenTurnarounds} from '../../utils/specimenTimes';

/** The dialog the page has open: a step, Edit, Attach Report, or Open Lab Case. */
type Dialog = {kind: 'step'; step: SpecimenStep} | {kind: 'edit'} | {kind: 'report'} | {kind: 'openCase'} | null;

/** One step of a specimen's journey, as its page lists them. */
export interface SpecimenStepLine {
  label: string;
  /** "3 Nov 2026, 10:00 by k.ashford", or "Not yet". */
  words: string;
  /** A reference, reason or detail under it. */
  details: string[];
  isDone: boolean;
}

const by = (at: string | null | undefined, who: string | null | undefined) =>
  at ? `${formatDateTime(at)}${who ? ` by ${who}` : ''}` : 'Not yet';

/**
 * A specimen's steps, in order: Taken, Sent, Received, then Reported, or how it was closed (rejected by the lab, lost or
 * entered in error) in place of the steps it never reached. Each says when and who, with its reference; a closing says
 * why (the reason, and the detail typed).
 */
export function specimenSteps(specimen: Specimen): SpecimenStepLine[] {
  const steps: SpecimenStepLine[] = [
    {label: 'Taken', words: by(specimen.takenAtUtc, specimen.takenByUsername), details: [], isDone: true},
    {
      label: 'Sent',
      words: by(specimen.sentAtUtc, specimen.sentByUsername),
      details: specimen.courierReference ? [`Courier or batch reference: ${specimen.courierReference}`] : [],
      isDone: Boolean(specimen.sentAtUtc),
    },
    {
      label: 'Received',
      words: by(specimen.receivedAtUtc, specimen.receivedByUsername),
      details: specimen.labReference ? [`Lab's reference: ${specimen.labReference}`] : [],
      isDone: Boolean(specimen.receivedAtUtc),
    },
  ];
  const isClosedEarly = specimen.status === 'RejectedByLab' || specimen.status === 'Lost' || specimen.status === 'EnteredInError';
  if (!isClosedEarly)
    return [
      ...steps,
      {label: 'Reported', words: by(specimen.reportedAtUtc, specimen.reportedByUsername), details: [], isDone: Boolean(specimen.reportedAtUtc)},
    ];
  const closing: SpecimenStepLine = {
    label: specimenStatusLabel(specimen.status),
    words: by(specimen.closedAtUtc, specimen.closedByUsername),
    details: [
      specimen.rejectionReason ? `Reason: ${rejectionReasonLabel(specimen.rejectionReason)}` : null,
      specimen.closedDetail?.trim() || null,
    ].filter((line): line is string => Boolean(line)),
    isDone: true,
  };
  // The steps it reached, then how it closed.
  return [...steps.filter((step) => step.isDone), closing];
}

/** The reasons each action is offered, beside the specimen's own flags: the permission it needs. */
function useOffered(specimen: Specimen | undefined) {
  const canRecord = useCan('RecordSpecimens');
  const canProcess = useCan('ProcessSpecimens');
  return {
    edit: Boolean(specimen?.canEdit) && canRecord,
    sent: Boolean(specimen?.canMarkSent) && canRecord,
    received: Boolean(specimen?.canMarkReceived) && canProcess,
    report: Boolean(specimen?.canReport) && canProcess,
    // A received sample with no lab case (MC15): the lab opens one, choosing the test.
    openCase: Boolean(specimen?.canOpenLabCase) && canProcess,
    rejected: Boolean(specimen?.canReject) && canProcess,
    lost: Boolean(specimen?.canMarkLost) && (canRecord || canProcess),
    // Whoever recorded it, in its first 24 hours, or a holder of CorrectRecords: the API's flag says which.
    enteredInError: Boolean(specimen?.canMarkEnteredInError),
  };
}

/**
 * One specimen (`/specimens/$id`): its Details, with the patient first (never folding), then its details (type, lab, status, when the report is
 * expected and Overdue at lab, who asked for it, where it was taken and the notes for the lab), its steps with who and
 * when, the turnaround between them, the report (a download, with its thumbnail for a picture) and the other specimens
 * its stage took. Print Label and Print Request Sheet open the print views. The steps it can take next are offered as
 * its `can...` flags and the person's permissions allow: Edit, Mark Sent, Mark Received, Attach Report, and in More,
 * Reject, Mark Lost and Entered in Error. Each sends the version shown; a change made meanwhile is the API's 409, in its
 * words, and the specimen is read again. Opening it is recorded as viewing it.
 *
 * Turnaround and Overdue at lab are date arithmetic on the recorded times (decision 88): nothing here is coloured or
 * flagged by what the specimen showed.
 */
export function SpecimenPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  const {data: specimen, error, isPending} = useQuery(specimenQuery(id));
  const offered = useOffered(specimen);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [open, setOpen] = useState({steps: true, report: true, others: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const isMissing = error instanceof ApiError && error.status === 404;

  const close = () => setDialog(null);

  return (
    <Page
      title={specimen ? `Sample ${specimen.number}` : 'Sample'}
      subtitle={specimen?.specimenTypeName ?? undefined}
      backHref={specimen?.pathwayId ? `/pathways/${specimen.pathwayId}` : '/lab/samples'}
      backLabel={specimen?.pathwayId ? 'Back to pathway' : 'Back to samples'}
      isLoading={isPending}
      loadingMessage="Loading sample..."
      fill={specimen !== undefined}
      // No banner: the patient is at the top of Details, which never folds and comes first (as on the pathway page).
      message={
        isMissing ? (
          <AlertMessage label="Sample not found" message="Sample not found. It may have been deleted." />
        ) : error ? (
          <AlertMessage label="Sample not loaded" message="The sample could not be loaded. Please try again." />
        ) : specimen?.patientFlags && hasPatientFlags(specimen.patientFlags) ? (
          <PatientFlagBanners patient={specimen.patientFlags} />
        ) : (
          specimen?.readOnlyReason && <SummaryAlert status="info" label="Read-only sample" message={specimen.readOnlyReason} />
        )
      }
      actions={specimen && <Actions specimen={specimen} offered={offered} onOpen={setDialog} />}
      // Each dialog is mounted only while it's open, so it starts afresh (at now, and empty) every time, and nothing
      // time-based is rendered on the server.
      dialogs={
        specimen && (
          <>
            {dialog?.kind === 'step' && <SpecimenStepDialog step={dialog.step} specimen={specimen} onClose={close} />}
            {dialog?.kind === 'edit' && <SpecimenDetailsDialog isOpen onClose={close} patient={specimen.patient} specimen={specimen} />}
            {dialog?.kind === 'report' && <ReportSpecimenDialog isOpen specimen={specimen} onClose={close} />}
            {dialog?.kind === 'openCase' && <OpenLabCaseDialog specimen={specimen} onClose={close} />}
          </>
        )
      }>
      {specimen &&
        (isNarrow ? (
          <FoldingCard sections={[details(specimen), steps(specimen), report(specimen), others(specimen)]} />
        ) : (
          <TwoColumns start={<FoldingCard sections={[details(specimen), report(specimen)]} />} end={<FoldingCard sections={[steps(specimen), others(specimen)]} />} />
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

  function details(specimen: Specimen) {
    return patientDetailsSection({patientId: specimen.patientId, patient: specimen.patient}, <SpecimenDetails specimen={specimen} />);
  }

  function steps(specimen: Specimen) {
    return section('steps', 'Steps', <SpecimenSteps specimen={specimen} />);
  }

  function report(specimen: Specimen) {
    return section('report', 'Report', <SpecimenReport specimen={specimen} />);
  }

  function others(specimen: Specimen) {
    return section('others', 'Others from this stage', <OtherSpecimens specimen={specimen} />);
  }
}

interface ActionsProps {
  specimen: Specimen;
  offered: ReturnType<typeof useOffered>;
  onOpen: (dialog: Dialog) => void;
}

/** Print Label and Print Request Sheet, then the next steps the specimen can take, with the closing ones under More. */
function Actions({specimen, offered, onOpen}: ActionsProps) {
  const closing: DropdownMenuItemData[] = [
    ...(offered.rejected
      ? [{id: 'rejected', label: 'Reject', description: "The lab can't test it", icon: AppIcon.StatusCancelled, onClick: () => onOpen({kind: 'step', step: 'rejected'})}]
      : []),
    ...(offered.lost ? [{id: 'lost', label: 'Mark Lost', description: "It can't be found", icon: AppIcon.Warning, onClick: () => onOpen({kind: 'step', step: 'lost'})}] : []),
    ...(offered.enteredInError
      ? [
          {
            id: 'enteredInError',
            label: 'Entered in Error',
            description: 'Recorded by mistake, or on the wrong patient',
            icon: AppIcon.EnteredInError,
            variant: 'destructive' as const,
            onClick: () => onOpen({kind: 'step', step: 'enteredInError'}),
          },
        ]
      : []),
  ];
  return (
    <>
      <Button label="Print Label" variant="secondary" icon={<Icon icon={AppIcon.Print} />} href={specimenPrintHref(specimen.id ?? '', 'label')} />
      <Button
        label="Print Request Sheet"
        variant="secondary"
        icon={<Icon icon={AppIcon.Print} />}
        href={specimenPrintHref(specimen.id ?? '', 'request')}
      />
      {offered.edit && <Button label="Edit" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} onClick={() => onOpen({kind: 'edit'})} />}
      {offered.sent && (
        <Button label="Mark Sent" variant="primary" icon={<Icon icon={AppIcon.SpecimenSent} />} onClick={() => onOpen({kind: 'step', step: 'sent'})} />
      )}
      {offered.received && (
        <Button
          label="Mark Received"
          variant="primary"
          icon={<Icon icon={AppIcon.SpecimenReceived} />}
          onClick={() => onOpen({kind: 'step', step: 'received'})}
        />
      )}
      {offered.report && <Button label="Attach Report" variant="primary" icon={<Icon icon={AppIcon.Upload} />} onClick={() => onOpen({kind: 'report'})} />}
      {offered.openCase && (
        <Button label="Open Lab Case" variant="primary" icon={<Icon icon={AppIcon.LabCases} />} onClick={() => onOpen({kind: 'openCase'})} />
      )}
      {closing.length > 0 && <DropdownMenu items={closing} alignment="end" button={{label: 'More', variant: 'secondary', icon: <Icon icon={AppIcon.Menu} />}} />}
    </>
  );
}

const shown = (value: string | null | undefined) => value?.trim() || '—';

/** The specimen's details: where it is, where it came from, and what was asked of the lab. */
function SpecimenDetails({specimen}: {specimen: Specimen}) {
  return (
    <VStack gap={6} hAlign="stretch">
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Status">
          <HStack gap={2} vAlign="center" wrap="wrap">
            <SpecimenStatusToken status={specimen.status} />
            {specimen.isOverdueAtLab && <OverdueAtLabToken />}
          </HStack>
        </MetadataListItem>
        {specimen.clock ? (
          // In a lab case: its strict clock (MC19), the sooner of the lab's target and the hospital's date.
          <MetadataListItem label="Due (its lab case's clock)">{labClockLine(specimen.clock)}</MetadataListItem>
        ) : (
          <MetadataListItem label="Expected by">
            {`${formatDateTime(specimen.expectedByUtc ?? '')} (${specimen.expectedTurnaroundDays} ${specimen.expectedTurnaroundDays === 1 ? 'day' : 'days'})`}
          </MetadataListItem>
        )}
        <MetadataListItem label="From">
          <HStack gap={2} vAlign="center" wrap="wrap">
            <SampleSourceToken source={specimen.source} />
            <Text type="body">{shown(sampleSourceName(specimen))}</Text>
          </HStack>
        </MetadataListItem>
        <MetadataListItem label="Test">{shown(specimen.labTestName)}</MetadataListItem>
        <MetadataListItem label="Lab case">
          {specimen.labCaseId ? <Link href={`/lab/cases/${specimen.labCaseId}`}>{specimen.labCaseNumber ?? 'Open the case'}</Link> : 'None yet'}
        </MetadataListItem>
        <MetadataListItem label="Type">{shown(specimen.specimenTypeName)}</MetadataListItem>
        <MetadataListItem label={specimen.source === 'Hospital' ? 'Registered by' : 'Requesting clinician'}>{shown(specimen.requestingUsername)}</MetadataListItem>
        <MetadataListItem label="Site">{shown(specimen.site)}</MetadataListItem>
        {specimen.pathwayId ? (
          <>
            <MetadataListItem label="Pathway">
              <Link href={`/pathways/${specimen.pathwayId}`}>{specimen.pathwayTitle}</Link>
            </MetadataListItem>
            <MetadataListItem label="Stage">{shown(specimen.stageName)}</MetadataListItem>
          </>
        ) : (
          specimen.hospitalRequestId && (
            <MetadataListItem label="Hospital request">
              <Link href={`/hospital-requests/${specimen.hospitalRequestId}`}>Open the request</Link>
            </MetadataListItem>
          )
        )}
      </MetadataList>
      <VStack gap={1} hAlign="stretch">
        <Text type="body" weight="semibold">
          Notes for the lab
        </Text>
        <Text type="body" style={PRESERVE_LINES}>
          {shown(specimen.requestNotes)}
        </Text>
      </VStack>
    </VStack>
  );
}

/** Notes keep the lines they were typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/** Each step with who and when, then the turnaround between them (date arithmetic only). */
function SpecimenSteps({specimen}: {specimen: Specimen}) {
  const turnarounds = specimenTurnarounds(specimen);
  return (
    <VStack gap={6} hAlign="stretch">
      <List edge="flush" hasDividers aria-label="Steps">
        {specimenSteps(specimen).map((step) => (
          <ListItem
            key={step.label}
            align="first-line"
            startContent={<Icon icon={step.isDone ? AppIcon.Success : AppIcon.StatusUnassigned} color="secondary" />}
            label={
              <Text type="body" weight="semibold">
                {step.label}
              </Text>
            }
            description={
              <VStack gap={0}>
                <Text type="body" color={step.isDone ? undefined : 'secondary'}>
                  {step.words}
                </Text>
                {step.details.map((detail) => (
                  <Text key={detail} type="supporting" color="secondary" style={PRESERVE_LINES}>
                    {detail}
                  </Text>
                ))}
              </VStack>
            }
          />
        ))}
      </List>
      {turnarounds.length > 0 && (
        <VStack gap={2} hAlign="stretch">
          <Text type="body" weight="semibold">
            Turnaround
          </Text>
          <MetadataList columns={2} label={{position: 'top'}}>
            {turnarounds.map((turnaround) => (
              <MetadataListItem key={turnaround.label} label={turnaround.label}>
                {turnaround.words}
              </MetadataListItem>
            ))}
          </MetadataList>
        </VStack>
      )}
    </VStack>
  );
}

/**
 * The lab's report, as the patient's files list it (its thumbnail for a picture, its name and who attached it, and
 * Download), or a plain download link when the list doesn't have it. Never shown inline: Download downloads.
 */
function SpecimenReport({specimen}: {specimen: Specimen}) {
  const reportId = specimen.reportAttachmentId;
  const files = useQuery({...attachmentsQuery({patientId: specimen.patientId ?? ''}), enabled: Boolean(reportId)});
  if (!reportId)
    return (
      <Text type="body" color="secondary">
        {specimen.isOpen ? 'No report yet.' : 'No report: the sample was closed without one.'}
      </Text>
    );
  const file = files.data?.items?.find((attachment) => attachment.id === reportId);
  if (file)
    return (
      <List edge="flush" aria-label="Report">
        <AttachmentRow attachment={file} isNarrow showsAttachedTo canDelete={false} />
      </List>
    );
  return (
    <HStack>
      <Button label="Download Report" variant="secondary" icon={<Icon icon={AppIcon.Download} />} href={attachmentContentUrl(reportId)} as={DownloadLink} />
    </HStack>
  );
}

/** The other specimens its stage took (a rejected or lost one's retake, say), oldest first. */
function OtherSpecimens({specimen}: {specimen: Specimen}) {
  const others = specimen.otherSpecimensFromStage ?? [];
  if (others.length === 0)
    return (
      <Text type="body" color="secondary">
        {specimen.stageId ? 'No other samples were taken at this stage.' : 'A hospital sample has no pathway stage.'}
      </Text>
    );
  return (
    <List edge="flush" hasDividers aria-label="Others from this stage">
      {others.map((other) => (
        <ListItem
          key={other.id}
          label={
            <Text type="body" weight="semibold">
              <Link href={`/specimens/${other.id}`}>{other.number}</Link>
            </Text>
          }
          description={
            <Text type="supporting" color="secondary">
              {`${other.specimenTypeName} · taken ${formatDateTime(other.takenAtUtc ?? '')}`}
            </Text>
          }
          endContent={<SpecimenStatusToken status={other.status} />}
        />
      ))}
    </List>
  );
}
