import {createFileRoute} from '@tanstack/react-router';
import {mySpecialtiesQuery} from '../../../api/specialties';
import {preload} from '../../../api/preload';
import {AccountPage} from '../../../pages/account/AccountPage';

export const Route = createFileRoute('/_app/account/')({
  loader: ({context}) => preload(context.queryClient, mySpecialtiesQuery),
  component: AccountPage,
});
