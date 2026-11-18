import {createFileRoute} from '@tanstack/react-router';
import {AddPatientPage} from '../../../pages/patients/AddPatientPage';

/** `?then=referral`: adding the patient a referral is for, from Register Referral, which it goes back to. */
export const Route = createFileRoute('/_app/patients/add')({
  validateSearch: (search: Record<string, unknown>): {then?: 'referral'} => ({
    then: search.then === 'referral' ? 'referral' : undefined,
  }),
  component: function AddPatientRoute() {
    return <AddPatientPage then={Route.useSearch().then} />;
  },
});
