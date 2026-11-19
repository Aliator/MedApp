import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {sessionQuery, slotsWords, useCancelSession, type CancelledSession, type ClinicSession, type ClinicSessionDetail} from '../../api/clinics';
import {problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {formatCalendarDay, formatTimeRange} from '../../utils/calendarSlots';
import {formatAppointmentTime, formatTime} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';
import {DialogFrame} from '../modals/DialogFrame';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {TellPatientField, usePatientMessagingOn} from './TellPatientField';

/** The longest reason for cancelling a session (the API's limit, as an appointment's). */
const MAX_REASON = 500;

export interface ClinicSessionDialogProps {
  /** The session to show; the dialog is open while it's set. */
  sessionId: string | null;
  onClose: () => void;
  /** Change it (its room, team, time or slots): the caller opens the session form. Shown when it can be changed. */
  onEdit?: (session: ClinicSession) => void;
  /** Book a patient into a free slot that hasn't started: the caller opens the booking dialog with that slot. */
  onBookSlot?: (session: ClinicSession, startUtc: string) => void;
}

type Step = 'view' | 'cancelling';

/**
 * A clinic session (centre plan decisions 123 and 126): its clinician, room, team, time and how full it is, then
 * each slot, free or with the patient booked into it (opening it lists them, which the API records as a read). A free
 * slot that hasn't started can be booked. Holders of ManageClinics can change it or cancel it: cancelling asks for a
 * reason (about the clinic, never a patient), cancels every appointment booked into it that hasn't started (those that
 * have stay, for their outcome), and then lists the patients to tell, by name, NHS number and time; a session cancelled
 * earlier shows that list again. While patient messages are on, cancelling offers "Tell the patients by text or
 * email" (ticked: centre plan decision 130), and the list then says messages will go to those who chose them; the
 * list is still there for phoning anyone who didn't.
 */
export function ClinicSessionDialog({sessionId, onClose, onEdit, onBookSlot}: ClinicSessionDialogProps) {
  const isOpen = sessionId !== null;
  const detail = useQuery({...sessionQuery(sessionId ?? ''), enabled: isOpen});
  const cancel = useCancelSession();
  const announce = useAnnounce();
  const [step, setStep] = useState<Step>('view');
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [cancelled, setCancelled] = useState<CancelledSession | null>(null);
  const [tellPatients, setTellPatients] = useState(true);
  // Whether the session's cancellation told the patients (for what the call list says).
  const [toldPatients, setToldPatients] = useState(false);
  const isMessagingOn = usePatientMessagingOn(isOpen);

  const close = () => {
    setStep('view');
    setReason('');
    setProblem(null);
    setCancelled(null);
    setTellPatients(true);
    setToldPatients(false);
    onClose();
  };

  const session = cancelled?.session ?? detail.data?.session;
  const canChange = session?.canChange === true && cancelled === null;

  // Only when someone is booked into it: otherwise there's nobody to tell.
  const offersTell = isMessagingOn && (session?.bookedCount ?? 0) > 0;

  const confirmCancel = () => {
    if (!session || reason.trim() === '') return;
    setProblem(null);
    const tells = offersTell ? tellPatients : undefined;
    cancel.mutate(
      {id: session.id ?? '', reason: reason.trim(), expectedLastUpdated: session.lastUpdated ?? '', tellPatients: tells},
      {
        onSuccess: (result) => {
          setCancelled(result);
          setToldPatients(tells === true);
          setStep('view');
          announce('Clinic session cancelled');
        },
        onError: (error) => setProblem(problemMessage(error, 'The clinic session could not be cancelled. Please try again.')),
      },
    );
  };

  const footer =
    step === 'cancelling' ? (
      <>
        <Button label="Back" variant="secondary" isDisabled={cancel.isPending} onClick={() => setStep('view')} />
        <Button
          label="Cancel Session"
          variant="destructive"
          isDisabled={reason.trim() === ''}
          isLoading={cancel.isPending}
          onClick={confirmCancel}
        />
      </>
    ) : (
      <>
        {canChange && onEdit && session && <Button label="Edit" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} onClick={() => onEdit(session)} />}
        {canChange && (
          <Button label="Cancel Session" variant="secondary" icon={<Icon icon={AppIcon.Cancel} />} onClick={() => setStep('cancelling')} />
        )}
        <Button label="Close" variant="primary" onClick={close} />
      </>
    );

  return (
    <DialogFrame
      isOpen={isOpen}
      title="Clinic Session"
      icon={<Icon icon={AppIcon.Clinics} size="lg" color="accent" />}
      onClose={close}
      width={560}
      purpose={cancel.isPending ? 'required' : step === 'cancelling' ? 'form' : 'info'}
      footer={footer}>
      {isOpen && (
        <VStack gap={4} hAlign="stretch">
          {problem && <AlertMessage label="Session not cancelled" message={problem} />}
          {detail.isError && <AlertMessage label="Session not loaded" message="The clinic session could not be loaded. It may have been removed." />}
          {!session ? (
            !detail.isError && <Skeleton height="var(--spacing-20)" />
          ) : (
            <>
              {cancelled && <AlertMessage status="success" label="Clinic session cancelled" message={cancelledWords(cancelled, toldPatients)} />}
              <MetadataList label={{position: 'start'}}>
                <MetadataListItem label="When">{`${formatCalendarDay(session.date ?? '')}, ${formatTimeRange(session.startUtc ?? '', session.endUtc ?? '')}`}</MetadataListItem>
                <MetadataListItem label="Clinician">{session.staffUsername}</MetadataListItem>
                <MetadataListItem label="Room">{`${session.roomName}, ${session.siteName}`}</MetadataListItem>
                <MetadataListItem label="Team">{session.teamName}</MetadataListItem>
                <MetadataListItem label="Slots">{`${session.slotMinutes} minutes · ${slotsWords(session)}`}</MetadataListItem>
                {session.status === 'Cancelled' && session.cancelReason && (
                  <MetadataListItem label="Cancelled because">{session.cancelReason}</MetadataListItem>
                )}
              </MetadataList>
              {step === 'cancelling' ? (
                <VStack gap={3} hAlign="stretch">
                  <Text type="body">
                    {session.bookedCount
                      ? `Cancelling it cancels the ${session.bookedCount === 1 ? 'appointment' : `${session.bookedCount} appointments`} booked into it that haven’t started, with this reason, and lists the patients to tell. Any that have started stay, for their outcome to be recorded.`
                      : 'Nobody is booked into it yet.'}
                  </Text>
                  <TextArea
                    label="Reason"
                    description="About the clinic (the clinician is away, the room is closed), never a patient."
                    value={reason}
                    onChange={setReason}
                    rows={2}
                    maxLength={MAX_REASON}
                    isRequired
                  />
                  {offersTell && <TellPatientField value={tellPatients} onChange={setTellPatients} isPlural isDisabled={cancel.isPending} />}
                </VStack>
              ) : cancelled ? (
                <PatientsToTell cancelled={cancelled} />
              ) : session.status === 'Cancelled' ? (
                // A session cancelled earlier: its call list again, from the session itself.
                <PatientsToTell cancelled={{session, patientsToTell: detail.data?.patientsToTell ?? []}} />
              ) : (
                <Slots detail={detail.data} canBook={session.status !== 'Cancelled' && onBookSlot !== undefined} onBook={(start) => onBookSlot?.(session, start)} />
              )}
            </>
          )}
        </VStack>
      )}
    </DialogFrame>
  );
}

