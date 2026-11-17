import type {IconType} from '@astryxdesign/core/Icon';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Popover} from '@astryxdesign/core/Popover';
import {SideNav} from '@astryxdesign/core/SideNav';
import {VStack} from '@astryxdesign/core/Stack';
import {useEffect, useLayoutEffect, useRef, useState, type ReactNode} from 'react';
import {useNavSettings} from '../../app/navSettings';

/** One item of the side nav: its icon for the folded nav, and its row as the open nav shows it. */
export interface NavRailEntry {
  key: string;
  label: string;
  icon: IconType;
  isSelected: boolean;
  /**
   * For a toggle rather than a page (the notepad): whether it's on. It's filled as a current page is, but announced
   * as pressed, never as the current page.
   */
  isPressed?: boolean;
  /** Its page is the one in the half view: marked a shade darker than the current page's. */
  isHalfView?: boolean;
  /** Where its icon goes; without one (Logout), the icon only shows its row. */
  href?: string;
  /** Its row, a SideNavItem (with its buttons, and any items under it). */
  row: ReactNode;
}

export interface NavRailProps {
  /** The items, in groups (Main, Administration): a group starts a little lower, as the open nav's sections do. */
  groups: NavRailEntry[][];
  /** Wraps each row brought out, e.g. to track the pointer over its buttons. */
  wrapRow?: (row: ReactNode) => ReactNode;
}

/** How long a row stays out after the pointer leaves, so it can cross onto the row. */
const CLOSE_DELAY_MS = 150;

/**
 * The folded side nav's icons (as in a half view). Hovering an icon brings its row out over it and the page,
 * exactly where and as it would be in the open nav: the row's icon over the folded icon, then its name, its
 * buttons, and any items under it, which start at the nav's edge so the icons below stay in sight.
 *
 * Each icon's hover area is the nav's full width and touches the next one's within a group, so moving up or down
 * a group always has an item out, one at a time. Off the items (between groups, below them, off the nav), the
 * row goes.
 */
export function NavRail({groups, wrapRow = (row) => row}: NavRailProps) {
  const [openKey, setOpenKey] = useState<string | null>(null);
  // How long the pointer rests on an item before its row comes out (Account Settings, Accessibility).
  const {flyoutDelayMs} = useNavSettings();
  const isAnyOpen = useRef(false);
  const opening = useRef<{key: string; timer: number} | null>(null);
  const closing = useRef<number | undefined>(undefined);
  useEffect(() => {
    isAnyOpen.current = openKey !== null;
  }, [openKey]);
  useEffect(
    () => () => {
      window.clearTimeout(closing.current);
      if (opening.current) window.clearTimeout(opening.current.timer);
    },
    [],
  );

  // A row comes out after the pointer has rested on its item for a moment; once one is out, moving to another
  // item swaps at once (no wait at every item on the way down the nav).
  const show = (key: string) => {
    window.clearTimeout(closing.current);
    if (isAnyOpen.current) {
      if (opening.current) window.clearTimeout(opening.current.timer);
      opening.current = null;
      setOpenKey(key);
      return;
    }
    if (opening.current?.key === key) return;
    if (opening.current) window.clearTimeout(opening.current.timer);
    if (flyoutDelayMs === 0) {
      opening.current = null;
      setOpenKey(key);
      return;
    }
    opening.current = {
      key,
      timer: window.setTimeout(() => {
        opening.current = null;
        setOpenKey(key);
      }, flyoutDelayMs),
    };
  };
  const hideSoon = (key: string) => {
    if (opening.current?.key === key) {
      window.clearTimeout(opening.current.timer);
      opening.current = null;
    }
    window.clearTimeout(closing.current);
    closing.current = window.setTimeout(
      () => setOpenKey((current) => (current === key ? null : current)),
      CLOSE_DELAY_MS,
    );
  };

  return (
    <VStack gap={0}>
      {groups.flatMap((entries, g) =>
        entries.map((entry, i) => (
          <RailItem
            key={entry.key}
            entry={entry}
            isGroupStart={g > 0 && i === 0}
            isOpen={openKey === entry.key}
            onShow={() => show(entry.key)}
            onHide={() => hideSoon(entry.key)}
            wrapRow={wrapRow}
          />
        )),
      )}
    </VStack>
  );
}

/** Ends a popover's opening or closing animation at once (where the browser has the Web Animations API). */
function finishAnimations(element: HTMLElement) {
  if (typeof element.getAnimations !== 'function') return;
  element.getAnimations({subtree: true}).forEach((animation) => animation.finish());
}

interface RailItemProps {
  entry: NavRailEntry;
  /** The first of a later group: it starts a little lower (the gap above it belongs to no item). */
  isGroupStart: boolean;
  isOpen: boolean;
  onShow: () => void;
  onHide: () => void;
  wrapRow: (row: ReactNode) => ReactNode;
}

