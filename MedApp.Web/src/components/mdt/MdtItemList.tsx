import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useState} from 'react';
import {mdtStatusWords, useMdtItemActions, type MdtItem, type MdtMeeting} from '../../api/mdt';
import {useAnnounce} from '../../app/announcements';
import {formatDateTime} from '../../utils/dates';
import {PatientBanner} from '../patients/PatientBanner';
import {AppIcon} from '../shared/icons';
import {DeferMdtItemDialog, RecordMdtOutcomeDialog, WithdrawMdtItemDialog} from './MdtDialogs';

export interface MdtItemListProps {
  items: readonly MdtItem[];
  /** The MDTs read, for moving an item to a later one and for who can have been present. */
  meetings: readonly MdtMeeting[];
  isLoadingMeetings?: boolean;
  /** Each item with the patient banner (the MDT page); the pathway page leaves it out, as its own banner shows them. */
  showsPatient?: boolean;
  /** Each item with a link to its pathway (the MDT page). */
  linksPathway?: boolean;
  /** Hides the actions (the print view). */
  isReadOnly?: boolean;
  /** Said when there's nothing. */
  emptyText: string;
}

type Asking = {item: MdtItem; kind: 'outcome' | 'defer' | 'withdraw'};

/** Where an item stands, in words: its meeting, or that its meeting was cancelled. */
export function mdtMeetingWords(item: MdtItem): string {
  if (item.meetingStartUtc) return `${item.teamName ?? ''} MDT, ${formatDateTime(item.meetingStartUtc)}`;
  if (item.meetingCancelledStartUtc)
    return `Meeting cancelled (${formatDateTime(item.meetingCancelledStartUtc)})${item.status === 'Listed' || item.status === 'Deferred' ? ': list again' : ''}`;
  return `${item.teamName ?? ''} MDT`;
}

/**
 * MDT items, in the order given (the API's: listed order on a meeting, newest first on a pathway): the patient banner
 * where asked (hazard H-58), the question, where it stands, the outcome as written and who was present once discussed,
 * and the actions each item's flags allow (Record Outcome, Move, Withdraw), each in its dialog. Nothing is coloured
 * or ordered by clinical content.
 */
export function MdtItemList({items, meetings, isLoadingMeetings, showsPatient, linksPathway, isReadOnly, emptyText}: MdtItemListProps) {
  const [asking, setAsking] = useState<Asking | null>(null);
  const actions = useMdtItemActions();
  const announce = useAnnounce();
  const close = () => setAsking(null);
  const isBusy = actions.withdraw.isPending || actions.defer.isPending || actions.recordOutcome.isPending;
  const attendeesOf = (item: MdtItem) => meetings.find((m) => m.id === item.meetingId)?.attendees ?? [];

  if (items.length === 0) return <Text type="body" color="secondary">{emptyText}</Text>;

  return (
    <>
      <List>
        {items.map((item) => (
          <ListItem
            key={item.id}
            align="first-line"
            label={
              <VStack gap={1}>
                {showsPatient && item.patient && <PatientBanner patient={item.patient} isCompact />}
                <HStack gap={2} vAlign="center" wrap="wrap">
                  {linksPathway ? (
                    <Link href={`/pathways/${item.pathwayId}`}>{item.pathwayTitle ?? 'Pathway'}</Link>
                  ) : (
                    <Text type="body" weight="semibold">{mdtMeetingWords(item)}</Text>
                  )}
                  <Token label={mdtStatusWords(item.status)} size="sm" />
                </HStack>
              </VStack>
            }
            description={
              <VStack gap={1}>
                {linksPathway && <Text type="supporting">{mdtMeetingWords(item)}</Text>}
                <Text type="body">{`Question: ${item.question ?? ''}`}</Text>
                <Text type="supporting" color="secondary">{`Listed by ${item.listedByUsername ?? ''}, ${formatDateTime(item.listedAtUtc ?? '')}`}</Text>
                {item.status === 'Discussed' && (
                  <>
                    <Text type="body">{`Outcome: ${item.outcome ?? ''}`}</Text>
                    <Text type="supporting" color="secondary">
                      {`Recorded by ${item.outcomeByUsername ?? ''}, ${formatDateTime(item.outcomeAtUtc ?? '')}. Present: ${(item.present ?? []).join(', ')}`}
                    </Text>
                  </>
                )}
                {item.status === 'Withdrawn' && (
                  <Text type="supporting" color="secondary">{`Withdrawn by ${item.withdrawnByUsername ?? ''}, ${formatDateTime(item.withdrawnAtUtc ?? '')}`}</Text>
                )}
              </VStack>
            }
            endContent={
              !isReadOnly &&
              (item.canRecordOutcome || item.canDefer || item.canWithdraw) && (
                <HStack gap={1} vAlign="center" wrap="wrap">
                  {item.canRecordOutcome && (
                    <Button label="Record Outcome" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Mdt} />}
                      onClick={() => setAsking({item, kind: 'outcome'})} isDisabled={isBusy} />
                  )}
                  {item.canDefer && (
                    <Button label={item.meetingId ? 'Move' : 'List Again'} variant="ghost" size="sm" icon={<Icon icon={AppIcon.Calendar} />}
                      onClick={() => setAsking({item, kind: 'defer'})} isDisabled={isBusy} />
                  )}
                  {item.canWithdraw && (
                    <Button label="Withdraw" variant="ghost" size="sm" icon={<Icon icon={AppIcon.Withdraw} />}
                      onClick={() => setAsking({item, kind: 'withdraw'})} isDisabled={isBusy} />
                  )}
                </HStack>
              )
            }
          />
        ))}
      </List>
      {asking?.kind === 'outcome' && (
        <RecordMdtOutcomeDialog
          key={`outcome-${asking.item.id}`}
          item={asking.item}
          attendees={attendeesOf(asking.item)}
          isProcessing={actions.recordOutcome.isPending}
          onConfirm={(input, fail) =>
            actions.recordOutcome.mutate(
              {id: asking.item.id ?? '', expectedVersion: asking.item.version ?? 1, ...input},
              {onSuccess: () => {close(); announce('MDT outcome recorded');}, onError: fail},
            )
          }
          onCancel={close}
        />
      )}
      {asking?.kind === 'defer' && (
        <DeferMdtItemDialog
          key={`defer-${asking.item.id}`}
          item={asking.item}
          meetings={meetings}
          isLoadingMeetings={isLoadingMeetings}
          isProcessing={actions.defer.isPending}
          onConfirm={(meetingId, fail) =>
            actions.defer.mutate(
              {id: asking.item.id ?? '', meetingId, expectedVersion: asking.item.version ?? 1},
              {onSuccess: () => {close(); announce('MDT item moved');}, onError: fail},
            )
          }
          onCancel={close}
        />
      )}
      {asking?.kind === 'withdraw' && (
        <WithdrawMdtItemDialog
          key={`withdraw-${asking.item.id}`}
          item={asking.item}
          isProcessing={actions.withdraw.isPending}
          onConfirm={(_, fail) =>
            actions.withdraw.mutate(
              {id: asking.item.id ?? '', expectedVersion: asking.item.version ?? 1},
              {onSuccess: () => {close(); announce('MDT item withdrawn');}, onError: fail},
            )
          }
          onCancel={close}
        />
      )}
    </>
  );
}
