import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {ageOn, formatDate} from '../../utils/dates';
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

/** "AHMED, Freya": the family name first, in capitals, as NHS patient banners show it. */
export function bannerName({firstName, lastName}: Pick<BannerPatient, 'firstName' | 'lastName'>): string {
  const family = lastName?.trim().toLocaleUpperCase('en-GB') ?? '';
  const given = firstName?.trim() ?? '';
  return family && given ? `${family}, ${given}` : family || given || 'Unknown patient';
}

/** "46 years", "1 year", or "under 1 year"; null when the date can't give one. */
export function ageText(dateOfBirth: string): string | null {
  const age = ageOn(dateOfBirth);
  if (age === null) return null;
  if (age === 0) return 'under 1 year';
  return `${age} ${age === 1 ? 'year' : 'years'}`;
}

/**
 * Who a page is about, above its content on every page about one patient: their name (family name first, in
 * capitals), date of birth with age, and NHS number, with small tokens when the record is restricted, disputed or
 * anonymised (the full `PatientFlagBanners` stay in the page's message). It's the app's control for the
 * wrong-patient hazards (H-01, H-02): the same details, in the same place and order, wherever someone is about to
 * read or change a patient's care. Follows NHS Digital's patient banner guidance where it fits.
 *
 * Give it to `Page` as its `banner`. It wraps onto more lines in a narrow window or a half view, and reads to a
 * screen reader as a "Patient" region, with the NHS number a digit at a time.
 */
export function PatientBanner({patient}: {patient: BannerPatient}) {
  const isNarrow = useIsNarrow();
  const age = patient.dateOfBirth ? ageText(patient.dateOfBirth) : null;
  const nhsNumber = patient.nhsNumber?.trim() ?? '';

  return (
    // A card in the header bars' shade (`-muted`), so it follows the chosen colours and stands apart from the content.
    <Card variant="muted" role="region" aria-label="Patient" padding={isNarrow ? 3 : 4}>
      <HStack gap={isNarrow ? 3 : 6} vAlign="center" wrap="wrap">
        <Text type={isNarrow ? 'body' : 'large'} weight="bold">
          {bannerName(patient)}
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
