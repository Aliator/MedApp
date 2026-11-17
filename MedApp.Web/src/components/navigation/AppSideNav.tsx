import {Icon} from '@astryxdesign/core/Icon';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {IconButton} from '@astryxdesign/core/IconButton';
import {NavIcon} from '@astryxdesign/core/NavIcon';
import {SideNav, SideNavHeading, SideNavItem, SideNavSection, type SideNavProps} from '@astryxdesign/core/SideNav';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Theme} from '@astryxdesign/core/theme';
import {useToast} from '@astryxdesign/core/Toast';
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
  type MouseEvent,
  type ReactNode,
} from 'react';
import {patientLabelsQuery} from '../../api/patients';
import {procedureLabelsQuery} from '../../api/procedures';
import {useColorMode} from '../../app/colorMode';
import {useNavSettings} from '../../app/navSettings';
import {defaultThemeSlug, themes} from '../../astryx-themes';
import {useCurrentUser, useLogout} from '../../auth/auth';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {useHalfViews} from '../pages/halfView';
import {AppIcon} from '../shared/icons';
import {NavRail, type NavRailEntry} from './NavRail';
import {procedureNavLabel} from './navLabels';
import {MAX_PINS, usePins, type PinKind, type Pins} from './pins';
import {NotificationBell} from './NotificationBell';
import {useNotepad} from '../notepad/NotepadContext';
import {NOTEPAD_WINDOW_BLOCKED, openNotepadWindow} from '../notepad/notepadWindow';

const ADMIN_PAGES = [
  {label: 'Users', icon: AppIcon.Users, href: '/admin/users'},
  {label: 'Stages', icon: AppIcon.StageDefinitions, href: '/admin/stages'},
  {label: 'Templates', icon: AppIcon.Templates, href: '/admin/templates'},
  {label: 'Activity Types', icon: AppIcon.ActivityTypes, href: '/admin/activity-types'},
  {label: 'Audit Log', icon: AppIcon.AuditLog, href: '/admin/audit'},
  {label: 'Insights', icon: AppIcon.Insights, href: '/admin/insights'},
  {label: 'Activity Log', icon: AppIcon.ActivityLog, href: '/admin/activity'},
  {label: 'Retention Review', icon: AppIcon.Retention, href: '/admin/retention'},
  {label: 'Monitoring', icon: AppIcon.Monitoring, href: '/admin/monitoring'},
  {label: 'Admin Settings', icon: AppIcon.Settings, href: '/admin/settings'},
];

/** Is `path` the current page or one under it? Home only matches itself (Blazor's NavLinkMatch.All). */
export function isCurrentSection(pathname: string, path: string): boolean {
  if (path === '/') return pathname === '/';
  return pathname === path || pathname.startsWith(`${path}/`);
}

const NEEDS_BOOKING = '/calendar/needs-booking';

/** The Calendar page, but not Needs booking (under its address, with a nav item of its own). */
const isCalendarPage = (pathname: string) => isCurrentSection(pathname, '/calendar') && !isCurrentSection(pathname, NEEDS_BOOKING);

const ID = '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})';
const PATIENT_PAGE = new RegExp(`^/patients/${ID}(?:/|$)`, 'i');
const PROCEDURE_PAGE = new RegExp(`^/procedures/${ID}(?:/|$)`, 'i');

/** The patient whose pages (view, edit, procedures) are open, from the path; null elsewhere. */
export function openPatientId(pathname: string): string | null {
  return PATIENT_PAGE.exec(pathname)?.[1] ?? null;
}