/**
 * What cancelling did, in words. With `toldPatients` (cancelled with "Tell the patients"), it says texts and emails will
 * go to those who chose them, and asks staff to tell anyone else.
 */
export function cancelledWords(cancelled: CancelledSession, toldPatients = false): string {
  const count = cancelled.patientsToTell?.length ?? 0;
  if (count === 0) return 'Clinic session cancelled. Nobody was booked into it.';
  const what = `Clinic session cancelled, with ${count === 1 ? 'its appointment' : `its ${count} appointments`}.`;
  if (toldPatients)
    return `${what} A text or email will go to ${count === 1 ? 'the patient if they chose one' : 'each patient who chose one'}; please tell anyone who didn't.`;
  return `${what} Please tell ${count === 1 ? 'this patient' : 'these patients'}.`;
}

function PatientsToTell({cancelled}: {cancelled: CancelledSession}) {
  const patients = cancelled.patientsToTell ?? [];
  if (patients.length === 0) return null;
  return (
    <VStack gap={2} hAlign="stretch" role="list" aria-label="Patients to tell">
      {patients.map((p) => (
        <HStack key={p.appointmentId} gap={3} vAlign="center" role="listitem">
          <StackItem size="fill">
            <VStack gap={0}>
              <Text type="body" weight="semibold">
                {p.patientName}
              </Text>
              <Text type="supporting" color="secondary">
                {p.nhsNumber ? `NHS No. ${formatNhsNumber(p.nhsNumber)}` : 'No NHS number'}
              </Text>
            </VStack>
          </StackItem>
          <Text type="body">{formatAppointmentTime(p.startUtc ?? '')}</Text>
        </HStack>
      ))}
    </VStack>
  );
}

function Slots({
  detail,
  canBook,
  onBook,
}: {
  detail: ClinicSessionDetail | undefined;
  canBook: boolean;
  onBook: (startUtc: string) => void;
}) {
  const slots = detail?.slots ?? [];
  return (
    <VStack gap={1} hAlign="stretch" role="list" aria-label="Slots">
      {slots.map((slot) => {
        const time = formatTime(slot.startUtc ?? '');
        return (
          <HStack key={slot.startUtc} gap={3} vAlign="center" role="listitem">
            <Text type="body" weight="semibold">
              {time}
            </Text>
            <StackItem size="fill">
              {slot.appointmentId ? (
                <Text type="body">{`${slot.patientName}${slot.appointmentStatus && slot.appointmentStatus !== 'Booked' ? ` (${statusWords(slot.appointmentStatus)})` : ''}`}</Text>
              ) : (
                <Text type="body" color="secondary">
                  {slot.isPast ? 'Free (started)' : 'Free'}
                </Text>
              )}
            </StackItem>
            {canBook && !slot.appointmentId && !slot.isPast && (
              <Button label={`Book ${time}`} variant="secondary" size="sm" onClick={() => onBook(slot.startUtc ?? '')}>
                Book
              </Button>
            )}
          </HStack>
        );
      })}
    </VStack>
  );
}

const statusWords = (status: string) => (status === 'DidNotAttend' ? 'did not attend' : status.toLowerCase());
