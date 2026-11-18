import {Button} from '@astryxdesign/core/Button';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {calendarQuery, staffWithPermissionQuery} from '../../api/calendar';
import {SLOT_LENGTHS, useSaveSession, type ClinicSession} from '../../api/clinics';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {roomsQuery} from '../../api/sites';
import {specialtiesQuery} from '../../api/specialties';
import {useAnnounce} from '../../app/announcements';
import {addMinutes, minutesBetween, sameMoment, ukToday} from '../../utils/calendarSlots';
import {formatTime, WEEK_STARTS_ON} from '../../utils/dates';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {DialogFrame} from '../modals/DialogFrame';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

export interface SessionFormDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** A session to change; otherwise a new one. */
  session?: ClinicSession;
  /** For a new one: a day to start on (`yyyy-mm-dd`). */
  date?: string;
  onSaved?: (session: ClinicSession) => void;
}

/** The fields a 400 can name, shown on the fields themselves. */
const FIELDS = ['staffUserId', 'roomId', 'specialtyId', 'startUtc', 'endUtc', 'slotMinutes'];

/** Every 15 minutes from `from` to `to` (UTC, ISO), both included. */
function stepsBetween(from: string, to: string): string[] {
  const steps: string[] = [];
  for (let t = from; minutesBetween(t, to) >= 0; t = addMinutes(t, 15)) steps.push(t);
  return steps;
}

/**
 * Adding or changing a clinic session (centre plan decision 123, ManageClinics): the clinician (anyone who can be booked
 * with: BookAppointments' holders are who appointments go with), the day, the start and end inside their working day
 * that day (from their calendar, so the browser does no time zone arithmetic), the slot length, the room and the
 * specialty. A clash on their calendar or in the room, or a room whose site is closed or shut then, is the API's 409, in
 * its words. A session with appointments booked into it can only change its room and specialty (the API says so).
 */
export function SessionFormDialog(props: SessionFormDialogProps) {
  // A fresh form each time it opens.
  const [opened, setOpened] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setOpened((n) => n + 1);
  }
  return <SessionForm key={opened} {...props} />;
}

