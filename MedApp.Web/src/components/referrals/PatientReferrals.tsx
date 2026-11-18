import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {openFirst, patientReferralsQuery} from '../../api/referrals';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {ReferralList} from './ReferralList';

export interface PatientReferralsProps {
  patientId: string;
  /** Whether a referral can be registered for them now (not while the record is restricted or anonymised). */
  canRegister: boolean;
}

/**
 * A patient's referrals, for their page: the ones awaiting triage first (oldest received first), then the most recent
 * others, each linking to its page; and Register Referral, with the patient chosen.
 */
export function PatientReferrals({patientId, canRegister}: PatientReferralsProps) {
  const {data, isPending, isError} = useQuery(patientReferralsQuery(patientId));
  const referrals = openFirst(data?.items ?? []);
  const more = (data?.totalCount ?? 0) - referrals.length;

  return (
    <VStack gap={3} hAlign="stretch">
      {isError ? (
        <AlertMessage message="The referrals could not be loaded. Please try again." />
      ) : isPending ? (
        <Text type="body" color="secondary">
          Loading referrals...
        </Text>
      ) : referrals.length === 0 ? (
        <Text type="body" color="secondary">
          No referrals yet.
        </Text>
      ) : (
        <ReferralList referrals={referrals} label="Referrals" />
      )}
      {more > 0 && (
        <Text type="supporting" color="secondary">
          {`And ${more} older ${more === 1 ? 'referral' : 'referrals'}: the timeline has them all.`}
        </Text>
      )}
      {canRegister && (
        <HStack>
          {/* TODO(useCan): show only to holders of RegisterReferrals once `useCan` is on main; the API refuses others (403). */}
          <Button
            label="Register Referral"
            variant="secondary"
            icon={<Icon icon={AppIcon.Referrals} />}
            href={`/referrals/new?patientId=${patientId}`}
          />
        </HStack>
      )}
    </VStack>
  );
}
