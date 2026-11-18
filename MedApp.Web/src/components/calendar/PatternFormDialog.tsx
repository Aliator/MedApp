import {Button} from '@astryxdesign/core/Button';
import type {ISODateString} from '@astryxdesign/core/Calendar';
import {DateInput} from '@astryxdesign/core/DateInput';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {staffWithPermissionQuery} from '../../api/calendar';
import {SLOT_LENGTHS, useSavePattern, type ClinicPattern} from '../../api/clinics';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {roomsQuery} from '../../api/sites';
import {specialtiesQuery} from '../../api/specialties';
import {useAnnounce} from '../../app/announcements';
import {ukToday} from '../../utils/calendarSlots';
import {WEEK_STARTS_ON} from '../../utils/dates';
import {hourOptions, WEEK_DAYS} from '../../utils/workingHours';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {DialogFrame} from '../modals/DialogFrame';
import {AlertMessage, ValidationAlert} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

export interface PatternFormDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** A pattern to change; otherwise a new one. */
  pattern?: ClinicPattern;
}

const FIELDS = ['staffUserId', 'roomId', 'specialtyId', 'dayOfWeek', 'starts', 'ends', 'slotMinutes', 'everyWeeks', 'fromDate', 'untilDate'];

const clock = (time: string | null | undefined, fallback: string) => (time ? time.slice(0, 5) : fallback);
const minutesOf = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));

/**
 * Adding or changing a clinic pattern (centre plan decision 125, ManageClinics): the clinician, room and specialty, a day
 * of the week, UK clock times in 15-minute steps between 06:00 and 23:00, the slot length, every 1 to 4 weeks, from a
 * date and optionally until one. It makes no sessions itself: Create sessions does, when asked. Changing it leaves the
 * sessions it has already made as they are.
 */
export function PatternFormDialog(props: PatternFormDialogProps) {
  const [opened, setOpened] = useState(0);
  const [wasOpen, setWasOpen] = useState(props.isOpen);
  if (props.isOpen !== wasOpen) {
    setWasOpen(props.isOpen);
    if (props.isOpen) setOpened((n) => n + 1);
  }
  return <PatternForm key={opened} {...props} />;
}

