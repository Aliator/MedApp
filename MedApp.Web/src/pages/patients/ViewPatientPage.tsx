import {Button} from '@astryxdesign/core/Button';
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
import {patientQuery, type Patient} from '../../api/patients';
import {NO_TIMELINE_FILTERS, type TimelineFilters} from '../../api/timeline';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PatientTimeline} from '../../components/procedures/PatientTimeline';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {FoldingCard} from '../../components/views/FoldingCard';
import {PanelCard} from '../../components/views/PanelCard';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {ageOn, formatDate} from '../../utils/dates';

/**
 * A patient's details (replaces Blazor's ViewPatient) beside their timeline: everything that happened to their
 * procedures, with what's due. Like the procedure page, the two cards fill the page and each scrolls on its own; a
 * narrow window or a half view has one card of two folding sections instead (see PatientView). An unknown patient
 * goes back to the list, as in Blazor.
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
  const {data: patient, error, isPending} = useQuery(patientQuery(id));
  // Held here, so they carry over when the window switches between one card and two.
  const [filters, setFilters] = useState<TimelineFilters>(NO_TIMELINE_FILTERS);
  const [open, setOpen] = useState({details: true, timeline: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));

  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) void navigate({to: '/patients', replace: true});
  }, [error, navigate]);

  const timeline = <PatientTimeline patientId={id} filters={filters} onFiltersChange={setFilters} />;

  return (
    <Page
      title="View Patient"
      backHref="/patients"
      backLabel="Back to patients"
      isLoading={isPending}
      loadingMessage="Loading patient..."
      fill={patient !== undefined}
      message={
        error &&
        !(error instanceof ApiError && error.status === 404) && <AlertMessage message="The patient could not be loaded. Please try again." />
      }>
      {patient &&
        (isNarrow ? (
          // One card: the details, then the timeline, each folding; they scroll together.
          <FoldingCard
            footerActions={<Actions patient={patient} />}
            sections={[
              {
                key: 'details',
                title: 'Details',
                isOpen: open.details,
                onToggle: () => toggle('details'),
                children: (
                  <Section variant="transparent" padding={6}>
                    <VStack gap={6}>
                      <PatientDetails patient={patient} />
                    </VStack>
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
              <ViewCard fill footerActions={<Actions patient={patient} />}>
                <PatientDetails patient={patient} />
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

/** Procedures and Edit Patient. */
function Actions({patient}: {patient: Patient}) {
  return (
    <>
      <Button label="Procedures" variant="secondary" icon={<Icon icon={AppIcon.Procedures} />} href={`/patients/${patient.id}/procedures`} />
      <Button label="Edit Patient" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} href={`/patients/${patient.id}/edit`} />
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
