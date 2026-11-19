import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useState} from 'react';
import {consultStatusWords, useConsultActions, type Consult} from '../../api/consults';
import {useAnnounce} from '../../app/announcements';
import {formatDateTime} from '../../utils/dates';
import {PatientBanner} from '../patients/PatientBanner';
import {AppIcon} from '../shared/icons';
import {EndConsultDialog, ReplyConsultDialog} from './ConsultDialogs';

export interface ConsultListProps {
  consults: readonly Consult[];
  /** Each consult with the patient banner and a link to its pathway (a team's incoming list). */
  showsPathway?: boolean;
  /** Said when there's nothing. */
  emptyText: string;
}

type Asking = {consult: Consult; kind: 'reply' | 'close' | 'withdraw'};

/** Who asked whom: "Dermatology asked the Lab". */
export const consultHeading = (consult: Consult) => `${consult.askingTeamName ?? 'A team'} asked ${consult.askedTeamName ?? 'a team'}`;

/**
 * Consults, in the order given (the API's: newest asked first on a pathway, oldest first in a team's incoming list):
 * who asked whom, its status, the question, the reply as written once given, and the actions each consult's flags allow
 * (Reply for the asked team; Close after a reply, or Withdraw before one, for the asking team), each in its dialog.
 * Nothing is coloured or ordered by clinical content.
 */
export function ConsultList({consults, showsPathway, emptyText}: ConsultListProps) {
  const [asking, setAsking] = useState<Asking | null>(null);
  const actions = useConsultActions();
  const announce = useAnnounce();
  const close = () => setAsking(null);
  const isBusy = actions.reply.isPending || actions.close.isPending || actions.withdraw.isPending;

  if (consults.length === 0) return <Text type="body" color="secondary">{emptyText}</Text>;

  return (
    <>
      <List>
        {consults.map((consult) => (
          <ListItem
            key={consult.id}
            align="first-line"
            label={
              <VStack gap={1}>
                {showsPathway && consult.patient && <PatientBanner patient={consult.patient} isCompact />}
                <HStack gap={2} vAlign="center" wrap="wrap">
                  {showsPathway ? (
                    <Link href={`/pathways/${consult.pathwayId}`}>{consult.pathwayTitle ?? 'Pathway'}</Link>
                  ) : (
                    <Text type="body" weight="semibold">{consultHeading(consult)}</Text>
                  )}
                  <Token label={consultStatusWords(consult.status)} size="sm" />
                </HStack>
              </VStack>
            }
            description={
              <VStack gap={1}>
                {showsPathway && <Text type="supporting">{consultHeading(consult)}</Text>}
                <Text type="body">{`Question: ${consult.question ?? ''}`}</Text>
                <Text type="supporting" color="secondary">
                  {`Asked by ${consult.requestedByUsername ?? ''}, ${formatDateTime(consult.requestedAtUtc ?? '')}`}
                </Text>
                {consult.reply && (
                  <>
                    <Text type="body">{`Reply: ${consult.reply}`}</Text>
                    <Text type="supporting" color="secondary">
                      {`Replied by ${consult.repliedByUsername ?? ''}, ${formatDateTime(consult.repliedAtUtc ?? '')}`}
                    </Text>
                  </>
                )}
                {(consult.status === 'Closed' || consult.status === 'Withdrawn') && (
                  <Text type="supporting" color="secondary">
                    {`${consult.status === 'Closed' ? 'Closed' : 'Withdrawn'} by ${consult.closedByUsername ?? ''}, ${formatDateTime(consult.closedAtUtc ?? '')}`}
                  </Text>
                )}
              </VStack>
            }
            endContent={
              (consult.canReply || consult.canClose || consult.canWithdraw) && (
                <HStack gap={1} vAlign="center" wrap="wrap">
                  {consult.canReply && (
                    <Button label="Reply" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Question} />}
                      onClick={() => setAsking({consult, kind: 'reply'})} isDisabled={isBusy} />
                  )}
                  {consult.canClose && (
                    <Button label="Close Consult" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Check} />}
                      onClick={() => setAsking({consult, kind: 'close'})} isDisabled={isBusy} />
                  )}
                  {consult.canWithdraw && (
                    <Button label="Withdraw" variant="ghost" size="sm" icon={<Icon icon={AppIcon.Withdraw} />}
                      onClick={() => setAsking({consult, kind: 'withdraw'})} isDisabled={isBusy} />
                  )}
                </HStack>
              )
            }
          />
        ))}
      </List>
      {asking?.kind === 'reply' && (
        <ReplyConsultDialog
          key={`reply-${asking.consult.id}`}
          consult={asking.consult}
          isProcessing={actions.reply.isPending}
          onConfirm={(reply, fail) =>
            actions.reply.mutate(
              {id: asking.consult.id ?? '', reply, expectedVersion: asking.consult.version ?? 1},
              {onSuccess: () => {close(); announce('Reply sent');}, onError: fail},
            )
          }
          onCancel={close}
        />
      )}
      {(asking?.kind === 'close' || asking?.kind === 'withdraw') && (
        <EndConsultDialog
          key={`${asking.kind}-${asking.consult.id}`}
          consult={asking.consult}
          kind={asking.kind}
          isProcessing={actions.close.isPending || actions.withdraw.isPending}
          onConfirm={(_, fail) =>
            actions[asking.kind === 'close' ? 'close' : 'withdraw'].mutate(
              {id: asking.consult.id ?? '', expectedVersion: asking.consult.version ?? 1},
              {onSuccess: () => {close(); announce(asking.kind === 'close' ? 'Consult closed' : 'Consult withdrawn');}, onError: fail},
            )
          }
          onCancel={close}
        />
      )}
    </>
  );
}
