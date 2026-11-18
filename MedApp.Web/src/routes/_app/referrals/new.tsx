import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {patientQuery} from '../../../api/patients';
import {patientOpenReferralsQuery} from '../../../api/referrals';
import {specialtiesQuery} from '../../../api/specialties';
import {RegisterReferralPage} from '../../../pages/referrals/RegisterReferralPage';

/** `?patientId=`: the patient the referral is for, once found (or from their page, or after adding them). */
export const Route = createFileRoute('/_app/referrals/new')({
  validateSearch: (search: Record<string, unknown>): {patientId?: string} => ({
    patientId: typeof search.patientId === 'string' && search.patientId ? search.patientId.toLowerCase() : undefined,
  }),
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(
      context.queryClient,
      specialtiesQuery(false),
      ...(deps.patientId ? [patientQuery(deps.patientId), patientOpenReferralsQuery(deps.patientId)] : []),
    ),
  component: function RegisterReferralRoute() {
    const {patientId} = Route.useSearch();
    const navigate = Route.useNavigate();
    return (
      <RegisterReferralPage
        // Keyed by patient, so choosing another starts the form again.
        key={patientId ?? 'none'}
        patientId={patientId}
        onPatientChange={(next) => void navigate({search: next ? {patientId: next} : {}, replace: true})}
      />
    );
  },
});
