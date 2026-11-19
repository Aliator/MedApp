import {Icon} from '@astryxdesign/core/Icon';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {IconButton} from '@astryxdesign/core/IconButton';
import {NavIcon} from '@astryxdesign/core/NavIcon';
import {SideNav, SideNavHeading, SideNavItem, SideNavSection, type SideNavProps} from '@astryxdesign/core/SideNav';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Theme} from '@astryxdesign/core/theme';
import {useQuery} from '@tanstack/react-query';
import {useRouter, useRouterState} from '@tanstack/react-router';
import {
  createContext,
  Fragment,
  useContext,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type FocusEvent,
  type ReactNode,
} from 'react';
import {createPortal} from 'react-dom';
import {patientLabelsQuery} from '../../api/patients';
import {pathwayLabelsQuery} from '../../api/pathways';
import {useColorMode} from '../../app/colorMode';
import {defaultThemeSlug, themes} from '../../astryx-themes';
import {can, isClinicStaff, useCurrentUser, useLogout} from '../../auth/auth';
import {rolesSummary} from '../../auth/roles';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {useHalfViews} from '../pages/halfView';
import {placeBesideNav} from './besideNav';
import {HALF_VIEW_HREF} from './HalfViewClicks';
import {AppIcon} from '../shared/icons';
import {FlyoutHoldContext, NavRail, type NavRailEntry} from './NavRail';
import {pathwayNavLabel} from './navLabels';
import {ADMIN_CATEGORIES, useNavCategories, type AdminCategory, type AdminPage} from './navCategories';
import {MAX_PINS, usePins, type PinKind, type Pins} from './pins';
import {NotificationBell} from './NotificationBell';
import {NOTEPAD_PATH, useNotepad} from '../notepad/NotepadContext';
import {readMeNavEntry} from '../../demo/readMeNav';
import {READ_ME_PATH as READ_ME} from '../../demo/readMeSeen';

/** Is `path` the current page or one under it? Home only matches itself (Blazor's NavLinkMatch.All). */
export function isCurrentSection(pathname: string, path: string): boolean {
  if (path === '/') return pathname === '/';
  return pathname === path || pathname.startsWith(`${path}/`);
}

const ID = '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})';
const PATIENT_PAGE = new RegExp(`^/patients/${ID}(?:/|$)`, 'i');
const PATHWAY_PAGE = new RegExp(`^/pathways/${ID}(?:/|$)`, 'i');

/** The patient whose pages (view, edit, pathways) are open, from the path; null elsewhere. */
export function openPatientId(pathname: string): string | null {
  return PATIENT_PAGE.exec(pathname)?.[1] ?? null;
}

/** The pathway whose page is open, from the path; null elsewhere. */
export function openPathwayId(pathname: string): string | null {
  return PATHWAY_PAGE.exec(pathname)?.[1] ?? null;
}

/**
 * Which nav rows show their buttons (half view, pin): the one under the pointer, and the one focused from the
 * keyboard, so the buttons are there to Tab to (a click's focus doesn't count, so they don't linger). The others keep their buttons' space but hide them, so a row doesn't
 * change shape (or move its link) as the pointer passes.
 */
const ActiveRowsContext = createContext<(href: string) => boolean>(() => true);

/** The address of the nav row an element is in (its link's, or for a row with no link, its `data-nav-row`), or null. */
function rowHref(target: EventTarget | null): string | null {
  if (!(target instanceof Element)) return null;
  // The strip between a row's menu button and its open menu (RowMenu) belongs to that row.
  const bridge = target.closest('[data-nav-bridge]');
  if (bridge) return bridge.getAttribute('data-nav-bridge');
  const row = target.closest('.astryx-side-nav-item');
  return row?.querySelector('a[href]')?.getAttribute('href') ?? row?.querySelector('[data-nav-row]')?.getAttribute('data-nav-row') ?? null;
}

/**
 * Focus from the keyboard (Tab), which shows the row's buttons so they can be reached. Focus from a click
 * doesn't, so they go as soon as the pointer leaves the row.
 */
function isKeyboardFocus(target: EventTarget | null): boolean {
  try {
    return target instanceof Element && target.matches(':focus-visible');
  } catch {
    return false; // A browser without :focus-visible: hover alone.
  }
}

/** Shown while its row is hovered or focused; otherwise hidden, keeping its place. */
const shownFor = (isActive: boolean) => (isActive ? undefined : ({visibility: 'hidden'} as const));

/**
 * Rows brought out of the folded nav (NavRail) always show their buttons: the row is only out while the pointer
 * is on its item, and buttons appearing and vanishing as it moves over the rows looked like a flicker.
 */
function AlwaysShownButtons({children}: {children: ReactNode}) {
  return <ActiveRowsContext.Provider value={() => true}>{children}</ActiveRowsContext.Provider>;
}

