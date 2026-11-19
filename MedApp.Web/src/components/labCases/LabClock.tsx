import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {hospitalDueBasis, labClockCountdown, labClockSetBy, labClockStateLabel, type LabClock} from '../../api/labClocks';
import {useAccessibility} from '../../app/accessibility';
import {formatDate, formatShortDate} from '../../utils/dates';
import {AppIcon} from '../shared/icons';

/**
 * A lab case's strict clock (MC19), the same way everywhere it shows: the countdown ("Due in 3 days", "2 days
 * past due"), the due date, and a flag once it's at risk or overdue. Time only: it says nothing about the sample or its
 * results. `LabClockToken` is the flag, `LabClockCell` a list's Due column, `LabClockDetails` the case page's account
 * of both deadlines and which one applies (hazard H-71).
 */

const COLORS: Record<string, TokenColor> = {
  OnTime: 'teal',
  AtRisk: 'orange',
  Breached: 'red',
  ReportedOnTime: 'green',
  ReportedLate: 'orange',
  Stopped: 'gray',
};

/** The clock's state as a flag: On time, At risk, Breached, Reported on time or late, Clock stopped. */
export function LabClockToken({clock}: {clock: LabClock}) {
  const {flags} = useAccessibility();
  const warns = clock.state === 'AtRisk' || clock.state === 'Breached';
  return (
    <Token
      size="sm"
      label={labClockStateLabel(clock.state)}
      color={COLORS[clock.state ?? ''] ?? 'default'}
      icon={flags.statusIcons && warns ? <Icon icon={AppIcon.Overdue} /> : undefined}
    />
  );
}

/** A list's Due column: the countdown and the due date, with the flag when at risk or overdue. No clock: a dash. */
export function LabClockCell({clock}: {clock?: LabClock | null}) {
  if (!clock)
    return (
      <Text type="body" color="secondary">
        —
      </Text>
    );
  const flagged = clock.state === 'AtRisk' || clock.state === 'Breached';
  return (
    <VStack gap={1} hAlign="start">
      <Text type="body" maxLines={1}>
        {labClockCountdown(clock)}
      </Text>
      <Text type="supporting" color="secondary" maxLines={1}>
        {`Due ${formatShortDate(clock.dueOn ?? '')}`}
      </Text>
      {flagged && <LabClockToken clock={clock} />}
    </VStack>
  );
}

/** The countdown in words for a one-line list: "Due in 3 days (at risk), 21 Nov 2026". */
export const labClockLine = (clock: LabClock | null | undefined) =>
  clock ? `${labClockCountdown(clock)}${clock.state === 'AtRisk' ? ' (at risk)' : ''}, due ${formatShortDate(clock.dueOn ?? '')}` : null;

/**
 * The case page's clock: when it's due and which deadline set it, both deadlines (the lab's target with its limit, and
 * the hospital's date with how it was found), and when it's at risk from.
 */
export function LabClockDetails({clock}: {clock?: LabClock | null}) {
  if (!clock)
    return (
      <Text type="body" color="secondary">
        The clock starts when the lab receives the sample.
      </Text>
    );
  return (
    <MetadataList columns={2} label={{position: 'top'}}>
      <MetadataListItem label="Due">
        <VStack gap={1} hAlign="start">
          <Text type="body">{formatDate(clock.dueOn ?? '')}</Text>
          <HStack gap={2} vAlign="center" wrap="wrap">
            <LabClockToken clock={clock} />
            <Text type="supporting" color="secondary">
              {labClockCountdown(clock)}
            </Text>
          </HStack>
        </VStack>
      </MetadataListItem>
      <MetadataListItem label="Set by">{`${labClockSetBy(clock)} (the sooner applies)`}</MetadataListItem>
      <MetadataListItem label="Lab target">{`${formatDate(clock.labDueOn ?? '')}; latest acceptable ${formatShortDate(clock.labLimitOn ?? '')}`}</MetadataListItem>
      {clock.hospitalDueOn && (
        <MetadataListItem label="The hospital's date">{`${formatDate(clock.hospitalDueOn)}, ${hospitalDueBasis(clock.hospitalDueBasis)}`}</MetadataListItem>
      )}
      <MetadataListItem label="At risk from">{formatDate(clock.atRiskOn ?? '')}</MetadataListItem>
    </MetadataList>
  );
}
