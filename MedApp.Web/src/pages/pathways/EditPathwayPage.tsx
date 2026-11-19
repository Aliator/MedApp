import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {can, useCurrentUser} from '../../auth/auth';
import {ApiError} from '../../api/errors';
import {pathwayQuery, useUpdatePathway, type Pathway, type PathwayDetailsInput} from '../../api/pathways';
import {FormCard} from '../../components/forms/FormCard';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {Page} from '../../components/pages/Page';
import {statusLabel} from '../../components/pathways/PathwayCells';
import {PathwaySummary} from '../../components/pathways/PathwaySummary';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {PATHWAY_DETAILS_FIELDS, PathwayDetailsFields} from './PathwayDetailsFields';
import {NOT_ASSIGNED_MESSAGE} from './PathwayPage';

const toInput = (p: Pathway): PathwayDetailsInput => ({
  title: p.title ?? '',
  dueDate: p.dueDate ?? '',
  priority: p.priority ?? 'Medium',
});

const pageProps = {
  title: 'Edit Pathway',
  backLabel: 'Back to pathway',
} as const;

/**
 * Edit a pathway's details: title, target date and priority (not its stages, which come from its template, nor its
 * notes, which are added and edited one at a time on its page). A pathway from a referral whose waiting-time clock
 * is running or paused shows its target date read-only, "from the waiting-time clock", and doesn't send it: the clock
 * sets it (decision 32). Once the clock has stopped, it's edited by hand again.
 * This part loads the pathway; an unknown one goes back to the list. The form mounts once it's here, so it
 * starts from the pathway's details.
 */
export function EditPathwayPage({id}: {id: string}) {
  const navigate = useNavigate();
  const {data: pathway, error, isPending} = useQuery(pathwayQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: '/pathways', replace: true});
  }, [isMissing, navigate]);

  if (pathway) return <EditPathwayForm id={id} pathway={pathway} />;

  return (
    <Page {...pageProps} backHref={`/pathways/${id}`} isLoading={isPending || isMissing} loadingMessage="Loading pathway..."
      message={error && !isMissing && <AlertMessage label="Pathway not loaded" message="The pathway could not be loaded. Please try again." />}
    />
  );
}

function EditPathwayForm({id, pathway}: {id: string; pathway: Pathway}) {
  const navigate = useNavigate();
  const user = useCurrentUser();
  const update = useUpdatePathway(id);
  const announce = useAnnounce();
  const [form, setForm] = useState<PathwayDetailsInput>(() => toInput(pathway));
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const backToPathway = () => void navigate({to: '/pathways/$id', params: {id}});
  // Decision 18: saving changes to a pathway that isn't yours asks first. The save confirmation asks it.
  // Holders of AssignPathways aren't warned: handing out and looking after any pathway is their job.
  const isMine = can(user, 'AssignPathways') || (pathway.assignments?.some((a) => a.username === user.username) ?? false);
  const clock = pathway.referral?.clock;
  const isDueDateFromClock = Boolean(clock) && clock?.state !== 'Stopped';

  const save = () =>
    update.mutate(isDueDateFromClock ? {title: form.title, priority: form.priority} : form, {
      onSuccess: () => {
        announce('Pathway saved');
        backToPathway();
      },
      onError: (e) => {
        setProblem(saveProblem(e, 'save', 'pathway'));
        setIsConfirming(false);
      },
    });

  const banner = pathway.patient && <PatientBanner patient={pathway.patient} />;
  // The patient's restricted, disputed or anonymised banners, as on every page about the patient (hazard H-22).
  const flags = pathway.patientFlags && <PatientFlagBanners patient={pathway.patientFlags} />;

  if (!pathway.canEditDetails) {
    return (
      <Page {...pageProps} onBack={backToPathway} banner={banner} message={flags}>
        <AlertMessage
          label="Details can't be edited"
          message={
            // A restricted or anonymised record says so; otherwise the pathway's status is why.
            pathway.readOnlyReason ?? `This pathway is ${statusLabel(pathway.status ?? '').toLowerCase()}, so its details can't be edited.`
          }
        />
      </Page>
    );
  }

  return (
    <Page
      {...pageProps}
      subtitle={pathway.patientName ? `For ${pathway.patientName}` : undefined}
      onBack={backToPathway}
      banner={banner}
      // The form scrolls inside its card, so the banner above it stays in view.
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={PATHWAY_DETAILS_FIELDS} />
        ) : problem.message ? (
          <AlertMessage label="Not saved" message={problem.message} />
        ) : (
          flags
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title={isMine ? 'Confirm Changes' : 'Not Assigned to You'}
          message={isMine ? 'Are you sure you want to save these changes?' : NOT_ASSIGNED_MESSAGE}
          intent={isMine ? undefined : 'warning'}
          icon={AppIcon.Save}
          confirmText="Confirm Save"
          isProcessing={update.isPending}
          onConfirm={save}
          onCancel={() => setIsConfirming(false)}>
          <PathwaySummary title={form.title} dueDate={isDueDateFromClock ? undefined : form.dueDate} priority={form.priority} />
        </ConfirmDialog>
      }>
      <FormCard
        onSave={() => {
          setProblem(NO_PROBLEM);
          setIsConfirming(true);
        }}
        onCancel={backToPathway}
        isProcessing={update.isPending}
        fill>
        <PathwayDetailsFields
          value={form}
          onChange={setForm}
          errors={problem.validation}
          isDisabled={update.isPending}
          isDueDateFromClock={isDueDateFromClock}
        />
      </FormCard>
    </Page>
  );
}
