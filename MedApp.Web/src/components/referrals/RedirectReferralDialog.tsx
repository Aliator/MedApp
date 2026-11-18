import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {MAX_REDIRECTED_TO, MAX_TRIAGE_DETAIL, useRedirectReferral, type Referral} from '../../api/referrals';
import {specialtiesQuery} from '../../api/specialties';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {AppIcon} from '../shared/icons';
import {TriageDialog, type TriageStep} from './TriageDialog';

const FIELDS = ['specialtyId', 'elsewhere', 'detail'] as const;

/** Where a referral is redirected: to another specialty here, or elsewhere (another provider). */
type Destination = 'specialty' | 'elsewhere';

export interface RedirectReferralDialogProps {
  isOpen: boolean;
  referral: Referral;
  onCancel: () => void;
  /** Called with the referral, redirected: in another specialty's inbox, or closed as redirected elsewhere. */
  onRedirected: (referral: Referral) => void;
}

/**
 * Redirecting a referral (decision 21), one of two ways, neither chosen to start with: to **another specialty here**
 * (one that isn't archived, other than its own, by name: it moves to that specialty's inbox, still awaiting triage,
 * its wait still counted from when it was received) or **elsewhere** (another provider, named in 200 characters: it's
 * closed as redirected elsewhere). Either way with why (500 characters, required). Then Check and Confirm shows the
 * patient, the specialty and where it's going before it's sent (hazard H-41). A 400 goes back to the form with the
 * errors on their fields; a 403 or 409 shows the API's words, and the referral is read again. Mount it afresh for
 * each opening (a `key`), so it starts empty.
 */
export function RedirectReferralDialog({isOpen, referral, onCancel, onRedirected}: RedirectReferralDialogProps) {
  const redirect = useRedirectReferral(referral.id ?? '');
  const specialties = useQuery({...specialtiesQuery(false), enabled: isOpen});
  const [step, setStep] = useState<TriageStep>('form');
  const [destination, setDestination] = useState<Destination | ''>('');
  const [specialtyId, setSpecialtyId] = useState('');
  const [elsewhere, setElsewhere] = useState('');
  const [detail, setDetail] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const others = (specialties.data ?? [])
    .filter((s) => !s.isArchived && s.id !== referral.specialtyId)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', 'en-GB', {sensitivity: 'base'}));
  const specialty = others.find((s) => s.id === specialtyId);
  const isDetailTooLong = detail.length > MAX_TRIAGE_DETAIL;
  const isWhereTooLong = elsewhere.length > MAX_REDIRECTED_TO;
  const hasDestination = destination === 'specialty' ? specialty !== undefined : destination === 'elsewhere' && elsewhere.trim() !== '' && !isWhereTooLong;
  const canContinue = hasDestination && detail.trim() !== '' && !isDetailTooLong;

  const confirm = () => {
    setMessage(null);
    const expectedVersion = referral.version ?? 0;
    redirect.mutate(
      destination === 'specialty' ? {specialtyId, detail, expectedVersion} : {elsewhere, detail, expectedVersion},
      {
        onSuccess: (redirected) => onRedirected(redirected),
        onError: (e) => {
          if (e instanceof ApiValidationError) {
            setErrors(e.errors);
            setStep('form');
            return;
          }
          setMessage(problemMessage(e, 'The referral could not be redirected. Please try again.'));
        },
      },
    );
  };

  return (
    <TriageDialog
      isOpen={isOpen}
      title="Redirect Referral"
      icon={AppIcon.StatusChange}
      referral={referral}
      step={step}
      canContinue={canContinue}
      isProcessing={redirect.isPending}
      outcome={
        destination === 'specialty'
          ? `It moves to ${specialty?.name ?? 'that specialty'}'s referrals, still awaiting triage, and its wait still counts from when it was received.`
          : "It's closed as redirected elsewhere; it stays in the patient's record. Whoever registered it is told, so the referrer can be written to."
      }
      errors={errors}
      fields={FIELDS}
      message={message}
      onContinue={() => {
        setMessage(null);
        setStep('check');
      }}
      onBack={() => setStep('form')}
      onConfirm={confirm}
      onCancel={onCancel}
      summary={
        <>
          <MetadataListItem label="Redirected to">{destination === 'specialty' ? `${specialty?.name ?? ''}, here` : elsewhere.trim()}</MetadataListItem>
          <MetadataListItem label="Why">{detail.trim()}</MetadataListItem>
        </>
      }>
      <VStack gap={4} hAlign="stretch">
        <RadioList label="Redirect to" value={destination} onChange={(value) => setDestination(value as Destination)} isRequired>
          <RadioListItem value="specialty" label="Another specialty here" description="It waits for triage there instead." />
          <RadioListItem value="elsewhere" label="Elsewhere" description="Another provider: the referral is closed here." />
        </RadioList>
        {destination === 'specialty' && (
          <>
            <AnchoredSelector
              label="Specialty"
              placeholder="Choose a specialty"
              options={others.map((s) => ({value: s.id ?? '', label: s.name ?? ''}))}
              value={specialtyId || undefined}
              onChange={(value: string) => setSpecialtyId(value)}
              isRequired
              isDisabled={specialties.isPending}
              status={fieldStatus(errors, 'specialtyId')}
            />
            {specialties.isSuccess && others.length === 0 && (
              <Text type="body" color="secondary">
                There's no other specialty here to redirect it to.
              </Text>
            )}
          </>
        )}
        {destination === 'elsewhere' && (
          <TextInput
            label="Where to"
            description="The provider or service it was sent to."
            value={elsewhere}
            onChange={setElsewhere}
            autoComplete="off"
            isRequired
            status={
              fieldStatus(errors, 'elsewhere') ??
              (isWhereTooLong ? {type: 'error', message: `At most ${MAX_REDIRECTED_TO} characters.`} : undefined)
            }
          />
        )}
        <TextArea
          label="Why"
          description="For the record, and for whoever writes to the referrer."
          value={detail}
          onChange={setDetail}
          rows={3}
          isRequired
          status={
            fieldStatus(errors, 'detail') ?? (isDetailTooLong ? {type: 'error', message: `At most ${MAX_TRIAGE_DETAIL} characters.`} : undefined)
          }
        />
      </VStack>
    </TriageDialog>
  );
}
