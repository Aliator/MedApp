import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {ApiError} from '../../api/errors';
import {referralInput, referralQuery, useReferralActions, type Referral, type ReferralDetailsInput} from '../../api/referrals';
import {FormCard} from '../../components/forms/FormCard';
import {Page} from '../../components/pages/Page';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {REFERRAL_DETAILS_FIELDS, ReferralDetailsFields} from '../../components/referrals/ReferralDetailsFields';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

const pageProps = {title: 'Edit Referral', backLabel: 'Back to referral'} as const;

/**
 * Edit a referral's details while it awaits triage (decision 13): the same fields as Register Referral. This part loads
 * the referral; the form mounts once it's here, so it starts from its details. The save carries the version shown
 * (`expectedVersion`), so a change someone made meanwhile is the API's 409, in its words, with Reload.
 */
export function EditReferralPage({id}: {id: string}) {
  const {data: referral, error, isPending} = useQuery(referralQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;

  if (referral) return <EditReferralForm id={id} referral={referral} />;

  return (
    <Page
      {...pageProps}
      backHref={`/referrals/${id}`}
      isLoading={isPending}
      loadingMessage="Loading referral..."
      message={
        error && <AlertMessage message={isMissing ? 'Referral not found. It may have been deleted.' : 'The referral could not be loaded. Please try again.'} />
      }
    />
  );
}

function EditReferralForm({id, referral}: {id: string; referral: Referral}) {
  const navigate = useNavigate();
  const announce = useAnnounce();
  const {edit} = useReferralActions(id);
  const [form, setForm] = useState<ReferralDetailsInput>(() => referralInput(referral));
  // The version the form started from: a save sends it, so a change someone made meanwhile is refused (409) until
  // Reload starts the form again from what's there now. The referral itself is read again after the 409.
  const [baseVersion, setBaseVersion] = useState(referral.version ?? 0);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [isStale, setIsStale] = useState(false);

  const reload = () => {
    setForm(referralInput(referral));
    setBaseVersion(referral.version ?? 0);
    setProblem(NO_PROBLEM);
    setIsStale(false);
  };
  const backToReferral = () => void navigate({to: '/referrals/$id', params: {id}});

  const save = () => {
    setProblem(NO_PROBLEM);
    edit.mutate(
      {...form, expectedVersion: baseVersion},
      {
        onSuccess: () => {
          announce('Referral saved');
          backToReferral();
        },
        onError: (e) => {
          setProblem(saveProblem(e, 'save', 'referral'));
          setIsStale(e instanceof ApiError && e.status === 409);
        },
      },
    );
  };

  const banner = referral.patient && <PatientBanner patient={referral.patient} />;
  const flags = referral.patientFlags && <PatientFlagBanners patient={referral.patientFlags} />;

  if (!referral.canEdit && !isStale) {
    return (
      <Page {...pageProps} onBack={backToReferral} banner={banner} message={flags}>
        <AlertMessage
          message={referral.readOnlyReason ?? "This referral's details can't be edited: only a referral awaiting triage can be, by staff who register referrals."}
        />
      </Page>
    );
  }

  return (
    <Page
      {...pageProps}
      subtitle={`Referral to ${referral.specialtyName}`}
      onBack={backToReferral}
      banner={banner}
      // The form scrolls inside its card, so the banner above it stays in view.
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={REFERRAL_DETAILS_FIELDS} />
        ) : problem.message ? (
          <VStack gap={2} hAlign="stretch">
            <AlertMessage message={problem.message} />
            {isStale && (
              <HStack>
                <Button label="Reload" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={reload} />
              </HStack>
            )}
          </VStack>
        ) : (
          flags
        )
      }>
      <FormCard onSave={save} onCancel={backToReferral} isProcessing={edit.isPending} fill>
        <ReferralDetailsFields
          value={form}
          onChange={setForm}
          errors={problem.validation}
          isDisabled={edit.isPending}
          currentSpecialty={{id: referral.specialtyId ?? '', name: referral.specialtyName ?? ''}}
        />
      </FormCard>
    </Page>
  );
}
