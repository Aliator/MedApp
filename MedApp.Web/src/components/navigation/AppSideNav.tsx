import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {NavIcon} from '@astryxdesign/core/NavIcon';
import {SideNav, SideNavHeading, SideNavItem, SideNavSection} from '@astryxdesign/core/SideNav';
import {Text} from '@astryxdesign/core/Text';
import {Tooltip} from '@astryxdesign/core/Tooltip';
import {useQuery} from '@tanstack/react-query';
import {useRouterState} from '@tanstack/react-router';
import {useEffect, type ReactNode} from 'react';
import {ApiError} from '../../api/errors';
import {patientQuery} from '../../api/patients';
import {procedureQuery} from '../../api/procedures';
import {useColorMode} from '../../app/colorMode';
import {useCurrentUser, useLogout} from '../../auth/auth';
import {AppIcon} from '../shared/icons';
import {procedureNavLabel} from './navLabels';
import {MAX_PINS, usePins, type PinKind, type Pins} from './pins';

/** Is `path` the current page or one under it? Home only matches itself (Blazor's NavLinkMatch.All). */
export function isCurrentSection(pathname: string, path: string): boolean {
  if (path === '/') return pathname === '/';
  return pathname === path || pathname.startsWith(`${path}/`);
}

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

interface PinnableItemProps {
  id: string;
  /** This item's pages are the ones open. */
  isOpen: boolean;
  pins: Pins;
}

interface PinnableNavItemProps extends PinnableItemProps {
  kind: PinKind;
  label: string;
  /** The full name: in its pin button's name, and shown on hover when the label is shorter. */
  fullName: string;
  href: string;
  /** It no longer exists (a 404), so it drops out of the pins. */
  isGone: boolean;
}

/** A pinned or open patient or procedure in the side nav, indented under its section, with a pin button. */
function PinnableNavItem({id, isOpen, pins, kind, label, fullName, href, isGone}: PinnableNavItemProps) {
  const isPinned = pins.isPinned(id);
  const {unpin} = pins;

  useEffect(() => {
    if (isGone && isPinned) unpin(id);
  }, [isGone, isPinned, unpin, id]);

  const pinLabel = isPinned
    ? `Unpin ${fullName}`
    : pins.isFull
      ? `You can pin up to ${MAX_PINS} ${kind}. Unpin one to pin ${fullName}.`
      : `Pin ${fullName}`;

  const item = (
    <SideNavItem
      label={label}
      icon={AppIcon.SubItem}
      href={href}
      isSelected={isOpen}
      actions={
        <IconButton
          label={pinLabel}
          tooltip={pinLabel}
          icon={<Icon icon={isPinned ? AppIcon.Unpin : AppIcon.Pin} />}
          variant="ghost"
          isDisabled={!isPinned && pins.isFull}
          onClick={() => (isPinned ? unpin(id) : pins.pin(id))}
        />
      }
    />
  );
  // A short label (a procedure's "FA TKR") shows the full name on hover.
  return fullName === label ? (
    item
  ) : (
    <Tooltip content={fullName} placement="end">
      {item}
    </Tooltip>
  );
}

/** A patient under Patients, by name. Usually already cached by the patient's page or an earlier visit. */
function PatientNavItem(props: PinnableItemProps) {
  const patient = useQuery(patientQuery(props.id));
  const name = patient.data ? `${patient.data.firstName} ${patient.data.lastName}` : 'Patient';
  return (
    <PinnableNavItem
      {...props}
      kind="patients"
      label={name}
      fullName={name}
      href={`/patients/${props.id}`}
      isGone={patient.error instanceof ApiError && patient.error.status === 404}
    />
  );
}

