import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useNavigate, useRouterState} from '@tanstack/react-router';
import {useCallback, useEffect, useLayoutEffect, useRef, useState, useSyncExternalStore} from 'react';
import {createPortal} from 'react-dom';
import {useCurrentUser} from '../../auth/auth';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SampleText} from '../SampleText';
import {TOUR, TOUR_PARTS} from '../tour';
import {COACH_MARKS, type CoachMark} from './coachMarks';
import {coachPosition, endCoachTour, setCoachPosition, subscribeCoach, type CoachPosition} from './coachState';
import {COACH_CARD_ATTRIBUTE, findClickable, findTarget, isShown} from './findTarget';
import type {TourHost} from './tourHost';

export const COACH_TARGET_ATTRIBUTE = 'data-coach-target';

const HIGHLIGHT_CSS = `[${COACH_TARGET_ATTRIBUTE}]{outline:3px solid var(--color-accent) !important;outline-offset:3px !important;box-shadow:0 0 0 8px color-mix(in srgb, var(--color-accent) 30%, transparent) !important;}
[${COACH_CARD_ATTRIBUTE}]{position:fixed;inset:auto;margin:0;padding:0;border:0;background:transparent;overflow:visible;width:min(22rem, calc(100vw - 2rem));}`;

const SEARCH_INTERVAL_MS = 150;
const SEARCH_TRIES = 40;
const REVEAL_AFTER_TRIES = 2;
const GAP = 16;
const EDGE = 12;

export function marksFor(stepIndex: number): CoachMark[] {
  const step = TOUR[stepIndex];
  return COACH_MARKS[step.key] ?? [{text: step.title}];
}

export function pageForMark(stepIndex: number, markIndex: number): string | undefined {
  const step = TOUR[stepIndex];
  const marks = marksFor(stepIndex);
  const mark = marks[markIndex];
  if (mark.path) return mark.path;
  const opensHere = marks.some((m) => m.opensStepPage) ? mark.opensStepPage : markIndex === 0;
  return opensHere ? step.path : undefined;
}

export function nextPosition({step, mark}: CoachPosition): CoachPosition | null {
  if (mark + 1 < marksFor(step).length) return {step, mark: mark + 1};
  return step + 1 < TOUR.length ? {step: step + 1, mark: 0} : null;
}

export function previousPosition({step, mark}: CoachPosition): CoachPosition | null {
  if (mark > 0) return {step, mark: mark - 1};
  return step > 0 ? {step: step - 1, mark: marksFor(step - 1).length - 1} : null;
}

export function placeCard(target: DOMRect | null, card: {width: number; height: number}, view: {width: number; height: number}) {
  const clamp = (value: number, size: number, room: number) => Math.min(Math.max(value, EDGE), Math.max(EDGE, room - size - EDGE));
  if (!target) return {left: view.width - card.width - EDGE * 2, top: view.height - card.height - EDGE * 2};
  const middleTop = target.top + target.height / 2 - card.height / 2;
  const middleLeft = target.left + target.width / 2 - card.width / 2;
  if (target.right + GAP + card.width + EDGE <= view.width) return {left: target.right + GAP, top: clamp(middleTop, card.height, view.height)};
  if (target.bottom + GAP + card.height + EDGE <= view.height) return {left: clamp(middleLeft, card.width, view.width), top: target.bottom + GAP};
  if (target.top - GAP - card.height >= EDGE) return {left: clamp(middleLeft, card.width, view.width), top: target.top - GAP - card.height};
  if (target.left - GAP - card.width >= EDGE) return {left: target.left - GAP - card.width, top: clamp(middleTop, card.height, view.height)};
  return {left: view.width - card.width - EDGE * 2, top: view.height - card.height - EDGE * 2};
}

const supportsPopover = () => typeof HTMLElement !== 'undefined' && typeof HTMLElement.prototype.showPopover === 'function';

const topModalDialog = () => [...document.querySelectorAll('dialog')].filter((d) => d.matches(':modal')).pop() ?? null;

function showOnTop(element: HTMLElement | null) {
  if (!element || typeof element.showPopover !== 'function') return;
  try {
    if (element.matches(':popover-open')) element.hidePopover();
    element.showPopover();
  } catch {
    return;
  }
}

/**
 * The walkthrough, over the real app: the step's account and page, its highlights one at a time. `host` is the demo
 * it runs in (the light demo's `lightTourHost`, or the full demo's), which says where the steps' records are, checks
 * a step, switches account and resets the data.
 */
export function CoachTour({host}: {host: TourHost}) {
  const position = useSyncExternalStore(subscribeCoach, coachPosition, () => null);
  if (!position || !TOUR[position.step]) return null;
  return <CoachRun key={`${position.step}:${position.mark}`} position={position} host={host} />;
}

