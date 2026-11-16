import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {Appointment} from '../../api/appointments';
import {AppointmentStatusToken} from '../calendar/CalendarTokens';
import {appointmentHref, appointmentWhen} from './StageVisit';

export interface AppointmentListProps {
  appointments: readonly Appointment[];
  /** Names each one's procedure (the patient page); leave it off where they're all for one (the procedure page). */
  showProcedure?: boolean;
  /** Names the list for screen readers, e.g. "Appointments". */
  label: string;
}

/**
 * Appointments as a short list, in the order given: each one's status, when and with whom (a link that opens it on
 * the Calendar, where it can be rescheduled or cancelled), and what it's for (its stage, and with `showProcedure`
 * its procedure). Use it wherever a few of one patient's or procedure's appointments are listed: the procedure page
 * and the patient page.
 */
export function AppointmentList({appointments, showProcedure = false, label}: AppointmentListProps) {
  return (
    <List hasDividers density="compact" edge="flush" aria-label={label}>
      {appointments.map((appointment) => {
        const about = [showProcedure ? appointment.procedureTitle : null, appointment.stageName].filter(Boolean).join(', ');
        return (
          <ListItem
            key={appointment.id}
            label={
              <Text type="body" maxLines={2}>
                <Link href={appointmentHref(appointment)}>{appointmentWhen(appointment)}</Link>
              </Text>
            }
            description={
              <HStack gap={2} vAlign="center" wrap="wrap">
                <AppointmentStatusToken status={appointment.status ?? ''} />
                {about && (
                  <Text type="supporting" color="secondary" maxLines={1}>
                    {about}
                  </Text>
                )}
              </HStack>
            }
          />
        );
      })}
    </List>
  );
}
