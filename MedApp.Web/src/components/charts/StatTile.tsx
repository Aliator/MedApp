import {Card} from '@astryxdesign/core/Card';
import {ClickableCard} from '@astryxdesign/core/ClickableCard';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {AppIcon} from '../shared/icons';

export interface StatTileProps {
  /** What's counted, e.g. "Overdue". */
  label: string;
  value: number;
  /** The same count at the point it's compared with. */
  previous: number;
  /** What `previous` is, after "than", e.g. "this time last week". */
  previousLabel: string;
  /**
   * Whether a rise is good news (completed work), bad news (overdue work), or neither (unset): it colours the arrow,
   * green or red. The words always say which way it went, so the colour is never the only sign.
   */
  isRiseGood?: boolean;
  /** Where the tile goes: the list of what it counts. Without one, it's a plain card. */
  href?: string;
}

/** How a count compares, in words: "3 more than this time last week", "Same as this time last week". */
export function describeChange(value: number, previous: number, previousLabel: string): string {
  const change = value - previous;
  if (change === 0) return `Same as ${previousLabel}`;
  return `${Math.abs(change)} ${change > 0 ? 'more' : 'fewer'} than ${previousLabel}`;
}

/**
 * A headline number in a card: what it counts, the number, and how it compares with an earlier point, with an arrow
 * for which way it went. With `href`, the whole tile is a link to the list it counts.
 *
 * Use a row of them (in a `Grid`) above a page's charts, for the few numbers that matter most.
 */
export function StatTile({label, value, previous, previousLabel, isRiseGood, href}: StatTileProps) {
  const change = value - previous;
  const comparison = describeChange(value, previous, previousLabel);
  const icon = change > 0 ? AppIcon.Increase : change < 0 ? AppIcon.Decrease : AppIcon.NoChange;
  const isGood = change === 0 || isRiseGood === undefined ? undefined : (change > 0) === isRiseGood;
  const content = (
    <VStack gap={1}>
      <Text type="label" color="secondary">
        {label}
      </Text>
      <Text type="display-3" weight="semibold">
        {value}
      </Text>
      <HStack gap={1} vAlign="center">
        <Icon icon={icon} size="sm" color={isGood === undefined ? 'secondary' : isGood ? 'success' : 'error'} />
        <Text type="supporting">{comparison}</Text>
      </HStack>
    </VStack>
  );
  return href ? (
    <ClickableCard label={`${label}: ${value}. ${comparison}.`} href={href} padding={4} width="100%" height="100%">
      {content}
    </ClickableCard>
  ) : (
    <Card padding={4} width="100%" height="100%">
      {content}
    </Card>
  );
}
