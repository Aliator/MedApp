import {Button} from '@astryxdesign/core/Button';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {isUnauthorized, sessionQuery, useExtendSession} from '../../api/session';
import {useLogout} from '../../auth/auth';
import {DialogFrame} from '../modals/DialogFrame';
import {AlertMessage} from '../shared/AlertMessage';

/** How long before the session ends the warning shows (WCAG 2.2.1 asks for at least 20 seconds to answer). */
export const WARNING_MS = 5 * 60_000;

/** How long after the session should have ended it's checked: the server's clock may be a moment behind. */
const END_CHECK_DELAY_MS = 1000;

/** Time left as people say it: "4 minutes 5 seconds", "1 minute", "45 seconds". Never less than 0 seconds. */
export function timeLeftText(ms: number): string {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  const amount = (n: number, unit: string) => `${n} ${unit}${n === 1 ? '' : 's'}`;
  if (minutes === 0) return amount(seconds, 'second');
  return seconds === 0 ? amount(minutes, 'minute') : `${amount(minutes, 'minute')} ${amount(seconds, 'second')}`;
}

/**
 * How far apart two answers for the same end can be: each is counted from when it arrived, so they're never quite
 * the same number.
 */
const SAME_END_MS = 5_000;

/**
 * Warns before the session times out (WCAG 2.2.1), instead of letting the next action fail. Sessions last 8 hours
 * from sign-in (`GET api/auth/session` says when this one ends). Five minutes before, a dialog shows the time left,
 * counting down, with Stay signed in (a full 8 hours from now, up to 24 hours from sign-in; after that it says so)
 * and Sign out. When it ends, it goes to the login page, which says the session timed out.
 *
 * It asks the API again as the warning is due, and as the session ends (and when the tab is shown again, as timers
 * sleep in hidden tabs), so a session extended in another tab isn't warned about. A 401 at any point means the
 * session is over. In the signed-in layout (`AppLayout`), once for both halves of a half view.
 */
export function SessionTimeout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const session = useQuery(sessionQuery);
  const extend = useExtendSession();
  const logout = useLogout();
  // The time now, while the warning is due (0 until then, and on the server), for the countdown.
  const [now, setNow] = useState(0);
  // The end the warning was closed for, so it stays closed until the end moves (by more than `SAME_END_MS`).
  const [dismissedFor, setDismissedFor] = useState<number | null>(null);
  const [hasEnded, setHasEnded] = useState(false);

  const {refetch, dataUpdatedAt, errorUpdatedAt} = session;
  const endsAt = session.data?.endsAt;
  const isOver = hasEnded || isUnauthorized(session.error) || isUnauthorized(extend.error);

  // Wakes up as the warning is due, then counts down and checks as the session ends. Set again after each answer
  // from the API (the end may have moved, or a check failed).
  useEffect(() => {
    if (endsAt === undefined) return;
    const left = endsAt - Date.now();
    if (left > WARNING_MS) {
      const warning = setTimeout(() => void refetch(), left - WARNING_MS);
      return () => clearTimeout(warning);
    }
    const tick = () => setNow(Date.now());
    const first = setTimeout(tick, 0);
    const ticking = setInterval(tick, 1000);
    const end = setTimeout(
      async () => {
        const result = await refetch();
        // A 401 ends it (above); a failure of any other kind, once its time is up, does too.
        if (result.isError && Date.now() >= endsAt) setHasEnded(true);
      },
      Math.max(0, left + END_CHECK_DELAY_MS),
    );
    return () => {
      clearTimeout(first);
      clearInterval(ticking);
      clearTimeout(end);
    };
  }, [endsAt, refetch, dataUpdatedAt, errorUpdatedAt]);

  // Timers sleep in a hidden tab (and a sleeping computer): looking again checks at once.
  useEffect(() => {
    const onShow = () => {
      if (document.visibilityState === 'visible') void refetch();
    };
    document.addEventListener('visibilitychange', onShow);
    return () => document.removeEventListener('visibilitychange', onShow);
  }, [refetch]);

  // Ended: to the login page, which says why. The cache is cleared after leaving, as signing out does.
  useEffect(() => {
    if (!isOver) return;
    void navigate({to: '/login', search: {expired: true}}).then(() => queryClient.clear());
  }, [isOver, navigate, queryClient]);

  if (endsAt === undefined || now === 0) return null;
  const left = endsAt - now;
  const isDismissed = dismissedFor !== null && Math.abs(endsAt - dismissedFor) < SAME_END_MS;
  const isOpen = left <= WARNING_MS && left > 0 && !isDismissed && !isOver;
  const canExtend = session.data?.canExtend ?? false;
  const time = timeLeftText(left);

  return (
    <DialogFrame
      isOpen={isOpen}
      title="Your session is ending"
      purpose={extend.isPending ? 'required' : 'form'}
      onClose={() => setDismissedFor(endsAt)}
      footer={
        <>
          <Button label="Sign out" variant="secondary" isDisabled={extend.isPending} onClick={() => logout.mutate()} />
          {canExtend ? (
            <Button label="Stay signed in" variant="primary" isLoading={extend.isPending} onClick={() => extend.mutate()} />
          ) : (
            <Button label="OK" variant="primary" onClick={() => setDismissedFor(endsAt)} />
          )}
        </>
      }>
      <VStack gap={4}>
        {extend.isError && !isUnauthorized(extend.error) && (
          <AlertMessage message="Your session could not be extended. Please try again." />
        )}
        <Text type="body">
          {canExtend
            ? `To keep patients' details safe, you'll be signed out in ${time}. Anything you haven't saved will be lost.`
            : `You've been signed in for 24 hours, the longest a session can last, so you'll be signed out in ${time}. Save your work, then sign in again.`}
        </Text>
      </VStack>
    </DialogFrame>
  );
}
