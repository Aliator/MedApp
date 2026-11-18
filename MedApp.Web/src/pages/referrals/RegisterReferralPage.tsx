import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError, ApiValidationError, type ValidationErrors} from '../../api/errors';
import {patientQuery} from '../../api/patients';
import {
  patientOpenReferralsQuery,
  POSSIBLE_DUPLICATE_FIELD,
  useRegisterReferral,
  type ReferralDetailsInput,
} from '../../api/referrals';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {emptyReferralDetails, REFERRAL_DETAILS_FIELDS, ReferralDetailsFields} from '../../components/referrals/ReferralDetailsFields';
import {ReferralList} from '../../components/referrals/ReferralList';
import {ReferralPatientFinder} from '../../components/referrals/ReferralPatientFinder';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

export interface RegisterReferralPageProps {
  /** The patient it's for (`?patientId=`), once chosen, or from their page. */
  patientId?: string;
  /** Chooses the patient (or, with none, goes back to finding one): the route puts it in the address. */
  onPatientChange: (patientId: string | undefined) => void;
}

/** Whether a 400 is only the server's possible duplicate (an open referral to the same team), to confirm. */
const isOnlyDuplicate = (errors: ValidationErrors) =>
  (errors.fields[POSSIBLE_DUPLICATE_FIELD]?.length ?? 0) > 0 &&
  Object.keys(errors.fields).every((field) => field === POSSIBLE_DUPLICATE_FIELD) &&
  errors.general.length === 0;

/**
 * Register Referral (decision 11): first the patient, found by NHS number or name (someone not in MedApp yet is added
 * first, and comes back chosen), shown as `PatientBanner` once chosen; then the details from the letter. The patient's
 * referrals still awaiting triage show above the details, those to the same team flagged. If the server finds a
 * possible duplicate it says so, and registering it anyway is confirmed ("This is a different referral"). A restricted
 * or anonymised record is refused with the API's words (give the letter to an admin, hazard H-40). Then on to the
 * referral's page, where its letter is attached.
 */
export function RegisterReferralPage({patientId, onPatientChange}: RegisterReferralPageProps) {
  const navigate = useNavigate();
  const announce = useAnnounce();
  const register = useRegisterReferral();
  const patient = useQuery({...patientQuery(patientId ?? ''), enabled: patientId !== undefined});
  const open = useQuery({...patientOpenReferralsQuery(patientId ?? ''), enabled: patientId !== undefined});
  const openReferrals = open.data?.items ?? [];
  const [details, setDetails] = useState<ReferralDetailsInput>(() => emptyReferralDetails());
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  // The server's words about a possible duplicate, while it asks to confirm.
  const [duplicate, setDuplicate] = useState<string | null>(null);
  const isMissing = patient.error instanceof ApiError && patient.error.status === 404;

  const backToList = () => void navigate({to: '/referrals'});

  const save = (confirmPossibleDuplicate: boolean) => {
    if (!patientId) return;
    setProblem(NO_PROBLEM);
    register.mutate(
      {patientId, confirmPossibleDuplicate, ...details},
      {
        onSuccess: (referral) => {
          setDuplicate(null);
          announce('Referral registered');
          void navigate({to: '/referrals/$id', params: {id: referral.id ?? ''}});
        },
        onError: (error) => {
          if (error instanceof ApiValidationError && isOnlyDuplicate(error.errors)) {
            setDuplicate(error.errors.fields[POSSIBLE_DUPLICATE_FIELD].join(' '));
            return;
          }
          setDuplicate(null);
          setProblem(saveProblem(error, 'add', 'referral'));
        },
      },
    );
  };

  const pageProps = {title: 'Register Referral', backHref: '/referrals', backLabel: 'Back to referrals'} as const;

  if (!patientId || isMissing) {
    return (
      <Page {...pageProps} message={isMissing && <AlertMessage message="Patient not found. They may have been deleted. Find them again." />}>
        <PanelCard title="Who is it for?">
          <ReferralPatientFinder onChoose={onPatientChange} />
        </PanelCard>
      </Page>
    );
  }

  const sameTeam = openReferrals.filter((r) => details.teamId && r.teamId === details.teamId);

  return (
    <Page
      {...pageProps}
      subtitle={patient.data ? `For ${patient.data.firstName} ${patient.data.lastName}` : undefined}
      isLoading={patient.isPending}
      loadingMessage="Loading patient..."
      fill
      banner={patient.data && <PatientBanner patient={patient.data} />}
      actions={
        <Button label="Change Patient" variant="secondary" icon={<Icon icon={AppIcon.Patients} />} onClick={() => onPatientChange(undefined)} />
      }
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={REFERRAL_DETAILS_FIELDS} />
        ) : problem.message ? (
          // A restricted or anonymised record: the API says to give the letter to an admin, so it isn't lost.
          <AlertMessage message={problem.message} />
        ) : patient.error ? (
          <AlertMessage message="The patient could not be loaded. Please try again." />
        ) : (
          patient.data && <PatientFlagBanners patient={patient.data} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={duplicate !== null}
          title="Possible Duplicate"
          message={duplicate ?? undefined}
          icon={AppIcon.Warning}
          intent="warning"
          confirmText="This Is a Different Referral"
          cancelText="Go Back"
          isProcessing={register.isPending}
          onConfirm={() => save(true)}
          onCancel={() => setDuplicate(null)}>
          {sameTeam.length > 0 && <ReferralList referrals={sameTeam} label="Open referrals to the same clinic" />}
        </ConfirmDialog>
      }>
      {patient.data && (
        <FormCard fill saveText="Register Referral" onSave={() => save(false)} onCancel={backToList} isProcessing={register.isPending}>
          {openReferrals.length > 0 && (
            <FormSection title="Already awaiting triage" useGrid={false}>
              <VStack gap={3} hAlign="stretch">
                <Text type="body">
                  {sameTeam.length > 0
                    ? 'This patient already has a referral to this team awaiting triage. Check it isn’t the same one.'
                    : 'This patient already has referrals awaiting triage.'}
                </Text>
                <ReferralList referrals={openReferrals} label="Referrals awaiting triage" sameTeamId={details.teamId || undefined} />
              </VStack>
            </FormSection>
          )}
          <ReferralDetailsFields value={details} onChange={setDetails} errors={problem.validation} isDisabled={register.isPending} />
        </FormCard>
      )}
    </Page>
  );
}