/** The half-view part of a row's menu: open its page in a half view, on the left or the right. */
function useHalfViewItems(href: string): DropdownMenuItemData[] {
  const halfViews = useHalfViews();
  const router = useRouter();
  const pathname = useRouterState({select: (state) => state.location.pathname});
  if (!halfViews) return [];

  // With a half view open, it opens on the left (the main view) or the right (the half view). Both are greyed out
  // while either side shows it, since a page is only open once. Greyed out with no words saying why (the owner's
  // choice): the row's own highlight already shows where it's open.
  if (halfViews.openPath !== null) {
    const openOn = href === pathname ? 'left' : href === halfViews.openPath ? 'right' : null;
    return [
      {
        id: 'left',
        label: 'Open on the left',
        icon: AppIcon.OpenLeft,
        isDisabled: openOn !== null,
        onClick: () => void router.navigate({href}),
      },
      {
        id: 'right',
        label: 'Open on the right',
        icon: AppIcon.OpenRight,
        isDisabled: openOn !== null,
        onClick: () => halfViews.open(href),
      },
    ];
  }

  // With none open, either side straight away (the owner, 19 November 2026): on the left, the current page moves to
  // the right (as Ctrl-click does); on the right, it opens beside the current page (as Shift-click does).
  const isOpen = href === pathname;
  return [
    {
      id: 'left',
      label: 'Open in half view on the left',
      icon: AppIcon.OpenLeft,
      isDisabled: isOpen,
      onClick: () => halfViews.openOnLeft(href),
    },
    {
      id: 'right',
      label: 'Open in half view on the right',
      icon: AppIcon.OpenRight,
      isDisabled: isOpen,
      onClick: () => halfViews.open(href),
    },
  ];
}

/** The gap between a row's menu button and its open menu, which the pointer can cross without the menu closing. */
interface Bridge {
  left: number;
  top: number;
  width: number;
  height: number;
}

/** Above the page it lies over (the menu itself is in the browser's top layer, above everything). */
const BRIDGE_Z_INDEX = 1000;

/** How long the pointer rests on a row's menu button before the menu opens (so passing over it doesn't). */
const MENU_OPEN_DELAY_MS = 150;
/** How long the menu stays after the pointer leaves the button and the menu, so it can cross between them. */
const MENU_CLOSE_DELAY_MS = 400;
/** How far the strip across the gap to an open menu reaches above and below the button and the menu. */
const BRIDGE_MARGIN_PX = 8;
/** How far the strip reaches into the menu, so it meets the menu wherever the menu settles. */
const BRIDGE_OVERLAP_PX = 24;

/**
 * A press that pins a menu the pointer resting opened doesn't go through the menu's own press handling (which would
 * close it). Without that, the browser's light dismiss of the menu (a `popover="auto"`) takes the release over the
 * button as a press outside and closes it. So, as the menu does for its own presses, the button is made the menu's
 * invoker until just after the release, when light dismiss has run; the menu's click capture (on the button) stops
 * that attribute toggling the menu on the click that follows.
 */
function holdInvokerThroughPress(control: HTMLElement) {
  const menuId = control.getAttribute('aria-controls');
  const doc = control.ownerDocument;
  const popoverId = menuId ? doc.getElementById(menuId)?.closest('[popover]')?.id : undefined;
  if (popoverId) {
    control.setAttribute('popovertarget', popoverId);
    const onPressEnd = () => {
      doc.removeEventListener('pointerup', onPressEnd, true);
      doc.removeEventListener('pointercancel', onPressEnd, true);
      // Light dismiss runs as the release's default action, after its listeners.
      window.setTimeout(() => control.removeAttribute('popovertarget'), 0);
    };
    doc.addEventListener('pointerup', onPressEnd, true);
    doc.addEventListener('pointercancel', onPressEnd, true);
  }
}

/**
 * A row's menu, behind one button at its end: its half views, then anything of its own (a pin, or a section's
 * show and hide). It opens when the pointer rests on the button, and closes once the pointer has left the button and
 * the menu. A click on the button (or the keyboard) pins it instead: it stays open, holding a folded nav's row out
 * with it, until a second click, Escape, a click elsewhere or an item chosen closes it. Like the rest of a row's
 * controls, the button shows while the row is hovered or focused (and while its menu is open), and always in the
 * folded nav's flyouts. No tooltip: its icon says what it is.
 */
