import {Center} from '@astryxdesign/core/Center';
import {useMediaQuery} from '@astryxdesign/core/hooks';
import {Layout, LayoutContent} from '@astryxdesign/core/Layout';
import {Spinner} from '@astryxdesign/core/Spinner';
import {StackItem, VStack} from '@astryxdesign/core/Stack';
import {useContext, useEffect, useRef, useState, type ReactNode, type RefObject} from 'react';
import {usePageTitle} from '../../app/announcements';
import {useNarrowBesideNotepad} from '../notepad/NotepadContext';
import {InHalfPaneContext, OverWholeWindowContext, useHalfView} from './halfView';
import {PageHeader, type PageHeaderProps} from './PageHeader';

export interface PageProps extends PageHeaderProps {
  /** Shows a spinner instead of the content, e.g. while the page's data loads. */
  isLoading?: boolean;
  loadingMessage?: string;
  /**
   * Caps the content width (px), centred. Every page uses the same width by default, so titles and cards line up
   * from page to page.
   */
  maxWidth?: number;
  /**
   * Makes the content fill the window's height below the header, e.g. so a list's table reaches the bottom of
   * the page however few rows it has. The content gets the remaining height to fill (`height="100%"`).
   */
  fill?: boolean;
  /**
   * Who the page is about, between the header and the content (a `PatientBanner` on every page about one patient).
   * It sits outside the cards that fold and scroll, so on a `fill` page, where only the cards scroll, it's always in
   * view: give a banner to `fill` pages, with a form in a `FormCard` with `fill`. The one exception is the record
   * export, a long printable document that scrolls as a whole, where the banner heads it and scrolls with it.
   */
  banner?: ReactNode;
  /** Dialogs the page opens. They render outside the content, so they stay available while it loads. */
  dialogs?: ReactNode;
  children?: ReactNode;
}

/**
 * The frame of every signed-in page (replaces Blazor's Page): header, then content or a loading spinner.
 * Auth checks and data loading live in the route (guard and queries), not here.
 */
/** The content width every page shares. */
export const PAGE_MAX_WIDTH = 1600;

/**
 * Below this width, pages with two side-by-side columns (Home, a pathway) don't fit them, and show one at a
 * time or stack them instead.
 */
export const NARROW_WINDOW_QUERY = '(max-width: 1399px)';

/**
 * Whether a page should use its narrow layout: in a narrow window, in a half view (two pages side by side), or beside
 * the docked notepad when that leaves too little room. Pages with two columns ask this rather than the window.
 */
export function useIsNarrow(): boolean {
  const isNarrowWindow = useIsNarrowWindow();
  const isNarrowBesideNotepad = useNarrowBesideNotepad();
  const isHalf = useHalfView().isHalf;
  // In a dialog, over the whole window, only the window counts.
  if (useContext(OverWholeWindowContext)) return isNarrowWindow;
  return isHalf || isNarrowWindow || isNarrowBesideNotepad;
}

/**
 * Whether the window itself is narrow, whatever's beside the page: for a dialog choosing its own size, as it sits over
 * the whole window (a dialog opened from a half view is as big as one opened from a page on its own).
 */
export const useIsNarrowWindow = () => useMediaQuery(NARROW_WINDOW_QUERY, false);

/**
 * A long page's header (the title, Back and its buttons) stays at the top while the page scrolls under it (the owner,
 * 19 November 2026). It's stuck to whatever scrolls the page: the window's main area, a half of a half view, or the
 * page beside the docked notepad. Its own background hides what passes under it; a little room above and below the
 * title while stuck is given back with negative margins, and it reaches across the page's side margins, so nothing
 * shows beside it, without moving it from where every page's title sits. Where it sticks (`top`) is set by
 * `useStickyHeader`.
 */
const STICKY_HEADER = {
  position: 'sticky',
  // Above what the page holds (Astryx's tables and focus rings use up to 3), below the app's overlays (500 and up).
  zIndex: 10,
  backgroundColor: 'var(--color-background-surface)',
  paddingBlock: 'var(--spacing-3)',
  marginBlock: 'calc(-1 * var(--spacing-3))',
  paddingInline: 'var(--spacing-6)',
  marginInline: 'calc(-1 * var(--spacing-6))',
} as const;

/** A line under the header while it's stuck, between it and the page scrolling under it. */
const STUCK_LINE = {boxShadow: '0 1px 0 var(--color-border)'} as const;

/** What scrolls this element: its nearest ancestor that scrolls up and down (one shown as its contents isn't a box). */
function scrollingAncestor(element: HTMLElement): HTMLElement | null {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const style = getComputedStyle(parent);
    if (style.display !== 'contents' && /auto|scroll/.test(style.overflowY)) return parent;
  }
  return null;
}

/**
 * Where the sticky header sticks, and whether it's stuck. A sticky element stops at the inside of its scroller's
 * padding (the main area has some), so it's given that back to stick at the very edge; and one pixel more, so that while
 * it's stuck a pixel is out of view, which is how it knows (its line, `STUCK_LINE`).
 */
function useStickyHeader(ref: RefObject<HTMLElement | null>): {top: number; isStuck: boolean} {
  const [top, setTop] = useState(-1);
  const [isStuck, setIsStuck] = useState(false);
  useEffect(() => {
    const element = ref.current;
    if (!element) return;
    const scroller = scrollingAncestor(element);
    setTop(-1 - (scroller ? parseFloat(getComputedStyle(scroller).paddingTop) || 0 : 0));
    if (typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(([entry]) => setIsStuck(entry.intersectionRatio < 1), {threshold: [1]});
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref]);
  return {top, isStuck};
}

export function Page({
  isLoading = false,
  loadingMessage = 'Loading...',
  maxWidth = PAGE_MAX_WIDTH,
  fill = false,
  banner,
  dialogs,
  children,
  ...header
}: PageProps) {
  usePageTitle(header.title, !useContext(InHalfPaneContext));
  const headerRef = useRef<HTMLElement>(null);
  const {top, isStuck} = useStickyHeader(headerRef);
  return (
    // A Layout sits flush in AppShell's content area and insets only its regions, so the page goes in a
    // LayoutContent region with the same 24px inset as pages without a Layout.
    <Layout height={fill ? 'fill' : 'auto'} padding={6} contentWidth={maxWidth}>
      {/* A page that grows scrolls in what holds it, so its region mustn't scroll itself (it never did, being as tall
          as the page), or the sticky header would stick to it rather than to what scrolls. A `fill` page's cards
          scroll, not the page, so its header is always in view already. */}
      <LayoutContent isScrollable={fill}>
        <VStack gap={6} height={fill ? '100%' : undefined}>
          {fill ? (
            <PageHeader {...header} />
          ) : (
            <VStack ref={headerRef} style={{...STICKY_HEADER, top, ...(isStuck ? STUCK_LINE : {})}} data-page-header="">
              <PageHeader {...header} />
            </VStack>
          )}
          {banner}
          {isLoading ? (
            <Center axis="horizontal" padding={10}>
              <Spinner size="lg" label={loadingMessage} />
            </Center>
          ) : fill ? (
            <StackItem size="fill">{children}</StackItem>
          ) : (
            children
          )}
        </VStack>
        {dialogs}
      </LayoutContent>
    </Layout>
  );
}
