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
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError, ApiValidationError, problemMessage, type ValidationErrors} from '../../api/errors';
import {whyCantChange} from '../../api/patients';
import {referralEventWords, referralQuery, referralStatusLabel, useReferralActions, type Referral} from '../../api/referrals';
import {Attachments} from '../../components/attachments/Attachments';
import {useAttachmentsState} from '../../components/attachments/useAttachmentsState';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PriorityToken} from '../../components/pathways/PathwayCells';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {CloseReferralDialog, type ReferralClosing} from '../../components/referrals/CloseReferralDialog';
import {ReferralStatusToken} from '../../components/referrals/ReferralCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDate, formatDateTime} from '../../utils/dates';

/** Why a rejected referral was rejected, in words. */
const REJECTION_REASONS: Record<string, string> = {
  NotOfferedHere: 'Not offered here',
  DoesNotMeetCriteria: "Doesn't meet the criteria",
  InsufficientInformation: 'Not enough information',
  Duplicate: 'A duplicate',
  Other: 'Other',
};

export const rejectionReasonWords = (reason: string | null | undefined) => (reason ? (REJECTION_REASONS[reason] ?? reason) : '');

/** What triage says for now: it isn't built yet (a later release), or why the referral can't be triaged. */
export function triageWords(referral: Pick<Referral, 'isOpen' | 'status' | 'whyCantTriage'>): string {
  if (!referral.isOpen) return `This referral is ${referralStatusLabel(referral.status).toLowerCase()}, so it isn't triaged.`;
  return referral.whyCantTriage?.trim() || "Triage isn't available in MedApp yet: a clinician of the specialty will accept, reject or redirect it in a later release.";
}

const ANNOUNCED: Record<ReferralClosing, string> = {withdraw: 'Referral withdrawn', enteredInError: 'Referral marked entered in error'};

/**
 * One referral (`/referrals/$id`): the patient's banner, then its details as typed from the letter (the specialty, when
 * it was received, the referrer and the reason), what became of it, the letter (attached here: "No letter attached
 * yet" until it is), triage (not built yet: it says so, or why it can't be triaged) and its history in words. While it
 * awaits triage, Edit Details, and in the More menu Withdraw and Entered in Error, each asking for a detail with the
 * patient shown, as the API's flags (`canEdit`, `canClose`) allow. A change someone made meanwhile is a 409, shown in
 * the API's words with Reload.
 *
 * Two cards side by side (details and the letter; triage and history), or in a narrow window one card of four folding
 * sections; either way the cards scroll, not the page.
 */
