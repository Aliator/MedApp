import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {attachmentContentUrl} from '../../api/attachments';
import {consentFormLabel, consentStatusLabel, pathwayConsentQuery, signedByLabel, type ConsentRecord} from '../../api/consent';
import type {Pathway} from '../../api/pathways';
import {useCan} from '../../auth/auth';
import {formatDate} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {ConsentActionDialog, type ConsentAction} from './ConsentActionDialog';
import {RecordConsentDialog} from './RecordConsentDialog';

const STATUS_COLORS: Record<string, TokenColor> = {Valid: 'green', Withdrawn: 'orange', EnteredInError: 'gray'};

/** A consent record's status in words, coloured by status only: it says nothing about whether the consent is valid in law. */
export function ConsentStatusToken({status}: {status: string | null | undefined}) {
  return <Token size="sm" label={consentStatusLabel(status)} color={STATUS_COLORS[status ?? ''] ?? 'default'} />;
}

/**
 * A pathway's consent records (the pathway page's Consent section), oldest first: the stage, the form and its version,
 * when and by whom it was signed, its status, its scan (or "No scan"), and "Withdrawn after the stage was finished"
 * (decision 147: flagged, nothing changes by itself). Record Consent (holders of RecordConsent, while a stage can take
 * it) and each record's Withdraw, Entered in Error and Add Scan follow the API's flags. MedApp records consent; it
 * never assesses it. It lays out no padding of its own.
 */
export function PathwayConsent({pathway}: {pathway: Pathway}) {
  const canRecord = useCan('RecordConsent');
  const {data, isPending, isError} = useQuery(pathwayConsentQuery(pathway.id ?? ''));
  const [isRecording, setIsRecording] = useState(false);
  const [acting, setActing] = useState<{action: ConsentAction; record: ConsentRecord} | null>(null);

  if (isError) return <AlertMessage message="The consent records could not be loaded. Please try again." />;
  if (isPending)
    return (
      <Text type="body" color="secondary">
        Loading consent...
      </Text>
    );

  const recordable = (pathway.stages ?? []).filter((s) => s.canRecordConsent);
  const needing = (pathway.stages ?? []).filter((s) => s.needsConsent);
  const records = data ?? [];

  return (
    <VStack gap={3} hAlign="stretch">
      <HStack gap={3} vAlign="center" wrap="wrap" justify="between">
        <Text type="supporting" color="secondary">
          {needing.length === 0
            ? 'No stage of this pathway needs consent recorded.'
            : `Needs consent recorded: ${needing.map((s) => s.name).join(', ')}.`}
        </Text>
        {canRecord && recordable.length > 0 && (
          <Button
            label="Record Consent"
            variant="secondary"
            size="sm"
            icon={<Icon icon={AppIcon.Consent} />}
            onClick={() => setIsRecording(true)}
          />
        )}
      </HStack>
      {records.length === 0 ? (
        <Text type="body" color="secondary">
          No consent recorded.
        </Text>
      ) : (
        <List edge="flush" hasDividers aria-label="Consent records">
          {records.map((record) => (
            <ListItem
              key={record.id}
              align="first-line"
              label={
                <Text type="body" weight="semibold">
                  {`${record.stageName}: ${consentFormLabel(record.formName, record.formVersion)}`}
                </Text>
              }
              description={
                <VStack gap={1}>
                  <Text type="supporting" color="secondary">
                    {`Signed ${formatDate(record.signedOn ?? '')} · ${signedByLabel(record.signedBy)} · recorded by ${record.recordedByUsername ?? 'Deleted user'}`}
                  </Text>
                  {record.status === 'Withdrawn' && (
                    <Text type="supporting" color="secondary">
                      {`Withdrawn ${formatDate(record.withdrawnOn ?? '')}: ${record.statusDetail ?? ''}`}
                    </Text>
                  )}
                  {record.status === 'EnteredInError' && (
                    <Text type="supporting" color="secondary">
                      {`Entered in error: ${record.statusDetail ?? ''}`}
                    </Text>
                  )}
                  {record.scanAttachmentId && record.status !== 'EnteredInError' && (
                    <Text type="supporting">
                      <Link href={attachmentContentUrl(record.scanAttachmentId)}>Download the scanned form</Link>
                    </Text>
                  )}
                  <HStack gap={2} wrap="wrap">
                    {record.canWithdraw && (
                      <Button label="Withdraw" variant="secondary" size="sm" onClick={() => setActing({action: 'withdraw', record})} />
                    )}
                    {record.canAttachScan && (
                      <Button label="Add Scan" variant="secondary" size="sm" onClick={() => setActing({action: 'attachScan', record})} />
                    )}
                    {record.canMarkEnteredInError && record.status !== 'EnteredInError' && (
                      <Button label="Entered in Error" variant="secondary" size="sm" onClick={() => setActing({action: 'enteredInError', record})} />
                    )}
                  </HStack>
                </VStack>
              }
              endContent={
                <HStack gap={2} vAlign="center" wrap="wrap">
                  <ConsentStatusToken status={record.status} />
                  {!record.scanAttachmentId && record.status === 'Valid' && <Token size="sm" label="No scan" color="yellow" />}
                  {record.withdrawnAfterStageFinished && <Token size="sm" label="Withdrawn after the stage was finished" color="red" />}
                </HStack>
              }
            />
          ))}
        </List>
      )}
      {isRecording && <RecordConsentDialog isOpen pathway={pathway} stages={recordable} onClose={() => setIsRecording(false)} />}
      {acting && (
        <ConsentActionDialog action={acting.action} record={acting.record} patient={pathway.patient} onClose={() => setActing(null)} />
      )}
    </VStack>
  );
}
