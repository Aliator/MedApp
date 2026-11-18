import {Link} from '@astryxdesign/core/Link';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {ageDescription, formatDate} from '../../utils/dates';
import {formatNhsNumber, spokenNhsNumber} from '../../utils/nhsNumber';
import {bannerName, type BannerPatient} from '../patients/PatientBanner';

/** Capitals for the family name, as `PatientBanner` shows it, without changing the text screen readers read. */
const CAPITALS = {textTransform: 'uppercase'} as const;

export interface PathwayPatientProps {
  /** Where the name links to: the patient's page. */
  patientId: string;
  patient: BannerPatient | undefined;
  /** The pathway's own word for the patient's name, while `patient` is missing. */
  patientName?: string | null;
}

/**
 * Who the pathway is about, at the top of its always-open Details: the patient's name (family name first, in
 * capitals, linking to their page), date of birth with age, and NHS number, as `PatientBanner` shows them. The
 * pathway page has no banner (the owner's decision, 18 November 2026): this is its control for the wrong-patient
 * hazards H-01 and H-02 instead, so keep everything it shows, and keep Details first and unfoldable.
 */
export function PathwayPatient({patientId, patient, patientName}: PathwayPatientProps) {
  const family = patient?.lastName?.trim() ?? '';
  const name = patient ? bannerName(patient) : patientName || 'Unknown patient';
  const age = patient?.dateOfBirth ? ageDescription(patient.dateOfBirth) : null;
  const nhsNumber = patient?.nhsNumber?.trim() ?? '';
  return (
    <MetadataList columns={2} label={{position: 'top'}}>
      <MetadataListItem label="Patient">
        <Link href={`/patients/${patientId}`}>
          {patient && family ? (
            <>
              <Text type="inherit" weight="bold" style={CAPITALS}>
                {family}
              </Text>
              <Text type="inherit" weight="bold">
                {name.slice(family.length)}
              </Text>
            </>
          ) : (
            <Text type="inherit" weight="bold">
              {name}
            </Text>
          )}
        </Link>
      </MetadataListItem>
      <MetadataListItem label="Date of birth">
        {patient?.dateOfBirth ? `${formatDate(patient.dateOfBirth)}${age ? ` (${age})` : ''}` : 'Not known'}
      </MetadataListItem>
      <MetadataListItem label="NHS number">
        {nhsNumber ? (
          <>
            {/* Seen grouped; heard a digit at a time, so it isn't read as one large number. */}
            <Text type="inherit" aria-hidden>
              {formatNhsNumber(nhsNumber)}
            </Text>
            <VisuallyHidden>{spokenNhsNumber(nhsNumber)}</VisuallyHidden>
          </>
        ) : (
          <Text type="inherit" color="secondary">
            No NHS number
          </Text>
        )}
      </MetadataListItem>
    </MetadataList>
  );
}