/** The procedure whose page is open, from the path; null elsewhere. */
export function openProcedureId(pathname: string): string | null {
  return PROCEDURE_PAGE.exec(pathname)?.[1] ?? null;
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

/** The half-view part of a row's menu: open its page beside this one, or on either side while a half view is open. */
function useHalfViewItems(label: string, href: string): DropdownMenuItemData[] {
  const halfViews = useHalfViews();
  const router = useRouter();
  const pathname = useRouterState({select: (state) => state.location.pathname});
  if (!halfViews) return [];

  // With a half view open, it opens on the left (the main view) or the right (the half view). Both are greyed out
  // while either side shows it, since a page is only open once.
  if (halfViews.openPath !== null) {
    const openOn = href === pathname ? 'left' : href === halfViews.openPath ? 'right' : null;
    const already = openOn ? `${label} is already open on the ${openOn}` : undefined;
    return [
      {
        id: 'left',
        label: 'Open on the left',
        description: already,
        icon: AppIcon.OpenLeft,
        isDisabled: openOn !== null,
        onClick: () => void router.navigate({href}),
      },
      {
        id: 'right',
        label: 'Open on the right',
        description: already,
        icon: AppIcon.OpenRight,
        isDisabled: openOn !== null,
        onClick: () => halfViews.open(href),
      },
    ];
  }

  const isOpen = href === pathname;
  return [
    {
      id: 'half',
      label: 'Open in half view',
      description: isOpen ? `${label} is already open` : undefined,
      icon: AppIcon.HalfView,
      isDisabled: isOpen,
      onClick: () => halfViews.open(href),
    },
  ];
}

/** How long the pointer rests on a row's menu button before the menu opens (so passing over it doesn't). */
const MENU_OPEN_DELAY_MS = 150;
/** How long the menu stays after the pointer leaves the button and the menu, so it can cross between them. */
const MENU_CLOSE_DELAY_MS = 200;

/**
 * A row's menu, behind one button at its end: its half views, then anything of its own (a pin, or a section's
 * show and hide). It opens when the pointer rests on the button (or from the keyboard), and closes once the pointer
 * has left the button and the menu. Like the rest of a row's controls, the button shows while the row is hovered or
 * focused (and while its menu is open), and always in the folded nav's flyouts. No tooltip: its icon says what it is.
 */
function RowMenu({label, href, items = []}: {label: string; href: string; items?: DropdownMenuItemData[]}) {
  const isActive = useContext(ActiveRowsContext)(href);
  const {mode} = useColorMode();
  const [isOpen, setIsOpen] = useState(false);
  const timer = useRef<number | undefined>(undefined);
  const triggerId = useRef<string | null>(null);
  useEffect(() => () => window.clearTimeout(timer.current), []);

  // Once it's open, the menu is set where it should be, found by the button that names it:
  // - In the app's own mode, not the nav's (always dark). Astryx hosts it in the row (the nearest place it may),
  //   outside any Theme put round it here, so its colours are switched there: the theme's follow `color-scheme`.
  // - On the cards' colour (a popover's own is the theme's, and the nav's theme sets the card token to its own).
  // - Beside the nav, as far out from its edge as the row's highlight is in from it, and level with the highlight
  //   (Astryx anchors it to the button, which sits inside both).
  useLayoutEffect(() => {
    if (!isOpen) return;
    let frame = 0;
    let tries = 0;
    const place = () => {
      const menu = triggerId.current && document.querySelector(`[aria-labelledby="${CSS.escape(triggerId.current)}"]`);
      const button = triggerId.current ? document.getElementById(triggerId.current) : null;
      const panel = menu instanceof HTMLElement ? (menu.closest<HTMLElement>('[popover]') ?? menu) : null;
      const row = button?.closest<HTMLElement>('.astryx-side-nav-item');
      const nav = button?.closest<HTMLElement>('.astryx-side-nav');
      if (panel && row && nav) {
        (panel.closest<HTMLElement>('[data-astryx-layer-root]') ?? panel).style.colorScheme = mode;
        // The menu's own surface: the popover element itself or inside it, never one round it (a flyout's).
        const surface = panel.matches('.astryx-popover') ? panel : (panel.querySelector<HTMLElement>('.astryx-popover') ?? panel);
        surface.style.backgroundColor = 'var(--medapp-card-background)';
        panel.style.translate = '';
        const box = panel.getBoundingClientRect();
        if (box.width > 0) {
          // The nav's edge; in a folded nav's flyout, the row's own (the flyout is just its rows). The gap is the
          // nav's inset of its rows.
          const navRight = nav.getBoundingClientRect().right;
          const rowRight = row.getBoundingClientRect().right;
          const edge = row.closest('[popover]') ? rowRight : navRight;
          const dx = edge + (navRight - rowRight) - box.left;
          const dy = row.getBoundingClientRect().top - box.top;
          panel.style.translate = `${dx}px ${dy}px`;
          return;
        }
      }
      // Not there or not laid out yet: try again next frame (a few times).
      if (++tries < 5) frame = requestAnimationFrame(place);
    };
    place();
    return () => cancelAnimationFrame(frame);
  }, [isOpen, mode]);

  const all = [...useHalfViewItems(label, href), ...items];
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
      onMouseLeave={() => setSoon(false, MENU_CLOSE_DELAY_MS)}
      style={shownFor(isActive || isOpen)}>
      <DropdownMenu
        items={all}
        // Beside the nav, level with the row, rather than under the row (over the rows below it).
        placement="end"
        alignment="start"
        isMenuOpen={isOpen}
        onOpenChange={(open) => {
          window.clearTimeout(timer.current);
          setIsOpen(open);
        }}
        renderTrigger={(trigger) => {
          triggerId.current = trigger.id;
          return (
            <IconButton
              {...trigger}
              label={`${label} options`}
              icon={<Icon icon={AppIcon.Menu} />}
              variant="ghost"
              // Already open from the pointer resting on it: a press leaves it open, rather than closing it.
              onPointerDown={(event) => !isOpen && trigger.onPointerDown(event)}
              onClick={(event) => !isOpen && trigger.onClick(event)}
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
  /** The full name, which its buttons' names use (the label may be short, as a procedure's is). */
  fullName: string;
  href: string;
  /** It no longer exists (a 404), so it drops out of the pins. */
  isGone: boolean;
}

/** A pinned or open patient or procedure in the side nav, indented under its section, with a pin button. */
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

/** A procedure under Procedures: the patient's initials and the procedure's acronym ("FA TKR"), from the labels lookup. */
function ProcedureNavItem(props: PinnableItemProps) {
  const labels = useQuery(procedureLabelsQuery(props.ids));
  const label = labels.data?.find((l) => l.id === props.id);
  return (
    <PinnableNavItem
      {...props}
      kind="procedures"
      label={label ? procedureNavLabel(label.patientName ?? '', label.title ?? '') : 'Procedure'}
      fullName={label ? `${label.title} for ${label.patientName}` : 'Procedure'}
      href={`/procedures/${props.id}`}
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
 * A main section (Patients, Procedures) with its pinned items, then the open item if it isn't pinned, indented
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

/**
 * Side navigation (replaces Blazor's NavMenu): main pages, admin pages for admins, and the account footer.
 * Under Patients and under Procedures, as indented sub-items: the ones you've pinned (up to MAX_PINS of each,
 * kept until you unpin them), then the one whose pages are open, if it isn't pinned. Each item's half-view button
 * opens its page beside the current one.
 */
export interface AppSideNavProps {
  /** Folding the nav to its icons, as a half view does (see AppLayout). */
  collapsible?: SideNavProps['collapsible'];
}

export function AppSideNav({collapsible}: AppSideNavProps = {}) {
  const user = useCurrentUser();
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

  // With a half view open, clicking a nav link (a row, or a row brought out of the folded nav) either opens its
  // page on its own, closing the half view, or (Account Settings) just opens it on the left, as a link does. The
  // row's buttons open it on either side. Modified clicks (new tab etc.) are left to the browser.
  const {closesHalfView} = useNavSettings();
  const openInFull = (event: MouseEvent<HTMLElement>) => {
    if (!halfViews || halfPath === null || !closesHalfView) return;
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const href = event.target instanceof Element ? event.target.closest('a[href]')?.getAttribute('href') : null;
    if (!href?.startsWith('/')) return;
    event.preventDefault();
    halfViews.openFull(href);
  };

  const patientPins = usePins('patients', user.username);
  const procedurePins = usePins('procedures', user.username);

  const [hoveredRow, setHoveredRow] = useState<string | null>(null);
  const [focusedRow, setFocusedRow] = useState<string | null>(null);
  const isActiveRow = (href: string) => href === hoveredRow || href === focusedRow;
  const leaveFocus = (event: FocusEvent<HTMLElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget)) setFocusedRow(null);
  };

  // Each item once: its row for the open nav, and its icon for the folded one (a half view), whose hover brings
  // the same row out over the page.
  const main: NavRailEntry[] = [
    {
      key: 'tasks',
      label: 'My Tasks',
      icon: AppIcon.Tasks,
      href: '/tasks',
      isSelected: isAt('/tasks'),
      isHalfView: isHalfAt('/tasks'),
      row: (
        <SideNavItem
          label="My Tasks"
          icon={AppIcon.Tasks}
          href="/tasks"
          isSelected={isAt('/tasks')}
          ref={halfViewRow(isHalfAt('/tasks'))}
          actions={<RowMenu label="My Tasks" href="/tasks" />}
        />
      ),
    },
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
      key: 'procedures',
      label: 'Procedures',
      icon: AppIcon.Procedures,
      href: '/procedures',
      isSelected: isAt('/procedures'),
      isHalfView: isHalfAt('/procedures'),
      row: (
        <PinnableSection
          label="Procedures"
          icon={AppIcon.Procedures}
          href="/procedures"
          openId={openProcedureId(pathname)}
          isAtSection={isAt('/procedures')}
          halfOpenId={halfPath ? openProcedureId(halfPath) : null}
          isHalfAtSection={halfPath !== null && isCurrentSection(halfPath, '/procedures')}
          pins={procedurePins}
          Item={ProcedureNavItem}
        />
      ),
    },
    // The calendar, and the appointments asked for but not yet booked (under the calendar's address, but an item of
    // their own).
    ...[
      {key: 'calendar', label: 'Calendar', icon: AppIcon.Calendar, href: '/calendar', isHere: (path: string) => isCalendarPage(path)},
      {key: 'needs-booking', label: 'Needs Booking', icon: AppIcon.NeedsBooking, href: NEEDS_BOOKING, isHere: (path: string) => isCurrentSection(path, NEEDS_BOOKING)},
    ].map(({key, label, icon, href, isHere}) => {
      const isSelected = isHere(pathname);
      const isHalfView = halfPath !== null && isHere(halfPath) && !isSelected;
      return {
        key,
        label,
        icon,
        href,
        isSelected,
        isHalfView,
        row: (
          <SideNavItem
            label={label}
            icon={icon}
            href={href}
            isSelected={isSelected}
            ref={halfViewRow(isHalfView)}
            actions={<RowMenu label={label} href={href} />}
          />
        ),
      };
    }),
  ];
  const admin: NavRailEntry[] = user.isAdmin
    ? ADMIN_PAGES.map((page) => ({
        key: page.href,
        label: page.label,
        icon: page.icon,
        href: page.href,
        isSelected: isAt(page.href),
        isHalfView: isHalfAt(page.href),
        row: (
          <SideNavItem
            label={page.label}
            icon={page.icon}
            href={page.href}
            isSelected={isAt(page.href)}
            ref={halfViewRow(isHalfAt(page.href))}
            actions={<RowMenu label={page.label} href={page.href} />}
          />
        ),
      }))
    : [];
  // The notepad opens beside every page (or over it, on a phone), and stays open while moving around.
  const notepad = useNotepad();
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
              onClick={() => (notepad.isOpen ? notepad.requestClose() : notepad.open())}
              actions={<NotepadWindowButton />}
            />
          ),
        },
      ]
    : [];
  const account: NavRailEntry[] = [
    ...notepadEntry,
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
              {user.isAdmin ? 'Administrator' : 'User'}
            </Text>
          }
        />
      ),
    },
    {
      key: 'logout',
      label: 'Logout',
      icon: AppIcon.Logout,
      isSelected: false,
      row: (
        <SideNavItem
          label="Logout"
          icon={AppIcon.Logout}
          isDisabled={logout.isPending}
          onClick={() => setIsConfirmingLogout(true)}
        />
      ),
    },
  ];
  const trackRow = (row: ReactNode) => <AlwaysShownButtons>{row}</AlwaysShownButtons>;
  const isFolded = typeof collapsible === 'object' && collapsible.isCollapsed === true;
  const rows = (entries: NavRailEntry[]) => entries.map((entry) => <Fragment key={entry.key}>{entry.row}</Fragment>);

  return (
    <ActiveRowsContext.Provider value={isActiveRow}>
      <SideNav
        collapsible={collapsible}
        onClickCapture={openInFull}
        onMouseOver={(event) => setHoveredRow(rowHref(event.target))}
        onMouseLeave={() => setHoveredRow(null)}
        onFocus={(event) => setFocusedRow(isKeyboardFocus(event.target) ? rowHref(event.target) : null)}
        onBlur={leaveFocus}
        header={
          isFolded ? (
            // Folded, Astryx's heading adds a "MedApp" tooltip it can't leave out; the app's mark speaks for itself,
            // so it's a plain link here, like the rail's icons.
            // With the bell under it, as the rail has no room beside it.
            <VStack gap={1} hAlign="center">
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
          title="Log Out"
          message="Are you sure you want to log out?"
          icon={AppIcon.Logout}
          confirmText="Log Out"
          isProcessing={logout.isPending}
          onConfirm={() => logout.mutate()}
          onCancel={() => setIsConfirmingLogout(false)}
        />
      </Theme>
    </ActiveRowsContext.Provider>
  );
}

/** The Notepad row's key among the rows that show their buttons (it has no link to be known by). */
const NOTEPAD_ROW = 'notepad';

/**
 * The Notepad row's button that opens the notepad in a small window of its own (`/notepad`), or brings that window
 * forward. The notepad docked here (or the sheet) then closes, saving first, so it isn't open in two places. A browser
 * that blocks the window gets a message saying how to allow it. Shown like the other rows' buttons.
 */
function NotepadWindowButton() {
  const isActive = useContext(ActiveRowsContext)(NOTEPAD_ROW);
  const notepad = useNotepad();
  const toast = useToast();
  const open = () => {
    // At once, in the click, so the browser lets it open a window.
    if (!openNotepadWindow()) {
      toast({body: NOTEPAD_WINDOW_BLOCKED, type: 'error', uniqueID: 'notepad-window-blocked'});
      return;
    }
    if (notepad?.isOpen) notepad.requestClose();
  };
  return (
    <IconButton
      label="Open notepad in a new window"
      icon={<Icon icon={AppIcon.NotepadWindow} />}
      variant="ghost"
      onClick={open}
      style={shownFor(isActive)}
    />
  );
}