function PatternForm({isOpen, onClose, pattern}: PatternFormDialogProps) {
  const save = useSavePattern();
  const announce = useAnnounce();
  const [today] = useState(() => ukToday());
  const [staffUserId, setStaffUserId] = useState(pattern?.staffUserId ?? '');
  const [roomId, setRoomId] = useState(pattern?.roomId ?? '');
  const [specialtyId, setSpecialtyId] = useState(pattern?.specialtyId ?? '');
  const [dayOfWeek, setDayOfWeek] = useState(pattern?.dayOfWeek ?? 'Monday');
  const [starts, setStarts] = useState(clock(pattern?.starts, '09:00'));
  const [ends, setEnds] = useState(clock(pattern?.ends, '12:00'));
  const [slotMinutes, setSlotMinutes] = useState(pattern?.slotMinutes ?? 20);
  const [everyWeeks, setEveryWeeks] = useState(pattern?.everyWeeks ?? 1);
  const [fromDate, setFromDate] = useState<string | undefined>(pattern?.fromDate ?? today);
  const [untilDate, setUntilDate] = useState<string | undefined>(pattern?.untilDate ?? undefined);
  const [validation, setValidation] = useState<ValidationErrors | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const staff = useQuery({...staffWithPermissionQuery('BookAppointments'), enabled: isOpen});
  const rooms = useQuery({...roomsQuery(), enabled: isOpen});
  const specialties = useQuery({...specialtiesQuery(false), enabled: isOpen});

  const length = minutesOf(ends) - minutesOf(starts);
  const timesProblem = length <= 0 ? 'It must end after it starts.' : length % slotMinutes !== 0 ? `It must divide into whole ${slotMinutes}-minute slots.` : null;
  const untilProblem = untilDate && fromDate && untilDate < fromDate ? 'The until date must be on or after the from date.' : null;
  const canSave = staffUserId !== '' && roomId !== '' && specialtyId !== '' && fromDate !== undefined && !timesProblem && !untilProblem && !save.isPending;

  const submit = () => {
    if (!canSave) return;
    setValidation(null);
    setProblem(null);
    save.mutate(
      {
        id: pattern?.id ?? undefined,
        expectedLastUpdated: pattern?.lastUpdated,
        staffUserId,
        roomId,
        specialtyId,
        dayOfWeek,
        starts,
        ends,
        slotMinutes,
        everyWeeks,
        fromDate,
        untilDate: untilDate ?? null,
      },
      {
        onSuccess: () => {
          announce(pattern ? 'Clinic pattern saved' : 'Clinic pattern added');
          onClose();
        },
        onError: (error) => {
          if (error instanceof ApiValidationError) setValidation(error.errors);
          else setProblem(problemMessage(error, 'The clinic pattern could not be saved. Please try again.'));
        },
      },
    );
  };

  return (
    <DialogFrame
      isOpen={isOpen}
      title={pattern ? 'Edit Clinic Pattern' : 'New Clinic Pattern'}
      icon={<Icon icon={AppIcon.ClinicPatterns} size="lg" color="accent" />}
      onClose={onClose}
      width={560}
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
            onChange={setStaffUserId}
            isLoading={staff.isLoading}
            hasSearch={(staff.data?.length ?? 0) > 8}
            status={fieldStatus(validation, 'staffUserId')}
            options={(staff.data ?? []).map((s) => ({value: s.userId ?? '', label: s.username ?? ''}))}
          />
          <HStack gap={3} vAlign="start" wrap="wrap">
            <StackItem size="fill">
              <AnchoredSelector
                label="Day"
                value={dayOfWeek}
                onChange={setDayOfWeek}
                status={fieldStatus(validation, 'dayOfWeek')}
                options={WEEK_DAYS.map((d) => ({value: d.name, label: d.name}))}
              />
            </StackItem>
            <StackItem size="fill">
              <AnchoredSelector label="Starts" value={starts} onChange={setStarts} options={hourOptions(starts)} status={fieldStatus(validation, 'starts')} />
            </StackItem>
            <StackItem size="fill">
              <AnchoredSelector
                label="Ends"
                value={ends}
                onChange={setEnds}
                options={hourOptions(ends)}
                status={timesProblem ? {type: 'error', message: timesProblem} : fieldStatus(validation, 'ends')}
              />
            </StackItem>
          </HStack>
          <HStack gap={3} vAlign="start" wrap="wrap">
            <StackItem size="fill">
              <AnchoredSelector
                label="Slot length"
                value={String(slotMinutes)}
                onChange={(value) => setSlotMinutes(Number(value))}
                status={fieldStatus(validation, 'slotMinutes')}
                options={[...new Set([...SLOT_LENGTHS, slotMinutes])].sort((a, b) => a - b).map((m) => ({value: String(m), label: `${m} minutes`}))}
              />
            </StackItem>
            <StackItem size="fill">
              <AnchoredSelector
                label="Every"
                value={String(everyWeeks)}
                onChange={(value) => setEveryWeeks(Number(value))}
                status={fieldStatus(validation, 'everyWeeks')}
                options={[1, 2, 3, 4].map((n) => ({value: String(n), label: n === 1 ? 'Week' : `${n} weeks`}))}
              />
            </StackItem>
          </HStack>
          <HStack gap={3} vAlign="start" wrap="wrap">
            <StackItem size="fill">
              <DateInput label="From" value={fromDate as ISODateString | undefined} onChange={setFromDate} weekStartsOn={WEEK_STARTS_ON} format="date_weekday" />
            </StackItem>
            <StackItem size="fill">
              <DateInput
                label="Until"
                isOptional
                value={untilDate as ISODateString | undefined}
                onChange={setUntilDate}
                weekStartsOn={WEEK_STARTS_ON}
                format="date_weekday"
                status={untilProblem ? {type: 'error', message: untilProblem} : fieldStatus(validation, 'untilDate')}
              />
            </StackItem>
          </HStack>
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
