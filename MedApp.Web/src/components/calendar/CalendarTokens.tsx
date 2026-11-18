import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {ACTIVITY_COLOURS, type ActivityColour, type MeetingReply} from '../../api/calendar';
import {appointmentStatusLabel} from '../../api/appointments';
import {useAccessibility} from '../../app/accessibility';
import {AppIcon} from '../shared/icons';

/**
 * Tokens for the calendar and appointments: an appointment's status, an activity type, whether a stage involves a
 * visit, and an attendee's reply to a meeting. Use them wherever these show (the calendar, Needs booking, the
 * procedure and patient pages, the setup pages, a meeting), so they look the same everywhere. Like `StatusToken`, they
 * add an icon when "Icons on statuses" is on.
 */

/** Each appointment status's colour; the calendar grid colours its appointments the same way. */
export const APPOINTMENT_STATUS_COLOURS: Record<string, TokenColor> = {
  Requested: 'yellow',
  Booked: 'blue',
  Attended: 'green',
  DidNotAttend: 'red',
  Cancelled: 'gray',
};

const APPOINTMENT_STATUS_ICONS: Record<string, IconType> = {
  Requested: AppIcon.NeedsBooking,
  Booked: AppIcon.AppointmentBooked,
  Attended: AppIcon.Success,
  DidNotAttend: AppIcon.AppointmentDidNotAttend,
  Cancelled: AppIcon.StatusCancelled,
};

/** A token's icon, if the person wants icons on statuses (Account Settings, Accessibility). */
function useStatusIcon(icon: IconType | undefined) {
  const {flags} = useAccessibility();
  return flags.statusIcons && icon ? <Icon icon={icon} /> : undefined;
}

/** An appointment's status: Needs booking (yellow), Booked (blue), Attended (green), Did not attend (red), Cancelled (gray). */
export function AppointmentStatusToken({status}: {status: string}) {
  const icon = useStatusIcon(APPOINTMENT_STATUS_ICONS[status]);
  return (
    <Token size="sm" label={appointmentStatusLabel(status)} color={APPOINTMENT_STATUS_COLOURS[status] ?? 'default'} icon={icon} />
  );
}

/** An activity type's colour as a Token colour: one of the fixed palette, or gray for anything else. */
export function activityColour(colour: string | null | undefined): ActivityColour {
  return (ACTIVITY_COLOURS as readonly string[]).includes(colour ?? '') ? (colour as ActivityColour) : 'gray';
}

/** An activity type (Lunch, Training, ...) in its colour. */
export function ActivityToken({name, colour}: {name: string; colour: string | null | undefined}) {
  const icon = useStatusIcon(AppIcon.BlockTime);
  return <Token size="sm" label={name} color={activityColour(colour)} icon={icon} />;
}

const VISIT_LABELS: Record<string, string> = {Suggested: 'Visit suggested', Required: 'Visit required'};

/** Whether a stage involves a visit: "Visit suggested" or "Visit required"; nothing for None. */
export function VisitRequirementToken({requirement}: {requirement: string | null | undefined}) {
  const icon = useStatusIcon(requirement === 'Required' ? AppIcon.Alert : AppIcon.Book);
  const label = VISIT_LABELS[requirement ?? ''];
  if (!label) return null;
  return <Token size="sm" label={label} color={requirement === 'Required' ? 'orange' : 'cyan'} icon={icon} />;
}

const MEETING_REPLIES: Record<MeetingReply, {label: string; color: TokenColor; icon: IconType}> = {
  Accepted: {label: 'Accepted', color: 'green', icon: AppIcon.MeetingAccepted},
  Declined: {label: 'Declined', color: 'red', icon: AppIcon.MeetingDeclined},
  NoReply: {label: 'No reply', color: 'gray', icon: AppIcon.MeetingNoReply},
};

/** A meeting attendee's reply: Accepted (green), Declined (red) or No reply (gray, also for anything unknown). */
export function MeetingReplyToken({reply}: {reply: string | null | undefined}) {
  const shown = MEETING_REPLIES[(reply ?? '') as MeetingReply] ?? MEETING_REPLIES.NoReply;
  const icon = useStatusIcon(shown.icon);
  return <Token size="sm" label={shown.label} color={shown.color} icon={icon} />;
}