/** A procedure under Procedures: the patient's initials and the procedure's acronym ("FA TKR"), in full as a tooltip. */
function ProcedureNavItem(props: PinnableItemProps) {
  const procedure = useQuery(procedureQuery(props.id));
  const data = procedure.data;
  return (
    <PinnableNavItem
      {...props}
      kind="procedures"
      label={data ? procedureNavLabel(data.patientName ?? '', data.title ?? '') : 'Procedure'}
      fullName={data ? `${data.title} for ${data.patientName}` : 'Procedure'}
      href={`/procedures/${props.id}`}
      isGone={procedure.error instanceof ApiError && procedure.error.status === 404}
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
  pins: Pins;
  Item: (props: PinnableItemProps) => ReactNode;
}

/**
 * A main section (Patients, Procedures) with its pinned items, then the open item if it isn't pinned, indented
 * under it. It shows its open arrow while it has any.
 */
function PinnableSection({label, icon, href, openId, isAtSection, pins, Item}: PinnableSectionProps) {
  const ids = openId && !pins.isPinned(openId) ? [...pins.ids, openId] : pins.ids;
  if (ids.length === 0) return <SideNavItem label={label} icon={icon} href={href} isSelected={isAtSection} />;

  return (
    <SideNavItem label={label} icon={icon} href={href} isSelected={isAtSection && openId === null} collapsible>
      {ids.map((id) => (
        <Item key={id} id={id} isOpen={id === openId} pins={pins} />
      ))}
    </SideNavItem>
  );
}

/**
 * Side navigation (replaces Blazor's NavMenu): main pages, admin pages for admins, and the account footer.
 * Under Patients and under Procedures, as indented sub-items: the ones you've pinned (up to MAX_PINS of each,
 * kept until you unpin them), then the one whose pages are open, if it isn't pinned.
 */
export function AppSideNav() {
  const user = useCurrentUser();
  const logout = useLogout();
  const {mode, toggle} = useColorMode();
  const pathname = useRouterState({select: (state) => state.location.pathname});
  const isAt = (path: string) => isCurrentSection(pathname, path);
  const nextMode = mode === 'dark' ? 'light' : 'dark';

  const patientPins = usePins('patients', user.username);
  const procedurePins = usePins('procedures', user.username);

  return (
    <SideNav
      header={
        <SideNavHeading
          heading="MedApp"
          headingHref="/"
          icon={<NavIcon icon={<Icon icon={AppIcon.Shield} size="sm" />} />}
        />
      }
      footer={
        <SideNavSection title="Account" isHeaderHidden>
          <SideNavItem
            label={user.username}
            icon={AppIcon.Account}
            href="/account"
            isSelected={isAt('/account')}
            endContent={
              <Text type="supporting" color="secondary">
                {user.isAdmin ? 'Administrator' : 'User'}
              </Text>
            }
          />
          <SideNavItem
            label="Logout"
            icon={AppIcon.Logout}
            isDisabled={logout.isPending}
            onClick={() => logout.mutate()}
          />
        </SideNavSection>
      }
      footerIcons={
        <IconButton
          label={`Switch to ${nextMode} mode`}
          tooltip={`Switch to ${nextMode} mode`}
          icon={<Icon icon={mode === 'dark' ? AppIcon.LightMode : AppIcon.DarkMode} />}
          variant="ghost"
          onClick={toggle}
        />
      }>
      <SideNavSection title="Main" isHeaderHidden>
        <SideNavItem label="Home" icon={AppIcon.Home} href="/" isSelected={isAt('/')} />
        <PinnableSection
          label="Patients"
          icon={AppIcon.Patients}
          href="/patients"
          openId={openPatientId(pathname)}
          isAtSection={isAt('/patients')}
          pins={patientPins}
          Item={PatientNavItem}
        />
        <PinnableSection
          label="Procedures"
          icon={AppIcon.Procedures}
          href="/procedures"
          openId={openProcedureId(pathname)}
          isAtSection={isAt('/procedures')}
          pins={procedurePins}
          Item={ProcedureNavItem}
        />
      </SideNavSection>
      {user.isAdmin && (
        <SideNavSection title="Administration">
          <SideNavItem label="Users" icon={AppIcon.Users} href="/admin/users" isSelected={isAt('/admin/users')} />
        </SideNavSection>
      )}
    </SideNav>
  );
}
