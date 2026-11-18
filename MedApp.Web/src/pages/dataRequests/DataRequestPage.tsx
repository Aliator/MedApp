import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {
  dataRequestQuery,
  dataRequestStatusWords,
  identityCheckMethodWords,
  packProblem,
  reviewItemKindWords,
  sendMethodWords,
  useDataRequestPack,
  type DataRequest,
} from '../../api/dataRequests';
import {ApiError} from '../../api/errors';
import {patientQuery} from '../../api/patients';
import {DataRequestStatusToken, OverdueToken} from '../../components/dataRequests/DataRequestCells';
import {
  extensionChoices,
  ExtendDialog,
  IdentityCheckDialog,
  RecordSentDialog,
  RefuseDialog,
  WithdrawDialog,
} from '../../components/dataRequests/DataRequestDialogs';
import {DataRequestReviewDialog} from '../../components/dataRequests/DataRequestReviewDialog';
import {Page} from '../../components/pages/Page';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDate, formatDateTime} from '../../utils/dates';

/** The dialog open, if any. */
type Dialog = 'identity' | 'extend' | 'sent' | 'refuse' | 'withdraw' | 'review' | null;

/** What's announced when each is done. */
const DONE: Record<NonNullable<Dialog>, string> = {
  identity: 'Identity check recorded',
  extend: 'Due date extended',
  sent: 'Data request recorded as sent',
  refuse: 'Data request refused',
  withdraw: 'Data request withdrawn',
  review: 'Review saved',
};

/** Plain text keeps the lines it was written with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/** Who asked, in words: the patient, or someone on their behalf with their relationship. */
export function askedByWords(request: Pick<DataRequest, 'requestedByPatient' | 'requesterName' | 'requesterRelationship'>): string {
  if (request.requestedByPatient) return 'The patient';
  const relationship = request.requesterRelationship ? ` (${request.requesterRelationship})` : '';
  return `${request.requesterName || 'Someone on their behalf'}${relationship}`;
}

/** How a request ended, in words, or null while it's open. */
export function outcomeWords(request: DataRequest): string | null {
  const by = request.closedByUsername ? ` by ${request.closedByUsername}` : '';
  switch (request.status) {
    case 'Sent':
      return `Sent ${formatDate(request.sentOn ?? '')}${request.sendMethod ? `, ${sendMethodWords(request.sendMethod).toLowerCase()}` : ''}${by}`;
    case 'Refused':
      return `Refused${request.closedAtUtc ? ` ${formatDateTime(request.closedAtUtc)}` : ''}${by}`;
    case 'Withdrawn':
      return `Withdrawn${request.closedAtUtc ? ` ${formatDateTime(request.closedAtUtc)}` : ''}${by}`;
    default:
      return null;
  }
}

/**
 * One data request (`/data-requests/$id`): the patient's banner, then the request (when it was received and who
 * asked), its due date (the first, and any extension with its reason), the identity check, the review (who,
 * when, and what was withheld with why) and how it ended.
 *
 * - **Its actions follow the response's flags only,** which the API gives only to holders of ManagePrivacy (admins:
 *   only they deal with data requests). While it's open (`canManage`): Record Identity Check, Extend Due Date (while an
 *   extension is left), Record as Sent, Refuse and Withdraw, each from a dialog showing the patient and sending the
 *   version the page showed; and (`canReview`) Review, which lists the record's items to withhold under the serious harm
 *   test, on an appropriate health professional's opinion taken outside MedApp.
 * - **Download Pack** (`canMakePack`) makes the pack now and saves it; it's never stored. Otherwise the page says why it
 *   can't be made yet (`packBlockedReason`).
 */