function RowMenu({
  label,
  href,
  items = [],
  isRightOnly = false,
}: {
  label: string;
  href: string;
  items?: DropdownMenuItemData[];
  /** Offers only the half view (the right), never the main view: the notepad's page is only ever shown in a half view. */
  isRightOnly?: boolean;
}) {
  const isActive = useContext(ActiveRowsContext)(href);
  const {mode} = useColorMode();
  const [isOpen, setIsOpen] = useState(false);
  // Opened with a click (or the keyboard), not just by the pointer resting on the button: it stays open whatever the
  // pointer does, until it's closed (a second click, Escape, a click elsewhere, or an item chosen), and it holds a
  // folded nav's row out with it.
  const [isPinned, setIsPinned] = useState(false);
  // Whether the menu reported opening during a handler: only a press, click or key that really opened it pins it
  // (the menu ignores some, such as a right-click, and spends the click after a press that closed it).
  const didOpen = useRef(false);
  const openPinned = (open: () => void) => {
    didOpen.current = false;
    open();
    if (didOpen.current) setIsPinned(true);
  };
  const holdFlyout = useContext(FlyoutHoldContext);
  const isHolding = isOpen && isPinned;
  useEffect(() => {
    if (!holdFlyout || !isHolding) return;
    holdFlyout(true);
    return () => holdFlyout(false);
  }, [holdFlyout, isHolding]);
  // The gap between the button and its open menu (the menu sits a little off the nav's edge), in the window's
  // coordinates: an invisible strip covers it while the menu is open (below).
  const [bridge, setBridge] = useState<Bridge | null>(null);
  const timer = useRef<number | undefined>(undefined);
  const triggerId = useRef<string | null>(null);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  // Once it's open, the menu is set where it should be (`placeBesideNav`: beside the nav, level with the row, in the
  // app's own mode and on the cards' colour), found by the button that names it.
  useLayoutEffect(() => {
    if (!isOpen) return;
    let frame = 0;
    let tries = 0;
    let isCancelled = false;
    const place = () => {
      const menu = triggerId.current && document.querySelector(`[aria-labelledby="${CSS.escape(triggerId.current)}"]`);
      const button = triggerId.current ? document.getElementById(triggerId.current) : null;
      const row = button?.closest<HTMLElement>('.astryx-side-nav-item');
      if (menu instanceof HTMLElement && row) {
        const surface = placeBesideNav({panel: menu, row, mode});
        if (surface) {
          // Where the gap is once the menu has moved: from the button's end into the menu (the menu is in the
          // browser's top layer, so the part under it never takes the pointer), and a little above and below both.
          // Measured again once the menu has settled: it slides into place, and a strip measured mid-way stopped
          // short of it, leaving a dead zone that closed the menu under a slow pointer.
          const measure = () => {
            if (isCancelled) return;
            const from = button!.getBoundingClientRect();
            const to = surface.getBoundingClientRect();
            const top = Math.min(from.top, to.top) - BRIDGE_MARGIN_PX;
            const bottom = Math.max(from.bottom, to.bottom) + BRIDGE_MARGIN_PX;
            setBridge(to.left > from.right ? {left: from.right, top, width: to.left + BRIDGE_OVERLAP_PX - from.right, height: bottom - top} : null);
          };
          frame = requestAnimationFrame(() => {
            measure();
            // Only animations that end by themselves: an endless one would hold the second measurement back for good.
            const settling = (typeof surface.getAnimations === 'function' ? surface.getAnimations({subtree: true}) : []).filter((animation) => {
              const end = animation.effect?.getComputedTiming().endTime;
              return typeof end === 'number' && Number.isFinite(end);
            });
            if (settling.length > 0) void Promise.allSettled(settling.map((animation) => animation.finished)).then(measure);
          });
          return;
        }
      }
      // Not there or not laid out yet: try again next frame (a few times).
      if (++tries < 5) frame = requestAnimationFrame(place);
    };
    place();
    return () => {
      isCancelled = true;
      cancelAnimationFrame(frame);
    };
  }, [isOpen, mode]);

  const halfViewItems = useHalfViewItems(href);
  const all = [...(isRightOnly ? halfViewItems.filter((item) => item.id !== 'left') : halfViewItems), ...items];
  if (all.length === 0) return null;

  const setSoon = (open: boolean, delay: number) => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => setIsOpen(open), delay);
  };
  return (
    // The menu is part of this (in React's tree), so the pointer moving onto it doesn't count as leaving.
    <HStack
      gap={0}
      onMouseEnter={() => setSoon(true, MENU_OPEN_DELAY_MS)}
      // A pinned menu stays when the pointer leaves.
      onMouseLeave={() => {
        if (!isPinned) setSoon(false, MENU_CLOSE_DELAY_MS);
      }}
      style={shownFor(isActive || isOpen)}>
      {/* The pointer crossing from the button to the menu crosses a gap that's part of neither, which closed the menu
          (and a folded nav's row brought out, and the row's buttons). While the menu is open, an invisible strip
          covers that gap. It's in this row's React tree (a portal), so the pointer on it is still on the row, its
          menu and the flyout, and nothing closes. Over the page, outside the side nav, as the nav would cut it off. */}
      {isOpen &&
        bridge &&
        createPortal(
          <VStack
            aria-hidden
            data-nav-bridge={href}
            style={{
              position: 'fixed',
              left: bridge.left,
              top: bridge.top,
              width: bridge.width,
              height: bridge.height,
              zIndex: BRIDGE_Z_INDEX,
            }}
          />,
          document.body,
        )}
      <DropdownMenu
        items={all}
        // Beside the nav, level with the row, rather than under the row (over the rows below it).
        placement="end"
        alignment="start"
        isMenuOpen={isOpen}
        // Closed by the menu itself (a press on its pinned button, Escape, a click elsewhere, an item chosen): let go.
        // Opening isn't taken as pinning here, as the menu reports opening when the pointer resting opened it too.
        onOpenChange={(open) => {
          window.clearTimeout(timer.current);
          didOpen.current = open;
          setIsOpen(open);
          if (!open) setIsPinned(false);
        }}
        renderTrigger={(trigger) => {
          triggerId.current = trigger.id;
          return (
            <IconButton
              {...trigger}
              label={`${label} options`}
              icon={<Icon icon={AppIcon.Menu} />}
              variant="ghost"
              // Closed, a press opens it pinned. Open from the pointer resting on it, a press pins it (rather than closing
              // it). Pinned, a press closes it, through the menu's own press handling (which spends the click after).
              onPointerDown={(event) => {
                if (!isOpen) openPinned(() => trigger.onPointerDown(event));
                else if (isPinned) trigger.onPointerDown(event);
                else holdInvokerThroughPress(event.currentTarget);
              }}
              // A click that isn't a press's (Enter or Space on the button, or a screen reader's) does the same.
              onClick={(event) => {
                if (!isOpen) openPinned(() => trigger.onClick(event));
                else if (!isPinned) {
                  window.clearTimeout(timer.current);
                  setIsPinned(true);
                } else trigger.onClick(event);
              }}
              // Opened from the keyboard, it's pinned too.
              onKeyDown={(event) => (isOpen ? trigger.onKeyDown(event) : openPinned(() => trigger.onKeyDown(event)))}
            />
          );
        }}
      />
    </HStack>
  );
}

