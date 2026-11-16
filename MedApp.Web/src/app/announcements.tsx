import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode} from 'react';
import {useAccessibility} from './accessibility';

interface AnnouncerContextValue {
  /** Has screen readers say this, politely (after what they're saying), if announcements are on. */
  announce: (message: string) => void;
  /** The page last named (its title, and whether it's the main one), or null before the first. */
  lastPageRef: {current: string | null};
}

const AnnouncerContext = createContext<AnnouncerContextValue | null>(null);

/**
 * Screen reader announcements (Account Settings, Accessibility): a hidden live region that says which page opened,
 * what a list found, and that a form was saved or a stage finished: things sighted people see change but a screen
 * reader wouldn't mention.
 */
export function AnnouncerProvider({children}: {children: ReactNode}) {
  const {flags} = useAccessibility();
  const [message, setMessage] = useState('');
  const lastPageRef = useRef<string | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const pending = useRef<string[]>([]);

  const announce = useCallback(
    (next: string) => {
      if (!flags.announcements) return;
      // Cleared first and set a moment later, so the same message twice is still said twice. Messages that come
      // close together (a page, then its list's results) are said together.
      pending.current.push(next);
      setMessage('');
      clearTimeout(timer.current);
      timer.current = setTimeout(() => {
        setMessage(pending.current.join('. '));
        pending.current = [];
      }, 150);
    },
    [flags.announcements],
  );
  useEffect(() => () => clearTimeout(timer.current), []);

  const value = useMemo(() => ({announce, lastPageRef}), [announce]);
  return (
    <AnnouncerContext.Provider value={value}>
      {children}
      <VisuallyHidden as="div" role="status" aria-live="polite" aria-atomic="true" data-testid="announcer">
        {message}
      </VisuallyHidden>
    </AnnouncerContext.Provider>
  );
}

const NO_ANNOUNCER: AnnouncerContextValue = {announce: () => {}, lastPageRef: {current: null}};

/** Announces to screen readers (see `AnnouncerProvider`); outside it (tests of one part), does nothing. */
export function useAnnounce(): (message: string) => void {
  return (useContext(AnnouncerContext) ?? NO_ANNOUNCER).announce;
}

/**
 * Names the page: the browser tab's title (WCAG 2.4.2), unless it's the second page of a half view (`isMain`
 * false), and an announcement of it as it opens (not the first page, which the browser announces itself).
 */
export function usePageTitle(title: string, isMain: boolean) {
  const {announce, lastPageRef} = useContext(AnnouncerContext) ?? NO_ANNOUNCER;
  useEffect(() => {
    if (isMain) document.title = `${title} - MedApp`;
  }, [title, isMain]);
  useEffect(() => {
    const page = `${isMain}:${title}`;
    // The first page is the one the browser loaded; the same page again is a re-render, not a new page.
    if (lastPageRef.current !== null && lastPageRef.current !== page) {
      announce(isMain ? `${title} page` : `${title} page, opened beside`);
    }
    lastPageRef.current = page;
  }, [title, isMain, announce, lastPageRef]);
}
