import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useId} from 'react';
import {ageDescription, formatDate} from '../../utils/dates';
import {formatNhsNumber, spokenNhsNumber} from '../../utils/nhsNumber';
import {useIsNarrow} from '../pages/Page';
import {AppIcon} from '../shared/icons';

/** Who the banner is about: a patient (`PatientResponse`) or a procedure's patient (`ProcedurePatientResponse`). */
export interface BannerPatient {
  firstName?: string | null;
  lastName?: string | null;
  /** YYYY-MM-DD. */
  dateOfBirth?: string | null;
  /** Ten digits, or none. */
  nhsNumber?: string | null;
  isRestricted?: boolean;
  isDisputed?: boolean;
  isAnonymised?: boolean;
}

/** The family and given names, trimmed, in their own case. */
function nameParts({firstName, lastName}: Pick<BannerPatient, 'firstName' | 'lastName'>) {
  return {family: lastName?.trim() ?? '', given: firstName?.trim() ?? ''};
}

/**
 * "Ahmed, Freya": the family name first, as NHS patient banners show it, in its own case. The banner shows the
 * family name in capitals with CSS, so the text (what screen readers read, and the region's name) keeps its case.
 */
export function bannerName(patient: Pick<BannerPatient, 'firstName' | 'lastName'>): string {
  const {family, given} = nameParts(patient);
  return family && given ? `${family}, ${given}` : family || given || 'Unknown patient';
}

/** Capitals for the family name, as the guidance shows it, without changing the text. */
const CAPITALS = {textTransform: 'uppercase'} as const;

/**
 * Who a page is about, above its content on every page about one patient: their name (family name first, in
 * capitals), date of birth with age, and NHS number, with small tokens when the record is restricted, disputed or
 * anonymised (the full `PatientFlagBanners` stay in the page's message). It's the app's control for the
 * wrong-patient hazards (H-01, H-02): the same details, in the same place and order, wherever someone is about to
 * read or change a patient's care. Follows NHS Digital's patient banner guidance where it fits: ages under 2 years
 * are in months, under 3 months in weeks and under 4 weeks in days (`ageDescription`).
 *
 * Give it to `Page` as its `banner`. It wraps onto more lines in a narrow window or a half view. To a screen reader
 * it's a region named for the patient ("Patient: Ahmed, Freya"), so two in half views are told apart, with the NHS
 * number read a digit at a time.
 */
export function PatientBanner({patient}: {patient: BannerPatient}) {
  const isNarrow = useIsNarrow();
  const labelId = useId();
  const nameId = useId();
  const {family} = nameParts(patient);
  const name = bannerName(patient);
  const age = patient.dateOfBirth ? ageDescription(patient.dateOfBirth) : null;
  const nhsNumber = patient.nhsNumber?.trim() ?? '';

  return (
    // A card in the header bars' shade (`-muted`), so it follows the chosen colours and stands apart from the content.
    <Card variant="muted" role="region" aria-labelledby={`${labelId} ${nameId}`} padding={isNarrow ? 3 : 4}>
      <VisuallyHidden id={labelId}>Patient:</VisuallyHidden>
      <HStack gap={isNarrow ? 3 : 6} vAlign="center" wrap="wrap">
        <Text id={nameId} type={isNarrow ? 'body' : 'large'} weight="bold">
          {family ? (
            <>
              <Text type="inherit" style={CAPITALS}>
                {family}
              </Text>
              {name.slice(family.length)}
            </>
          ) : (
            name
          )}
        </Text>
        <Text>
          {patient.dateOfBirth ? `Born ${formatDate(patient.dateOfBirth)}${age ? ` (${age})` : ''}` : 'Date of birth not known'}
        </Text>
        {nhsNumber ? (
          <Text>
            {/* Seen grouped; heard a digit at a time, so it isn't read as one large number. */}
            <Text aria-hidden>NHS No. {formatNhsNumber(nhsNumber)}</Text>
            <VisuallyHidden>NHS number {spokenNhsNumber(nhsNumber)}</VisuallyHidden>
          </Text>
        ) : (
          <Text color="secondary">No NHS number</Text>
        )}
        {(patient.isRestricted || patient.isDisputed || patient.isAnonymised) && (
          <HStack gap={2} wrap="wrap">
            {patient.isRestricted && <Token label="Restricted" color="orange" size="sm" icon={<Icon icon={AppIcon.Lock} size="sm" />} />}
            {patient.isDisputed && <Token label="Disputed" color="blue" size="sm" icon={<Icon icon={AppIcon.Info} size="sm" />} />}
            {patient.isAnonymised && <Token label="Anonymised" color="gray" size="sm" icon={<Icon icon={AppIcon.Shield} size="sm" />} />}
          </HStack>
        )}
      </HStack>
    </Card>
  );
}
