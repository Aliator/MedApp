import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {Appointment} from '../../api/appointments';
import type {ProcedureStage} from '../../api/procedures';
import {formatAppointmentTime, parseUtc} from '../../utils/dates';
import {AppointmentStatusToken, VisitRequirementToken} from '../calendar/CalendarTokens';
import {AppIcon} from '../shared/icons';

/**
 * A stage's visit (docs/calendar-plan.md, decisions 22 to 26): where a stage that involves a visit stands, from the
 * procedure's appointments, and what can be done about it.
 */

/** Where a stage's visit stands: the appointment that says so, if any. */
export interface StageVisitState {
  /** Attended, Booked, Requested (Requires appointment), DidNotAttend, or None (nothing yet, or only cancelled ones). */
  status: 'Attended' | 'Booked' | 'Requested' | 'DidNotAttend' | 'None';
  appointment?: Appointment;
}

const startOf = (a: Appointment) => (a.startUtc ? parseUtc(a.startUtc).getTime() : 0);
const latest = (appointments: Appointment[]) => [...appointments].sort((a, b) => startOf(b) - startOf(a))[0];

/**
 * A stage's visit, from its appointments: Attended if any was; otherwise its open one (Booked, or Requested: a stage
 * has at most one); otherwise the latest it didn't attend; otherwise none. Cancelled ones don't count.
 */
export function stageVisit(stageId: string | undefined, appointments: readonly Appointment[]): StageVisitState {
  const own = appointments.filter((a) => a.stageId === stageId && a.status !== 'Cancelled');
  const of = (status: string) => own.filter((a) => a.status === status);
  const attended = latest(of('Attended'));
  if (attended) return {status: 'Attended', appointment: attended};
  const open = of('Booked')[0] ?? of('Requested')[0];
  if (open) return {status: open.status as 'Booked' | 'Requested', appointment: open};
  const missed = latest(of('DidNotAttend'));
  return missed ? {status: 'DidNotAttend', appointment: missed} : {status: 'None'};
}

/** Whether a stage involves a visit (Suggested or Required). */
export const involvesVisit = (stage: ProcedureStage) => stage.visitRequirement === 'Suggested' || stage.visitRequirement === 'Required';

/**
 * Whether finishing the stage now is finishing it without a visit: it involves one, none is marked Attended, and it
 * isn't being finished again after an admin unlocked it (which doesn't ask again).
 */
export const finishesWithoutVisit = (stage: ProcedureStage, visit: StageVisitState) =>
  involvesVisit(stage) && visit.status !== 'Attended' && stage.state !== 'Completed';

/** An appointment's time and who it's with: "Mon 23 Nov 2026, 10:00 to 10:30 with j.smith". */
export function appointmentWhen(appointment: Appointment): string {
  if (!appointment.startUtc) return 'Not booked yet';
  const time = formatAppointmentTime(appointment.startUtc, appointment.endUtc);
  return appointment.staffUsername ? `${time} with ${appointment.staffUsername}` : time;
}

/** Where an appointment opens: on the Calendar, in its own dialog. */
export const appointmentHref = (appointment: Appointment) => `/calendar?appointment=${appointment.id}`;

/**
 * A stage's visit in a stage's title: whether it involves one, and, once there's an appointment, where it stands
 * (Requires appointment; Booked, with when and with whom; Attended; Did not attend). Nothing for a stage without a visit.
 */
export function StageVisitTokens({stage, appointments}: {stage: ProcedureStage; appointments: readonly Appointment[]}) {
  if (!involvesVisit(stage)) return null;
  const visit = stageVisit(stage.id, appointments);
  return (
    <HStack gap={2} vAlign="center" wrap="wrap">
      <VisitRequirementToken requirement={stage.visitRequirement} />
      {visit.status !== 'None' && <AppointmentStatusToken status={visit.status} />}
      {visit.status === 'Booked' && visit.appointment && (
        <Text type="supporting" color="secondary">
          {appointmentWhen(visit.appointment)}
        </Text>
      )}
    </HStack>
  );
}

