import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {ApiError} from '../../api/errors';
import {specimenQuery, type Specimen, type SpecimenSheet} from '../../api/specimens';
import {Page} from '../../components/pages/Page';
import {PatientBanner} from '../../components/patients/PatientBanner';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SpecimenQrCode} from '../../components/specimens/SpecimenQrCode';
import {formatDate, formatDateTime} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';

/**
 * While printing, only the sheet shows, at the top of the paper: the side nav, the page's header, the banner and the
 * buttons are left off. Everything else is hidden, not removed, so the page keeps its layout on screen.
 */
const PRINT_CSS =
  '@media print { body * { visibility: hidden !important; } [data-specimen-sheet], [data-specimen-sheet] * { visibility: visible !important; } ' +
  '[data-specimen-sheet] { position: absolute; inset-block-start: 0; inset-inline-start: 0; box-shadow: none !important; } }';

/** The patient's name as a label prints it: the family name in capitals first, "AHMED, Freya". */
export function labelName(patient: Specimen['patient']): string {
  const family = patient?.lastName?.trim() ?? '';
  const given = patient?.firstName?.trim() ?? '';
  return family && given ? `${family.toUpperCase()}, ${given}` : family.toUpperCase() || given || 'Unknown patient';
}

export interface SpecimenPrintPageProps {
  id: string;
  /** The label (the default), or the request sheet for the lab. */
  sheet: SpecimenSheet;
}

/**
 * A specimen's label or request sheet, to print (`/specimens/$id/print?sheet=label|request`; decision 87): the
 * specimen number as text and as a QR code (drawn from the server's path; it holds the number only), the patient's name,
 * date of birth and NHS number, the type and when it was taken. The request sheet adds the site, the notes for the lab,
 * the requesting clinician and the lab. Print calls the browser's print, which prints the sheet alone. Opening it reads
 * the specimen, which is recorded as viewing it.
 */
export function SpecimenPrintPage({id, sheet}: SpecimenPrintPageProps) {
  const {data: specimen, error, isPending} = useQuery(specimenQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;
  const isRequest = sheet === 'request';

  return (
    <Page
      title={isRequest ? 'Request Sheet' : 'Specimen Label'}
      subtitle={specimen ? `Specimen ${specimen.number}` : undefined}
      backHref={`/specimens/${id}`}
      backLabel="Back to specimen"
      isLoading={isPending}
      loadingMessage="Loading specimen..."
      banner={specimen?.patient && <PatientBanner patient={specimen.patient} />}
      message={
        error && <AlertMessage message={isMissing ? 'Specimen not found. It may have been deleted.' : 'The specimen could not be loaded. Please try again.'} />
      }
      actions={specimen && <Button label="Print" variant="primary" icon={<Icon icon={AppIcon.Print} />} onClick={() => window.print()} />}>
      <style data-medapp-print="">{PRINT_CSS}</style>
      {specimen && (isRequest ? <RequestSheet specimen={specimen} /> : <Label specimen={specimen} />)}
    </Page>
  );
}

/** The identifiers every sheet carries: the number, the patient, the type and when it was taken, beside the QR code. */
function Identifiers({specimen}: {specimen: Specimen}) {
  const patient = specimen.patient;
  return (
    <HStack gap={4} vAlign="start">
      <SpecimenQrCode path={specimen.label?.qrPath ?? ''} size={specimen.label?.size ?? 0} number={specimen.number ?? ''} />
      <VStack gap={1}>
        <Text type="large" weight="bold">
          {specimen.number}
        </Text>
        <Text type="body" weight="bold">
          {labelName(patient)}
        </Text>
        <Text type="body">{`Born ${patient?.dateOfBirth ? formatDate(patient.dateOfBirth) : 'unknown'}`}</Text>
        <Text type="body">{patient?.nhsNumber ? `NHS No. ${formatNhsNumber(patient.nhsNumber)}` : 'No NHS number'}</Text>
        <Text type="body">{specimen.specimenTypeName}</Text>
        <Text type="body">{`Taken ${formatDateTime(specimen.takenAtUtc ?? '')}`}</Text>
      </VStack>
    </HStack>
  );
}

function Label({specimen}: {specimen: Specimen}) {
  return (
    <HStack>
      <Card data-specimen-sheet="" padding={4} aria-label={`Label for specimen ${specimen.number}`}>
        <Identifiers specimen={specimen} />
      </Card>
    </HStack>
  );
}

/** Notes keep the lines they were typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

function RequestSheet({specimen}: {specimen: Specimen}) {
  return (
    <Card data-specimen-sheet="" padding={6} width="100%" aria-label={`Request sheet for specimen ${specimen.number}`}>
      <VStack gap={6} hAlign="stretch">
        <Heading level={2}>{`Request for ${specimen.laboratoryName}`}</Heading>
        <Identifiers specimen={specimen} />
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Lab">{specimen.laboratoryName}</MetadataListItem>
          <MetadataListItem label="Requesting clinician">{specimen.requestingUsername}</MetadataListItem>
          <MetadataListItem label="Site">{specimen.site?.trim() || '—'}</MetadataListItem>
          <MetadataListItem label="Taken by">{specimen.takenByUsername}</MetadataListItem>
        </MetadataList>
        <VStack gap={1} hAlign="stretch">
          <Text type="body" weight="semibold">
            Notes for the lab
          </Text>
          <Text type="body" style={PRESERVE_LINES}>
            {specimen.requestNotes?.trim() || '—'}
          </Text>
        </VStack>
      </VStack>
    </Card>
  );
}
