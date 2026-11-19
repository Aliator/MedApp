import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {sampleSourceName, specimenStatusLabel, type SpecimenSummary} from '../../api/specimens';
import {useAccessibility} from '../../app/accessibility';
import {formatDateTime} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';
import {AppIcon} from '../shared/icons';

const STATUS_COLORS: Record<string, TokenColor> = {
  Taken: 'blue',
  Sent: 'purple',
  Received: 'teal',
  Reported: 'green',
  RejectedByLab: 'orange',
  Lost: 'red',
  EnteredInError: 'gray',
};

const STATUS_ICONS: Record<string, IconType> = {
  Taken: AppIcon.Specimens,
  Sent: AppIcon.SpecimenSent,
  Received: AppIcon.SpecimenReceived,
  Reported: AppIcon.Success,
  RejectedByLab: AppIcon.StatusCancelled,
  Lost: AppIcon.Warning,
  EnteredInError: AppIcon.EnteredInError,
};

/**
 * A specimen's status in words ("Taken", "Rejected by the lab", "Entered in error"), with its icon when "Icons on
 * statuses" is on. The colour says where it is in its journey, never anything about what it showed. Use it wherever a
 * specimen's status shows.
 */
export function SpecimenStatusToken({status}: {status: string | null | undefined}) {
  const {flags} = useAccessibility();
  const icon = flags.statusIcons && status && STATUS_ICONS[status] ? <Icon icon={STATUS_ICONS[status]} /> : undefined;
  return <Token size="sm" label={specimenStatusLabel(status)} color={STATUS_COLORS[status ?? ''] ?? 'default'} icon={icon} />;
}

/**
 * "Overdue at lab": still open after the type's expected days. A date comparison (decision 88), the API's
 * `isOverdueAtLab`; it says nothing clinical.
 */
export function OverdueAtLabToken() {
  const {flags} = useAccessibility();
  return <Token size="sm" label="Overdue at lab" color="red" icon={flags.statusIcons ? <Icon icon={AppIcon.Overdue} /> : undefined} />;
}

/**
 * Where a sample came from (MC14, H-73): "Clinic" or "Hospital", always shown, so a clinic's and the hospital's samples
 * can't be confused in the one database.
 */
export function SampleSourceToken({source}: {source: string | null | undefined}) {
  const isHospital = source === 'Hospital';
  return <Token size="sm" label={isHospital ? 'Hospital' : 'Clinic'} color={isHospital ? 'purple' : 'teal'} />;
}

/** Where a sample came from, for a list: the source, with the clinic's or hospital's name under it. */
export function SampleSourceCell({specimen}: {specimen: Pick<SpecimenSummary, 'source' | 'requestingTeamName' | 'hospitalName'>}) {
  return (
    <VStack gap={1} hAlign="start">
      <SampleSourceToken source={specimen.source} />
      <Text type="supporting" color="secondary" maxLines={1}>
        {sampleSourceName(specimen) || '—'}
      </Text>
    </VStack>
  );
}

/** A sample's number, linked to its page (a list's main link), with its type under it. */
export function SpecimenNumberCell({specimen}: {specimen: Pick<SpecimenSummary, 'id' | 'number' | 'specimenTypeName'>}) {
  return (
    <VStack gap={0} hAlign="start">
      <Text type="body" weight="semibold" maxLines={1}>
        <Link href={`/specimens/${specimen.id}`}>{specimen.number}</Link>
      </Text>
      <Text type="supporting" color="secondary" maxLines={1}>
        {specimen.specimenTypeName}
      </Text>
    </VStack>
  );
}

/** The patient a sample is from, linked to their page, with their NHS number under the name. */
export function SpecimenPatientCell({specimen}: {specimen: Pick<SpecimenSummary, 'patientId' | 'patientName' | 'patientNhsNumber'>}) {
  return (
    <VStack gap={0} hAlign="start">
      <Text type="body" maxLines={1}>
        <Link href={`/patients/${specimen.patientId}`}>{specimen.patientName}</Link>
      </Text>
      <Text type="supporting" color="secondary" maxLines={1}>
        {specimen.patientNhsNumber ? `NHS No. ${formatNhsNumber(specimen.patientNhsNumber)}` : 'No NHS number'}
      </Text>
    </VStack>
  );
}

/** The latest thing that happened to a specimen, for a list: "Reported 4 Nov 2026, 10:00", "Taken 2 Nov 2026, 09:00". */
export function latestStep(specimen: Pick<SpecimenSummary, 'status' | 'takenAtUtc' | 'sentAtUtc' | 'receivedAtUtc' | 'reportedAtUtc' | 'closedAtUtc'>): string {
  const at =
    specimen.closedAtUtc ?? specimen.reportedAtUtc ?? specimen.receivedAtUtc ?? specimen.sentAtUtc ?? specimen.takenAtUtc ?? '';
  return `${specimenStatusLabel(specimen.status)} ${formatDateTime(at)}`;
}
