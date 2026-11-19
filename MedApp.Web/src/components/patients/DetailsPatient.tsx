import {Link} from '@astryxdesign/core/Link';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import type {ReactNode} from 'react';
import {ageDescription, formatDate} from '../../utils/dates';
import {formatNhsNumber, spokenNhsNumber} from '../../utils/nhsNumber';
import type {FoldingSection} from '../views/FoldingCard';
import {bannerName, PatientFlagTokens, type BannerPatient} from './PatientBanner';

/** Capitals for the family name, as `PatientBanner` shows it, without changing the text screen readers read. */
const CAPITALS = {textTransform: 'uppercase'} as const;

export interface DetailsPatientProps {
  /** Where the name links to: the patient's page. Without one, the name is plain text. */
  patientId?: string | null;
  patient: BannerPatient | undefined | null;
  /** The record's own word for the patient's name, while `patient` is missing. */
  patientName?: string | null;
}

/**
 * Who a view page is about, at the top of its always-open Details: the patient's name (family name first, in
 * capitals, linking to their page), date of birth with age, NHS number and the record's flags as tokens, as
 * `PatientBanner` shows them. View pages about one record (a pathway, a referral, a specimen...) have no banner (the
 * owner's decisions, 18 and 19 November 2026): this is their control for the wrong-patient hazards H-01 and H-02
 * instead, so keep everything it shows, and keep Details first and unfoldable.
 */
export function DetailsPatient({patientId, patient, patientName}: DetailsPatientProps) {
  const family = patient?.lastName?.trim() ?? '';
  const name = patient ? bannerName(patient) : patientName || 'Unknown patient';
  const age = patient?.dateOfBirth ? ageDescription(patient.dateOfBirth) : null;
  const nhsNumber = patient?.nhsNumber?.trim() ?? '';
  const shownName =
    patient && family ? (
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
    );
  return (
    <MetadataList columns={2} label={{position: 'top'}}>
      <MetadataListItem label="Patient">{patientId ? <Link href={`/patients/${patientId}`}>{shownName}</Link> : shownName}</MetadataListItem>
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
      {patient && (patient.isRestricted || patient.isDisputed || patient.isAnonymised) && (
        <MetadataListItem label="Record">
          <PatientFlagTokens patient={patient} />
        </MetadataListItem>
      )}
    </MetadataList>
  );
}

/**
 * A view page's Details section with the patient at its top (`DetailsPatient`), then the page's own details: always
 * open, with no chevron. Put it first in the page's first card (and first in the one card when narrow).
 */
export function patientDetailsSection(patient: DetailsPatientProps, children?: ReactNode): FoldingSection {
  return {
    key: 'details',
    title: 'Details',
    isOpen: true,
    canFold: false,
    onToggle: () => {},
    children: (
      <Section variant="transparent" padding={6}>
        <VStack gap={6} hAlign="stretch">
          <DetailsPatient {...patient} />
          {children}
        </VStack>
      </Section>
    ),
  };
}