export function DataRequestPage({id}: {id: string}) {
  const announce = useAnnounce();
  const {data: request, error, isPending} = useQuery(dataRequestQuery(id));
  const patient = useQuery({...patientQuery(request?.patientId ?? ''), enabled: Boolean(request?.patientId)});
  const pack = useDataRequestPack();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [open, setOpen] = useState({request: true, due: true, identity: true, review: true, outcome: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const isMissing = error instanceof ApiError && error.status === 404;
  const bannerPatient = patient.data ? {...patient.data, id: request?.patientId} : null;

  const download = () => {
    setActionError(null);
    pack.mutate(id, {
      onSuccess: () => announce('Pack downloaded'),
      onError: (e) => setActionError(packProblem(e)),
    });
  };

  const done = (kind: NonNullable<Dialog>) => () => {
    announce(DONE[kind]);
    setDialog(null);
  };

  return (
    <Page
      title="Data Request"
      subtitle={request ? `${request.patientName} · ${dataRequestStatusWords(request.status)}` : undefined}
      backHref="/data-requests"
      backLabel="Back to data requests"
      isLoading={isPending}
      loadingMessage="Loading data request..."
      fill={request !== undefined}
      banner={bannerPatient && <PatientBanner patient={bannerPatient} />}
      message={
        isMissing ? (
          <AlertMessage message="Data request not found." />
        ) : error ? (
          <AlertMessage message="The data request could not be loaded. Please try again." />
        ) : (
          actionError && <AlertMessage message={actionError} onDismiss={() => setActionError(null)} />
        )
      }
      actions={request && headerActions(request)}
      dialogs={request && dialogs(request)}>
      {request && (
        <FoldingCard sections={[requestSection(request), dueSection(request), identitySection(request), reviewSection(request), outcomeSection(request)]} />
      )}
    </Page>
  );

  function headerActions(request: DataRequest) {
    const isChecked = Boolean(request.identityCheckedOn);
    const more: DropdownMenuItemData[] = request.canManage
      ? [
          ...(isChecked ? [{id: 'identity', label: 'Change Identity Check', icon: AppIcon.IdentityCheck, onClick: () => setDialog('identity')}] : []),
          ...(extensionChoices(request).length > 0
            ? [{id: 'extend', label: 'Extend Due Date', description: 'Up to two more months', icon: AppIcon.Wait, onClick: () => setDialog('extend')}]
            : []),
          {id: 'refuse', label: 'Refuse', icon: AppIcon.StatusCancelled, variant: 'destructive' as const, onClick: () => setDialog('refuse')},
          {id: 'withdraw', label: 'Withdraw', description: 'The requester withdrew it', icon: AppIcon.Withdraw, onClick: () => setDialog('withdraw')},
        ]
      : [];
    return (
      <>
        {request.canMakePack && (
          <Button label="Download Pack" variant="secondary" icon={<Icon icon={AppIcon.Download} />} isLoading={pack.isPending} onClick={download} />
        )}
        {request.canReview && <Button label="Review" variant={request.reviewedAtUtc ? 'secondary' : 'primary'} icon={<Icon icon={AppIcon.SignOff} />} onClick={() => setDialog('review')} />}
        {request.canManage && !isChecked && (
          <Button label="Record Identity Check" variant="primary" icon={<Icon icon={AppIcon.IdentityCheck} />} onClick={() => setDialog('identity')} />
        )}
        {request.canManage && (
          <Button label="Record as Sent" variant={request.canMakePack ? 'primary' : 'secondary'} icon={<Icon icon={AppIcon.Send} />} onClick={() => setDialog('sent')} />
        )}
        {more.length > 0 && <DropdownMenu items={more} alignment="end" button={{label: 'More', variant: 'secondary', icon: <Icon icon={AppIcon.Menu} />}} />}
      </>
    );
  }

  function dialogs(request: DataRequest) {
    if (!dialog) return null;
    const props = {request, patient: bannerPatient, onCancel: () => setDialog(null), onDone: done(dialog)};
    switch (dialog) {
      case 'identity':
        return <IdentityCheckDialog {...props} />;
      case 'extend':
        return <ExtendDialog {...props} />;
      case 'sent':
        return <RecordSentDialog {...props} />;
      case 'refuse':
        return <RefuseDialog {...props} />;
      case 'withdraw':
        return <WithdrawDialog {...props} />;
      default:
        return <DataRequestReviewDialog {...props} />;
    }
  }

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

  function requestSection(request: DataRequest) {
    return section(
      'request',
      'Request',
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Status">
          <DataRequestStatusToken status={request.status} />
        </MetadataListItem>
        <MetadataListItem label="Received">{formatDate(request.receivedOn ?? '')}</MetadataListItem>
        <MetadataListItem label="Asked by">{askedByWords(request)}</MetadataListItem>
        <MetadataListItem label="Logged">{`${formatDateTime(request.createdAtUtc ?? '')} by ${request.createdByUsername}`}</MetadataListItem>
      </MetadataList>,
    );
  }

  function dueSection(request: DataRequest) {
    const isExtended = (request.extensionMonths ?? 0) > 0;
    return section(
      'due',
      'Due date',
      <VStack gap={4} hAlign="stretch">
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Due">
            <HStack gap={2} vAlign="center" wrap="wrap">
              <Text type="body">{formatDate(request.dueOn ?? '')}</Text>
              {request.isOverdue && <OverdueToken />}
            </HStack>
          </MetadataListItem>
          <MetadataListItem label="First due">{formatDate(request.firstDueOn ?? '')}</MetadataListItem>
          <MetadataListItem label="Extended">
            {isExtended ? `By ${request.extensionMonths === 1 ? 'one month' : `${request.extensionMonths} months`}` : 'No'}
          </MetadataListItem>
        </MetadataList>
        {isExtended && request.extensionReason && <Reason label="Why it was extended" text={request.extensionReason} />}
      </VStack>,
    );
  }

  function identitySection(request: DataRequest) {
    return section(
      'identity',
      'Identity check',
      request.identityCheckedOn ? (
        <VStack gap={4} hAlign="stretch">
          <MetadataList columns={2} label={{position: 'top'}}>
            <MetadataListItem label="Checked on">{formatDate(request.identityCheckedOn)}</MetadataListItem>
            <MetadataListItem label="How">{identityCheckMethodWords(request.identityCheckMethod)}</MetadataListItem>
            <MetadataListItem label="By">{request.identityCheckedByUsername || '—'}</MetadataListItem>
          </MetadataList>
          {request.identityCheckDetail && <Reason label="Detail" text={request.identityCheckDetail} />}
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          Not checked yet. The pack can't be made until it is.
        </Text>
      ),
    );
  }

  function reviewSection(request: DataRequest) {
    const withheld = request.withheld ?? [];
    return section(
      'review',
      'Clinician review',
      request.reviewedAtUtc ? (
        <VStack gap={4} hAlign="stretch">
          <MetadataList columns={2} label={{position: 'top'}}>
            <MetadataListItem label="Reviewed">{formatDateTime(request.reviewedAtUtc)}</MetadataListItem>
            <MetadataListItem label="By">{request.reviewedByUsername || '—'}</MetadataListItem>
          </MetadataList>
          {withheld.length === 0 ? (
            <Text type="body">Nothing withheld.</Text>
          ) : (
            <VStack gap={1} hAlign="stretch">
              <Text type="body" weight="semibold">
                {withheld.length === 1 ? 'One item withheld' : `${withheld.length} items withheld`}
              </Text>
              <List edge="flush" density="compact" hasDividers aria-label="Withheld">
                {withheld.map((item) => (
                  <ListItem key={`${item.kind}:${item.itemId}`} label={reviewItemKindWords(item.kind)} description={item.reason ?? ''} />
                ))}
              </List>
            </VStack>
          )}
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          Not reviewed yet. A clinician reviews what goes in the pack before it can be made.
        </Text>
      ),
    );
  }

  function outcomeSection(request: DataRequest) {
    const outcome = outcomeWords(request);
    return section(
      'outcome',
      'Outcome',
      <VStack gap={4} hAlign="stretch">
        {outcome ? <Text type="body">{outcome}</Text> : <Text type="body">Open: not yet answered.</Text>}
        {request.status === 'Refused' && request.refusalReason && <Reason label="Reason given" text={request.refusalReason} />}
        {request.status === 'Open' && !request.canMakePack && request.packBlockedReason && (
          <Text type="body" color="secondary">
            {`Pack: ${request.packBlockedReason}`}
          </Text>
        )}
        {request.canMakePack && (
          <Text type="body" color="secondary">
            The pack is ready to make: the report as a PDF, the record as JSON, and the files, leaving out what the review withheld. It&apos;s made when
            downloaded and never stored.
          </Text>
        )}
      </VStack>,
    );
  }
}

/** A reason or detail in words, under its label, keeping its lines. */
function Reason({label, text}: {label: string; text: string}) {
  return (
    <VStack gap={1} hAlign="stretch">
      <Text type="body" weight="semibold">
        {label}
      </Text>
      <Text type="body" style={PRESERVE_LINES}>
        {text}
      </Text>
    </VStack>
  );
}
