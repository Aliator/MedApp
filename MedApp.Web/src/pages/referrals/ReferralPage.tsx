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
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {ApiError, ApiValidationError, problemMessage, type ValidationErrors} from '../../api/errors';
import {whyCantChange} from '../../api/patients';
import {
  isOutsideMyTeams,
  referralEventWords,
  referralQuery,
  referralStatusLabel,
  rejectionReasonWords,
  useReferralActions,
  type Referral,
  type ReferralEvent,
} from '../../api/referrals';
import {Attachments} from '../../components/attachments/Attachments';
import {useAttachmentsState} from '../../components/attachments/useAttachmentsState';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PriorityToken} from '../../components/pathways/PathwayCells';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AcceptReferralDialog} from '../../components/referrals/AcceptReferralDialog';
import {CloseReferralDialog, type ReferralClosing} from '../../components/referrals/CloseReferralDialog';
import {PatientReferrals} from '../../components/referrals/PatientReferrals';
import {RedirectReferralDialog} from '../../components/referrals/RedirectReferralDialog';
import {ReferralStatusToken} from '../../components/referrals/ReferralCells';
import {ReferralInformationDialog, type ReferralInformationStep} from '../../components/referrals/ReferralInformationDialog';
import {RejectReferralDialog} from '../../components/referrals/RejectReferralDialog';
import {LettersAbout} from '../../components/letters/LettersAbout';
import {ClockPauseList, WaitingClockDetails} from '../../components/referrals/WaitingClock';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDate, formatDateTime} from '../../utils/dates';

/** The statuses triage ends in: a referral in one of them has been triaged, and isn't triaged again (decision 23). */
const TRIAGED = ['Accepted', 'Rejected', 'RedirectedElsewhere'];

/**
 * What the Triage section says: that it's been triaged, or closed without triage; for a triager, what they can do; for
 * anyone else, the API's reason they can't (`whyCantTriage`: not a member of the team, or without the permission).
 */
export function triageWords(referral: Pick<Referral, 'isOpen' | 'status' | 'canTriage' | 'whyCantTriage'>): string {
  if (!referral.isOpen) {
    const status = referralStatusLabel(referral.status).toLowerCase();
    return TRIAGED.includes(referral.status ?? '')
      ? `This referral has been triaged: ${status}. A referral isn't triaged again.`
      : `This referral is ${status}, so it isn't triaged.`;
  }
  if (referral.canTriage)
    return referral.status === 'InformationRequested'
      ? 'More information has been asked of the referrer. Record it when it arrives; meanwhile you can still accept, reject or redirect it.'
      : 'Read the referral and its letter, then decide: accept it into a pathway, reject it, or redirect it.';
  return referral.whyCantTriage?.trim() || 'Only a member of its team who can triage referrals can accept, reject or redirect it.';
}

const ANNOUNCED: Record<ReferralClosing, string> = {withdraw: 'Referral withdrawn', enteredInError: 'Referral marked entered in error'};

/** A triage decision being made: which dialog is open (asking for more information, or recording it came, included). */
type Triage = 'accept' | 'reject' | 'redirect' | ReferralInformationStep;

/**
 * One referral (`/referrals/$id`): the patient's banner, then its details as typed from the letter (the team, when
 * it was received, the referrer and the reason), what became of it, the letter (attached here: "No letter attached
 * yet" until it is), triage and its history in words. While it awaits triage, Edit Details, and in the More menu
 * Withdraw and Entered in Error, each asking for a detail with the patient shown, as the API's flags (`canEdit`,
 * `canClose`) allow. A change someone made meanwhile is a 409, shown in the API's words with Reload.
 *
 * **Triage** (decisions 18 to 25): for whoever can triage it (`canTriage`: TriageReferrals, and a member of its
 * team), Accept, Reject and Redirect, each a form then Check and Confirm with the patient shown (hazard H-41).
 * Nothing is suggested: the clinician decides and the app records. Anyone else is told why they can't
 * (`whyCantTriage`). Accepting opens the new pathway's page.
 *
 * The patient's other referrals (every status) sit under Triage, so a possible duplicate is seen before deciding
 * (hazard H-44).
 *
 * The waiting-time clock sits under the details: the wait, the target date, Breached, Paused or Stopped and why, and
 * its pauses (paused and resumed from its pathway's page).
 *
 * Two cards side by side (details, waiting time and the letter; triage, the other referrals and history), or in a
 * narrow window one card of six folding sections; either way the cards scroll, not the page.
 */
