import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {ApiError} from '../../api/errors';
import {patientQuery, useSetPatientFlag, whyCantChange, type Patient} from '../../api/patients';
import {useCurrentUser} from '../../auth/auth';
import {Page} from '../../components/pages/Page';
import {AnonymiseDialog} from '../../components/patients/AnonymiseDialog';
import {PatientAccessReport} from '../../components/patients/PatientAccessReport';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {PatientFlagDialog, type PatientFlagChange} from '../../components/patients/PatientFlagDialog';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {ageOn, formatDate} from '../../utils/dates';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/**
 * A patient's details (replaces Blazor's ViewPatient). An unknown patient goes back to the list, as in Blazor.
 * Banners say when the record is restricted, disputed or anonymised. Admins also get its privacy actions (export,
 * restrict, mark disputed, anonymise) and its access report: who has accessed the record, and when.
 */
export function ViewPatientPage({id}: {id: string}) {
  const navigate = useNavigate();
  const me = useCurrentUser();
  const {data: patient, error, isPending} = useQuery(patientQuery(id));
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) void navigate({to: '/patients', replace: true});
  }, [error, navigate]);

  const age = patient?.dateOfBirth ? ageOn(patient.dateOfBirth) : null;
  const canChange = patient ? whyCantChange(patient) === null : false;

  return (
    <Page
      title="View Patient"
      backHref="/patients"
      backLabel="Back to patients"
      isLoading={isPending}
      loadingMessage="Loading patient..."
      message={
        error && !(error instanceof ApiError && error.status === 404) ? (
          <AlertMessage message="The patient could not be loaded. Please try again." />
        ) : (
          patient && (
            <VStack gap={2} hAlign="stretch">
              {problem.message && <AlertMessage message={problem.message} />}
              <PatientFlagBanners patient={patient} />
            </VStack>
          )
        )
      }>
      {patient && (
        <VStack gap={6} hAlign="stretch">
          <ViewCard
            footerActions={
              <>
                {me.isAdmin && <PrivacyActions patient={patient} onProblem={setProblem} />}
                <Button
                  label="Procedures"
                  variant="secondary"
                  icon={<Icon icon={AppIcon.Procedures} />}
                  href={`/patients/${patient.id}/procedures`}
                />
                {canChange && (
                  <Button
                    label="Edit Patient"
                    variant="secondary"
                    icon={<Icon icon={AppIcon.Edit} />}
                    href={`/patients/${patient.id}/edit`}
                  />
                )}
              </>
            }>
            <ViewSection title="Personal Information">
              <MetadataListItem label="First Name">{patient.firstName}</MetadataListItem>
              <MetadataListItem label="Last Name">{patient.lastName}</MetadataListItem>
              <MetadataListItem label="Date of Birth">{formatDate(patient.dateOfBirth ?? '')}</MetadataListItem>
              <MetadataListItem label="Age">{age !== null ? `${age} ${age === 1 ? 'year' : 'years'}` : '—'}</MetadataListItem>
            </ViewSection>
            <ViewSection title="Contact Information" useGrid={false}>
              <MetadataListItem label="Email Address">
                <Link href={`mailto:${patient.email}`}>{patient.email}</Link>
              </MetadataListItem>
            </ViewSection>
            <ViewSection title="Record Information" useGrid={false}>
              <MetadataListItem label="Patient ID">
                <Text type="code">{patient.id}</Text>
              </MetadataListItem>
            </ViewSection>
          </ViewCard>
          {me.isAdmin && <PatientAccessReport patientId={id} />}
        </VStack>
      )}
    </Page>
  );
}

/**
 * An admin's privacy actions for the record, behind one Privacy button: export everything held (a subject access
 * request), restrict or lift the restriction, mark or clear a dispute, and anonymise. Each asks to confirm first.
 */
function PrivacyActions({patient, onProblem}: {patient: Patient; onProblem: (problem: SaveProblem) => void}) {
  const id = patient.id ?? '';
  const setFlag = useSetPatientFlag(id);
  const [change, setChange] = useState<PatientFlagChange | null>(null);
  const [isAnonymising, setIsAnonymising] = useState(false);
  const [dialogKey, setDialogKey] = useState(0);

  const open = (next: PatientFlagChange) => {
    onProblem(NO_PROBLEM);
    setDialogKey((k) => k + 1);
    setChange(next);
  };

  const confirmFlag = (reason: string) => {
    if (!change) return;
    setFlag.mutate(
      {...change, reason},
      {
        onSuccess: () => setChange(null),
        onError: (e) => {
          onProblem(saveProblem(e, 'save'));
          setChange(null);
        },
      },
    );
  };

  const items: DropdownMenuItemData[] = [
    {id: 'export', label: 'Export Record', description: 'Everything held, for a subject access request', icon: AppIcon.View, href: `/patients/${id}/export`},
    patient.isRestricted
      ? {id: 'restrict', label: 'Lift Restriction', icon: AppIcon.Lock, onClick: () => open({flag: 'restriction', set: false})}
      : {
          id: 'restrict',
          label: 'Restrict Record',
          description: 'Nothing can be changed until it’s lifted',
          icon: AppIcon.Lock,
          isDisabled: patient.isAnonymised === true,
          onClick: () => open({flag: 'restriction', set: true}),
        },
    patient.isDisputed
      ? {id: 'dispute', label: 'Clear Dispute', icon: AppIcon.Info, onClick: () => open({flag: 'dispute', set: false})}
      : {
          id: 'dispute',
          label: 'Mark as Disputed',
          description: 'The patient disputes something in the record',
          icon: AppIcon.Info,
          isDisabled: patient.isAnonymised === true,
          onClick: () => open({flag: 'dispute', set: true}),
        },
    {
      id: 'anonymise',
      label: 'Anonymise Record',
      description: patient.isAnonymised ? 'Already anonymised' : patient.isRestricted ? 'Lift the restriction first' : 'Can’t be undone',
      icon: AppIcon.Delete,
      variant: 'destructive',
      isDisabled: patient.isAnonymised === true || patient.isRestricted === true,
      onClick: () => {
        onProblem(NO_PROBLEM);
        setDialogKey((k) => k + 1);
        setIsAnonymising(true);
      },
    },
  ];

  return (
    <>
      <DropdownMenu items={items} alignment="end" button={{label: 'Privacy', variant: 'secondary', icon: <Icon icon={AppIcon.Shield} />}} />
      <PatientFlagDialog
        key={`flag-${dialogKey}`}
        change={change}
        isProcessing={setFlag.isPending}
        onConfirm={confirmFlag}
        onCancel={() => setChange(null)}
      />
      <AnonymiseDialog
        key={`anonymise-${dialogKey}`}
        patient={isAnonymising ? {id, firstName: patient.firstName, lastName: patient.lastName, dateOfBirth: patient.dateOfBirth} : null}
        onDone={() => setIsAnonymising(false)}
        onCancel={() => setIsAnonymising(false)}
      />
    </>
  );
}