export function ReferralPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const {data: referral, error, isPending, refetch} = useQuery(referralQuery(id));
  const actions = useReferralActions(id);
  const attachments = useAttachmentsState();
  const [closing, setClosing] = useState<ReferralClosing | null>(null);
  const [dialogKey, setDialogKey] = useState(0);
  const [closeErrors, setCloseErrors] = useState<ValidationErrors | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [open, setOpen] = useState({details: true, letter: true, triage: true, history: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const isMissing = error instanceof ApiError && error.status === 404;
  const closeMutation = closing === 'enteredInError' ? actions.enteredInError : actions.withdraw;

  const startClosing = (next: ReferralClosing) => {
    setActionError(null);
    setCloseErrors(null);
    setDialogKey((key) => key + 1);
    setClosing(next);
  };

  const confirmClose = (detail: string) => {
    if (!closing || !referral) return;
    const which = closing;
    closeMutation.mutate(
      {detail, expectedVersion: referral.version ?? 0},
      {
        onSuccess: () => {
          announce(ANNOUNCED[which]);
          setClosing(null);
        },
        onError: (e) => {
          if (e instanceof ApiValidationError) {
            setCloseErrors(e.errors);
            return;
          }
          // A change made meanwhile (or the record restricted) is the API's 409, in its words; the referral is read again.
          setClosing(null);
          setActionError(problemMessage(e, 'The referral could not be changed. Please try again.'));
        },
      },
    );
  };

  const reload = () => {
    setActionError(null);
    void refetch();
  };

  const isLocked = referral?.patientFlags ? whyCantChange(referral.patientFlags) !== null : false;

  return (
    <Page
      title={referral ? `Referral to ${referral.specialtyName}` : 'Referral'}
      backHref="/referrals"
      backLabel="Back to referrals"
      isLoading={isPending}
      loadingMessage="Loading referral..."
      fill={referral !== undefined}
      banner={referral?.patient && <PatientBanner patient={referral.patient} />}
      message={
        isMissing ? (
          <AlertMessage message="Referral not found. It may have been deleted." />
        ) : error ? (
          <AlertMessage message="The referral could not be loaded. Please try again." />
        ) : actionError ? (
          <VStack gap={2} hAlign="stretch">
            <AlertMessage message={actionError} onDismiss={() => setActionError(null)} />
            <HStack>
              <Button label="Reload" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={reload} />
            </HStack>
          </VStack>
        ) : referral?.patientFlags && (referral.patientFlags.isRestricted || referral.patientFlags.isDisputed || referral.patientFlags.isAnonymised) ? (
          <PatientFlagBanners patient={referral.patientFlags} />
        ) : (
          referral?.readOnlyReason && <SummaryAlert status="info" label="Read-only referral" message={referral.readOnlyReason} />
        )
      }
      actions={referral && <Actions referral={referral} isBusy={closeMutation.isPending} onClose={startClosing} />}
      dialogs={
        referral && (
          <CloseReferralDialog
            key={dialogKey}
            closing={closing}
            referral={referral}
            isProcessing={closeMutation.isPending}
            errors={closeErrors}
            onConfirm={confirmClose}
            onCancel={() => setClosing(null)}
          />
        )
      }>
      {referral &&
        (isNarrow ? (
          <FoldingCard sections={[details(referral), letter(referral), triage(referral), history(referral)]} />
        ) : (
          <TwoColumns start={<FoldingCard sections={[details(referral), letter(referral)]} />} end={<FoldingCard sections={[triage(referral), history(referral)]} />} />
        ))}
    </Page>
  );

  function details(referral: Referral): FoldingSection {
    return {
      key: 'details',
      title: 'Details',
      isOpen: open.details,
      onToggle: () => toggle('details'),
      children: (
        <Section variant="transparent" padding={6}>
          <ReferralDetails referral={referral} />
        </Section>
      ),
    };
  }

  /** The letter: files attached to the referral. Added while `canAttachFiles`; never while the record is locked. */
  function letter(referral: Referral): FoldingSection {
    return {
      key: 'letter',
      title: 'Letter',
      isOpen: open.letter,
      onToggle: () => toggle('letter'),
      children: (
        <Section variant="transparent" padding={6}>
          <Attachments
            target={{referralId: referral.id ?? id}}
            canChange={referral.canAttachFiles ?? false}
            isLocked={isLocked}
            state={attachments}
            emptyText="No letter attached yet."
          />
        </Section>
      ),
    };
  }

  function triage(referral: Referral): FoldingSection {
    return {
      key: 'triage',
      title: 'Triage',
      isOpen: open.triage,
      onToggle: () => toggle('triage'),
      children: (
        <Section variant="transparent" padding={6}>
          <Text type="body" color="secondary">
            {triageWords(referral)}
          </Text>
        </Section>
      ),
    };
  }

  function history(referral: Referral): FoldingSection {
    return {
      key: 'history',
      title: 'History',
      isOpen: open.history,
      onToggle: () => toggle('history'),
      children: (
        <Section variant="transparent" padding={6}>
          <ReferralHistory referral={referral} />
        </Section>
      ),
    };
  }
}

/** Edit Details, and in a More menu Withdraw and Entered in Error, as the API's flags allow; and View Patient. */
function Actions({referral, isBusy, onClose}: {referral: Referral; isBusy: boolean; onClose: (closing: ReferralClosing) => void}) {
  const items: DropdownMenuItemData[] = [
    {id: 'withdraw', label: 'Withdraw', description: 'The referrer or the patient withdrew it', icon: AppIcon.Withdraw, onClick: () => onClose('withdraw')},
    {
      id: 'enteredInError',
      label: 'Entered in Error',
      description: 'On the wrong patient, or registered twice',
      icon: AppIcon.EnteredInError,
      variant: 'destructive',
      onClick: () => onClose('enteredInError'),
    },
  ];
  return (
    <>
      <Button label="View Patient" variant="secondary" icon={<Icon icon={AppIcon.View} />} href={`/patients/${referral.patientId}`} />
      {referral.canEdit && (
        <Button label="Edit Details" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} href={`/referrals/${referral.id}/edit`} isDisabled={isBusy} />
      )}
      {referral.canClose && <DropdownMenu items={items} alignment="end" button={{label: 'More', variant: 'secondary', icon: <Icon icon={AppIcon.Menu} />}} />}
    </>
  );
}

