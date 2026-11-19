import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {resultStatusLabel, type ResultSummary} from '../../api/results';
import {useAccessibility} from '../../app/accessibility';
import {formatShortDate} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';
import {AppIcon} from '../shared/icons';

const STATUS_COLORS: Record<string, TokenColor> = {
  AwaitingSignOff: 'blue',
  Seen: 'purple',
  SignedOff: 'green',
  EnteredInError: 'gray',
};

const STATUS_ICONS: Record<string, IconType> = {
  AwaitingSignOff: AppIcon.Results,
  Seen: AppIcon.View,
  SignedOff: AppIcon.SignOff,
  EnteredInError: AppIcon.EnteredInError,
};

/**
 * A result's status in words ("Awaiting sign-off", "Seen", "Signed off", "Entered in error"), with its icon when
 * "Icons on statuses" is on. The colour says where it is in its sign-off, never anything about what the report says
 * (MedApp never reads it). Use it wherever a result's status shows.
 */
export function ResultStatusToken({status}: {status: string | null | undefined}) {
  const {flags} = useAccessibility();
  const icon = flags.statusIcons && status && STATUS_ICONS[status] ? <Icon icon={STATUS_ICONS[status]} /> : undefined;
  return <Token size="sm" label={resultStatusLabel(status)} color={STATUS_COLORS[status ?? ''] ?? 'default'} icon={icon} />;
}

/**
 * "Needs a clinician": awaiting sign-off, and its responsible clinician can no longer sign it off (their roles changed,
 * or their account is gone), so someone must take it over. The API's `needsClinician`; it says nothing clinical.
 */
export function NeedsClinicianToken() {
  const {flags} = useAccessibility();
  return (
    <Token
      size="sm"
      label="Needs a clinician"
      color="orange"
      description="Its responsible clinician can no longer sign results off. Someone who can should take it over."
      icon={flags.statusIcons ? <Icon icon={AppIcon.Warning} /> : undefined}
    />
  );
}

/** The patient a result is about, linked to their page, with their NHS number and date of birth under the name. */
export function ResultPatientCell({result}: {result: Pick<ResultSummary, 'patientId' | 'patientName' | 'patientNhsNumber' | 'patientDateOfBirth'>}) {
  return (
    <VStack gap={0} hAlign="start">
      <Text type="body" maxLines={1}>
        <Link href={`/patients/${result.patientId}`}>{result.patientName}</Link>
      </Text>
      <Text type="supporting" color="secondary" maxLines={1}>
        {result.patientNhsNumber ? `NHS No. ${formatNhsNumber(result.patientNhsNumber)}` : 'No NHS number'}
      </Text>
      {result.patientDateOfBirth && (
        <Text type="supporting" color="secondary" maxLines={1}>
          {`Born ${formatShortDate(result.patientDateOfBirth)}`}
        </Text>
      )}
    </VStack>
  );
}

/** A result's title, linked to its page (a list's main link), with its specimen's number under it when it has one. */
export function ResultTitleCell({result}: {result: Pick<ResultSummary, 'id' | 'title' | 'specimenNumber' | 'source'>}) {
  return (
    <VStack gap={0} hAlign="start">
      <Text type="body" weight="semibold" maxLines={2}>
        <Link href={`/results/${result.id}`}>{result.title}</Link>
      </Text>
      <Text type="supporting" color="secondary" maxLines={1}>
        {result.specimenNumber ? `Sample ${result.specimenNumber}` : 'External report'}
      </Text>
    </VStack>
  );
}

/** How old a result is, in whole days since it was reported: "Today", "1 day", "12 days". */
export const ageWords = (days: number | null | undefined) => (!days || days <= 0 ? 'Today' : days === 1 ? '1 day' : `${days} days`);
