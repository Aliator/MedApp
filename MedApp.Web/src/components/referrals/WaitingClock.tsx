import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {pauseReasonWords, stopReasonWords, waitWords, type ClockPause, type WaitingClock} from '../../api/referrals';
import {useAccessibility} from '../../app/accessibility';
import {formatDate, formatShortDate} from '../../utils/dates';
import {AppIcon} from '../shared/icons';

/**
 * A referral's waiting-time clock, the same way everywhere it shows (decision 38): "Day 84 of 126", with Wait breached
 * (in the error style, like Overdue), Paused (with its review date, flagged once that has passed: hazard H-42) and
 * Stopped. Administrative: whole UK days against the team's target for the priority, never ranked by anything
 * clinical, and never sent anywhere. `WaitCell` is a table's Wait column, `WaitingClockDetails` a page's account of
 * it, and `ClockPauseList` its pauses.
 */

/** A token's icon, if the person wants icons on statuses (Account Settings, Accessibility). */
function useStatusIcon(icon: IconType) {
  const {flags} = useAccessibility();
  return flags.statusIcons ? <Icon icon={icon} /> : undefined;
}

/** The Wait breached flag: the wait has gone past its target. */
export function BreachedToken() {
  return <Token size="sm" label="Wait breached" color="red" icon={useStatusIcon(AppIcon.Overdue)} />;
}

/** The Paused flag. */
export function PausedToken() {
  return <Token size="sm" label="Paused" color="orange" icon={useStatusIcon(AppIcon.PauseClock)} />;
}

/** The Stopped flag: the clock stopped, and never restarts. */
export function StoppedToken() {
  return <Token size="sm" label="Stopped" color="gray" icon={useStatusIcon(AppIcon.ClockStopped)} />;
}

/** The Review overdue flag: a paused clock whose review date has passed (hazard H-42). */
export function ReviewOverdueToken() {
  return <Token size="sm" label="Review overdue" color="red" icon={useStatusIcon(AppIcon.Overdue)} />;
}

const isPaused = (clock: WaitingClock) => clock.state === 'Paused';
const isStopped = (clock: WaitingClock) => clock.state === 'Stopped';

/** The clock's flags, in a wrapping row: Wait breached, Paused, Review overdue, Stopped. Nothing for a clock running in time. */
export function ClockTokens({clock}: {clock: WaitingClock}) {
  const hasAny = clock.isBreached || isPaused(clock) || isStopped(clock);
  if (!hasAny) return null;
  return (
    <HStack gap={1} vAlign="center" wrap="wrap">
      {clock.isBreached && <BreachedToken />}
      {isPaused(clock) && <PausedToken />}
      {isPaused(clock) && clock.isReviewOverdue && <ReviewOverdueToken />}
      {isStopped(clock) && <StoppedToken />}
    </HStack>
  );
}

/** "Review 3 Feb 2027" for a paused clock; nothing otherwise. */
export const reviewWords = (clock: WaitingClock) =>
  isPaused(clock) && clock.pauseReviewOn ? `Review ${formatShortDate(clock.pauseReviewOn)}` : null;

/**
 * A list's Wait column: "Day 84 of 126", then the review date while paused, then the flags (stacked, so a narrow
 * column doesn't cut them off). A pathway or referral with no clock (a pathway not from a referral) says "No clock".
 */
export function WaitCell({clock}: {clock?: WaitingClock | null}) {
  if (!clock)
    return (
      <Text type="body" color="secondary">
        No clock
      </Text>
    );
  const review = reviewWords(clock);
  return (
    <VStack gap={1} hAlign="start">
      <VStack gap={0} hAlign="start">
        <Text type="body">{waitWords(clock)}</Text>
        {review && (
          <Text type="supporting" color="secondary">
            {review}
          </Text>
        )}
      </VStack>
      <ClockTokens clock={clock} />
    </VStack>
  );
}

/**
 * A page's account of the clock: the wait and its flags, the target date, the day the wait counts from (once a pause
 * has moved it) and the days paused, and while paused since when and the review date (flagged once past), or once
 * stopped when and why. A review date that has passed is flagged with the wait (Review overdue).
 */
export function WaitingClockDetails({clock}: {clock: WaitingClock}) {
  const paused = isPaused(clock);
  const stopped = isStopped(clock);
  return (
    <MetadataList columns={2} label={{position: 'top'}}>
      <MetadataListItem label="Wait">
        <VStack gap={1} hAlign="start">
          <Text type="body">{waitWords(clock)}</Text>
          <ClockTokens clock={clock} />
        </VStack>
      </MetadataListItem>
      <MetadataListItem label="Target date">{clock.targetOn ? formatDate(clock.targetOn) : '—'}</MetadataListItem>
      <MetadataListItem label="Wait counted from">{clock.waitStartOn ? formatDate(clock.waitStartOn) : '—'}</MetadataListItem>
      <MetadataListItem label="Days paused">{String(clock.pausedDays ?? 0)}</MetadataListItem>
      {paused && (
        <MetadataListItem label="Paused since">{clock.pausedSinceOn ? formatDate(clock.pausedSinceOn) : '—'}</MetadataListItem>
      )}
      {paused && (
        <MetadataListItem label="Review date">{clock.pauseReviewOn ? formatDate(clock.pauseReviewOn) : '—'}</MetadataListItem>
      )}
      {stopped && <MetadataListItem label="Stopped">{clock.stoppedOn ? formatDate(clock.stoppedOn) : '—'}</MetadataListItem>}
      {stopped && <MetadataListItem label="Why it stopped">{stopReasonWords(clock.stopReason) || '—'}</MetadataListItem>}
    </MetadataList>
  );
}

/**
 * Every pause of a clock, as the API sends them: the reason in words, its detail, who paused it and when, its review
 * date, and who resumed it and when (with how many days it lasted), or that it still lasts. "No pauses" when none.
 */
export function ClockPauseList({pauses}: {pauses: readonly ClockPause[]}) {
  if (pauses.length === 0)
    return (
      <Text type="body" color="secondary">
        No pauses.
      </Text>
    );
  return (
    <List edge="flush" hasDividers aria-label="Pauses">
      {pauses.map((pause) => (
        <ListItem
          key={pause.id}
          align="first-line"
          label={
            <Text type="body" weight="semibold">
              {pauseReasonWords(pause.reason)}
            </Text>
          }
          description={
            <VStack gap={0}>
              {pause.detail?.trim() && (
                <Text type="body" style={PRESERVE_LINES}>
                  {pause.detail}
                </Text>
              )}
              <Text type="supporting" color="secondary">
                {`Paused ${formatShortDate(pause.pausedOn ?? '')} by ${pause.pausedByUsername || 'someone'}, review ${formatShortDate(pause.reviewOn ?? '')}`}
              </Text>
              <Text type="supporting" color="secondary">
                {pause.resumedOn
                  ? `Resumed ${formatShortDate(pause.resumedOn)}${pause.resumedByUsername ? ` by ${pause.resumedByUsername}` : ''} (${daysWords(pause.days ?? 0)})`
                  : 'Still paused'}
              </Text>
            </VStack>
          }
        />
      ))}
    </List>
  );
}

const daysWords = (days: number) => `${days} ${days === 1 ? 'day' : 'days'}`;

/** A pause's detail keeps the lines it was typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;