export interface StageVisitProps {
  stage: ProcedureStage;
  /** The procedure's appointments (the stage's are picked out). */
  appointments: readonly Appointment[];
  /** Something is being saved: the buttons wait. */
  isBusy: boolean;
  /** Book the visit: the Requested appointment when there is one, otherwise a new one for the stage. */
  onBook: (requested?: Appointment) => void;
  /** Move a Booked visit to another time or member of staff. */
  onReschedule: (booked: Appointment) => void;
  /** Record whether the patient came, once the visit has started. */
  onOutcome: (booked: Appointment, outcome: 'Attended' | 'DidNotAttend') => void;
  /**
   * Record a visit that has already happened (a walk-in, or one booked outside MedApp), with its outcome: offered while
   * nothing is booked (none, Needs booking, or after one the patient didn't attend). Left out, it isn't offered.
   */
  onRecordPast?: () => void;
}

/**
 * The Visit part of the current stage's work (`StageWork`'s `visit`), for a stage that involves one: where its visit
 * stands, and the next thing to do. Requires appointment, or none: **Book Visit**. Booked: when and with whom,
 * **Reschedule**, and once it has started **Attended** and **Did not attend**. Did not attend: **Book Another**.
 * Attended: when, and nothing more to do. While nothing is booked, **Record Past Visit** records one that has already
 * happened (a walk-in). Each appointment links to the Calendar, where it can also be cancelled.
 */
export function StageVisit({stage, appointments, isBusy, onBook, onReschedule, onOutcome, onRecordPast}: StageVisitProps) {
  const visit = stageVisit(stage.id, appointments);
  const {appointment} = visit;
  const what =
    visit.status === 'Requested' || visit.status === 'None'
      ? stage.visitRequirement === 'Required'
        ? "Book it, and mark it Attended once it's happened: the stage can't be finished until then."
        : 'Book it, if the patient needs one.'
      : appointment && appointmentWhen(appointment);

  return (
    <VStack gap={2} role="group" aria-label="Visit">
      <HStack gap={2} vAlign="center" wrap="wrap">
        <Text type="body" weight="semibold">
          Visit
        </Text>
        <VisitRequirementToken requirement={stage.visitRequirement} />
        {visit.status !== 'None' && <AppointmentStatusToken status={visit.status} />}
      </HStack>
      <Text type="body" color="secondary">
        {appointment && visit.status !== 'Requested' ? <Link href={appointmentHref(appointment)}>{what}</Link> : what}
      </Text>
      <HStack gap={2} wrap="wrap">
        {(visit.status === 'Requested' || visit.status === 'None') && (
          <Button
            label="Book Visit"
            variant="secondary"
            icon={<Icon icon={AppIcon.Book} />}
            isDisabled={isBusy}
            onClick={() => onBook(visit.status === 'Requested' ? appointment : undefined)}
          />
        )}
        {visit.status === 'Booked' && appointment && (
          <>
            {appointment.canRecordOutcome && (
              <>
                <Button
                  label="Attended"
                  variant="secondary"
                  icon={<Icon icon={AppIcon.Success} />}
                  isDisabled={isBusy}
                  onClick={() => onOutcome(appointment, 'Attended')}
                />
                <Button
                  label="Did Not Attend"
                  variant="secondary"
                  icon={<Icon icon={AppIcon.AppointmentDidNotAttend} />}
                  isDisabled={isBusy}
                  onClick={() => onOutcome(appointment, 'DidNotAttend')}
                />
              </>
            )}
            {appointment.canReschedule && (
              <Button
                label="Reschedule"
                variant={appointment.canRecordOutcome ? 'ghost' : 'secondary'}
                icon={<Icon icon={AppIcon.Book} />}
                isDisabled={isBusy}
                onClick={() => onReschedule(appointment)}
              />
            )}
          </>
        )}
        {visit.status === 'DidNotAttend' && (
          <Button label="Book Another" variant="secondary" icon={<Icon icon={AppIcon.Book} />} isDisabled={isBusy} onClick={() => onBook()} />
        )}
        {onRecordPast && (visit.status === 'Requested' || visit.status === 'None' || visit.status === 'DidNotAttend') && (
          <Button label="Record Past Visit" variant="ghost" icon={<Icon icon={AppIcon.RecordPast} />} isDisabled={isBusy} onClick={onRecordPast} />
        )}
      </HStack>
    </VStack>
  );
}
