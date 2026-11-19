import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Thumbnail} from '@astryxdesign/core/Thumbnail';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {attachmentContentUrl, attachmentThumbnailUrl} from '../../api/attachments';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiError, problemMessage} from '../../api/errors';
import {resultActionLabel, resultQuery, resultSourceLabel, useResultActions, type Result} from '../../api/results';
import {useAnnounce} from '../../app/announcements';
import {useCan, useCurrentUser} from '../../auth/auth';
import {DownloadLink} from '../../components/attachments/Attachments';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {patientDetailsSection} from '../../components/patients/DetailsPatient';
import {hasPatientFlags, PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {ageWords, NeedsClinicianToken, ResultStatusToken} from '../../components/results/ResultCells';
import {
  RESPONSIBLE_PERMISSION,
  ReassignResultDialog,
  ResultEnteredInErrorDialog,
  SignOffResultDialog,
  TakeOverResultDialog,
} from '../../components/results/ResultDialogs';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDateTime} from '../../utils/dates';

/** The dialog the page has open. */
type Dialog = 'signOff' | 'takeOver' | 'reassign' | 'enteredInError' | null;

const by = (at: string | null | undefined, who: string | null | undefined) => (at ? `${formatDateTime(at)}${who ? ` by ${who}` : ''}` : 'Not yet');

/** A report's thumbnail: the API's small re-encoded image (never the file itself), at a fixed size. */
const REPORT_THUMBNAIL = {width: 'var(--spacing-32)', height: 'var(--spacing-32)'} as const;

/** Typed text keeps the lines it was typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/**
 * One result (`/results/$id`; decisions 91 to 97): the patient's flags, then Details (the patient first, never folding) and its details (where it came
 * from, linking to its specimen and pathway; who reported it, when and how many days ago; the responsible clinician,
 * with Needs a clinician; the status), the report (its thumbnail for a picture, and Download with its file name; never
 * shown inline), and its sign-off (seen, signed off with what was done, or entered in error). What the caller may do
 * follows the response's `can...` flags: **Mark as Seen**, **Sign Off**, **Take Over** (when it isn't theirs),
 * **Reassign** and, under More, **Entered in Error**. Each sends the version shown; a change made meanwhile is the API's
 * 409, in its words, and the result is read again. **MedApp never reads or interprets the report**: nothing here is
 * coloured or flagged by what it says. Opening it is recorded as viewing it.
 */
