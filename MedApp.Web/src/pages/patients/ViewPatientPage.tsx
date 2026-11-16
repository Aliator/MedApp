import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {ApiError} from '../../api/errors';
import {patientQuery, useSetPatientFlag, whyCantChange, type Patient} from '../../api/patients';
import {NO_TIMELINE_FILTERS, type TimelineFilters} from '../../api/timeline';
import {useCurrentUser} from '../../auth/auth';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AnonymiseDialog} from '../../components/patients/AnonymiseDialog';
import {PatientAccessReport} from '../../components/patients/PatientAccessReport';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {PatientFlagDialog, type PatientFlagChange} from '../../components/patients/PatientFlagDialog';
import {PatientTimeline} from '../../components/procedures/PatientTimeline';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {ageOn, formatDate} from '../../utils/dates';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';

/**
 * A patient's details (replaces Blazor's ViewPatient) beside their timeline: everything that happened to their
 * procedures, with what's due. Like the procedure page, the two cards fill the page and each scrolls on its own; a
 * narrow window or a half view has one card of two folding sections instead (see PatientView). An unknown patient
 * goes back to the list, as in Blazor.
 *
 * Banners say when the record is restricted, disputed or anonymised. Admins also get its privacy actions (export,
 * restrict, mark disputed, anonymise) and, under the details, its access report: who has accessed the record, and
 * when.
 */
export function ViewPatientPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  return <PatientView id={id} isNarrow={isNarrow} />;
}

export interface PatientViewProps {
  id: string;
  /** A narrow window or half view: one card with Details and Timeline sections that fold, instead of two cards. */
  isNarrow: boolean;
}

/** The patient page's content, laid out for the window (see ViewPatientPage). */
export function PatientView({id, isNarrow}: PatientViewProps) {
  const navigate = useNavigate();
  const me = useCurrentUser();
  const {data: patient, error, isPending} = useQuery(patientQuery(id));
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  // Held here, so they carry over when the window switches between one card and two.
  const [filters, setFilters] = useState<TimelineFilters>(NO_TIMELINE_FILTERS);
  const [open, setOpen] = useState({details: true, timeline: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));

  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) void navigate({to: '/patients', replace: true});
  }, [error, navigate]);

  const timeline = <PatientTimeline patientId={id} filters={filters} onFiltersChange={setFilters} />;
  const actions = patient && <Actions patient={patient} isAdmin={me.isAdmin} onProblem={setProblem} />;
  // The details, then (for admins) who has accessed the record.
  const details = patient && (
    <>
      <PatientDetails patient={patient} />
      {me.isAdmin && <PatientAccessReport patientId={id} />}
    </>
  );

  return (
    <Page
      title="View Patient"
      backHref="/patients"
      backLabel="Back to patients"
      isLoading={isPending}
      loadingMessage="Loading patient..."
      fill={patient !== undefined}
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
      {patient &&
        (isNarrow ? (
          // One card: the details, then the timeline, each folding; they scroll together.
          <FoldingCard
            footerActions={actions}
            sections={[
              {
                key: 'details',
                title: 'Details',
                isOpen: open.details,
                onToggle: () => toggle('details'),
                children: (
                  <Section variant="transparent" padding={6}>
                    <VStack gap={6}>{details}</VStack>
                  </Section>
                ),
              },
              {key: 'timeline', title: 'Timeline', isOpen: open.timeline, onToggle: () => toggle('timeline'), children: timeline},
            ]}
          />
        ) : (
          // The details card beside the timeline's.
          <TwoColumns
            start={
              <ViewCard fill footerActions={actions}>
                {details}
              </ViewCard>
            }
            end={
              <PanelCard title="Timeline" isBodyPadded={false}>
                {timeline}
              </PanelCard>
            }
          />
        ))}
    </Page>
  );
}

/**
 * Procedures and Edit Patient (not while the record is restricted or anonymised, when it can't be changed), and an
 * admin's Privacy menu.
 */
function Actions({patient, isAdmin, onProblem}: {patient: Patient; isAdmin: boolean; onProblem: (problem: SaveProblem) => void}) {
  return (
    <>
      {isAdmin && <PrivacyActions patient={patient} onProblem={onProblem} />}
      <Button label="Procedures" variant="secondary" icon={<Icon icon={AppIcon.Procedures} />} href={`/patients/${patient.id}/procedures`} />
      {whyCantChange(patient) === null && (
        <Button label="Edit Patient" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} href={`/patients/${patient.id}/edit`} />
      )}
    </>
  );
}

/** Who the patient is, how to contact them, and their record's ID. */
function PatientDetails({patient}: {patient: Patient}) {
  const age = patient.dateOfBirth ? ageOn(patient.dateOfBirth) : null;
  return (
    <>
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
    </>
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