/**
 * The page in the half view is marked too, a shade darker than the main page's (the selected row's fill at about
 * two-thirds strength). Astryx's selected fill can only mark one page, so this is set on the whole row (the
 * element around the row's link and its buttons), found from the link the row hands its ref to.
 */
export const HALF_VIEW_SHADE = 'color-mix(in srgb, var(--color-neutral) 65%, transparent)';
const halfViewRow = (isHalfView: boolean) => (link: HTMLElement | null) => {
  const row = link?.closest<HTMLElement>('.astryx-side-nav-item');
  if (row) row.style.backgroundColor = isHalfView ? HALF_VIEW_SHADE : '';
};

interface PinnableItemProps {
  id: string;
  /** Every item the section shows, so their labels come in one request. */
  ids: string[];
  /** This item's pages are the ones open. */
  isOpen: boolean;
  /** Its pages are the ones in the half view. */
  isHalfOpen: boolean;
  pins: Pins;
}

interface PinnableNavItemProps extends PinnableItemProps {
  kind: PinKind;
  label: string;
  /** The full name, which its buttons' names use (the label may be short, as a pathway's is). */
  fullName: string;
  href: string;
  /** It no longer exists (a 404), so it drops out of the pins. */
  isGone: boolean;
}

/** A pinned or open patient or pathway in the side nav, indented under its section, with a pin button. */
function PinnableNavItem({id, isOpen, isHalfOpen, pins, kind, label, fullName, href, isGone}: PinnableNavItemProps) {
  const isPinned = pins.isPinned(id);
  const {unpin} = pins;
  useEffect(() => {
    if (isGone && isPinned) unpin(id);
  }, [isGone, isPinned, unpin, id]);

  return (
    <SideNavItem
      label={label}
      icon={AppIcon.SubItem}
      href={href}
      isSelected={isOpen}
      ref={halfViewRow(isHalfOpen && !isOpen)}
      actions={
        <RowMenu
          label={fullName}
          href={href}
          items={[
            {
              id: 'pin',
              label: isPinned ? 'Unpin' : 'Pin',
              description: !isPinned && pins.isFull ? `You can pin up to ${MAX_PINS} ${kind}. Unpin one first.` : undefined,
              icon: isPinned ? AppIcon.Unpin : AppIcon.Pin,
              isDisabled: !isPinned && pins.isFull,
              onClick: () => (isPinned ? unpin(id) : pins.pin(id)),
            },
          ]}
        />
      }
    />
  );
}

/**
 * A patient under Patients, by name. The names come from the labels lookup, one request for the section's items,
 * which the API doesn't record as a view: showing a pin isn't opening the record. One missing from the answer no
 * longer exists, so it drops out of the pins.
 */
function PatientNavItem(props: PinnableItemProps) {
  const labels = useQuery(patientLabelsQuery(props.ids));
  const label = labels.data?.find((l) => l.id === props.id);
  const name = label?.name ?? 'Patient';
  return (
    <PinnableNavItem
      {...props}
      kind="patients"
      label={name}
      fullName={name}
      href={`/patients/${props.id}`}
      isGone={labels.isSuccess && !labels.isPlaceholderData && label === undefined}
    />
  );
}

/** A pathway under Pathways: the patient's initials and the pathway's acronym ("FA TKR"), from the labels lookup. */
function PathwayNavItem(props: PinnableItemProps) {
  const labels = useQuery(pathwayLabelsQuery(props.ids));
  const label = labels.data?.find((l) => l.id === props.id);
  return (
    <PinnableNavItem
      {...props}
      kind="pathways"
      label={label ? pathwayNavLabel(label.patientName ?? '', label.title ?? '') : 'Pathway'}
      fullName={label ? `${label.title} for ${label.patientName}` : 'Pathway'}
      href={`/pathways/${props.id}`}
      isGone={labels.isSuccess && !labels.isPlaceholderData && label === undefined}
    />
  );
}

interface PinnableSectionProps {
  label: string;
  icon: (typeof AppIcon)[keyof typeof AppIcon];
  href: string;
  /** The open item's ID, from the path, or null. */
  openId: string | null;
  /** Whether the current page is this section's (its list, or one of its items). */
  isAtSection: boolean;
  /** The half view's open item's ID, and whether its page is this section's (as for the current page). */
  halfOpenId: string | null;
  isHalfAtSection: boolean;
  pins: Pins;
  Item: (props: PinnableItemProps) => ReactNode;
}

/**
 * A main section (Patients, Pathways) with its pinned items, then the open item if it isn't pinned, indented
 * under it. While it has any, an arrow at the end of its row folds them away.
 */
