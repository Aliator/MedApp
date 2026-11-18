import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {isOpenReferralStatus, waitWords, type ReferralSummary} from '../../api/referrals';
import {formatShortDate} from '../../utils/dates';
import {PriorityToken} from '../pathways/PathwayCells';
import {daysAgoWords, ReferralStatusToken} from './ReferralCells';
import {ClockTokens} from './WaitingClock';

export interface ReferralListProps {
  referrals: readonly ReferralSummary[];
  /** Names the list for screen readers, e.g. "Referrals". */
  label: string;
  /** Flags the referrals to this team ("Same clinic"): possible duplicates of one being registered. */
  sameTeamId?: string;
}

/**
 * A few of one patient's referrals, each linking to its page: the team asked for, its status and the priority
 * asked for, then when it was received (and how long ago, while it's open) and from which practice. Used on the
 * patient's page and, while registering another, to show the ones still open.
 */
export function ReferralList({referrals, label, sameTeamId}: ReferralListProps) {
  return (
    <List edge="flush" density="compact" hasDividers aria-label={label}>
      {referrals.map((referral) => {
        const isOpen = isOpenReferralStatus(referral.status);
        const isSame = sameTeamId !== undefined && referral.teamId === sameTeamId;
        return (
          <ListItem
            key={referral.id}
            label={
              <Text type="body" weight="semibold" maxLines={2}>
                <Link href={`/referrals/${referral.id}`}>{`Referral to ${referral.teamName}`}</Link>
              </Text>
            }
            description={
              <VStack gap={1}>
                <HStack gap={2} vAlign="center" wrap="wrap">
                  <ReferralStatusToken status={referral.status} />
                  <PriorityToken priority={referral.requestedPriority ?? ''} />
                  {isSame && <Token size="sm" label="Same clinic" color="orange" />}
                  {referral.clock && <ClockTokens clock={referral.clock} />}
                </HStack>
                <Text type="supporting" color="secondary">
                  {[
                    `Received ${formatShortDate(referral.receivedOn ?? '')}${
                      referral.clock
                        ? ` (${waitWords(referral.clock).toLowerCase()})`
                        : isOpen
                          ? ` (${daysAgoWords(referral.daysSinceReceived ?? 0).toLowerCase()})`
                          : ''
                    }`,
                    referral.referrerPractice ? `from ${referral.referrerPractice}` : null,
                  ]
                    .filter(Boolean)
                    .join(' ')}
                </Text>
              </VStack>
            }
          />
        );
      })}
    </List>
  );
}
