import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {referralStatusLabel} from '../../api/referrals';
import {useAccessibility} from '../../app/accessibility';
import {formatShortDate} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';
import {AppIcon} from '../shared/icons';
import {TableActions} from '../tables/TableActions';

const STATUS_COLORS: Record<string, TokenColor> = {
  AwaitingTriage: 'blue',
  InformationRequested: 'orange',
  Accepted: 'green',
  Rejected: 'gray',
  RedirectedElsewhere: 'purple',
  Withdrawn: 'gray',
  EnteredInError: 'gray',
};

const STATUS_ICONS: Record<string, IconType> = {
  AwaitingTriage: AppIcon.Referrals,
  InformationRequested: AppIcon.Info,
  Accepted: AppIcon.Success,
  Rejected: AppIcon.StatusCancelled,
  RedirectedElsewhere: AppIcon.StatusChange,
  Withdrawn: AppIcon.Withdraw,
  EnteredInError: AppIcon.EnteredInError,
};

/**
 * A referral's status, in words ("Awaiting triage", "Information requested", "Redirected elsewhere", "Entered in error"), with its icon when
 * "Icons on statuses" is on. Use it wherever a referral's status shows.
 */
export function ReferralStatusToken({status}: {status: string | null | undefined}) {
  const {flags} = useAccessibility();
  const icon = flags.statusIcons && status && STATUS_ICONS[status] ? <Icon icon={STATUS_ICONS[status]} /> : undefined;
  return <Token size="sm" label={referralStatusLabel(status)} color={STATUS_COLORS[status ?? ''] ?? 'default'} icon={icon} />;
}

/** How long ago something was received, in whole days: "Today", "1 day ago", "12 days ago". */
export const daysAgoWords = (days: number) => (days <= 0 ? 'Today' : days === 1 ? '1 day ago' : `${days} days ago`);

/**
 * When a referral was received, and how long it has waited ("12 days ago"; for a closed one, how long it waited before
 * it was decided, as the API counts it). A referral with a waiting-time clock (`hasClock`) shows only the date: its
 * wait is the clock's, in a Wait column of its own.
 */
export function ReceivedCell({
  receivedOn,
  daysSinceReceived,
  isOpen,
  hasClock = false,
}: {
  receivedOn: string;
  daysSinceReceived: number;
  isOpen: boolean;
  hasClock?: boolean;
}) {
  return (
    <VStack gap={0} hAlign="start">
      <Text type="body">{formatShortDate(receivedOn)}</Text>
      {!hasClock && (
        <Text type="supporting" color="secondary">
          {isOpen ? daysAgoWords(daysSinceReceived) : `Waited ${daysSinceReceived} ${daysSinceReceived === 1 ? 'day' : 'days'}`}
        </Text>
      )}
    </VStack>
  );
}

/**
 * The patient a referral is for, linked to the referral (the row's main link), with their NHS number under the name
 * to tell patients with the same name apart.
 */
export function ReferralPatientCell({id, patientName, nhsNumber}: {id: string; patientName: string; nhsNumber?: string | null}) {
  return (
    <VStack gap={0} hAlign="start">
      <Text type="body" weight="semibold" maxLines={1}>
        <Link href={`/referrals/${id}`}>{patientName}</Link>
      </Text>
      <Text type="supporting" color="secondary" maxLines={1}>
        {nhsNumber ? `NHS No. ${formatNhsNumber(nhsNumber)}` : 'No NHS number'}
      </Text>
    </VStack>
  );
}

/** Whether the referral's letter is attached: Yes, or No in a warning colour (a letter can arrive after the request). */
export function LetterCell({hasLetter}: {hasLetter: boolean}) {
  return hasLetter ? <Token size="sm" label="Yes" color="green" /> : <Token size="sm" label="No" color="orange" />;
}

/** A referral row's actions: View referral, and View patient. */
export function ReferralActions({id, patientId}: {id: string; patientId: string}) {
  return (
    <TableActions
      actions={[
        {label: 'View referral', icon: AppIcon.View, href: `/referrals/${id}`},
        {label: 'View patient', icon: AppIcon.Patients, href: `/patients/${patientId}`},
      ]}
    />
  );
}

/** The referral list's column widths: the patient and practice take what's left, as in the pathway lists. */
export const REFERRAL_COLUMN_WIDTH = {
  patient: proportional(2, {minWidth: 72}),
  specialty: proportional(1.5, {minWidth: 56}),
  practice: proportional(1.5, {minWidth: 56}),
  received: pixel(112),
  /** "Day 128 of 126" over Breached and Paused. */
  wait: pixel(132),
  priority: pixel(82),
  letter: pixel(72),
  status: pixel(132),
  /** Two icon buttons. */
  actions: pixel(84),
};