function PinnableSection({
  label,
  icon,
  href,
  openId,
  isAtSection,
  halfOpenId,
  isHalfAtSection,
  pins,
  Item,
}: PinnableSectionProps) {
  // Pinned items, then the open one and the half view's (if they aren't pinned already).
  const ids = [...pins.ids];
  for (const id of [openId, halfOpenId]) if (id && !ids.includes(id)) ids.push(id);
  const isMainSelected = isAtSection && openId === null;
  const sectionShade = halfViewRow(isHalfAtSection && halfOpenId === null && !isMainSelected);
  const [isOpen, setIsOpen] = useState(true);
  if (ids.length === 0) {
    return (
      <SideNavItem
        label={label}
        icon={icon}
        href={href}
        isSelected={isAtSection}
        ref={halfViewRow(isHalfAtSection && !isAtSection)}
        actions={<RowMenu label={label} href={href} />}
      />
    );
  }

  // The items fold away from the row's menu (Astryx's own toggle would sit beside it).
  const fold = `${isOpen ? 'Hide' : 'Show'} pinned and open ${label.toLowerCase()}`;
  return (
    <SideNavItem
      label={label}
      icon={icon}
      href={href}
      isSelected={isMainSelected}
      ref={sectionShade}
      collapsible={false}
      actions={
        <RowMenu
          label={label}
          href={href}
          items={[
            {
              id: 'fold',
              label: fold,
              icon: isOpen ? AppIcon.ChevronUp : AppIcon.ChevronDown,
              onClick: () => setIsOpen(!isOpen),
            },
          ]}
        />
      }>
      {isOpen &&
        ids.map((id) => <Item key={id} id={id} ids={ids} isOpen={id === openId} isHalfOpen={id === halfOpenId} pins={pins} />)}
    </SideNavItem>
  );
}

interface AdminCategoryRowProps {
  category: AdminCategory;
  /** The pages in it this person can see (at least one). */
  pages: AdminPage[];
  isOpen: boolean;
  onToggle: () => void;
  /** In the folded nav's flyout: always open, as the pinned items under Patients are there. */
  isAlwaysOpen: boolean;
  pageRow: (page: AdminPage) => ReactNode;
}

/**
 * An admin category: a row that opens with a click (or Enter) to show its pages indented under it, as the pinned items
 * show under Patients. The row is Astryx's own toggle (`aria-expanded`). It opens by itself on its current page (or
 * the half view's), with no mark of its own (the owner took the dot away): the page's own row is marked.
 */
function AdminCategoryRow({category, pages, isOpen, onToggle, isAlwaysOpen, pageRow}: AdminCategoryRowProps) {
  return (
    <SideNavItem
      label={category.label}
      icon={category.icon}
      data-nav-category={category.key}
      collapsible={isAlwaysOpen ? false : {isCollapsed: !isOpen, onCollapsedChange: onToggle}}>
      {pages.map((page) => (
        <Fragment key={page.href}>{pageRow(page)}</Fragment>
      ))}
    </SideNavItem>
  );
}

/**
 * Side navigation (replaces Blazor's NavMenu): main pages, admin pages for admins, and the account footer.
 * Under Patients and under Pathways, as indented sub-items: the ones you've pinned (up to MAX_PINS of each,
 * kept until you unpin them), then the one whose pages are open, if it isn't pinned. Each item's half-view button
 * opens its page beside the current one.
 */
export interface AppSideNavProps {
  /** Folding the nav to its icons, as a half view does (see AppLayout). */
  collapsible?: SideNavProps['collapsible'];
}