export function ResultPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  const {data: result, error, isPending} = useQuery(resultQuery(id));
  const [dialog, setDialog] = useState<Dialog>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [open, setOpen] = useState({report: true, signOff: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const isMissing = error instanceof ApiError && error.status === 404;
  const close = () => setDialog(null);

  return (
    <Page
      title={result ? (result.title ?? 'Result') : 'Result'}
      subtitle={result ? (result.specimenNumber ? `Sample ${result.specimenNumber}` : resultSourceLabel(result.source)) : undefined}
      backHref={result?.pathwayId ? `/pathways/${result.pathwayId}` : '/results'}
      backLabel={result?.pathwayId ? 'Back to pathway' : 'Back to results'}
      isLoading={isPending}
      loadingMessage="Loading result..."
      fill={result !== undefined}
      // No banner: the patient is at the top of Details, which never folds and comes first (as on the pathway page).
      message={
        isMissing ? (
          <AlertMessage message="Result not found. It may have been deleted." />
        ) : error ? (
          <AlertMessage message="The result could not be loaded. Please try again." />
        ) : problem ? (
          <AlertMessage message={problem} />
        ) : result?.patientFlags && hasPatientFlags(result.patientFlags) ? (
          <PatientFlagBanners patient={result.patientFlags} />
        ) : (
          result?.readOnlyReason && <SummaryAlert status="info" label="Read-only result" message={result.readOnlyReason} />
        )
      }
      actions={result && <Actions result={result} onOpen={setDialog} onProblem={setProblem} />}
      // Each dialog is mounted only while it's open, so it starts empty every time.
      dialogs={
        result && (
          <>
            {dialog === 'signOff' && <SignOffResultDialog result={result} onClose={close} />}
            {dialog === 'reassign' && <ReassignResultDialog result={result} onClose={close} />}
            {dialog === 'takeOver' && <TakeOverDialog result={result} onClose={close} />}
            {dialog === 'enteredInError' && <ResultEnteredInErrorDialog result={result} onClose={close} />}
          </>
        )
      }>
      {result &&
        (isNarrow ? (
          <FoldingCard sections={[details(result), report(result), signOff(result)]} />
        ) : (
          <TwoColumns start={<FoldingCard sections={[details(result), report(result)]} />} end={<FoldingCard sections={[signOff(result)]} />} />
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

  function details(result: Result) {
    return patientDetailsSection({patientId: result.patientId, patient: result.patient}, <ResultDetails result={result} />);
  }

  function report(result: Result) {
    return section('report', 'Report', <ResultReport result={result} />);
  }

  function signOff(result: Result) {
    return section('signOff', 'Sign-off', <ResultSignOff result={result} />);
  }
}

/** The caller's own ID among the holders of SignOffResults (who can be responsible), to take a result over. */
function useMyClinicianId(isEnabled: boolean) {
  const user = useCurrentUser();
  const staff = useQuery({...staffWithPermissionQuery(RESPONSIBLE_PERMISSION), enabled: isEnabled});
  return staff.data?.find((member) => member.username === user.username)?.userId;
}

/** Take Over, with the caller's own ID. */
function TakeOverDialog({result, onClose}: {result: Result; onClose: () => void}) {
  const myId = useMyClinicianId(true);
  if (!myId) return null;
  return <TakeOverResultDialog result={result} userId={myId} onClose={onClose} />;
}

interface ActionsProps {
  result: Result;
  onOpen: (dialog: Dialog) => void;
  onProblem: (problem: string | null) => void;
}

/** What the caller may do now, by the result's `can...` flags: the sign-off's steps, then Entered in Error under More. */
function Actions({result, onOpen, onProblem}: ActionsProps) {
  const user = useCurrentUser();
  const canSignOffResults = useCan('SignOffResults');
  const announce = useAnnounce();
  const {seen} = useResultActions(result.id ?? '');
  const isMine = result.responsibleUsername === user.username;
  const canReassign = Boolean(result.canReassign && result.isAwaitingSignOff);
  const myId = useMyClinicianId(canReassign && !isMine && canSignOffResults);
  const canTakeOver = canReassign && !isMine && canSignOffResults && Boolean(myId);

  const markSeen = () => {
    onProblem(null);
    seen.mutate(
      {expectedVersion: result.version ?? 0},
      {
        onSuccess: () => announce('Result marked seen'),
        onError: (e) => onProblem(problemMessage(e, 'The result could not be marked seen. Please try again.')),
      },
    );
  };

  const more: DropdownMenuItemData[] = result.canMarkEnteredInError
    ? [
        {
          id: 'enteredInError',
          label: 'Entered in Error',
          description: 'On the wrong patient or sample',
          icon: AppIcon.EnteredInError,
          variant: 'destructive' as const,
          onClick: () => onOpen('enteredInError'),
        },
      ]
    : [];

  return (
    <>
      {result.canMarkSeen && (
        <Button label="Mark as Seen" variant="secondary" icon={<Icon icon={AppIcon.View} />} isLoading={seen.isPending} onClick={markSeen} />
      )}
      {canTakeOver && <Button label="Take Over" variant="secondary" icon={<Icon icon={AppIcon.Assign} />} onClick={() => onOpen('takeOver')} />}
      {canReassign && <Button label="Reassign" variant="secondary" icon={<Icon icon={AppIcon.Reassign} />} onClick={() => onOpen('reassign')} />}
      {result.canSignOff && <Button label="Sign Off" variant="primary" icon={<Icon icon={AppIcon.SignOff} />} onClick={() => onOpen('signOff')} />}
      {more.length > 0 && <DropdownMenu items={more} alignment="end" button={{label: 'More', variant: 'secondary', icon: <Icon icon={AppIcon.Menu} />}} />}
    </>
  );
}

const shown = (value: string | null | undefined) => value?.trim() || '—';

/** Where it came from, who reported it and when, who must sign it off, and where it stands. */
function ResultDetails({result}: {result: Result}) {
  return (
    <MetadataList columns={2} label={{position: 'top'}}>
      <MetadataListItem label="Status">
        <HStack gap={2} vAlign="center" wrap="wrap">
          <ResultStatusToken status={result.status} />
        </HStack>
      </MetadataListItem>
      <MetadataListItem label="Responsible clinician">
        <VStack gap={1} hAlign="start">
          <Text type="body">{shown(result.responsibleUsername)}</Text>
          {result.needsClinician && <NeedsClinicianToken />}
        </VStack>
      </MetadataListItem>
      <MetadataListItem label="Source">
        {result.specimenId ? (
          <VStack gap={0} hAlign="start">
            <Link href={`/specimens/${result.specimenId}`}>{`Sample ${result.specimenNumber ?? ''}`.trim()}</Link>
            {result.specimenTypeName && (
              <Text type="supporting" color="secondary">
                {result.specimenTypeName}
              </Text>
            )}
          </VStack>
        ) : (
          resultSourceLabel(result.source)
        )}
      </MetadataListItem>
      <MetadataListItem label="Pathway">
        {result.pathwayId ? <Link href={`/pathways/${result.pathwayId}`}>{result.pathwayTitle}</Link> : 'Not on a pathway'}
      </MetadataListItem>
      <MetadataListItem label="Reported">{by(result.reportedAtUtc, result.reportedByUsername)}</MetadataListItem>
      <MetadataListItem label="Age">{ageWords(result.ageDays)}</MetadataListItem>
    </MetadataList>
  );
}

/**
 * The report: its thumbnail for a picture (the API's small re-encoded image, decorative) and Download with its file
 * name. Never shown inline: Download downloads. Once it's entered in error, its file is deleted and it says so.
 */
function ResultReport({result}: {result: Result}) {
  // A lab case's report (MC15; the light demo's is the results the lab entered, as text).
  if (result.reportText && result.status !== 'EnteredInError')
    return (
      <VStack gap={2} hAlign="stretch">
        <Text type="body" style={{whiteSpace: 'pre-wrap'}}>
          {result.reportText}
        </Text>
        {result.labCaseId && <Link href={`/lab/cases/${result.labCaseId}`}>{`Lab case ${result.labCaseNumber ?? ''}`.trim()}</Link>}
      </VStack>
    );
  if (!result.reportFileName)
    return (
      <Text type="body" color="secondary">
        The report&apos;s file was deleted when the result was marked entered in error.
      </Text>
    );
  return (
    <VStack gap={3} hAlign="start">
      {result.reportHasThumbnail && <Thumbnail src={attachmentThumbnailUrl(result.reportAttachmentId ?? '')} alt="" aria-hidden style={REPORT_THUMBNAIL} />}
      <Button
        label={`Download ${result.reportFileName}`}
        variant="secondary"
        icon={<Icon icon={AppIcon.Download} />}
        href={attachmentContentUrl(result.reportAttachmentId ?? '')}
        as={DownloadLink}
      />
    </VStack>
  );
}

/** Seen, signed off (with what was done and its detail) or entered in error (with what was wrong), each by whom and when. */
function ResultSignOff({result}: {result: Result}) {
  if (result.status === 'EnteredInError')
    return (
      <VStack gap={3} hAlign="stretch">
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Entered in error">{by(result.enteredInErrorAtUtc, result.enteredInErrorByUsername)}</MetadataListItem>
          <MetadataListItem label="Seen">{by(result.seenAtUtc, result.seenByUsername)}</MetadataListItem>
        </MetadataList>
        <Detail label="What was wrong" text={result.errorDetail} />
      </VStack>
    );
  return (
    <VStack gap={3} hAlign="stretch">
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Seen">{by(result.seenAtUtc, result.seenByUsername)}</MetadataListItem>
        <MetadataListItem label="Signed off">{by(result.signedOffAtUtc, result.signedOffByUsername)}</MetadataListItem>
        {result.action && <MetadataListItem label="What was done">{resultActionLabel(result.action)}</MetadataListItem>}
      </MetadataList>
      {result.actionDetail?.trim() && <Detail label="Detail" text={result.actionDetail} />}
    </VStack>
  );
}

function Detail({label, text}: {label: string; text: string | null | undefined}) {
  return (
    <VStack gap={1} hAlign="stretch">
      <Text type="body" weight="semibold">
        {label}
      </Text>
      <Text type="body" style={PRESERVE_LINES}>
        {shown(text)}
      </Text>
    </VStack>
  );
}