export function ReferralPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const navigate = useNavigate();
  const me = useCurrentUser();
  const {data: referral, error, isPending, refetch} = useQuery(referralQuery(id));
  const actions = useReferralActions(id);
  const attachments = useAttachmentsState();
  const [closing, setClosing] = useState<ReferralClosing | null>(null);
  const [triage, setTriage] = useState<Triage | null>(null);
  const [dialogKey, setDialogKey] = useState(0);
  const [closeErrors, setCloseErrors] = useState<ValidationErrors | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [open, setOpen] = useState({details: true, wait: true, letter: true, letters: true, triage: true, others: true, history: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const isMissing = error instanceof ApiError && error.status === 404;
  const closeMutation = closing === 'enteredInError' ? actions.enteredInError : actions.withdraw;

  const startClosing = (next: ReferralClosing) => {
    setActionError(null);
    setCloseErrors(null);
    setDialogKey((key) => key + 1);
    setClosing(next);
  };

  /** Opens a triage dialog. Each is mounted only while open, so its choices start empty every time. */
  const startTriage = (next: Triage) => {
    setActionError(null);
    setTriage(next);
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
      title={referral ? `Referral to ${referral.teamName}` : 'Referral'}
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
          <>
            <CloseReferralDialog
              key={dialogKey}
              closing={closing}
              referral={referral}
              isProcessing={closeMutation.isPending}
              errors={closeErrors}
              onConfirm={confirmClose}
              onCancel={() => setClosing(null)}
            />
            {triage === 'accept' && (
              <AcceptReferralDialog
                isOpen
                referral={referral}
                onCancel={() => setTriage(null)}
                onAccepted={(pathway) => {
                  announce('Referral accepted: pathway created');
                  setTriage(null);
                  void navigate({to: '/pathways/$id', params: {id: pathway.id ?? ''}});
                }}
              />
            )}
            {triage === 'reject' && (
              <RejectReferralDialog
                isOpen
                referral={referral}
                onCancel={() => setTriage(null)}
                onRejected={() => {
                  announce('Referral rejected');
                  setTriage(null);
                }}
              />
            )}
            {(triage === 'ask' || triage === 'received') && (
              <ReferralInformationDialog
                step={triage}
                referral={referral}
                onCancel={() => setTriage(null)}
                onDone={() => {
                  announce(triage === 'ask' ? 'More information asked for' : 'Information received');
                  setTriage(null);
                }}
              />
            )}
            {triage === 'redirect' && (
              <RedirectReferralDialog
                isOpen
                referral={referral}
                onCancel={() => setTriage(null)}
                onRedirected={(redirected) => {
                  announce(redirected.isOpen ? `Referral redirected to ${redirected.teamName}` : 'Referral redirected elsewhere');
                  setTriage(null);
                  // Now another team's: this team no longer sees it (the team walls), so back to the list.
                  if (redirected.isOpen && isOutsideMyTeams(me.teams, redirected.teamId)) void navigate({to: '/referrals'});
                }}
              />
            )}
          </>
        )
      }>
      {referral &&
        (isNarrow ? (
          <FoldingCard
            sections={[details(referral), waitingTime(referral), letter(referral), letters(referral), triageSection(referral), others(referral), history(referral)]}
          />
        ) : (
          <TwoColumns
            start={<FoldingCard sections={[details(referral), waitingTime(referral), letter(referral), letters(referral)]} />}
            end={<FoldingCard sections={[triageSection(referral), others(referral), history(referral)]} />}
          />
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

  /**
   * The waiting-time clock (decisions 27 to 38): the wait against the team's target, the target date, Breached,
   * Paused (with its review date) or Stopped and why, and every pause. Pausing and resuming are on its pathway's page,
   * where the pathway's target date moves with them.
   */
  function waitingTime(referral: Referral): FoldingSection {
    const canChange = Boolean(referral.pathwayId) && (referral.canPauseClock || referral.canResumeClock);
    return {
      key: 'wait',
      title: 'Waiting time',
      isOpen: open.wait,
      onToggle: () => toggle('wait'),
      children: (
        <Section variant="transparent" padding={6}>
          {referral.clock ? (
            <VStack gap={6} hAlign="stretch">
              <WaitingClockDetails clock={referral.clock} />
              <VStack gap={2} hAlign="stretch">
                <Text type="body" weight="semibold">
                  Pauses
                </Text>
                <ClockPauseList pauses={referral.pauses ?? []} />
              </VStack>
              {canChange && (
                <Text type="body" color="secondary">
                  {referral.canResumeClock ? 'Resume the clock from ' : 'Pause the clock from '}
                  <Link href={`/pathways/${referral.pathwayId}`}>its pathway</Link>.
                </Text>
              )}
            </VStack>
          ) : (
            <Text type="body" color="secondary">
              This referral has no waiting-time clock.
            </Text>
          )}
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

  /**
   * Letters written about the referral: replies and requests for more information (decision 106), each linking to its
   * page, and Write Letter for holders of WriteLetters while the record can change.
   */
  function letters(referral: Referral): FoldingSection {
    return {
      key: 'letters',
      title: 'Letters',
      isOpen: open.letters,
      onToggle: () => toggle('letters'),
      children: (
        <Section variant="transparent" padding={6}>
          <LettersAbout about={{referralId: referral.id ?? id}} teamId={referral.teamId} patient={referral.patient} isLocked={isLocked} />
        </Section>
      ),
    };
  }

  /**
   * What triage says, and for a triager (`canTriage`, and still open) Accept, Reject and Redirect, with Ask for More
   * Information while it awaits triage, and Information Received once more has been asked for.
   */
  function triageSection(referral: Referral): FoldingSection {
    const canTriage = (referral.canTriage ?? false) && (referral.isOpen ?? false);
    return {
      key: 'triage',
      title: 'Triage',
      isOpen: open.triage,
      onToggle: () => toggle('triage'),
      children: (
        <Section variant="transparent" padding={6}>
          <VStack gap={4} hAlign="stretch">
            <Text type="body" color="secondary">
              {triageWords(referral)}
            </Text>
            {canTriage && (
              <HStack gap={2} wrap="wrap">
                <Button label="Accept" variant="primary" icon={<Icon icon={AppIcon.Success} />} onClick={() => startTriage('accept')} />
                <Button label="Reject" variant="secondary" icon={<Icon icon={AppIcon.StatusCancelled} />} onClick={() => startTriage('reject')} />
                <Button label="Redirect" variant="secondary" icon={<Icon icon={AppIcon.StatusChange} />} onClick={() => startTriage('redirect')} />
                {referral.status === 'AwaitingTriage' && (
                  <Button label="Ask for More Information" variant="secondary" icon={<Icon icon={AppIcon.Info} />} onClick={() => startTriage('ask')} />
                )}
                {referral.status === 'InformationRequested' && (
                  <Button label="Information Received" variant="secondary" icon={<Icon icon={AppIcon.Success} />} onClick={() => startTriage('received')} />
                )}
              </HStack>
            )}
          </VStack>
        </Section>
      ),
    };
  }

  /** The patient's other referrals, in every status, so a triager spots a duplicate before deciding (hazard H-44). */
  function others(referral: Referral): FoldingSection {
    return {
      key: 'others',
      title: "This patient's other referrals",
      isOpen: open.others,
      onToggle: () => toggle('others'),
      children: (
        <Section variant="transparent" padding={6}>
          <PatientReferrals
            patientId={referral.patientId ?? ''}
            canRegister={false}
            exceptReferralId={referral.id ?? id}
            sameTeamId={referral.teamId}
          />
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

/** The latest event of an action in a referral's history (the API sends it oldest first). */
const latest = (referral: Referral, action: string): ReferralEvent | undefined => (referral.history ?? []).filter((e) => e.action === action).at(-1);

/** What the referrer asked for, who registered it, and what became of it. */
function ReferralDetails({referral}: {referral: Referral}) {
  const isDecided = Boolean(referral.decidedAtUtc);
  const accepted = referral.status === 'Accepted' ? latest(referral, 'Accepted') : undefined;
  const redirectedFrom = latest(referral, 'TeamChanged')?.previousTeamName;
  return (
    <VStack gap={6} hAlign="stretch">
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Status">
          <ReferralStatusToken status={referral.status} />
        </MetadataListItem>
        <MetadataListItem label="Priority asked for">
          <PriorityToken priority={referral.requestedPriority ?? ''} />
        </MetadataListItem>
        <MetadataListItem label="Team">{shown(referral.teamName)}</MetadataListItem>
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
      {referral.redirectDetail?.trim() && (
        <VStack gap={1} hAlign="stretch">
          <Text type="body" weight="semibold">
            {`Redirected from ${redirectedFrom ?? 'another team'}`}
          </Text>
          <Text type="body" style={PRESERVE_LINES}>
            {referral.redirectDetail}
          </Text>
        </VStack>
      )}
      {isDecided && (
        <VStack gap={1} hAlign="stretch">
          <Text type="body" weight="semibold">
            Outcome
          </Text>
          <Text type="body">
            {`${referralStatusLabel(referral.status)} ${formatDateTime(referral.decidedAtUtc ?? '')} by ${referral.decidedByUsername ?? 'unknown'}`}
          </Text>
          {accepted && (accepted.templateName || accepted.priority) && (
            <Text type="body">
              {[accepted.templateName ? `Template: ${accepted.templateName}` : null, accepted.priority ? `Priority: ${accepted.priority}` : null]
                .filter(Boolean)
                .join(', ')}
            </Text>
          )}
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