function SessionForm({isOpen, onClose, session, date: initialDate, onSaved}: SessionFormDialogProps) {
  const save = useSaveSession();
  const announce = useAnnounce();
  const [now] = useState(() => new Date());
  const today = ukToday(now);
  const isEditing = session !== undefined;

  const [staffUserId, setStaffUserId] = useState(session?.staffUserId ?? '');
  const [date, setDate] = useState<string | undefined>(session?.date ?? initialDate);
  const [start, setStart] = useState<string | undefined>(session?.startUtc);
  const [end, setEnd] = useState<string | undefined>(session?.endUtc);
  const [slotMinutes, setSlotMinutes] = useState(session?.slotMinutes ?? 20);
  const [roomId, setRoomId] = useState(session?.roomId ?? '');
  const [specialtyId, setSpecialtyId] = useState(session?.specialtyId ?? '');
  const [validation, setValidation] = useState<ValidationErrors | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const staff = useQuery({...staffWithPermissionQuery('BookAppointments'), enabled: isOpen});
  const rooms = useQuery({...roomsQuery(), enabled: isOpen});
  const specialties = useQuery({...specialtiesQuery(false), enabled: isOpen});
  const calendar = useQuery({
    ...calendarQuery({staffUserId, from: date ?? today, to: date ?? today}),
    enabled: isOpen && staffUserId !== '' && date !== undefined,
  });
  const day = calendar.data?.days?.[0];
  const works = day?.isWorkingDay === true && day.workStartUtc && day.workEndUtc;
  const steps = works ? stepsBetween(day.workStartUtc!, day.workEndUtc!) : [];
  const withCurrent = (times: string[], current: string | undefined) =>
    current && !times.some((t) => sameMoment(t, current)) ? [...times, current].sort() : times;
  const starts = withCurrent(steps.slice(0, -1), start);
  const ends = withCurrent(steps.filter((t) => start !== undefined && minutesBetween(start, t) > 0), end);
  const length = start && end ? minutesBetween(start, end) : 0;
  const divides = length > 0 && length % slotMinutes === 0;

  const canSave =
    staffUserId !== '' && roomId !== '' && specialtyId !== '' && start !== undefined && end !== undefined && length > 0 && divides && !save.isPending;

  const submit = () => {
    if (!canSave) return;
    setValidation(null);
    setProblem(null);
    save.mutate(
      {
        id: session?.id ?? undefined,
        expectedLastUpdated: session?.lastUpdated,
        staffUserId,
        roomId,
        specialtyId,
        startUtc: start,
        endUtc: end,
        slotMinutes,
      },
      {
        onSuccess: (saved) => {
          announce(isEditing ? 'Clinic session saved' : 'Clinic session added');
          onSaved?.(saved);
          onClose();
        },
        onError: (error) => {
          if (error instanceof ApiValidationError) setValidation(error.errors);
          else setProblem(problemMessage(error, 'The clinic session could not be saved. Please try again.'));
        },
      },
    );
  };

  const whyNoTimes =
    staffUserId === '' || date === undefined
      ? 'Choose a clinician and a day first.'
      : calendar.data && !works
        ? (day?.closedDayName ? `Closed: ${day.closedDayName}.` : 'They don’t work that day.')
        : undefined;

  return (
    <DialogFrame
      isOpen={isOpen}
      title={isEditing ? 'Edit Clinic Session' : 'New Clinic Session'}
      icon={<Icon icon={AppIcon.Clinics} size="lg" color="accent" />}
      onClose={onClose}
      width={520}
      purpose={save.isPending ? 'required' : 'form'}
      footer={
        <>
          <Button label="Cancel" variant="secondary" isDisabled={save.isPending} onClick={onClose} />
          <Button label="Save" variant="primary" isDisabled={!canSave} isLoading={save.isPending} onClick={submit} />
        </>
      }>
      {isOpen && (
        <VStack gap={4} hAlign="stretch">
          {problem && <AlertMessage message={problem} />}
          {validation && <ValidationAlert errors={validation} fields={FIELDS} />}
          <AnchoredSelector
            label="Clinician"
            value={staffUserId}
            placeholder="Choose a clinician"
            onChange={(id) => {
              setStaffUserId(id);
              setStart(undefined);
              setEnd(undefined);
            }}
            isDisabled={isEditing}
            disabledMessage={isEditing ? 'To give it to someone else, cancel it and make another.' : undefined}
            isLoading={staff.isLoading}
            hasSearch={(staff.data?.length ?? 0) > 8}
            status={fieldStatus(validation, 'staffUserId')}
            options={(staff.data ?? []).map((s) => ({value: s.userId ?? '', label: s.username ?? ''}))}
          />
          <DateInput
            label="Day"
            value={date as ISODateString | undefined}
            onChange={(value) => {
              setDate(value);
              setStart(undefined);
              setEnd(undefined);
            }}
            min={today as ISODateString}
            weekStartsOn={WEEK_STARTS_ON}
            format="date_weekday"
          />
          <HStack gap={3} vAlign="start">
            <StackItem size="fill">
              <AnchoredSelector
                label="Start"
                value={start ?? ''}
                placeholder="Choose a time"
                onChange={(value) => {
                  setStart(value);
                  if (end && minutesBetween(value, end) <= 0) setEnd(undefined);
                }}
                isDisabled={whyNoTimes !== undefined}
                disabledMessage={whyNoTimes}
                isLoading={calendar.isLoading}
                status={fieldStatus(validation, 'startUtc')}
                options={starts.map((t) => ({value: t, label: formatTime(t)}))}
              />
            </StackItem>
            <StackItem size="fill">
              <AnchoredSelector
                label="End"
                value={end ?? ''}
                placeholder="Choose a time"
                onChange={setEnd}
                isDisabled={whyNoTimes !== undefined || start === undefined}
                disabledMessage={whyNoTimes ?? 'Choose a start first.'}
                status={fieldStatus(validation, 'endUtc')}
                options={ends.map((t) => ({value: t, label: formatTime(t)}))}
              />
            </StackItem>
          </HStack>
          <AnchoredSelector
            label="Slot length"
            value={String(slotMinutes)}
            onChange={(value) => setSlotMinutes(Number(value))}
            status={
              length > 0 && !divides
                ? {type: 'error', message: `The session must divide into whole ${slotMinutes}-minute slots.`}
                : fieldStatus(validation, 'slotMinutes')
            }
            options={[...new Set([...SLOT_LENGTHS, slotMinutes])].sort((a, b) => a - b).map((m) => ({value: String(m), label: `${m} minutes`}))}
          />
          {length > 0 && divides && (
            <Text type="supporting" color="secondary">
              {`${length / slotMinutes} slots`}
            </Text>
          )}
          <AnchoredSelector
            label="Room"
            value={roomId}
            placeholder="Choose a room"
            onChange={setRoomId}
            isLoading={rooms.isLoading}
            hasSearch={(rooms.data?.length ?? 0) > 8}
            status={fieldStatus(validation, 'roomId')}
            options={(rooms.data ?? []).map((r) => ({value: r.id ?? '', label: `${r.name} (${r.siteName})`}))}
          />
          <AnchoredSelector
            label="Specialty"
            value={specialtyId}
            placeholder="Choose a specialty"
            onChange={setSpecialtyId}
            isLoading={specialties.isLoading}
            status={fieldStatus(validation, 'specialtyId')}
            options={(specialties.data ?? []).map((s) => ({value: s.id ?? '', label: s.name ?? ''}))}
          />
        </VStack>
      )}
    </DialogFrame>
  );
}