const shown = (value: string | null | undefined) => value?.trim() || '—';

/** What the referrer asked for, who registered it, and what became of it. */
function ReferralDetails({referral}: {referral: Referral}) {
  const isDecided = Boolean(referral.decidedAtUtc);
  return (
    <VStack gap={6} hAlign="stretch">
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Status">
          <ReferralStatusToken status={referral.status} />
        </MetadataListItem>
        <MetadataListItem label="Priority asked for">
          <PriorityToken priority={referral.requestedPriority ?? ''} />
        </MetadataListItem>
        <MetadataListItem label="Specialty">{shown(referral.specialtyName)}</MetadataListItem>
        <MetadataListItem label="Received">{referral.receivedOn ? formatDate(referral.receivedOn) : '—'}</MetadataListItem>
        {/* "(from the directory)" when staff chose it from the GP practice directory, not only typed it. */}
        <MetadataListItem label="Practice">
          {referral.practiceId ? `${shown(referral.referrerPractice)} (from the directory)` : shown(referral.referrerPractice)}
        </MetadataListItem>
        <MetadataListItem label="Clinician">{shown(referral.referrerClinician)}</MetadataListItem>
        <MetadataListItem label="ODS code">{shown(referral.referrerOdsCode)}</MetadataListItem>
        <MetadataListItem label="Registered">
          {`${formatDateTime(referral.registeredAtUtc ?? '')} by ${referral.registeredByUsername ?? 'unknown'}`}
        </MetadataListItem>
        <MetadataListItem label="Letters attached">{String(referral.letterCount ?? 0)}</MetadataListItem>
      </MetadataList>
      <VStack gap={1} hAlign="stretch">
        <Text type="body" weight="semibold">
          Reason for referral
        </Text>
        <Text type="body" style={PRESERVE_LINES}>
          {shown(referral.reason)}
        </Text>
      </VStack>
      {isDecided && (
        <VStack gap={1} hAlign="stretch">
          <Text type="body" weight="semibold">
            Outcome
          </Text>
          <Text type="body">
            {`${referralStatusLabel(referral.status)} ${formatDateTime(referral.decidedAtUtc ?? '')} by ${referral.decidedByUsername ?? 'unknown'}`}
          </Text>
          {referral.rejectionReason && <Text type="body">{`Why: ${rejectionReasonWords(referral.rejectionReason)}`}</Text>}
          {referral.redirectedTo && <Text type="body">{`Redirected to ${referral.redirectedTo}`}</Text>}
          {referral.decisionDetail && (
            <Text type="body" style={PRESERVE_LINES}>
              {referral.decisionDetail}
            </Text>
          )}
          {referral.pathwayId && (
            <Text type="body">
              <Link href={`/pathways/${referral.pathwayId}`}>Open its pathway</Link>
            </Text>
          )}
        </VStack>
      )}
    </VStack>
  );
}

/** The reason and details keep the lines they were typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/** Everything that happened to it, oldest first, in words: who did what, and when. */
function ReferralHistory({referral}: {referral: Referral}) {
  const events = referral.history ?? [];
  if (events.length === 0)
    return (
      <Text type="body" color="secondary">
        Nothing recorded yet.
      </Text>
    );
  return (
    <List edge="flush" hasDividers aria-label="History">
      {events.map((event) => {
        const {title, detail} = referralEventWords(event);
        return (
          <ListItem
            key={event.id}
            align="first-line"
            label={
              <Text type="body" weight="semibold">
                {title}
              </Text>
            }
            description={
              <VStack gap={0}>
                {detail && <Text type="body">{detail}</Text>}
                <Text type="supporting" color="secondary">
                  {formatDateTime(event.atUtc ?? '')}
                </Text>
              </VStack>
            }
          />
        );
      })}
    </List>
  );
}