export function AppSideNav({collapsible}: AppSideNavProps = {}) {
  const user = useCurrentUser();
  // As whoami says it (an API from before account types says none, and everything shows as before).
  const isAdminAccount = user.accountType === 'Admin';
  // Staff in the lab team, whose nav leads with the lab's home (MC17).
  const isLabStaff = !isAdminAccount && (user.teams ?? []).some((t) => t.kind === 'Lab') && can(user, 'ViewPatients');
  // Staff in a clinic team, whose nav leads with the team's home (MC24).
  const hasTeamHome = isClinicStaff(user);
  const logout = useLogout();
  const [isConfirmingLogout, setIsConfirmingLogout] = useState(false);
  const {mode} = useColorMode();
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  });
  const isAt = (path: string) => isCurrentSection(pathname, path);
  // The half view's page, marked a shade darker than the current page (where it isn't the current page too).
  const halfViews = useHalfViews();
  const halfPath = halfViews?.openPath ?? null;
  const isHalfAt = (path: string) => halfPath !== null && isCurrentSection(halfPath, path) && !isAt(path);

  // A nav link always opens its page in the main view (the left, with a half view open, keeping the half view); its
  // row's menu opens it on either side, and Shift-click on the right and Ctrl-click on the left (`HalfViewClicks`).

  const patientPins = usePins('patients', user.username);
  const pathwayPins = usePins('pathways', user.username);

  const [hoveredRow, setHoveredRow] = useState<string | null>(null);
  const [focusedRow, setFocusedRow] = useState<string | null>(null);
  const isActiveRow = (href: string) => href === hoveredRow || href === focusedRow;
  const leaveFocus = (event: FocusEvent<HTMLElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget)) setFocusedRow(null);
  };

  // Each item once: its row for the open nav, and its icon for the folded one (a half view), whose hover brings
  // the same row out over the page.
  const main: NavRailEntry[] = [
    // The light demo's Read Me, in its build only (docs/agents/light-demo.md).
    ...(__LIGHT_DEMO__ ? [readMeNavEntry({isSelected: isAt(READ_ME), isHalfView: isHalfAt(READ_ME), halfViewRef: halfViewRow(isHalfAt(READ_ME)), menu: <RowMenu label="Read Me" href={READ_ME} />})] : []),
    // A clinic team's home leads the nav for clinic staff (MC24): the team's work and theirs.
    ...(hasTeamHome
      ? [
          {
            key: 'team',
            label: 'Team',
            icon: AppIcon.Teams,
            href: '/team',
            isSelected: pathname === '/team' || pathname === '/team/',
            isHalfView: halfPath === '/team' && pathname !== '/team',
            row: (
              <SideNavItem
                label="Team"
                icon={AppIcon.Teams}
                href="/team"
                isSelected={pathname === '/team' || pathname === '/team/'}
                ref={halfViewRow(halfPath === '/team' && pathname !== '/team')}
                actions={<RowMenu label="Team" href="/team" />}
              />
            ),
          },
        ]
      : []),
    // The lab's home leads the nav for lab staff (MC17): what's waiting for the lab.
    ...(isLabStaff
      ? [
          {
            key: 'lab',
            label: 'Lab',
            icon: AppIcon.Lab,
            href: '/lab',
            isSelected: pathname === '/lab' || pathname === '/lab/',
            isHalfView: halfPath === '/lab' && pathname !== '/lab',
            row: (
              <SideNavItem
                label="Lab"
                icon={AppIcon.Lab}
                href="/lab"
                isSelected={pathname === '/lab' || pathname === '/lab/'}
                ref={halfViewRow(halfPath === '/lab' && pathname !== '/lab')}
                actions={<RowMenu label="Lab" href="/lab" />}
              />
            ),
          },
        ]
      : []),
    // Admins make no clinical changes (decision 182), so the clinical worklists (My Worklist, Results, Letters) aren't
    // theirs; they read everything from Patients, Pathways and Referrals.
    ...(isAdminAccount
      ? []
      : [
          {
            key: 'tasks',
            label: 'My Worklist',
            icon: AppIcon.Tasks,
            href: '/tasks',
            isSelected: isAt('/tasks'),
            isHalfView: isHalfAt('/tasks'),
            row: (
              <SideNavItem
                label="My Worklist"
                icon={AppIcon.Tasks}
                href="/tasks"
                isSelected={isAt('/tasks')}
                ref={halfViewRow(isHalfAt('/tasks'))}
                actions={<RowMenu label="My Worklist" href="/tasks" />}
              />
            ),
          },
        ]),
    {
      key: 'referrals',
      label: 'Referrals',
      icon: AppIcon.Referrals,
      href: '/referrals',
      isSelected: isAt('/referrals'),
      isHalfView: isHalfAt('/referrals'),
      row: (
        <SideNavItem
          label="Referrals"
          icon={AppIcon.Referrals}
          href="/referrals"
          isSelected={isAt('/referrals')}
          ref={halfViewRow(isHalfAt('/referrals'))}
          actions={<RowMenu label="Referrals" href="/referrals" />}
        />
      ),
    },
    // The samples worklist, for whoever records or processes specimens; a specimen's page counts as under it.
    ...(can(user, 'RecordSpecimens') || can(user, 'ProcessSpecimens')
      ? [
          {
            key: 'samples',
            label: 'Samples',
            icon: AppIcon.Specimens,
            href: '/lab/samples',
            isSelected: isAt('/lab/samples') || isAt('/specimens'),
            isHalfView: isHalfAt('/lab/samples') || isHalfAt('/specimens'),
            row: (
              <SideNavItem
                label="Samples"
                icon={AppIcon.Specimens}
                href="/lab/samples"
                isSelected={isAt('/lab/samples') || isAt('/specimens')}
                ref={halfViewRow(isHalfAt('/lab/samples') || isHalfAt('/specimens'))}
                actions={<RowMenu label="Samples" href="/lab/samples" />}
              />
            ),
          },
        ]
      : []),
    // The lab's cases (MC15), for the lab, who work them; a case's page counts as under it.
    ...(can(user, 'ProcessSpecimens')
      ? [
          {
            key: 'lab-cases',
            label: 'Lab Cases',
            icon: AppIcon.LabCases,
            href: '/lab/cases',
            isSelected: isAt('/lab/cases'),
            isHalfView: isHalfAt('/lab/cases'),
            row: (
              <SideNavItem
                label="Lab Cases"
                icon={AppIcon.LabCases}
                href="/lab/cases"
                isSelected={isAt('/lab/cases')}
                ref={halfViewRow(isHalfAt('/lab/cases'))}
                actions={<RowMenu label="Lab Cases" href="/lab/cases" />}
              />
            ),
          },
        ]
      : []),
    // The hospitals' requests (MC18), for the lab, who register them; a request's page counts as under it.
    ...(can(user, 'ProcessSpecimens')
      ? [
          {
            key: 'hospital-requests',
            label: 'Hospital Requests',
            icon: AppIcon.Hospitals,
            href: '/hospital-requests',
            isSelected: isAt('/hospital-requests'),
            isHalfView: isHalfAt('/hospital-requests'),
            row: (
              <SideNavItem
                label="Hospital Requests"
                icon={AppIcon.Hospitals}
                href="/hospital-requests"
                isSelected={isAt('/hospital-requests')}
                ref={halfViewRow(isHalfAt('/hospital-requests'))}
                actions={<RowMenu label="Hospital Requests" href="/hospital-requests" />}
              />
            ),
          },
        ]
      : []),
    // Results awaiting sign-off and signed off, for anyone who can view patients; a result's page counts as under it.
    ...(can(user, 'ViewPatients') && !isAdminAccount
      ? [
          {
            key: 'results',
            label: 'Results',
            icon: AppIcon.Results,
            href: '/results',
            isSelected: isAt('/results'),
            isHalfView: isHalfAt('/results'),
            row: (
              <SideNavItem
                label="Results"
                icon={AppIcon.Results}
                href="/results"
                isSelected={isAt('/results')}
                ref={halfViewRow(isHalfAt('/results'))}
                actions={<RowMenu label="Results" href="/results" />}
              />
            ),
          },
        ]
      : []),
    // Letters to write, approve and send, for anyone who can view patients (decision 108).
    ...(can(user, 'ViewPatients') && !isAdminAccount
      ? [
          {
            key: 'letters',
            label: 'Letters',
            icon: AppIcon.Letters,
            href: '/letters',
            isSelected: isAt('/letters'),
            isHalfView: isHalfAt('/letters'),
            row: (
              <SideNavItem
                label="Letters"
                icon={AppIcon.Letters}
                href="/letters"
                isSelected={isAt('/letters')}
                ref={halfViewRow(isHalfAt('/letters'))}
                actions={<RowMenu label="Letters" href="/letters" />}
              />
            ),
          },
        ]
      : []),
    // No count badges (the owner, 18 November 2026): what needs attention is on the home page. Practices moved to
    // the Calendar category, Data Requests to Oversight (navCategories.ts), and the MDT list under the Calendar page.
    {
      key: 'patients',
      label: 'Patients',
      icon: AppIcon.Patients,
      href: '/patients',
      isSelected: isAt('/patients'),
      isHalfView: isHalfAt('/patients'),
      row: (
        <PinnableSection
          label="Patients"
          icon={AppIcon.Patients}
          href="/patients"
          openId={openPatientId(pathname)}
          isAtSection={isAt('/patients')}
          halfOpenId={halfPath ? openPatientId(halfPath) : null}
          isHalfAtSection={halfPath !== null && isCurrentSection(halfPath, '/patients')}
          pins={patientPins}
          Item={PatientNavItem}
        />
      ),
    },
    {
      key: 'pathways',
      label: 'Pathways',
      icon: AppIcon.Pathways,
      href: '/pathways',
      isSelected: isAt('/pathways'),
      isHalfView: isHalfAt('/pathways'),
      row: (
        <PinnableSection
          label="Pathways"
          icon={AppIcon.Pathways}
          href="/pathways"
          openId={openPathwayId(pathname)}
          isAtSection={isAt('/pathways')}
          halfOpenId={halfPath ? openPathwayId(halfPath) : null}
          isHalfAtSection={halfPath !== null && isCurrentSection(halfPath, '/pathways')}
          pins={pathwayPins}
          Item={PathwayNavItem}
        />
      ),
    },
  ];
  // The admin pages, in categories that open with a click; one shows only if the person can see a page in it.
  const isFolded = typeof collapsible === 'object' && collapsible.isCollapsed === true;
  const adminCategories = useNavCategories(`${pathname} ${halfPath ?? ''}`);
  const adminPageRow = (page: AdminPage) => (
    <SideNavItem
      label={page.label}
      icon={page.icon}
      href={page.href}
      isSelected={isAt(page.href)}
      ref={halfViewRow(isHalfAt(page.href))}
      actions={<RowMenu label={page.label} href={page.href} />}
    />
  );
  const admin: NavRailEntry[] = ADMIN_CATEGORIES.flatMap((category) => {
    const pages = category.pages.filter((page) => can(user, page.permission));
    if (pages.length === 0) return [];
    // Holding the current page (or the half view's), it opens by itself, but isn't marked: only the page's row is.
    const holds = pages.some((page) => isAt(page.href) || isHalfAt(page.href));
    return [
      {
        key: `category:${category.key}`,
        label: category.label,
        icon: category.icon,
        // Not a page: never marked as one.
        isSelected: false,
        row: (
          <AdminCategoryRow
            category={category}
            pages={pages}
            isOpen={adminCategories.isOpen(category.key, holds)}
            onToggle={() => adminCategories.toggle(category.key, holds)}
            isAlwaysOpen={isFolded}
            pageRow={adminPageRow}
          />
        ),
      },
    ];
  });
  // The notepad opens beside every page (or over it, on a phone), and stays open while moving around.
  const notepad = useNotepad();
  const toggleNotepad = () => {
    if (!notepad) return;
    if (notepad.isOpen) {
      notepad.requestClose();
      return;
    }
    // Shown in the half view, it closes there first (its box saves as it goes), so it isn't open twice side by side.
    if (halfPath === NOTEPAD_PATH) halfViews?.close();
    notepad.open();
  };
  const notepadEntry: NavRailEntry[] = notepad
    ? [
        {
          key: 'notepad',
          label: 'Notepad',
          icon: notepad.isOpen ? AppIcon.NotesOpen : AppIcon.Notes,
          // A toggle, not a page: announced as pressed while it's open (never as the current page), and filled.
          isSelected: false,
          isPressed: notepad.isOpen,
          row: (
            <SideNavItem
              label="Notepad"
              icon={notepad.isOpen ? AppIcon.NotesOpen : AppIcon.Notes}
              aria-pressed={notepad.isOpen}
              data-notepad-toggle=""
              data-nav-row={NOTEPAD_ROW}
              // Shift-click (or Ctrl-click) opens the notepad in the half view, on the right (HalfViewClicks), as its menu does.
              {...{[HALF_VIEW_HREF]: NOTEPAD_PATH}}
              onClick={toggleNotepad}
              // Beside the page, in the half view; never on the left, as its page isn't one of its own.
              actions={<RowMenu label="Notepad" href={NOTEPAD_ROW} isRightOnly />}
            />
          ),
        },
      ]
    : [];
  // The calendar, at the bottom under the notepad (the owner's choice), marked current on every page under it
  // (Requires Appointment, reached from the Calendar page, too).
  const calendarEntry: NavRailEntry = {
    key: 'calendar',
    label: 'Calendar',
    icon: AppIcon.Calendar,
    href: '/calendar',
    isSelected: isAt('/calendar'),
    isHalfView: isHalfAt('/calendar'),
    row: (
      <SideNavItem
        label="Calendar"
        icon={AppIcon.Calendar}
        href="/calendar"
        isSelected={isAt('/calendar')}
        ref={halfViewRow(isHalfAt('/calendar'))}
        actions={<RowMenu label="Calendar" href="/calendar" />}
      />
    ),
  };
  const account: NavRailEntry[] = [
    ...notepadEntry,
    calendarEntry,
    {
      key: 'account',
      label: user.username,
      icon: AppIcon.Account,
      href: '/account',
      isSelected: isAt('/account'),
      isHalfView: isHalfAt('/account'),
      row: (
        <SideNavItem
          label={user.username}
          icon={AppIcon.Account}
          href="/account"
          isSelected={isAt('/account')}
          ref={halfViewRow(isHalfAt('/account'))}
          // No menu: Account Settings opens in the main view only.
          endContent={
            <Text type="supporting" color="secondary">
              {rolesSummary(user.roles)}
            </Text>
          }
        />
      ),
    },
    {
      key: 'logout',
      label: 'Sign Out',
      icon: AppIcon.Logout,
      isSelected: false,
      row: (
        <SideNavItem
          label="Sign Out"
          icon={AppIcon.Logout}
          isDisabled={logout.isPending}
          onClick={() => setIsConfirmingLogout(true)}
        />
      ),
    },
  ];
  const trackRow = (row: ReactNode) => <AlwaysShownButtons>{row}</AlwaysShownButtons>;
  const rows = (entries: NavRailEntry[]) => entries.map((entry) => <Fragment key={entry.key}>{entry.row}</Fragment>);

  return (
    <ActiveRowsContext.Provider value={isActiveRow}>
      <SideNav
        collapsible={collapsible}
        onMouseOver={(event) => setHoveredRow(rowHref(event.target))}
        onMouseLeave={() => setHoveredRow(null)}
        onFocus={(event) => setFocusedRow(isKeyboardFocus(event.target) ? rowHref(event.target) : null)}
        onBlur={leaveFocus}
        header={
          isFolded ? (
            // Folded, Astryx's heading adds a "MedApp" tooltip it can't leave out; the app's mark speaks for itself,
            // so it's a plain link here, like the rail's icons.
            // With the bell under it, as the rail has no room beside it.
            // Room under the logo for the bell's count, which sits over its top right corner.
            // The column is as wide as the nav's rows, so the bell's list opens as far out from the nav as a row's menu
            // does (NotificationBell).
            <VStack gap={3} hAlign="center" data-nav-header="">
              <IconButton
                label="MedApp"
                icon={<NavIcon icon={<Icon icon={AppIcon.Shield} size="sm" />} />}
                variant="ghost"
                href="/"
                style={{borderRadius: 'var(--radius-element)'}}
              />
              <NotificationBell />
            </VStack>
          ) : (
            <SideNavHeading
              heading="MedApp"
              headingHref="/"
              icon={<NavIcon icon={<Icon icon={AppIcon.Shield} size="sm" />} />}
              headerEndContent={<NotificationBell />}
            />
          )
        }
        footer={
          // Lifted off the window's bottom edge, clear of the link preview the browser shows in the bottom-left
          // corner while the pointer is on a link.
          <VStack gap={0} style={{paddingBlockEnd: 'var(--spacing-6)'}}>
            {isFolded ? (
              <NavRail groups={[account]} wrapRow={trackRow} />
            ) : (
              <SideNavSection title="Account" isHeaderHidden>
                {rows(account)}
              </SideNavSection>
            )}
          </VStack>
        }>
        {isFolded ? (
          <NavRail groups={admin.length > 0 ? [main, admin] : [main]} wrapRow={trackRow} />
        ) : (
          <>
            <SideNavSection title="Main" isHeaderHidden>
              {rows(main)}
            </SideNavSection>
            {admin.length > 0 && <SideNavSection title="Administration">{rows(admin)}</SideNavSection>}
          </>
        )}
      </SideNav>
      {/* The nav is always dark; the dialog takes the app's own mode. */}
      <Theme theme={themes[defaultThemeSlug]} mode={mode}>
        <ConfirmDialog
          isOpen={isConfirmingLogout}
          title="Sign Out"
          message="Are you sure you want to sign out?"
          icon={AppIcon.Logout}
          confirmText="Sign Out"
          isProcessing={logout.isPending}
          onConfirm={() => logout.mutate()}
          onCancel={() => setIsConfirmingLogout(false)}
        />
      </Theme>
    </ActiveRowsContext.Provider>
  );
}

/** The Notepad row's key among the rows that show their buttons (it has no link to be known by): its page's address. */
const NOTEPAD_ROW = NOTEPAD_PATH;