function CoachRun({position, host: tour}: {position: CoachPosition; host: TourHost}) {
  const me = useCurrentUser();
  const navigate = useNavigate();
  const here = useRouterState({select: (state) => `${state.location.pathname}${state.location.searchStr}`});
  const step = TOUR[position.step];
  const marks = marksFor(position.step);
  const mark = marks[Math.min(position.mark, marks.length - 1)];
  const key = `${position.step}:${position.mark}`;
  const account = step.account ?? step.switchTo;
  const isSwitching = account !== undefined && account !== me.username;
  const tourPage = pageForMark(position.step, position.mark);
  // This demo's own address for it (the full demo looks its records up): null until it's known.
  const page = tour.usePage(tourPage);

  const [target, setTarget] = useState<HTMLElement | null>(null);
  const [search, setSearch] = useState(0);
  const [searched, setSearched] = useState(-1);
  const [host, setHost] = useState<HTMLElement | null>(null);
  const card = useRef<HTMLDivElement | null>(null);
  const revealed = useRef(false);
  const switched = useRef(false);

  useEffect(() => {
    if (isSwitching) {
      if (switched.current) return;
      switched.current = true;
      setCoachPosition({...position, opened: tourPage ? key : position.opened});
      void tour.switchAccount(account!, tourPage ?? here);
      return;
    }
    if (page && position.opened !== key) {
      setCoachPosition({...position, opened: key});
      void navigate({href: page} as Parameters<typeof navigate>[0]);
    }
  }, [account, here, isSwitching, key, navigate, page, position, tour, tourPage]);

  useEffect(() => {
    if (isSwitching || !mark.target) return;
    let tries = 0;
    const timer = window.setInterval(() => {
      const found = findTarget(mark.target!) as HTMLElement | null;
      if (found) {
        window.clearInterval(timer);
        setTarget(found);
        setSearched(search);
        if (found.getBoundingClientRect().bottom > window.innerHeight || found.getBoundingClientRect().top < 0)
          found.scrollIntoView({block: 'center'});
        return;
      }
      tries += 1;
      if (mark.reveal && !revealed.current && tries >= REVEAL_AFTER_TRIES) {
        revealed.current = true;
        findClickable(mark.reveal)?.click();
      }
      if (tries >= SEARCH_TRIES) {
        window.clearInterval(timer);
        setSearched(search);
      }
    }, SEARCH_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [isSwitching, mark, search]);

  useEffect(() => {
    if (!target) return;
    target.setAttribute(COACH_TARGET_ATTRIBUTE, '');
    showOnTop(card.current);
    return () => target.removeAttribute(COACH_TARGET_ATTRIBUTE);
  }, [target]);

  useLayoutEffect(() => {
    let frame = 0;
    const tick = () => {
      const dialog = topModalDialog();
      setHost((current) => (current === dialog ? current : dialog));
      if (target && (!target.isConnected || !isShown(target))) {
        setTarget(null);
        setSearch((n) => n + 1);
      }
      const element = card.current;
      if (element) {
        const box = element.getBoundingClientRect();
        const {left, top} = placeCard(target?.getBoundingClientRect() ?? null, {width: box.width, height: box.height}, {width: window.innerWidth, height: window.innerHeight});
        const x = `${Math.round(left)}px`;
        const y = `${Math.round(top)}px`;
        if (element.style.left !== x) element.style.left = x;
        if (element.style.top !== y) element.style.top = y;
      }
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [target]);

  const attach = useCallback((element: HTMLDivElement | null) => {
    card.current = element;
    showOnTop(element);
  }, []);

  const go = (next: CoachPosition | null) => (next ? setCoachPosition(next) : endCoachTour());
  const doIt = () => {
    target?.click();
    go(nextPosition(position));
  };

  const isSearching = !isSwitching && mark.target !== undefined && searched !== search;
  const part = TOUR_PARTS.find((p) => p.key === step.part)!;
  const person = account ? tour.accountLabel(account) : undefined;
  const why = tour.useWhyStepCantOpen(step, position.mark === 0 && !isSwitching);
  const isLast = nextPosition(position) === null;
  const isFirst = previousPosition(position) === null;

  const content = (
    <div ref={attach} popover={supportsPopover() ? 'manual' : undefined} role="dialog" aria-label={`Guided tour: ${step.title}`} {...{[COACH_CARD_ATTRIBUTE]: ''}}>
      <style>{HIGHLIGHT_CSS}</style>
      <Card elevation="high" padding={4}>
        <VStack gap={2}>
          <Text type="supporting">{`${part.title} · Step ${position.step + 1} of ${TOUR.length}`}</Text>
          <Text type="label" weight="semibold">
            {step.title}
          </Text>
          {person && <Text type="supporting">{`As ${person}`}</Text>}
          <Text type="body">{isSwitching ? `Switching to ${person ?? account}…` : mark.text}</Text>
          {!isSwitching && !isSearching && mark.target && !target && (
            <Text type="supporting">It isn’t showing on this page right now.</Text>
          )}
          {why && <AlertMessage status="info" label="About this step" message={why} />}
          {why && (
            <HStack>
              <Button
                label="Reset the Demo"
                variant="secondary"
                size="sm"
                icon={<Icon icon={AppIcon.Refresh} />}
                onClick={() => {
                  setCoachPosition({step: position.step, mark: 0});
                  void tour.resetDemo();
                }}
              />
            </HStack>
          )}
          {mark.showsSampleText && step.sampleText && <SampleText lines={step.sampleText} />}
          <HStack gap={2} wrap="wrap" vAlign="center">
            <Text type="supporting">{`${position.mark + 1} of ${marks.length}`}</Text>
            <HStack gap={2} wrap="wrap" justify="end" width="100%">
              <Button label="End Tour" variant="ghost" size="sm" onClick={endCoachTour} />
              {!isFirst && <Button label="Back" variant="secondary" size="sm" icon={<Icon icon={AppIcon.ChevronLeft} />} onClick={() => go(previousPosition(position))} />}
              {mark.canDoIt && target && <Button label="Do It for Me" variant="secondary" size="sm" onClick={doIt} />}
              <Button label={isLast ? 'Finish' : 'Next'} variant="primary" size="sm" icon={isLast ? <Icon icon={AppIcon.Check} /> : <Icon icon={AppIcon.ChevronRight} />} onClick={() => go(nextPosition(position))} />
            </HStack>
          </HStack>
        </VStack>
      </Card>
    </div>
  );

  return createPortal(content, host ?? document.body);
}