function RailItem({entry, isGroupStart, isOpen, onShow, onHide, wrapRow}: RailItemProps) {
  const icon = useRef<HTMLElement | null>(null);
  const flyout = useRef<HTMLElement | null>(null);
  const rowsBox = useRef<HTMLElement | null>(null);
  const lastPanel = useRef<HTMLElement | null>(null);

  // The popover opens beside the icon; move it so it starts at the nav's edge and its row's icon sits over the
  // folded icon, as the open nav would have it. Then line up the rows under the item's own.
  useLayoutEffect(() => {
    // The popover itself (the top-layer element), so its surface moves with the row.
    const panel = (flyout.current?.closest('[popover]') as HTMLElement | null) ?? flyout.current?.parentElement;
    const anchor = icon.current;
    const box = rowsBox.current;
    if (panel) lastPanel.current = panel;
    if (!isOpen) {
      // Closing is instant too, so the row is gone in the same moment the icon's fill comes back (no flicker of
      // the two together).
      const closed = lastPanel.current;
      if (closed) {
        finishAnimations(closed);
        const frame = requestAnimationFrame(() => finishAnimations(closed));
        return () => cancelAnimationFrame(frame);
      }
      return;
    }
    if (!panel || !anchor || !box) return;
    const nav = anchor.closest('.astryx-app-shell-sidenav')?.getBoundingClientRect();
    const rows = [...box.querySelectorAll<HTMLElement>('.astryx-side-nav-item')];
    const [first, ...under] = rows;

    panel.style.translate = '';
    // The top-layer element too lets the pointer through (only the rows' box takes it).
    panel.style.pointerEvents = 'none';
    // No opening animation (a popover grows and fades in from its icon): the row should simply be there, as the
    // open nav's would. Astryx skips it inside a reduced-motion mark; anything already started is finished.
    panel.setAttribute('data-astryx-reduced-motion', '');
    const settle = () => finishAnimations(panel);
    settle();
    for (const row of under) {
      row.style.marginInlineStart = '';
      row.style.marginBlockStart = '';
      row.style.inlineSize = '';
      const link = row.querySelector<HTMLElement>('a');
      if (link) link.style.paddingInlineStart = '';
    }
    box.style.clipPath = '';
    box.style.inlineSize = '';

    // The item's row shows as hovered for as long as it's out (it's only out while the pointer is on its item,
    // and the hover area is a little bigger than the row): a hover tint over the row's own colour, the current
    // page's fill or the half view's shade included, as hovering a row in the open nav looks.
    if (first) first.style.backgroundImage = 'linear-gradient(var(--color-overlay-hover), var(--color-overlay-hover))';

    const anchorBox = anchor.getBoundingClientRect();
    const firstBox = first?.getBoundingClientRect();
    const dx = (nav?.left ?? 0) - panel.getBoundingClientRect().left;
    const dy = firstBox ? anchorBox.top + anchorBox.height / 2 - (firstBox.top + firstBox.height / 2) : 0;
    panel.style.translate = `${dx}px ${dy}px`;
    const settleFrame = requestAnimationFrame(settle);
    if (!nav || !first || under.length === 0) return () => cancelAnimationFrame(settleFrame);

    // The rows under the item's own (a pinned patient) start exactly at the nav's right edge: touching it, not
    // over it, so the icons below stay in sight and in reach. The rows start where the icon's hover area does
    // (the open nav's rows and its icons share that inset), so that's the distance from there to the nav's right
    // edge: measured on the nav, which doesn't move, rather than on the popover, which may still be settling.
    // They end where the item's row does (the open nav's width); below that row the box is cut back to the nav's
    // edge, so only the item's own row covers its icon.
    // (Inside the hover area's padding: the area itself reaches out to the nav's edges.)
    // Each is also spaced from the row above by the gap between the icons (the hover areas' padding, above and
    // below), so it lines up with the icon beside it, as the item's own row does with its icon.
    const zoneElement = anchor.closest<HTMLElement>('.astryx-stack');
    const zoneStyle = zoneElement ? getComputedStyle(zoneElement) : null;
    const rowsStart =
      zoneElement && zoneStyle
        ? zoneElement.getBoundingClientRect().left + parseFloat(zoneStyle.paddingLeft)
        : nav.left;
    const toNavEdge = Math.max(0, nav.right - rowsStart);
    const iconGap = zoneStyle ? parseFloat(zoneStyle.paddingTop) + parseFloat(zoneStyle.paddingBottom) : 0;
    for (const row of under) {
      // Sized from the item's row: the group around them sizes to its content, not the box.
      row.style.inlineSize = `${first.getBoundingClientRect().width - toNavEdge}px`;
      row.style.marginInlineStart = `${toNavEdge}px`;
      row.style.marginBlockStart = `${iconGap}px`;
      // The open nav indents these under the item's name; starting at the nav's edge, they need no indent, so the
      // arrow and name sit at the row's start, and the spare room falls between the name and the buttons.
      const link = row.querySelector<HTMLElement>('a');
      if (link) link.style.paddingInlineStart = '0px';
    }
    // The cut: the box's corners where it's cut back (the item's row's bottom-left, and the bottom-left of the rows
    // under it) are rounded like the rows, so a row's highlight fills its corner with no box showing round it. Only
    // the straight edge of the rows under it touches the nav. (The shadow is cast around the box as cut.)
    const boxRect = box.getBoundingClientRect();
    const width = boxRect.width;
    const height = boxRect.height;
    const firstBottom = first.getBoundingClientRect().bottom - boxRect.top;
    const radius = parseFloat(getComputedStyle(first).borderBottomLeftRadius) || 0;
    const edge = toNavEdge;
    box.style.clipPath =
      `path('M 0 0 H ${width} V ${height} H ${edge + radius} A ${radius} ${radius} 0 0 1 ${edge} ${height - radius} ` +
      `V ${firstBottom} H ${radius} A ${radius} ${radius} 0 0 1 0 ${firstBottom - radius} Z')`;
    return () => cancelAnimationFrame(settleFrame);
  }, [isOpen]);

  return (
    // The hover area is where the item's highlight bar would be: the nav's full width (reaching out over the nav's
    // own padding to both its edges) and the row's height, touching its neighbours' (no gap to fall through).
    <VStack
      hAlign="center"
      // Over: anywhere in it, its row included (a descendant), so moving between the row and the icon keeps it out.
      onMouseOver={onShow}
      onMouseLeave={onHide}
      style={{
        paddingBlock: 'var(--spacing-0-5)',
        // The gap before a group isn't part of any item, so the pointer there closes the row.
        marginBlockStart: isGroupStart ? 'var(--spacing-5)' : undefined,
        marginInline: 'calc(-1 * var(--spacing-2))',
        paddingInline: 'var(--spacing-2)',
      }}>
      <Popover
        isOpen={isOpen}
        onOpenChange={(open) => (open ? onShow() : onHide())}
        placement="end"
        alignment="start"
        role="none"
        hasAutoFocus={false}
        padding={0}
        label={entry.label}
        // No surface of its own: only the rows' rounded box sticks out, and only it takes the pointer, so the
        // nav's icons beside the rows under an item (Procedures, under a pinned patient) can still be hovered.
        style={{
          background: 'transparent',
          boxShadow: 'none',
          border: 'none',
          overflow: 'visible',
          pointerEvents: 'none',
        }}
        content={
          // A SideNav of its own, unfolded, so the row renders as in the open nav (the folded one around it would
          // draw it as an icon).
          <SideNav
            ref={flyout}
            collapsible={{isCollapsed: false, hasButton: false}}
            // The open nav's own width, so every row comes out the size it is there, its buttons in the same places.
            // The rows' shadow is cast from here, from the box's shape as cut (see the layout effect), so it follows
            // the cut's rounded corners and runs across to the nav under the rows below the item's.
            style={{
              background: 'transparent',
              pointerEvents: 'none',
              filter: 'drop-shadow(0 4px 8px rgb(0 0 0 / 0.35))',
            }}>
            <VStack
              ref={rowsBox}
              gap={0}
              style={{
                // Just the rows, on the nav's own colour and rounded like a row: the hovered or selected row lights
                // up as in the open nav, and the others stay the nav's colour.
                backgroundColor: 'var(--medapp-nav-background, #0e1322)',
                borderRadius: 'var(--radius-element)',
                // The box takes the pointer; its cut-away part (beside the nav's icons) doesn't.
                pointerEvents: 'auto',
              }}>
              {wrapRow(entry.row)}
            </VStack>
          </SideNav>
        }>
        {/* The popover anchors to the icon through its ref; the icon's click is its own (a link, or showing the row). */}
        {({ref}) => (
          <IconButton
            ref={(el: HTMLElement | null) => {
              ref(el);
              icon.current = el;
            }}
            label={entry.label}
            icon={<Icon icon={entry.icon} />}
            // The nav's own highlight, not a circle: a rounded square (as a nav row's), shaded on hover, and filled
            // like a selected row for the current page. While its row is out, the row's own icon stands in for it,
            // so it drops the fill (which could peek out around the row); it stays in place for the pointer.
            variant="ghost"
            style={{
              borderRadius: 'var(--radius-element)',
              // No fade: the fill comes back the moment the row goes (a fade showed as a flicker).
              transition: 'none',
              backgroundColor: isOpen
                ? undefined
                : entry.isSelected || entry.isPressed
                  ? 'var(--color-neutral)'
                  : entry.isHalfView
                    ? 'color-mix(in srgb, var(--color-neutral) 65%, transparent)'
                    : undefined,
            }}
            aria-current={entry.isSelected ? 'page' : undefined}
            aria-pressed={entry.isPressed}
            href={entry.href}
            onClick={entry.href ? undefined : onShow}
          />
        )}
      </Popover>
    </VStack>
  );
}
