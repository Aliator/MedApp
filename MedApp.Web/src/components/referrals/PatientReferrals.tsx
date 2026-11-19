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
  /**
   * A referral to leave out: the one whose page this is (the referral page lists the patient's other referrals, so a
   * triager spots a duplicate before choosing an outcome: hazard H-44).
   */
  exceptReferralId?: string;
  /** A team whose referrals are marked "Same clinic" (the shown referral's: the likeliest duplicates). */
  sameTeamId?: string;
}

/**
 * A patient's referrals, in every status, for their page: the ones awaiting triage first (oldest received first), then
 * the most recent others, each linking to its page; and Register Referral, with the patient chosen. With
 * `exceptReferralId`, the patient's other referrals (the referral page's, for spotting a duplicate).
 */
export function PatientReferrals({patientId, canRegister, exceptReferralId, sameTeamId}: PatientReferralsProps) {
  const {data, isPending, isError} = useQuery(patientReferralsQuery(patientId));
  const items = data?.items ?? [];
  const referrals = openFirst(items.filter((r) => r.id !== exceptReferralId));
  const more = (data?.totalCount ?? 0) - items.length;

  return (
    <VStack gap={3} hAlign="stretch">
      {isError ? (
        <AlertMessage label="Referrals not loaded" />
      ) : isPending ? (
        <Text type="body" color="secondary">
          Loading referrals...
        </Text>
      ) : referrals.length === 0 ? (
        <Text type="body" color="secondary">
          {exceptReferralId ? 'No other referrals.' : 'No referrals yet.'}
        </Text>
      ) : (
        <ReferralList referrals={referrals} label={exceptReferralId ? 'Other referrals' : 'Referrals'} sameTeamId={sameTeamId} />
      )}
      {more > 0 && (
        <Text type="supporting" color="secondary">
          {`And ${more} older ${more === 1 ? 'referral' : 'referrals'}: the timeline has them all.`}
        </Text>
      )}
      {canRegister && (
        <HStack>
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
