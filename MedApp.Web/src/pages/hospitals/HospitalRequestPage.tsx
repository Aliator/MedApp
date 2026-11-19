import {Link} from '@astryxdesign/core/Link';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {ApiError} from '../../api/errors';
import {hospitalRequestQuery, labCaseStatusLabel, type HospitalRequest} from '../../api/hospitals';
import {LabRequestPriorityToken} from '../../components/hospitals/HospitalRequestCells';
import {Page} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {hasPatientFlags, PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {PanelCard} from '../../components/views/PanelCard';
import {formatDate, formatDateTime} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';

/** Why the NHS number on the request matters when it differs from the record's (hazard H-72). */
export const NHS_NUMBERS_DIFFER =
  "The NHS number on the request isn't the record's. Check this is the right patient before any work on the sample, and tell the hospital if the request is wrong.";

/** What the hospital sent: the hospital, its patient number, the NHS number, the surgeon, the priority and dates. */
function RequestDetails({request}: {request: HospitalRequest}) {
  return (
    <MetadataList>
      <MetadataListItem label="Hospital">
        {request.hospitalName}
        {request.hospitalOdsCode ? ` (${request.hospitalOdsCode})` : ''}
      </MetadataListItem>
      <MetadataListItem label="Hospital's patient number">{request.hospitalPatientNumber}</MetadataListItem>
      <MetadataListItem label="NHS number on the request">{request.nhsNumber ? formatNhsNumber(request.nhsNumber) : 'Not given'}</MetadataListItem>
      <MetadataListItem label="Surgeon">{request.surgeonName}</MetadataListItem>
      <MetadataListItem label="Priority">
        <LabRequestPriorityToken priority={request.priority} />
      </MetadataListItem>
      <MetadataListItem label="Cancer pathway">{request.isCancerPathway ? 'Yes, the request says so' : 'No'}</MetadataListItem>
      <MetadataListItem label="Request made">{formatDateTime(request.authoredOn ?? '')}</MetadataListItem>
      <MetadataListItem label="Report needed by">{request.reportNeededBy ? formatDate(request.reportNeededBy) : "No date given: the lab's limit applies"}</MetadataListItem>
    </MetadataList>
  );
}

/** The sample and the lab case it opened (the lab's own, with no pathway). */
function CaseDetails({request}: {request: HospitalRequest}) {
  return (
    <MetadataList>
      <MetadataListItem label="Lab case">
        <Link href={`/lab/cases/${request.labCaseId}`}>{request.labCaseNumber}</Link>
      </MetadataListItem>
      <MetadataListItem label="Status">{labCaseStatusLabel(request.labCaseStatus)}</MetadataListItem>
      <MetadataListItem label="Source">From the hospital</MetadataListItem>
      <MetadataListItem label="Test">{request.labTestName}</MetadataListItem>
      <MetadataListItem label="Type of sample">
        {request.specimenTypeName}
        {request.specimenTypeSnomedCode ? ` (SNOMED CT ${request.specimenTypeSnomedCode})` : ''}
      </MetadataListItem>
      <MetadataListItem label="Collected">{request.collectedAtUtc ? formatDateTime(request.collectedAtUtc) : 'Not given'}</MetadataListItem>
      <MetadataListItem label="Registered">
        {formatDateTime(request.registeredAtUtc ?? '')}
        {request.registeredByUsername ? ` by ${request.registeredByUsername}` : ''}
      </MetadataListItem>
      <MetadataListItem label="Patient's record">
        <Link href={`/patients/${request.patientId}`}>Open their record</Link>
      </MetadataListItem>
    </MetadataList>
  );
}

/**
 * A hospital request and its lab case (`/hospital-requests/{id}`, MC18). The patient banner (the record's name, date of
 * birth and NHS number) sits over the request's own numbers, the hospital's patient number and the NHS number it gave,
 * so both can be checked against each other on every case (hazard H-72); when the NHS numbers differ, it says so. Then
 * what the hospital asked (priority, cancer pathway, dates, as given) and the lab case it opened. Opening it is recorded
 * as viewing the case. Hospital cases are the lab's: anyone else gets a 404.
 */
export function HospitalRequestPage({id}: {id: string}) {
  const {data: request, isPending, error} = useQuery(hospitalRequestQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;

  return (
    <Page
      title={request ? `Lab Case ${request.labCaseNumber}` : 'Hospital Request'}
      subtitle={request ? `From ${request.hospitalName}, hospital number ${request.hospitalPatientNumber}` : undefined}
      backHref="/hospital-requests"
      backLabel="Back to hospital requests"
      isLoading={isPending}
      loadingMessage="Loading hospital request..."
      fill={request !== undefined}
      banner={request?.patient && <PatientBanner patient={request.patient} />}
      message={
        isMissing ? (
          <AlertMessage message="Hospital request not found. It may have been deleted, or it's another team's." />
        ) : error ? (
          <AlertMessage message="The hospital request could not be loaded. Please try again." />
        ) : request?.nhsNumbersDiffer ? (
          <AlertMessage status="warning" message={NHS_NUMBERS_DIFFER} />
        ) : (
          request?.patient && hasPatientFlags(request.patient) && <PatientFlagBanners patient={request.patient} />
        )
      }>
      {request && (
        <TwoColumns
          start={
            <PanelCard title="The Request">
              <Section variant="transparent">
                <RequestDetails request={request} />
              </Section>
            </PanelCard>
          }
          end={
            <VStack gap={4} hAlign="stretch">
              <PanelCard title="The Sample and Lab Case">
                <Section variant="transparent">
                  <CaseDetails request={request} />
                </Section>
              </PanelCard>
            </VStack>
          }
        />
      )}
    </Page>
  );
}
