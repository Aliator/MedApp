import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {ToggleButton} from '@astryxdesign/core/ToggleButton';
import {useQuery} from '@tanstack/react-query';
import {sessionsQuery, slotsWords, type ClinicSession} from '../../api/clinics';
import {teamsQuery} from '../../api/teams';
import {addMinutes, formatShortDay, sameMoment} from '../../utils/calendarSlots';
import {formatTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';

/** A slot of a clinic session, chosen for an appointment: its session, clinician, room and times. */
export interface ClinicSlotChoice {
  clinicSessionId: string;
  staffUserId: string;
  staffUsername: string;
  roomName: string;
  startUtc: string;
  endUtc: string;
}

/** A session's slot starting at `startUtc`, as a choice. */
export const slotOf = (session: ClinicSession, startUtc: string): ClinicSlotChoice => ({
  clinicSessionId: session.id ?? '',
  staffUserId: session.staffUserId ?? '',
  staffUsername: session.staffUsername ?? '',
  roomName: session.roomName ?? '',
  startUtc,
  endUtc: addMinutes(startUtc, session.slotMinutes ?? 0),
});

/** How a session is listed for booking: "Tue 3, k.ashford, Room 2, 4 of 12 free". */
export const sessionHeading = (session: ClinicSession) =>
  `${formatShortDay(session.date ?? '')}, ${session.staffUsername}, ${session.roomName}, ${slotsWords(session)}`;

export interface ClinicSlotPickerProps {
  /** UK dates, `yyyy-mm-dd`, of the sessions to list. */
  from: string;
  to: string;
  /** The appointment's team (its pathway's), when it has one: only its sessions are listed. */
  teamId?: string;
  chosen?: ClinicSlotChoice;
  onChoose: (slot: ClinicSlotChoice) => void;
}

/**
 * Booking into a clinic session (centre plan decision 124): the sessions with free slots between two dates, by date,
 * each with its clinician, room and how full it is ("Tue 3, k.ashford, Room 2, 4 of 12 free"), and its free slots as
 * buttons. Only the team's sessions when the appointment has one (its pathway's). Names no patient: the list holds only counts and
 * free times.
 */
export function ClinicSlotPicker({from, to, teamId, chosen, onChoose}: ClinicSlotPickerProps) {
  const sessions = useQuery(sessionsQuery({from, to, teamId, withFreeSlots: true}));
  const teams = useQuery({...teamsQuery(true), enabled: teamId !== undefined});
  const teamName = teams.data?.find((s) => s.id === teamId)?.name;

  if (sessions.isError) return <AlertMessage label="Clinic sessions not loaded" message="The clinic sessions could not be loaded. Please try again." />;
  if (!sessions.data)
    return (
      <VStack gap={3}>
        <Skeleton height="var(--spacing-12)" />
        <Skeleton height="var(--spacing-12)" />
      </VStack>
    );
  if (sessions.data.length === 0)
    return (
      <Text type="body" color="secondary">
        {teamName
          ? `No ${teamName} clinic sessions have a free slot in these weeks.`
          : 'No clinic sessions have a free slot in these weeks.'}
      </Text>
    );

  return (
    <VStack gap={4} hAlign="stretch">
      {sessions.data.map((session) => (
        <VStack key={session.id} gap={2} hAlign="stretch" role="group" aria-label={sessionHeading(session)}>
          <Text type="label" weight="semibold">
            {sessionHeading(session)}
          </Text>
          {teamId === undefined && (
            <Text type="supporting" color="secondary">
              {session.teamName}
            </Text>
          )}
          <HStack gap={1.5} wrap="wrap">
            {(session.freeSlotStartsUtc ?? []).map((start) => {
              const isChosen = chosen !== undefined && chosen.clinicSessionId === session.id && sameMoment(chosen.startUtc, start);
              return (
                <ToggleButton
                  key={start}
                  size="sm"
                  label={formatTime(start)}
                  isPressed={isChosen}
                  onPressedChange={() => onChoose(slotOf(session, start))}
                />
              );
            })}
          </HStack>
        </VStack>
      ))}
    </VStack>
  );
}
