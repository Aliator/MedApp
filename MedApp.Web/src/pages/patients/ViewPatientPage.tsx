import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect} from 'react';
import {ApiError} from '../../api/errors';
import {patientQuery} from '../../api/patients';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {ageOn, formatDate} from '../../utils/dates';

/** A patient's details (replaces Blazor's ViewPatient). An unknown patient goes back to the list, as in Blazor. */
export function ViewPatientPage({id}: {id: string}) {
  const navigate = useNavigate();
  const {data: patient, error, isPending} = useQuery(patientQuery(id));

  useEffect(() => {
    if (error instanceof ApiError && error.status === 404) void navigate({to: '/patients', replace: true});
  }, [error, navigate]);

  const age = patient?.dateOfBirth ? ageOn(patient.dateOfBirth) : null;

  return (
    <Page
      title="View Patient"
      backHref="/patients"
      backLabel="Back to patients"
      isLoading={isPending}
      loadingMessage="Loading patient...">
      {error && !(error instanceof ApiError && error.status === 404) && (
        <AlertMessage message="The patient could not be loaded. Please try again." />
      )}
      {patient && (
        <ViewCard
          footerActions={
            <>
              <Button
                label="Procedures"
                variant="secondary"
                icon={<Icon icon={AppIcon.Procedures} />}
                href={`/patients/${patient.id}/procedures`}
              />
              <Button
                label="Edit Patient"
                variant="secondary"
                icon={<Icon icon={AppIcon.Edit} />}
                href={`/patients/${patient.id}/edit`}
              />
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
      )}
    </Page>
  );
}
